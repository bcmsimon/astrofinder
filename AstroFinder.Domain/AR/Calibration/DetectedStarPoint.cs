namespace AstroFinder.Domain.AR.Calibration;

/// <summary>
/// Bright star-like detection extracted from the camera frame.
/// </summary>
public sealed record DetectedStarPoint(
    double X,
    double Y,
    double PeakIntensity,
    double MeanIntensity,
    double Roundness);
