using AstroFinder.Domain.AR;
using Microsoft.Maui.Devices.Sensors;

namespace AstroFinder.App.Services;

/// <summary>
/// MAUI-based implementation of <see cref="IDeviceOrientationService"/> that derives
/// tilt-compensated heading and pitch from the <see cref="OrientationSensor"/> quaternion.
///
/// The MAUI OrientationSensor provides the rotation of the device's coordinate system
/// relative to the Earth's coordinate system (Earth: X=East, Y=North, Z=up;
/// Device: X=right, Y=up/top, Z=out-of-screen).
///
/// The quaternion columns give each device axis in Earth frame.  We use the device Y-axis
/// (pointing toward the phone's top edge in portrait) projected onto the Earth frame:
///   Earth representation of device Y = (2(xy+wz), 1-2(x²+z²), 2(yz-wx))
///                                        East        North        Up
///   Heading = atan2(East, North)  → 0°=North, 90°=East, clockwise from north.
///   Pitch   = asin(Up)            → 0°=horizon, +90°=zenith, –90°=nadir.
///
/// Using the full quaternion means heading is fully tilt-compensated: tilting the
/// phone to any angle while pointing at the same spot on the horizon produces the
/// same compass heading, eliminating the "moves to a completely new position as you
/// move the phone" symptom caused by un-compensated flat-plane compass readings.
/// </summary>
public sealed class DeviceOrientationService : IDeviceOrientationService, IDisposable
{
    private const double RadToDeg = 180.0 / Math.PI;

    // Adaptive EMA smoothing: small movements get heavy smoothing (low alpha),
    // large movements get fast tracking (high alpha).
    private const double HeadingSmallAlpha    = 0.12;
    private const double HeadingLargeAlpha    = 0.65;
    private const double HeadingTransitionDeg = 12.0;

    private const double PitchSmallAlpha    = 0.12;
    private const double PitchLargeAlpha    = 0.55;
    private const double PitchTransitionDeg = 10.0;

    // Minimum smoothed change required to fire PoseChanged.
    private const double HeadingThresholdDeg = 0.4;
    private const double PitchThresholdDeg   = 0.3;

    // Roll uses a fixed moderate alpha — roll changes are typically slower and
    // we want smooth overlay rotation without excessive lag.
    private const double RollAlpha = 0.20;

    private double _headingDegrees;
    private double _pitchDegrees;
    private double _rollDegrees;
    private double _lastFiredHeadingDegrees;
    private double _lastFiredPitchDegrees;
    private bool _isRunning;
    private bool _initialized;

    // -------------------------------------------------------------------------
    // IDeviceOrientationService
    // -------------------------------------------------------------------------

    public DevicePose CurrentPose => new(_headingDegrees, _pitchDegrees, _rollDegrees);

    public bool IsAvailable => OrientationSensor.Default.IsSupported;

    public Task<bool> StartAsync()
    {
        if (!IsAvailable)
            return Task.FromResult(false);

        if (_isRunning)
            return Task.FromResult(true);

        try
        {
            OrientationSensor.Default.ReadingChanged += OnOrientationReadingChanged;
            // SensorSpeed.UI: events dispatched on main thread, ~60 ms interval.
            OrientationSensor.Default.Start(SensorSpeed.UI);
            _isRunning = true;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceOrientationService] Start failed: {ex}");
            OrientationSensor.Default.ReadingChanged -= OnOrientationReadingChanged;
            return Task.FromResult(false);
        }
    }

    public void Stop()
    {
        if (!_isRunning)
            return;

        try
        {
            OrientationSensor.Default.Stop();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceOrientationService] Stop failed: {ex}");
        }
        finally
        {
            OrientationSensor.Default.ReadingChanged -= OnOrientationReadingChanged;
            _isRunning = false;
            _initialized = false;
        }
    }

    public event EventHandler<DevicePose>? PoseChanged;

    // -------------------------------------------------------------------------
    // Sensor callback (called on main thread via SensorSpeed.UI)
    // -------------------------------------------------------------------------

    private void OnOrientationReadingChanged(object? sender, OrientationSensorChangedEventArgs e)
    {
        var q = e.Reading.Orientation;   // System.Numerics.Quaternion
        double x = q.X, y = q.Y, z = q.Z, w = q.W;

        // Device Y-axis in Earth frame (East, North, Up components).
        // This is the direction the phone's top edge points (portrait forward direction).
        var ey = 2.0 * ((x * y) + (w * z));
        var ny = 1.0 - (2.0 * ((x * x) + (z * z)));
        var uy = 2.0 * ((y * z) - (w * x));

        // Device X-axis in Earth frame (East, North, Up components).
        // This is the direction the phone's right edge points.
        var ex = 1.0 - (2.0 * ((y * y) + (z * z)));
        var nx = 2.0 * ((x * y) - (w * z));
        var ux = 2.0 * ((x * z) + (w * y));

        // Heading: azimuth of device top (Y) in the horizontal plane, clockwise from north.
        var rawHeading = (Math.Atan2(ey, ny) * RadToDeg + 360.0) % 360.0;

        // Pitch: angle between device top (Y) and the horizontal plane.
        var rawPitch = Math.Asin(Math.Clamp(uy, -1.0, 1.0)) * RadToDeg;

        // Roll: rotation of the device around its forward axis.
        //
        // We want the angle the device X-axis has rotated from the "no-roll reference right"
        // direction.  No-roll reference right = the horizontal direction perpendicular to forward
        // in the sense of (forward × zenith), normalised:
        //   refRight = (ny, -ey, 0) / |horizontal_component_of_forward|
        //
        // Roll = atan2( (F × R) · Fhat, RefRight · R ) where F is forward, R is device-right.
        //
        // Simplified: since F and RefRight are already computed above,
        //   refRight_horiz = sqrt(ey²+ny²)   — the horizontal magnitude of forward
        //   (F × R) · F_hat — the signed component of R around F
        //   = (ny*ux - uy*nx)*ey + (uy*ex - ey*ux)*ny + (ey*nx - ny*ex)*uy  (normalised by |F|)
        //
        // For the simpler numerically-stable case, express roll as the angle of the device
        // X-axis projected perpendicular to the forward vector:
        //   R_perp = R - (R·F)*F  (component of device-right perpendicular to forward)
        //   For roll=0: R_perp points toward the "local right on the horizon plane + sky pull"
        //
        // A practical formula that is stable when the phone is held with any heading/pitch:
        //   roll = atan2(ux, -(nx*ny + ex*ey) / fHoriz)
        // but this is complex.  Use the standard approach:
        //   The "zero roll" direction of device-right in the perp-to-forward plane is
        //   RefRight = (ny, -ey, 0) / fHoriz.
        //   Its perpendicular within that plane = F_hat × RefRight
        //                                       = (ny*uy/fHoriz, -ey*uy/fHoriz, -(fHoriz))  / 1
        //   => roll = atan2( R · (F × RefRight), R · RefRight )
        //           = atan2( R · (-fHoriz_element), R · RefRight )
        // Working out F × RefRight for F=(ey,ny,uy) and RefRight=(ny,-ey,0)/fHoriz:
        //   F × RefRight = ( ny*0 - uy*(-ey), uy*ny - ey*0, ey*(-ey) - ny*ny ) / fHoriz
        //                = ( uy*ey, uy*ny, -(ey²+ny²) ) / fHoriz
        //                = ( uy*ey/fHoriz, uy*ny/fHoriz, -fHoriz )
        // so:
        //   roll_sin = R · (F × RefRight) = ex*uy*ey/fH + nx*uy*ny/fH - ux*fH
        //   roll_cos = R · RefRight       = ex*ny/fH - nx*ey/fH
        var fHoriz = Math.Sqrt((ey * ey) + (ny * ny));

        double rawRoll;
        if (fHoriz < 1e-6)
        {
            // Device is pointing near-zenith — roll is ill-defined from heading alone.
            // Fall through with 0 roll; the overlay is nearly 2D in this case.
            rawRoll = 0.0;
        }
        else
        {
            var rollSin = ((ex * uy * ey) + (nx * uy * ny)) / fHoriz - (ux * fHoriz);
            var rollCos = ((ex * ny) - (nx * ey)) / fHoriz;
            rawRoll = Math.Atan2(rollSin, rollCos) * RadToDeg;
        }

        if (!_initialized)
        {
            _headingDegrees = rawHeading;
            _pitchDegrees   = rawPitch;
            _rollDegrees    = rawRoll;
            _lastFiredHeadingDegrees = rawHeading;
            _lastFiredPitchDegrees   = rawPitch;
            _initialized = true;

            // First pose — fire unconditionally.
            PoseChanged?.Invoke(this, CurrentPose);
            return;
        }

        // Circular shortest-path EMA for heading to avoid the 359°→1° wrap glitch.
        var headingDiff = rawHeading - _headingDegrees;
        if (headingDiff >  180.0) headingDiff -= 360.0;
        if (headingDiff < -180.0) headingDiff += 360.0;
        var headingAlpha = AdaptiveAlpha(Math.Abs(headingDiff), HeadingSmallAlpha, HeadingLargeAlpha, HeadingTransitionDeg);
        _headingDegrees = ((_headingDegrees + headingAlpha * headingDiff) + 360.0) % 360.0;

        // Pitch EMA (no wrap needed — range is ±90°).
        var pitchDiff  = rawPitch - _pitchDegrees;
        var pitchAlpha = AdaptiveAlpha(Math.Abs(pitchDiff), PitchSmallAlpha, PitchLargeAlpha, PitchTransitionDeg);
        _pitchDegrees += pitchAlpha * pitchDiff;

        // Roll EMA with circular wrap.
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
        PoseChanged?.Invoke(this, CurrentPose);
    }

    private static double AdaptiveAlpha(double absDelta, double smallAlpha, double largeAlpha, double transitionDeg)
    {
        if (absDelta >= transitionDeg)
            return largeAlpha;
        var t = absDelta / transitionDeg;
        return smallAlpha + (t * (largeAlpha - smallAlpha));
    }

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    public void Dispose() => Stop();
}
