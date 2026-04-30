using CommunityToolkit.Maui.Views;

namespace AstroFinder.App.Services;

/// <summary>
/// Cross-platform <see cref="ICameraPreviewService"/> backed by
/// <see cref="CommunityToolkit.Maui.Views.CameraView"/>.
///
/// The <see cref="CameraView"/> instance lives in <c>SkyOverlayPage.xaml</c>; this
/// service is handed a reference via <see cref="Attach"/> once the page creates the
/// control.  Start/Stop delegate directly to the CameraView lifetime methods.
/// </summary>
public sealed class CommunityToolkitCameraPreviewService : ICameraPreviewService
{
    // Typical wide-angle FOV for a smartphone main camera.
    // TODO: read the actual FOV from CameraView.SelectedCamera once available in CTK.
    private const double DefaultFovDegrees = 65.0;

    private CameraView? _cameraView;

    /// <inheritdoc/>
    public double HorizontalFovDegrees => DefaultFovDegrees;

    /// <summary>
    /// Attaches the live <see cref="CameraView"/> control from the overlay page.
    /// Must be called on the UI thread before <see cref="StartAsync"/>.
    /// </summary>
    public void Attach(CameraView cameraView)
    {
        ArgumentNullException.ThrowIfNull(cameraView);
        _cameraView = cameraView;
    }

    /// <summary>Detaches the current <see cref="CameraView"/> reference.</summary>
    public void Detach() => _cameraView = null;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cameraView is null)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_cameraView is null) return;
            // CommunityToolkit.Maui CameraView starts automatically when visible.
            // Explicitly start the camera session if it has been stopped.
            _cameraView.IsVisible = true;
        });
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        if (_cameraView is null)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_cameraView is null) return;
            _cameraView.IsVisible = false;
        });
    }
}
