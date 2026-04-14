using AstroFinder.Domain.AR.Calibration;
using CommunityToolkit.Maui.Views;

namespace AstroFinder.App.Services;

/// <summary>
/// Default no-op frame source. Keeps calibrated mode safe when live pixel access is unavailable.
/// </summary>
public sealed class NullArFrameSource : IArFrameSource
{
    public void Attach(CameraView cameraView)
    {
    }

    public void Detach()
    {
    }

    public void SetActive(bool isActive)
    {
    }

    public bool TryGetLatestFrame(out GrayImageFrame frame)
    {
        frame = new GrayImageFrame(0, 0, []);
        return false;
    }
}
