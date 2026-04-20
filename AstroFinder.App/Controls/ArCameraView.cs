using AstroFinder.Domain.AR.Calibration;

namespace AstroFinder.App.Controls;

/// <summary>
/// Cross-platform MAUI view that hosts the ARCore (Android) or ARKit (iOS) camera
/// with tracking. On Android, backed by <c>ArCoreGLView</c> via a platform handler.
/// </summary>
public class ArCameraView : View
{
    private static readonly GrayImageFrame EmptyFrame =
        new(0, 0, []);
    private readonly object _frameGate = new();
    private GrayImageFrame _latestGrayFrame = EmptyFrame;

    /// <summary>
    /// Raised when ARCore/ARKit reports an error or status message.
    /// </summary>
    public event EventHandler<string>? StatusMessage;

    /// <summary>Raised when the page requests the AR session to pause.</summary>
    public event EventHandler? SessionPauseRequested;

    /// <summary>Raised when the page requests the AR session to resume.</summary>
    public event EventHandler? SessionResumeRequested;

    internal void RaiseStatusMessage(string message) =>
        StatusMessage?.Invoke(this, message);

    /// <summary>Pause the AR session (call from OnDisappearing).</summary>
    public void PauseSession() => SessionPauseRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Resume the AR session (call from OnAppearing).</summary>
    public void ResumeSession() => SessionResumeRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Raised when the page wants to set or clear the bitmap map overlay.</summary>
    public event EventHandler<MapOverlayArgs?>? MapOverlayRequested;

    /// <summary>Set the bitmap map overlay in the AR camera view.</summary>
    public void SetMapOverlay(object bitmap, float[] modelMatrix, float alpha = 0.80f) =>
        MapOverlayRequested?.Invoke(this, new MapOverlayArgs(bitmap, modelMatrix, alpha));

    /// <summary>Clear the bitmap map overlay.</summary>
    public void ClearMapOverlay() =>
        MapOverlayRequested?.Invoke(this, null);

    public bool TryGetLatestGrayFrame(out GrayImageFrame frame)
    {
        lock (_frameGate)
        {
            frame = _latestGrayFrame;
            return frame.Width > 0 && frame.Height > 0;
        }
    }

    internal void SetLatestGrayFrame(GrayImageFrame frame)
    {
        lock (_frameGate)
        {
            _latestGrayFrame = frame;
        }
    }
}

/// <summary>
/// Args for setting the AR map overlay. Bitmap is typed as object for cross-platform;
/// on Android it's an Android.Graphics.Bitmap.
/// </summary>
public sealed record MapOverlayArgs(object Bitmap, float[] ModelMatrix, float Alpha);
