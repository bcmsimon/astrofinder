namespace AstroFinder.Domain.AR.MountedPointing;

/// <summary>
/// Conservative thresholds for mounted-phone seeded solve and guidance.
/// </summary>
public sealed record MountedPointingSolveOptions(
    int MinDetectedStars = 6,
    int MinMatchedStars = 6,
    int MinInlierStars = 5,
    double MinConfidence = 0.35,
    double MaxMeanResidualPx = 14.0,
    double MinSpreadScore = 0.12,
    double OnTargetThresholdDeg = 0.8,
    double FineAdjustmentThresholdDeg = 2.2,
    double AxisIgnoreThresholdDeg = 0.35,
    double SlightAxisThresholdDeg = 0.9);
