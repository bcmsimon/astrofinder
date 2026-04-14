namespace AstroFinder.Domain.AR.Calibration;

/// <summary>
/// End-to-end lightweight star-registration pipeline for AR overlay correction.
/// </summary>
public sealed class ArOverlayCalibrationPipeline
{
    private readonly ConstrainedStarMatcher _matcher;
    private readonly RobustSimilarityFitter _fitter;
    private readonly TemporalCalibrationFilter _temporalFilter = new();

    public ArOverlayCalibrationPipeline(
        ConstrainedStarMatcher? matcher = null,
        RobustSimilarityFitter? fitter = null)
    {
        _matcher = matcher ?? new ConstrainedStarMatcher();
        _fitter = fitter ?? new RobustSimilarityFitter();
    }

    public CalibrationResult Calibrate(
        ArOverlayFrame sensorFrame,
        IReadOnlyList<DetectedStarPoint> detectedStars,
        out ArOverlayFrame correctedFrame)
    {
        var predicted = BuildPredictedPoints(sensorFrame);
        var matches = _matcher.Match(predicted, detectedStars);

        if (matches.Count < 4 || !_fitter.TryFit(matches, out var fit, out var inliers, out var residualPx))
        {
            _temporalFilter.DecayWithoutMeasurement();
            var fallback = _temporalFilter.GetBlendedTransform();
            correctedFrame = ApplyTransform(sensorFrame, fallback);

            return new CalibrationResult(
                Transform: fallback,
                MatchedCount: matches.Count,
                InlierCount: 0,
                MeanResidualPx: double.PositiveInfinity,
                SpreadScore: 0.0,
                Confidence: _temporalFilter.CurrentConfidence,
                UsedCorrection: _temporalFilter.CurrentConfidence > 0.15);
        }

        var spreadScore = CalibrationConfidenceModel.ComputeSpreadScore(
            inliers, sensorFrame.Intrinsics.WidthPx, sensorFrame.Intrinsics.HeightPx);
        var confidence = CalibrationConfidenceModel.Compute(
            matches.Count, inliers.Count, residualPx, spreadScore);

        var smoothed = _temporalFilter.Update(fit, confidence);
        var blended = _temporalFilter.GetBlendedTransform();
        correctedFrame = ApplyTransform(sensorFrame, blended);

        return new CalibrationResult(
            Transform: smoothed,
            MatchedCount: matches.Count,
            InlierCount: inliers.Count,
            MeanResidualPx: residualPx,
            SpreadScore: spreadScore,
            Confidence: _temporalFilter.CurrentConfidence,
            UsedCorrection: _temporalFilter.CurrentConfidence >= 0.25);
    }

    private static IReadOnlyList<PredictedStarPoint> BuildPredictedPoints(ArOverlayFrame frame)
    {
        var points = new List<PredictedStarPoint>();
        var idx = 0;

        void Add(ProjectedSkyObject p)
        {
            if (!p.InFrontOfCamera || p.ScreenPoint is null) return;

            points.Add(new PredictedStarPoint(
                idx++, p.ScreenPoint.Value.XPx, p.ScreenPoint.Value.YPx,
                p.Magnitude, p.Role, p.Label));
        }

        foreach (var star in frame.AsterismStars) Add(star);
        foreach (var star in frame.HopSteps) Add(star);
        foreach (var star in frame.BackgroundStars.OrderBy(s => s.Magnitude).Take(60)) Add(star);
        Add(frame.Target);

        return points;
    }

    private static ArOverlayFrame ApplyTransform(ArOverlayFrame frame, SimilarityTransform2D transform)
    {
        static ProjectedSkyObject Map(ProjectedSkyObject p, SimilarityTransform2D t)
        {
            if (p.ScreenPoint is null) return p;
            var (x, y) = t.Apply(p.ScreenPoint.Value.XPx, p.ScreenPoint.Value.YPx);
            return p with { ScreenPoint = new ArScreenPoint((float)x, (float)y) };
        }

        return frame with
        {
            Target = Map(frame.Target, transform),
            AsterismStars = frame.AsterismStars.Select(p => Map(p, transform)).ToList(),
            HopSteps = frame.HopSteps.Select(p => Map(p, transform)).ToList(),
            BackgroundStars = frame.BackgroundStars.Select(p => Map(p, transform)).ToList(),
        };
    }
}
