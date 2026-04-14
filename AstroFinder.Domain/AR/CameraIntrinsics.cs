namespace AstroFinder.Domain.AR;

/// <summary>
/// Camera field of view and preview surface dimensions.
/// Replaces the previous <c>CameraViewport</c> type with explicit vertical FOV.
/// </summary>
public sealed record CameraIntrinsics(
    double HorizontalFovDeg,
    double VerticalFovDeg,
    float WidthPx,
    float HeightPx)
{
    /// <summary>
    /// Creates intrinsics from a horizontal FOV, deriving the vertical FOV
    /// from the aspect ratio using the tangent formula.
    /// </summary>
    public static CameraIntrinsics FromHorizontalFov(double hFovDeg, float widthPx, float heightPx)
    {
        const double degToRad = Math.PI / 180.0;
        const double radToDeg = 180.0 / Math.PI;
        var vFovDeg = 2.0 * Math.Atan(Math.Tan(hFovDeg * degToRad / 2.0) * (heightPx / widthPx)) * radToDeg;
        return new CameraIntrinsics(hFovDeg, vFovDeg, widthPx, heightPx);
    }

    /// <summary>
    /// Safe default for when physical dimensions are not yet known.
    /// Approximates a typical phone camera in portrait mode (~48° wide).
    /// </summary>
    public static readonly CameraIntrinsics Default = FromHorizontalFov(48.0, 1080f, 1920f);
}
