using AstroFinder.Domain.AR.Calibration;
using CommunityToolkit.Maui.Views;

namespace AstroFinder.App.Services;

/// <summary>
/// Provides recent grayscale camera frames for AR star registration.
/// Implementations may return false when no frame is available.
/// </summary>
public interface IArFrameSource
{
    void Attach(CameraView cameraView);

    void Detach();

    void SetActive(bool isActive);

    bool TryGetLatestFrame(out GrayImageFrame frame);
}
