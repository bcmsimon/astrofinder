namespace AstroFinder.Domain.AR.Calibration;

/// <summary>
/// One calibration step output.
/// </summary>
public sealed record CalibrationResult(
    SimilarityTransform2D Transform,
    int MatchedCount,
    int InlierCount,
    double MeanResidualPx,
    double SpreadScore,
    double Confidence,
    bool UsedCorrection);
