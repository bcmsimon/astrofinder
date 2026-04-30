namespace AstroFinder.App.Services;

/// <summary>
/// No-op implementation of <see cref="ICameraPreviewService"/> used on platforms
/// that do not support the live camera overlay (Windows desktop, simulator).
/// Returns a typical smartphone wide-angle FOV so projection tests remain usable.
/// </summary>
public sealed class NullCameraPreviewService : ICameraPreviewService
{
    /// <summary>Fixed 65° FOV — approximates a typical smartphone main camera.</summary>
    public double HorizontalFovDegrees => 65.0;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
}
