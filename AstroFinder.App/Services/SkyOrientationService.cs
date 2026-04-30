using AstroFinder.Engine.Geometry;
using Microsoft.Maui.Devices.Sensors;

namespace AstroFinder.App.Services;

/// <summary>
/// Cross-platform implementation of <see cref="ISkyOrientationService"/> that uses:
/// <list type="bullet">
///   <item><see cref="OrientationSensor"/> — device quaternion (gyro + accel + mag fusion handled by OS)</item>
/// </list>
/// The OS-level <c>TYPE_ROTATION_VECTOR</c> sensor already fuses gyroscope, accelerometer,
/// and magnetometer internally. No external compass blending is applied here — adding a
/// second complementary filter on top of the fused output causes azimuth snapping when
/// the slower magnetometer reading catches up.
/// </summary>
public sealed class SkyOrientationService : ISkyOrientationService, IDisposable
{
    private const double Rad2Deg = 180.0 / Math.PI;

    // EMA smoothing on the fused quaternion — suppresses per-tick sensor noise without
    // introducing the azimuth-snapping of a secondary complementary filter.
    // 20% new reading / 80% history → ~80 ms time-constant at UI speed (≈60 Hz).
    private const double SmoothAlpha = 0.20;
    private double _sx, _sy, _sz, _sw = 1.0;
    private bool _hasSmooth;

    private readonly SensorSpeed _speed;
    private bool _running;

    public event EventHandler<SkyOrientation>? OrientationChanged;

    public SkyOrientationService(SensorSpeed speed = SensorSpeed.UI)
    {
        _speed = speed;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_running)
            return Task.CompletedTask;

        try
        {
            if (OrientationSensor.Default.IsSupported)
            {
                OrientationSensor.Default.ReadingChanged += OnOrientationChanged;
                OrientationSensor.Default.Start(_speed);
            }

            _running = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkyOrientationService] Start failed: {ex}");
            StopSensors();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (!_running)
            return Task.CompletedTask;

        StopSensors();
        _running = false;
        return Task.CompletedTask;
    }

    public void Dispose() => StopAsync().GetAwaiter().GetResult();

    // -------------------------------------------------------------------------

    private void OnOrientationChanged(object? sender, OrientationSensorChangedEventArgs e)
    {
        var q = e.Reading.Orientation;

        // Android TYPE_ROTATION_VECTOR gives a quaternion that rotates vectors FROM device
        // frame TO world frame (ENU: X=East, Y=North, Z=Up).
        // HOWEVER: MAUI's OrientationSensor wraps the Android sensor and may present the
        // quaternion as the INVERSE (world-to-device), which is its conjugate.
        // Using the conjugate = transpose of rotation matrix = swapping off-diagonal signs.
        //
        // Device frame (portrait): X=right, Y=up, Z=toward-user (out of screen).
        // Camera points INTO the screen = -Z_device.
        //
        // Rotation matrix column 2 (third column) = R * e_z, where e_z = (0,0,1).
        // That gives the world direction the device +Z axis points.
        // Camera forward = -Z_device, so camera_world = -(col2 of R).
        //
        // With MAUI quaternion convention (confirmed empirically to be world-to-device /
        // conjugate of what Android docs show):
        //   Use conjugate: negate x,y,z keeping w, which transposes the matrix.
        //   After conjugation the signs on cross-terms flip.
        double x = q.X, y = q.Y, z = q.Z, w = q.W;

        // EMA smooth the quaternion to suppress high-frequency sensor noise.
        // Quaternion dot product sign-flip ensures we always interpolate on the
        // shorter arc (avoids sign-flip discontinuity near the identity quaternion).
        if (!_hasSmooth)
        {
            _sx = x; _sy = y; _sz = z; _sw = w;
            _hasSmooth = true;
        }
        else
        {
            // Ensure we interpolate on the same hemisphere.
            if (_sx * x + _sy * y + _sz * z + _sw * w < 0)
            { x = -x; y = -y; z = -z; w = -w; }

            _sx += SmoothAlpha * (x - _sx);
            _sy += SmoothAlpha * (y - _sy);
            _sz += SmoothAlpha * (z - _sz);
            _sw += SmoothAlpha * (w - _sw);

            // Renormalize to keep it a unit quaternion.
            var len = Math.Sqrt(_sx * _sx + _sy * _sy + _sz * _sz + _sw * _sw);
            _sx /= len; _sy /= len; _sz /= len; _sw /= len;
        }
        x = _sx; y = _sy; z = _sz; w = _sw;

        // R * e_z column — same formula regardless of convention direction, but signs differ
        // in the cross terms. Both conventions agree on r22 = 1 - 2(x²+y²).
        // For the MAUI / Android sensor, empirical testing shows the forward vector should be
        // derived from the POSITIVE z column (not negated) to get correct north = low azimuth.
        var r02 = 2.0 * (x * z + w * y);
        var r12 = 2.0 * (y * z - w * x);
        var r22 = 1.0 - 2.0 * (x * x + y * y);

        // Camera forward = device +Z direction in world (camera on back of phone points away
        // from the user — same as +Z_device on a phone held to look through it).
        // For a rear camera: forward_world = +col2 = (r02, r12, r22).
        // Diagnostic confirmed: altitude was inverted (screen-up gave Alt +89° instead of -90°),
        // so negate fwdUp to correct the sign.
        var fwdEast  =  r02;
        var fwdNorth =  r12;
        var fwdUp    = -r22;

        // Altitude = asin(Up component).
        var altitudeRad = Math.Asin(Math.Clamp(fwdUp, -1.0, 1.0));

        // Azimuth from OrientationSensor quaternion (already OS-fused with magnetometer).
        var gyroAzRad = Math.Atan2(fwdEast, fwdNorth);
        var azimuthDeg = (gyroAzRad * Rad2Deg + 180.0) % 360.0;

        // Camera roll: rotation of the "up" screen axis around the line of sight.
        // camera_up_in_world = R * (0, 1, 0) = (r01, r11, r21)
        var r01 = 2.0 * (x * y - w * z);
        var r11 = 1.0 - 2.0 * (x * x + z * z);
        // Project camera-up onto the tangent plane perpendicular to forward.
        // Roll = angle between projected-up and the "altitude-increasing" direction.
        // altitude-increasing direction at current az/alt = d(forward)/d(alt):
        var sinAlt = Math.Sin(altitudeRad);
        var azRad = azimuthDeg * (Math.PI / 180.0);
        var sinAz = Math.Sin(azRad);
        var cosAz = Math.Cos(azRad);
        var upDirEast = -sinAlt * sinAz;
        var upDirNorth = -sinAlt * cosAz;

        // Roll = atan2(cross, dot) of (camera-up projected) vs (altitude-direction)
        // using only the East-North plane components.
        var rollRad = Math.Atan2(
            r01 * upDirNorth - r11 * upDirEast,
            r01 * upDirEast + r11 * upDirNorth);

        var orientation = new SkyOrientation
        {
            AzimuthDegrees = azimuthDeg,
            AltitudeDegrees = altitudeRad * Rad2Deg,
            RollDegrees = rollRad * Rad2Deg,
        };

        MainThread.BeginInvokeOnMainThread(() => OrientationChanged?.Invoke(this, orientation));
    }

    private void StopSensors()
    {
        if (OrientationSensor.Default.IsSupported && OrientationSensor.Default.IsMonitoring)
        {
            try { OrientationSensor.Default.Stop(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SkyOrientationService] OrientationSensor stop: {ex}"); }
            OrientationSensor.Default.ReadingChanged -= OnOrientationChanged;
        }
    }
}
