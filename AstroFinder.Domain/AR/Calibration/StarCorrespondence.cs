namespace AstroFinder.Domain.AR.Calibration;

/// <summary>
/// Predicted-to-detected star pair used for transform fitting.
/// </summary>
public sealed record StarCorrespondence(PredictedStarPoint Predicted, DetectedStarPoint Detected);
