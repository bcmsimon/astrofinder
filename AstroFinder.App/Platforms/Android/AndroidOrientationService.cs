using Android.Content;
using Android.Hardware;
using AstroFinder.Domain.AR;
using Microsoft.Maui.ApplicationModel;

namespace AstroFinder.App.Services;

/// <summary>
/// Android-specific orientation service that produces a <see cref="PoseMatrix"/>
/// using <c>SensorManager.GetRotationMatrixFromVector</c>.
///
/// Pipeline:
///   1. GetRotationMatrixFromVector -> raw rotation matrix (device->world)
///   2. Transpose -> world->device
///   3. Apply yaw offset for recenter support
///   4. Multiply by diag(1,1,-1) -> camera-from-device (rear camera Z flip)
///   5. Smooth in matrix space (EMA + Gram-Schmidt re-orthonormalization)
///   6. Emit PoseMatrix
/// </summary>
public sealed class AndroidOrientationService : Java.Lang.Object, IDeviceOrientationService, ISensorEventListener
{
    private SensorManager? _sensorManager;
    private Sensor? _rotationVectorSensor;
    private bool _isRunning;

    private PoseMatrix? _smoothedPose;
    private double _yawOffsetRad;

    public PoseMatrix CurrentPose => _smoothedPose ?? PoseMatrix.Identity();

    public bool IsAvailable
    {
        get
        {
            EnsureSensorManager();
            return _rotationVectorSensor != null;
        }
    }

    public Task<bool> StartAsync()
    {
        EnsureSensorManager();

        if (_rotationVectorSensor == null)
            return Task.FromResult(false);

        if (_isRunning)
            return Task.FromResult(true);

        try
        {
            _sensorManager!.RegisterListener(this, _rotationVectorSensor, SensorDelay.Game);
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
        if (!_isRunning) return;

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

    // ISensorEventListener

    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy) { }

    public void OnSensorChanged(SensorEvent? e)
    {
        if (e?.Values == null || e.Values.Count < 4) return;
        if (e.Sensor?.Type != SensorType.RotationVector
            && e.Sensor?.Type != SensorType.GameRotationVector)
            return;

        var values = new float[e.Values.Count];
        for (int i = 0; i < values.Length; i++)
            values[i] = e.Values[i];

        // Step 1: Get rotation matrix from sensor quaternion.
        // GetRotationMatrixFromVector returns a device-to-world matrix R
        // where world = R * device (ENU: +X east, +Y north, +Z up).
        var rawRotation = new float[9];
        SensorManager.GetRotationMatrixFromVector(rawRotation, values);

        // Step 2: Transpose to get world-to-device.
        // No RemapCoordinateSystem needed: portrait device axes (+X right,
        // +Y up, +Z out-of-screen) map directly to camera axes with a Z flip.
        var worldToDevice = new double[9];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                worldToDevice[r * 3 + c] = rawRotation[c * 3 + r];

        // Step 3: Apply yaw offset.
        var adjusted = ApplyYawOffset(worldToDevice, _yawOffsetRad);

        // Step 4: Camera-from-device axis remapping for portrait phone.
        // Android device frame: X=right, Y=top-of-phone, Z=out-of-screen-face.
        // Camera frame:         X=right, Y=up-on-screen, Z=forward-through-back-camera.
        // For portrait phone the back camera looks out through -Z (device),
        // and "up" in camera space is the top-of-phone direction (device Y)
        // when viewing through the back camera from behind. But the camera
        // looks through the BACK of the device, so:
        //   cam X =  device X  (right stays right)
        //   cam Y = -device Z  (out-of-screen flipped = behind-screen = camera up)
        //   cam Z =  device Y  (top-of-phone = camera forward direction)
        var cameraFromDevice = new double[]
        {
            1.0,  0.0, 0.0,
            0.0,  0.0, -1.0,
            0.0,  1.0, 0.0,
        };

        // Step 5: pose = cameraFromDevice * adjusted (world->camera).
        var pose = new PoseMatrix(Multiply3x3(cameraFromDevice, adjusted));

        // Step 6: Smooth in matrix space.
        _smoothedPose = ArMath.SmoothPose(_smoothedPose, pose);

        var result = _smoothedPose.Value;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PoseChanged?.Invoke(this, result);
        });
    }

    // Helpers

    private void EnsureSensorManager()
    {
        if (_sensorManager != null) return;

        if (Android.App.Application.Context.GetSystemService(Context.SensorService)
            is SensorManager sm)
        {
            _sensorManager = sm;
            _rotationVectorSensor = sm.GetDefaultSensor(SensorType.RotationVector)
                ?? sm.GetDefaultSensor(SensorType.GameRotationVector);
        }
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
}
