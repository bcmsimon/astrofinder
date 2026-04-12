namespace AstroFinder.Domain.AR;

/// <summary>
/// A sky object projected into camera-frame screen coordinates.
/// Coordinates are in the reference space defined by <see cref="ArOverlayFrame.Viewport"/>.
/// </summary>
public sealed record ArProjectedPoint(
    double ScreenX,
    double ScreenY,
    bool IsVisible,
    ArOverlayRole Role,
    string? Label,
    double Magnitude);
