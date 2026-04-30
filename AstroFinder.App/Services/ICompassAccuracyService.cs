namespace AstroFinder.App.Services;

/// <summary>
/// Reports the magnetometer / compass calibration status of the device.
/// When accuracy is low the AR overlay should prompt the user to perform
/// a figure-of-8 calibration gesture.
/// </summary>
public interface ICompassAccuracyService
{
    /// <summary>
    /// Raised whenever the calibration requirement changes.
    /// True = calibration needed; False = calibration sufficient.
    /// </summary>
    event EventHandler<bool> CalibrationNeededChanged;

    /// <summary>Current calibration state.</summary>
    bool IsCalibrationNeeded { get; }

    void Start();
    void Stop();
}
