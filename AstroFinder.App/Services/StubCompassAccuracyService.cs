namespace AstroFinder.App.Services;

/// <summary>
/// No-op implementation for platforms that do not support magnetometer
/// accuracy reporting (Windows, iOS stub). Always reports calibrated.
/// </summary>
internal sealed class StubCompassAccuracyService : ICompassAccuracyService
{
#pragma warning disable CS0067
    public event EventHandler<bool>? CalibrationNeededChanged;
#pragma warning restore CS0067

    public bool IsCalibrationNeeded => false;

    public void Start() { }
    public void Stop() { }
}
