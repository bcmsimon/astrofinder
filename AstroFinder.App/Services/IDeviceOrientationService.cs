using AstroFinder.Domain.AR;

namespace AstroFinder.App.Services;

/// <summary>
/// Provides real-time device orientation as a <see cref="PoseMatrix"/>
/// that transforms ENU world vectors into camera coordinates.
/// </summary>
public interface IDeviceOrientationService
{
    /// <summary>The most recently computed camera pose matrix.</summary>
    PoseMatrix CurrentPose { get; }

    /// <summary>
    /// True when the underlying sensors are present and usable on this device.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Starts the sensors. Returns <c>true</c> if started successfully.
    /// </summary>
    Task<bool> StartAsync();

    /// <summary>Stops the sensors and releases event subscriptions.</summary>
    void Stop();

    /// <summary>
    /// Raised on the main thread whenever a new orientation reading is available.
    /// </summary>
    event EventHandler<PoseMatrix>? PoseChanged;

    /// <summary>
    /// Stores a yaw offset so the current forward direction becomes the new reference.
    /// </summary>
    void Recenter();
}
