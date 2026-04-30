namespace AstroFinder.App.Services;

/// <summary>
/// Owns the camera viewfinder surface and reports the horizontal field of view.
/// Platform implementations wire a native camera surface; the MAUI layer sees
/// only start/stop and field-of-view.
///
/// Registration is platform-conditional in <c>MauiProgram.cs</c>:
/// <code>
/// #if ANDROID
///     builder.Services.AddSingleton&lt;ICameraPreviewService, AndroidCameraPreviewService&gt;();
/// #elif IOS
///     builder.Services.AddSingleton&lt;ICameraPreviewService, IosCameraPreviewService&gt;();
/// #endif
/// </code>
/// On Windows (desktop dev/test), register <see cref="NullCameraPreviewService"/> which
/// returns a fixed FOV and does not start any camera.
/// </summary>
public interface ICameraPreviewService
{
    /// <summary>Horizontal field of view in degrees reported by the device camera.</summary>
    double HorizontalFovDegrees { get; }

    /// <summary>Starts the camera preview surface.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the camera preview and releases the surface.</summary>
    Task StopAsync();
}
