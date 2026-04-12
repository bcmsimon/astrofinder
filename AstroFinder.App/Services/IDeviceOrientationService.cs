using AstroFinder.Domain.AR;

namespace AstroFinder.App.Services;

/// <summary>
/// Provides real-time device orientation data (compass heading + elevation pitch)
/// by fusing the hardware compass and accelerometer.
/// </summary>
public interface IDeviceOrientationService
{
    /// <summary>The most recently computed device pose.</summary>
    DevicePose CurrentPose { get; }

    /// <summary>
    /// True when the underlying sensors (compass + accelerometer) are
    /// present and usable on this device.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Starts the sensors.  Returns <c>true</c> if started successfully,
    /// <c>false</c> if the sensors are unavailable or an error occurred.
    /// </summary>
    Task<bool> StartAsync();

    /// <summary>Stops the sensors and releases event subscriptions.</summary>
    void Stop();

    /// <summary>
    /// Raised on the main thread whenever a new orientation reading is available.
    /// </summary>
    event EventHandler<DevicePose>? PoseChanged;
}
