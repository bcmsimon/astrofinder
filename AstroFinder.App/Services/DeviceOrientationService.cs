using AstroFinder.Domain.AR;
using Microsoft.Maui.Devices.Sensors;

namespace AstroFinder.App.Services;

/// <summary>
/// MAUI-based implementation of <see cref="IDeviceOrientationService"/> that builds
/// a <see cref="PoseMatrix"/> from the <see cref="OrientationSensor"/> quaternion.
///
/// Follows the same pipeline as <c>AndroidOrientationService</c> and the reference:
///   1. Quaternion -> rotation matrix (device->world, equivalent to getRotationMatrixFromVector)
///   2. Transpose -> world->device
///   3. Camera-from-device: diag(1,1,-1) for rear camera Z flip
///   4. Apply yaw offset for recenter
///   5. Smooth in matrix space (EMA + Gram-Schmidt)
/// </summary>
public sealed class DeviceOrientationService : IDeviceOrientationService, IDisposable
{
    private bool _isRunning;
    private PoseMatrix? _smoothedPose;
    private double _yawOffsetRad;

    public PoseMatrix CurrentPose => _smoothedPose ?? PoseMatrix.Identity();

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
            OrientationSensor.Default.Start(SensorSpeed.Game);
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
        if (!_isRunning) return;

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
            _smoothedPose = null;
        }
    }

    public event EventHandler<PoseMatrix>? PoseChanged;

    public void Recenter()
    {
        if (_smoothedPose is null) return;
        var m = _smoothedPose.Value.M;
        _yawOffsetRad = -Math.Atan2(m[6], m[7]);
    }

    private void OnOrientationReadingChanged(object? sender, OrientationSensorChangedEventArgs e)
    {
        var q = e.Reading.Orientation;
        double x = q.X, y = q.Y, z = q.Z, w = q.W;

        // Build rotation matrix from quaternion (device->world).
        // This is equivalent to Android's SensorManager.getRotationMatrixFromVector.
        // Standard Hamilton quaternion to rotation matrix R:
        var r00 = 1.0 - 2.0 * (y * y + z * z);
        var r01 = 2.0 * (x * y - w * z);
        var r02 = 2.0 * (x * z + w * y);
        var r10 = 2.0 * (x * y + w * z);
        var r11 = 1.0 - 2.0 * (x * x + z * z);
        var r12 = 2.0 * (y * z - w * x);
        var r20 = 2.0 * (x * z - w * y);
        var r21 = 2.0 * (y * z + w * x);
        var r22 = 1.0 - 2.0 * (x * x + y * y);

        // RemapCoordinateSystem equivalent for portrait: swap Y and Z axes.
        // Original: columns are X=East, Y=North, Z=Up device axes in world.
        // After remap for portrait upright: we want device Y (top edge) -> new Z,
        // device Z (out of screen) -> new -Y. But the simplest approach is:
        // Just transpose the device->world matrix to get world->device directly.
        // For portrait phone held upright: device X=right, Y=up, Z=out of screen.
        // R maps device->world. R^T maps world->device.
        var worldToDevice = new double[]
        {
            r00, r10, r20,
            r01, r11, r21,
            r02, r12, r22,
        };

        // Camera-from-device: flip Z for rear camera.
        var cameraFromDevice = new double[]
        {
            1.0, 0.0,  0.0,
            0.0, 1.0,  0.0,
            0.0, 0.0, -1.0,
        };

        var adjusted = ApplyYawOffset(worldToDevice, _yawOffsetRad);
        var pose = new PoseMatrix(Multiply3x3(cameraFromDevice, adjusted));
        _smoothedPose = ArMath.SmoothPose(_smoothedPose, pose);

        var result = _smoothedPose.Value;
        PoseChanged?.Invoke(this, result);
    }

    private static double[] ApplyYawOffset(double[] matrix, double yaw)
    {
        var c = Math.Cos(yaw);
        var s = Math.Sin(yaw);
        var yawMatrix = new double[]
        {
            c, -s, 0.0,
            s,  c, 0.0,
            0.0, 0.0, 1.0,
        };
        return Multiply3x3(matrix, yawMatrix);
    }

    private static double[] Multiply3x3(double[] a, double[] b)
    {
        var result = new double[9];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                result[r * 3 + c] =
                    a[r * 3 + 0] * b[0 * 3 + c] +
                    a[r * 3 + 1] * b[1 * 3 + c] +
                    a[r * 3 + 2] * b[2 * 3 + c];
        return result;
    }

    public void Dispose() => Stop();
}
