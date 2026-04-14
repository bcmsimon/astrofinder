using AstroFinder.Domain.AR;
using AstroFinder.Domain.AR.Calibration;

namespace AstroFinder.Domain.Tests.AR;

public class CalibrationMatcherAndFitTests
{
    [Fact]
    public void Matcher_ProducesCorrespondences_ForNearbyPredictions()
    {
        var predicted = new List<PredictedStarPoint>
        {
            new(0, 200, 200, 1.2, ArOverlayRole.AsterismStar, "A"),
            new(1, 420, 190, 1.5, ArOverlayRole.AsterismStar, "B"),
            new(2, 260, 360, 1.8, ArOverlayRole.AsterismStar, "C"),
            new(3, 520, 340, 2.0, ArOverlayRole.AsterismStar, "D"),
        };

        var detected = new List<DetectedStarPoint>
        {
            new(206, 204, 245, 30, 0.9),
            new(427, 192, 230, 30, 0.85),
            new(268, 365, 225, 30, 0.82),
            new(526, 345, 210, 30, 0.8),
            // outlier blob
            new(80, 80, 255, 30, 0.2),
        };

        var matcher = new ConstrainedStarMatcher(new ConstrainedStarMatcher.Options(SearchRadiusPx: 40));
        var matches = matcher.Match(predicted, detected);

        Assert.True(matches.Count >= 4);
    }

    [Fact]
    public void RobustFit_RecoversNearGroundTruth_WithOutliers()
    {
        var gt = new SimilarityTransform2D(1.015, 3.5 * Math.PI / 180.0, 12.0, -8.0);
        var random = new Random(42);

        var correspondences = new List<StarCorrespondence>();
        for (var i = 0; i < 12; i++)
        {
            var px = 120 + (i * 45);
            var py = 140 + ((i % 4) * 70);
            var (tx, ty) = gt.Apply(px, py);
            tx += (random.NextDouble() - 0.5) * 1.4;
            ty += (random.NextDouble() - 0.5) * 1.4;

            correspondences.Add(new StarCorrespondence(
                new PredictedStarPoint(i, px, py, 2.0, ArOverlayRole.BackgroundStar, null),
                new DetectedStarPoint(tx, ty, 200, 25, 0.8)));
        }

        // Add outliers.
        correspondences.Add(new StarCorrespondence(
            new PredictedStarPoint(100, 300, 300, 4.0, ArOverlayRole.BackgroundStar, null),
            new DetectedStarPoint(50, 620, 180, 25, 0.8)));
        correspondences.Add(new StarCorrespondence(
            new PredictedStarPoint(101, 500, 120, 4.0, ArOverlayRole.BackgroundStar, null),
            new DetectedStarPoint(700, 40, 180, 25, 0.8)));

        var fitter = new RobustSimilarityFitter(new RobustSimilarityFitter.Options(
            MaxIterations: 200,
            InlierThresholdPx: 6.0,
            MinInliers: 8));

        var ok = fitter.TryFit(correspondences, out var fit, out var inliers, out var residual);

        Assert.True(ok);
        Assert.True(inliers.Count >= 10);
        Assert.InRange(residual, 0.0, 3.0);
        Assert.InRange(Math.Abs(fit.Scale - gt.Scale), 0.0, 0.03);
        Assert.InRange(Math.Abs((fit.RotationRad - gt.RotationRad) * 180.0 / Math.PI), 0.0, 1.2);
        Assert.InRange(Math.Abs(fit.Tx - gt.Tx), 0.0, 4.0);
        Assert.InRange(Math.Abs(fit.Ty - gt.Ty), 0.0, 4.0);
    }
}
