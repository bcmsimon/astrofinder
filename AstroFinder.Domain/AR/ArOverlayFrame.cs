namespace AstroFinder.Domain.AR;

/// <summary>
/// A complete projected AR frame ready for rendering.
/// All screen coordinates are in pixels with origin at top-left.
/// </summary>
public sealed record ArOverlayFrame(
    ProjectedSkyObject Target,
    IReadOnlyList<ProjectedSkyObject> AsterismStars,
    IReadOnlyList<(int From, int To)> AsterismSegments,
    /// <summary>Anchor star at index 0, then each hop star in order.</summary>
    IReadOnlyList<ProjectedSkyObject> HopSteps,
    IReadOnlyList<ProjectedSkyObject> BackgroundStars,
    CameraIntrinsics Intrinsics,
    /// <summary>
    /// Off-screen arrow guidance toward the target, or null when on-target.
    /// </summary>
    ArrowGuidance? OffscreenArrow,
    /// <summary>
    /// Angular distance in degrees from the camera forward axis to the target.
    /// </summary>
    double TargetAngularDistanceDegrees,
    /// <summary>
    /// Text to show in the center reticle when on-target (null otherwise).
    /// </summary>
    string? CenterReticleText,
    /// <summary>
    /// Route line segments between consecutive hop nodes that are both visible.
    /// </summary>
    IReadOnlyList<(ArScreenPoint From, ArScreenPoint To)> RouteSegments);
