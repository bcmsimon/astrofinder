namespace AstroFinder.Domain.AR;

/// <summary>
/// A complete projected AR frame ready for rendering.
/// All screen coordinates are in the reference pixel space of <see cref="Viewport"/>;
/// the renderer scales them to the actual canvas dimensions.
/// </summary>
public sealed record ArOverlayFrame(
    ArProjectedPoint Target,
    IReadOnlyList<ArProjectedPoint> AsterismStars,
    IReadOnlyList<(int From, int To)> AsterismSegments,
    /// <summary>Anchor star at index 0, then each hop star in order.</summary>
    IReadOnlyList<ArProjectedPoint> HopSteps,
    IReadOnlyList<ArProjectedPoint> BackgroundStars,
    CameraViewport Viewport,
    /// <summary>
    /// Bearing from device pointing direction to the target, measured in degrees
    /// clockwise from "up on screen" (i.e. higher altitude).
    /// 0° = target is directly above, 90° = right, 180° = below, 270° = left.
    /// </summary>
    double TargetBearingDegrees,
    /// <summary>
    /// Angular distance in degrees from the device pointing direction to the target.
    /// </summary>
    double TargetAngularDistanceDegrees);
