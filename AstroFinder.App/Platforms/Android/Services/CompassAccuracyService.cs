using Android.Hardware;
using AstroFinder.App.Services;

namespace AstroFinder.App.Platforms.Android.Services;

/// <summary>
/// Monitors the accuracy of the rotation-vector sensor (which fuses
/// gyro + accelerometer + magnetometer) and reports whether a compass
/// calibration gesture is needed.
///
/// Android fires onAccuracyChanged with one of four values:
///   0 = SENSOR_STATUS_UNRELIABLE
///   1 = SENSOR_STATUS_ACCURACY_LOW
///   2 = SENSOR_STATUS_ACCURACY_MEDIUM
///   3 = SENSOR_STATUS_ACCURACY_HIGH
///
/// We treat anything below MEDIUM (2) as "needs calibration".
/// </summary>
internal sealed class CompassAccuracyService
    : Java.Lang.Object, ISensorEventListener, ICompassAccuracyService
{
    private const int AccuracyThreshold = 2; // SENSOR_STATUS_ACCURACY_MEDIUM

    private readonly SensorManager _sensorManager;
    private Sensor? _rotationSensor;
    private bool _isCalibrationNeeded;

    public event EventHandler<bool>? CalibrationNeededChanged;

    public bool IsCalibrationNeeded => _isCalibrationNeeded;

    public CompassAccuracyService()
    {
        _sensorManager = (SensorManager)global::Android.App.Application.Context
            .GetSystemService(global::Android.Content.Context.SensorService)!;
        _rotationSensor = _sensorManager.GetDefaultSensor(SensorType.RotationVector);
    }

    public void Start()
    {
        if (_rotationSensor is not null)
            _sensorManager.RegisterListener(this, _rotationSensor, SensorDelay.Ui);
    }

    public void Stop()
    {
        _sensorManager.UnregisterListener(this);
    }

    // ISensorEventListener ---------------------------------------------------

    public void OnSensorChanged(SensorEvent? e) { /* accuracy tracked via OnAccuracyChanged */ }

    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy)
    {
        if (sensor?.Type != SensorType.RotationVector)
            return;

        bool needed = (int)accuracy < AccuracyThreshold;
        if (needed == _isCalibrationNeeded)
            return;

        _isCalibrationNeeded = needed;

        // Marshal to UI thread for safe event dispatch
        global::Android.App.Application.SynchronizationContext?.Post(
            _ => CalibrationNeededChanged?.Invoke(this, needed), null);
    }
}
