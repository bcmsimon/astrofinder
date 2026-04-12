namespace AstroFinder.Domain.AR;

/// <summary>
/// Describes the pixel dimensions and field of view of the camera preview surface.
/// HorizontalFovDegrees is the FOV along the screen's X axis (width).
/// VerticalFovDegrees is derived from the aspect ratio using the correct tangent formula.
/// </summary>
public sealed record CameraViewport(
    double WidthPx,
    double HeightPx,
    double HorizontalFovDegrees)
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    /// <summary>
    /// Vertical field of view in degrees, computed via the tangent-based formula
    /// to correctly account for non-square aspect ratios.
    /// </summary>
    public double VerticalFovDegrees =>
        2.0 * Math.Atan(Math.Tan(HorizontalFovDegrees * DegToRad / 2.0) * (HeightPx / WidthPx)) * RadToDeg;

    /// <summary>
    /// Safe default for when physical dimensions are not yet known.
    /// Approximates a typical phone camera in portrait mode (about 48° wide, 77° tall).
    /// </summary>
    public static readonly CameraViewport Default = new(1080.0, 1920.0, 48.0);
}
