namespace AstroFinder.Domain.AR.Calibration;

/// <summary>
/// Confidence scoring for calibration correction usage.
/// </summary>
public static class CalibrationConfidenceModel
{
    public static double Compute(int matchedCount, int inlierCount, double meanResidualPx, double spreadScore)
    {
        if (matchedCount <= 0 || inlierCount <= 0)
        {
            return 0.0;
        }

        var inlierRatio = inlierCount / (double)Math.Max(1, matchedCount);
        var residualScore = 1.0 - Math.Clamp(meanResidualPx / 25.0, 0.0, 1.0);
        var countScore = Math.Clamp((inlierCount - 3) / 10.0, 0.0, 1.0);
        var spread = Math.Clamp(spreadScore, 0.0, 1.0);

        var confidence = (0.35 * inlierRatio) + (0.30 * residualScore) + (0.20 * countScore) + (0.15 * spread);
        return Math.Clamp(confidence, 0.0, 1.0);
    }

    public static double ComputeSpreadScore(IReadOnlyList<StarCorrespondence> inliers, double width, double height)
    {
        if (inliers.Count == 0 || width <= 1 || height <= 1)
        {
            return 0.0;
        }

        var minX = inliers.Min(c => c.Detected.X);
        var maxX = inliers.Max(c => c.Detected.X);
        var minY = inliers.Min(c => c.Detected.Y);
        var maxY = inliers.Max(c => c.Detected.Y);

        var xSpread = Math.Clamp((maxX - minX) / width, 0.0, 1.0);
        var ySpread = Math.Clamp((maxY - minY) / height, 0.0, 1.0);

        return 0.5 * (xSpread + ySpread);
    }
}
