namespace AstroFinder.Domain.AR;

/// <summary>
/// A single sky object to be projected into the AR overlay.
/// </summary>
public sealed record ArStarPoint(
    double RaHours,
    double DecDegrees,
    double Magnitude,
    string? Label,
    ArOverlayRole Role);
