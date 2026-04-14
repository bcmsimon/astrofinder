namespace AstroFinder.Domain.AR.Calibration;

/// <summary>
/// Catalog star projected into image coordinates using sensor-only pose.
/// </summary>
public sealed record PredictedStarPoint(
    int Index,
    double X,
    double Y,
    double Magnitude,
    ArOverlayRole Role,
    string? Label);
