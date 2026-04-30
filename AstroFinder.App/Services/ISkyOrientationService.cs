using AstroFinder.Engine.Geometry;

namespace AstroFinder.App.Services;

/// <summary>
/// Fuses accelerometer, gyroscope, and compass to produce a sky-pointing vector
/// at approximately 15 Hz while running.
/// Cross-platform implementation uses only MAUI Essentials — no platform split.
/// </summary>
public interface ISkyOrientationService
{
    /// <summary>Fired at ~15 Hz while running, carrying the current pointing vector.</summary>
    event EventHandler<SkyOrientation> OrientationChanged;

    /// <summary>Starts all underlying sensors. Safe to call multiple times.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops all underlying sensors and detaches event subscriptions.</summary>
    Task StopAsync();
}
