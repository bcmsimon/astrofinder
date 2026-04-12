using Android.Content;
using Android.Hardware;
using Android.OS;
using AstroFinder.Domain.AR;
using Microsoft.Maui.ApplicationModel;

namespace AstroFinder.App.Services;

/// <summary>
/// Android-specific implementation of <see cref="IDeviceOrientationService"/> that uses
/// TYPE_GAME_ROTATION_VECTOR — a gyroscope + accelerometer fusion sensor that deliberately
/// excludes the magnetometer.
///
/// Why not TYPE_ROTATION_VECTOR (what MAUI OrientationSensor uses)?
/// TYPE_ROTATION_VECTOR fuses gyro + accel + magnetometer. The magnetometer is sensitive to
/// nearby metal gear, electronics, and phone cases. In the field this causes sudden heading
/// jumps that make AR overlays teleport to false positions.
///
/// TYPE_GAME_ROTATION_VECTOR is gyro-dominated with accel gravity correction only. Heading
/// is relative to wherever the device was pointing at startup — it does not reference magnetic
/// north. That is the correct tradeoff for AR: smooth, stable, jitter-free orientation at the
/// cost of absolute north reference. The user points at the sky, the overlay follows stably.
///
/// Coordinate system — identical to MAUI OrientationSensor (Android rotation matrix convention):
///   Earth frame after getRotationMatrix: X=East, Y=North, Z=Up (when remapped; sensor gives
///   a different raw layout). We use the quaternion columns directly the same way as
///   DeviceOrientationService so the projection math is unchanged.
/// </summary>
public sealed class AndroidOrientationService : Java.Lang.Object, IDeviceOrientationService, ISensorEventListener
{
    private const double RadToDeg = 180.0 / Math.PI;

    // Adaptive EMA — same constants as DeviceOrientationService for parity.
    private const double HeadingSmallAlpha    = 0.12;
    private const double HeadingLargeAlpha    = 0.65;
    private const double HeadingTransitionDeg = 12.0;

    private const double PitchSmallAlpha    = 0.12;
    private const double PitchLargeAlpha    = 0.55;
    private const double PitchTransitionDeg = 10.0;

    private const double HeadingThresholdDeg = 0.4;
    private const double PitchThresholdDeg   = 0.3;
    private const double RollAlpha           = 0.20;

    // Quaternion LERP alpha — same as HeadingSmallAlpha for matching smoothness.
    // Applied in rotation space so there is no gimbal lock at zenith.
    private const double QuaternionAlpha = 0.12;

    private double _headingDegrees;
    private double _pitchDegrees;
    private double _rollDegrees;
    private double _lastFiredHeadingDegrees;
    private double _lastFiredPitchDegrees;
    private bool _isRunning;
    private bool _initialized;

    private SensorManager? _sensorManager;
    private Sensor? _gameRotationSensor;

    // Smoothed quaternion state (EMA in rotation space — no gimbal lock).
    // Initialised to identity orientation; first sensor reading overwrites.
    private double _sqx;
    private double _sqy;
    private double _sqz;
    private double _sqw = 1.0;

    // Trace logging — every 60th sensor event (~1 s at 60 Hz) to avoid flooding logcat.
    private int _rawLogCounter;

    // -------------------------------------------------------------------------
    // IDeviceOrientationService
    // -------------------------------------------------------------------------

    public DevicePose CurrentPose => new(_headingDegrees, _pitchDegrees, _rollDegrees)
        { Quaternion = (_sqx, _sqy, _sqz, _sqw) };

    public bool IsAvailable
    {
        get
        {
            EnsureSensorManager();
            return _gameRotationSensor != null;
        }
    }

    public Task<bool> StartAsync()
    {
        EnsureSensorManager();

        if (_gameRotationSensor == null)
            return Task.FromResult(false);

        if (_isRunning)
            return Task.FromResult(true);

        try
        {
            // SamplingPeriodUs = 16 666 µs ≈ 60 Hz — same rate as SensorSpeed.UI.
            _sensorManager!.RegisterListener(this, _gameRotationSensor, SensorDelay.Ui);
            _isRunning = true;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidOrientationService] Start failed: {ex}");
            return Task.FromResult(false);
        }
    }

    public void Stop()
    {
        if (!_isRunning)
            return;

        try
        {
            _sensorManager?.UnregisterListener(this);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidOrientationService] Stop failed: {ex}");
        }
        finally
        {
            _isRunning = false;
            _initialized = false;
        }
    }

    public event EventHandler<DevicePose>? PoseChanged;

    // -------------------------------------------------------------------------
    // ISensorEventListener
    // -------------------------------------------------------------------------

    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy) { }

    public void OnSensorChanged(SensorEvent? e)
    {
        if (e?.Values == null || e.Values.Count < 4)
            return;

        // Android TYPE_GAME_ROTATION_VECTOR reports a unit quaternion as
        // [x, y, z, w] (indices 0-3). The fifth value (index 4) is a heading
        // accuracy estimate — absent for GAME_ROTATION_VECTOR, which is fine.
        //
        // Android sensor quaternion convention: the quaternion rotates the
        // device coordinate system into the Earth coordinate system, same as
        // TYPE_ROTATION_VECTOR. However GAME_ROTATION_VECTOR uses an arbitrary
        // reference in the horizontal plane rather than magnetic north.
        //
        // The columns of the rotation matrix (which we derive from the quaternion)
        // give each device axis expressed in the Earth frame.
        // Device axes: X = screen right, Y = screen up / phone top, Z = out of screen.
        // Earth axes:  X (East-equivalent), Y (North-equivalent), Z (Up).
        double x = e.Values[0];
        double y = e.Values[1];
        double z = e.Values[2];
        double w = e.Values[3];

        // Normalise (Android guarantees unit length, but floating-point drift can occur).
        var len = Math.Sqrt(x * x + y * y + z * z + w * w);
        if (len < 1e-10) return;
        x /= len; y /= len; z /= len; w /= len;

        // Device Y-axis in Earth frame: direction phone top edge points.
        var ey = 2.0 * ((x * y) + (w * z));
        var ny = 1.0 - (2.0 * ((x * x) + (z * z)));
        var uy = 2.0 * ((y * z) - (w * x));

        // Device X-axis in Earth frame: direction phone right edge points.
        var ex = 1.0 - (2.0 * ((y * y) + (z * z)));
        var nx = 2.0 * ((x * y) - (w * z));
        var ux = 2.0 * ((x * z) + (w * y));

        // Heading: azimuth of device top in the horizontal plane. With
        // GAME_ROTATION_VECTOR this is relative to the initial device heading
        // (not magnetic north), which is exactly what we want — stable tracking,
        // no magnetometer noise.
        var rawHeading = (Math.Atan2(ey, ny) * RadToDeg + 360.0) % 360.0;

        // Pitch: elevation of device top above/below the horizon.
        var rawPitch = Math.Asin(Math.Clamp(uy, -1.0, 1.0)) * RadToDeg;

        // Roll: see DeviceOrientationService for formula derivation.
        var fHoriz = Math.Sqrt((ey * ey) + (ny * ny));
        double rawRoll;
        if (fHoriz < 1e-6)
        {
            rawRoll = 0.0;
        }
        else
        {
            var rollSin = ((ex * uy * ey) + (nx * uy * ny)) / fHoriz - (ux * fHoriz);
            var rollCos = ((ex * ny) - (nx * ey)) / fHoriz;
            rawRoll = Math.Atan2(rollSin, rollCos) * RadToDeg;
        }

        // Throttled raw-sensor trace — filter logcat with tag 'AOS.raw'.
        if (++_rawLogCounter % 60 == 0)
            System.Diagnostics.Debug.WriteLine(
                $"[AOS.raw#{_rawLogCounter}] q=({x:F4},{y:F4},{z:F4},{w:F4})  " +
                $"ey={ey:F4} ny={ny:F4} uy={uy:F4}  " +
                $"heading={rawHeading:F1}° pitch={rawPitch:F1}° roll={rawRoll:F1}°");

        // Pass raw quaternion along with Euler angles so ApplyReading can maintain
        // quaternion-space EMA for gimbal-lock-free camera matrix construction.
        MainThread.BeginInvokeOnMainThread(() => ApplyReading(x, y, z, w, rawHeading, rawPitch, rawRoll));
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void EnsureSensorManager()
    {
        if (_sensorManager != null)
            return;

        if (Android.App.Application.Context.GetSystemService(Context.SensorService)
            is SensorManager sm)
        {
            _sensorManager = sm;
            _gameRotationSensor = sm.GetDefaultSensor(SensorType.GameRotationVector);
        }
    }

    private void ApplyReading(
        double rawQx, double rawQy, double rawQz, double rawQw,
        double rawHeading, double rawPitch, double rawRoll)
    {
        if (!_initialized)
        {
            _headingDegrees = rawHeading;
            _pitchDegrees   = rawPitch;
            _rollDegrees    = rawRoll;
            _lastFiredHeadingDegrees = rawHeading;
            _lastFiredPitchDegrees   = rawPitch;
            _sqx = rawQx; _sqy = rawQy; _sqz = rawQz; _sqw = rawQw;
            _initialized = true;
            PoseChanged?.Invoke(this, CurrentPose);
            return;
        }

        // Quaternion LERP + renormalise — stable through gimbal lock at zenith.
        // Ensure shortest arc (prevent wrapping the long way round).
        if ((_sqx * rawQx + _sqy * rawQy + _sqz * rawQz + _sqw * rawQw) < 0)
        {
            rawQx = -rawQx; rawQy = -rawQy; rawQz = -rawQz; rawQw = -rawQw;
        }
        _sqx += QuaternionAlpha * (rawQx - _sqx);
        _sqy += QuaternionAlpha * (rawQy - _sqy);
        _sqz += QuaternionAlpha * (rawQz - _sqz);
        _sqw += QuaternionAlpha * (rawQw - _sqw);
        var qLen = Math.Sqrt(_sqx * _sqx + _sqy * _sqy + _sqz * _sqz + _sqw * _sqw);
        if (qLen > 1e-10) { _sqx /= qLen; _sqy /= qLen; _sqz /= qLen; _sqw /= qLen; }

        // Heading — circular EMA.
        var headingDiff = rawHeading - _headingDegrees;
        if (headingDiff >  180.0) headingDiff -= 360.0;
        if (headingDiff < -180.0) headingDiff += 360.0;
        var headingAlpha = AdaptiveAlpha(Math.Abs(headingDiff), HeadingSmallAlpha, HeadingLargeAlpha, HeadingTransitionDeg);
        _headingDegrees = ((_headingDegrees + headingAlpha * headingDiff) + 360.0) % 360.0;

        // Pitch — linear EMA.
        var pitchDiff  = rawPitch - _pitchDegrees;
        var pitchAlpha = AdaptiveAlpha(Math.Abs(pitchDiff), PitchSmallAlpha, PitchLargeAlpha, PitchTransitionDeg);
        _pitchDegrees += pitchAlpha * pitchDiff;

        // Roll — circular EMA.
        var rollDiff = rawRoll - _rollDegrees;
        if (rollDiff >  180.0) rollDiff -= 360.0;
        if (rollDiff < -180.0) rollDiff += 360.0;
        _rollDegrees = ((_rollDegrees + RollAlpha * rollDiff) + 360.0) % 360.0;
        if (_rollDegrees > 180.0) _rollDegrees -= 360.0;

        FireIfAboveThreshold();
    }

    private void FireIfAboveThreshold()
    {
        var headingDiff = _headingDegrees - _lastFiredHeadingDegrees;
        if (headingDiff >  180.0) headingDiff -= 360.0;
        if (headingDiff < -180.0) headingDiff += 360.0;

        var pitchDiff = _pitchDegrees - _lastFiredPitchDegrees;

        if (Math.Abs(headingDiff) < HeadingThresholdDeg
         && Math.Abs(pitchDiff)   < PitchThresholdDeg)
        {
            return;
        }

        _lastFiredHeadingDegrees = _headingDegrees;
        _lastFiredPitchDegrees   = _pitchDegrees;

        // Log every fired pose — filter logcat with tag 'AOS.fire'.
        System.Diagnostics.Debug.WriteLine(
            $"[AOS.fire] heading={_headingDegrees:F1}° pitch={_pitchDegrees:F1}° roll={_rollDegrees:F1}°");

        PoseChanged?.Invoke(this, CurrentPose);
    }

    private static double AdaptiveAlpha(double absDelta, double smallAlpha, double largeAlpha, double transitionDeg)
    {
        if (absDelta >= transitionDeg)
            return largeAlpha;
        var t = absDelta / transitionDeg;
        return smallAlpha + (t * (largeAlpha - smallAlpha));
    }
}
