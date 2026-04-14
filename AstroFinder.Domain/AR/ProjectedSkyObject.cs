namespace AstroFinder.Domain.AR;

/// <summary>
/// A sky object projected into the AR overlay, with full visibility state.
/// Replaces the previous <c>ArProjectedPoint</c> type.
/// </summary>
public sealed record ProjectedSkyObject(
    string Id,
    string? Label,
    ArScreenPoint? ScreenPoint,
    bool InFrontOfCamera,
    bool WithinViewport,
    double AltitudeDeg,
    double AzimuthDeg,
    Vec3 DirectionEnu,
    ArOverlayRole Role,
    double Magnitude);
