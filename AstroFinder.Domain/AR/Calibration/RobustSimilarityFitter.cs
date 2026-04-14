namespace AstroFinder.Domain.AR.Calibration;

/// <summary>
/// Robust similarity transform fitter using RANSAC.
/// </summary>
public sealed class RobustSimilarityFitter
{
    public sealed record Options(
        int MaxIterations = 120,
        double InlierThresholdPx = 16.0,
        int MinInliers = 4,
        int RandomSeed = 1337);

    private readonly Options _options;

    public RobustSimilarityFitter(Options? options = null)
    {
        _options = options ?? new Options();
    }

    public bool TryFit(
        IReadOnlyList<StarCorrespondence> correspondences,
        out SimilarityTransform2D transform,
        out IReadOnlyList<StarCorrespondence> inliers,
        out double meanResidualPx)
    {
        transform = SimilarityTransform2D.Identity;
        inliers = [];
        meanResidualPx = double.MaxValue;

        if (correspondences.Count < 2)
        {
            return false;
        }

        var rng = new Random(_options.RandomSeed);
        var bestInliers = new List<StarCorrespondence>();
        var bestTransform = SimilarityTransform2D.Identity;

        for (var iter = 0; iter < _options.MaxIterations; iter++)
        {
            var sample = SampleTwoDistinct(correspondences, rng);
            if (sample is null)
            {
                continue;
            }

            if (!TryFitFromTwo(sample.Value.A, sample.Value.B, out var candidate))
            {
                continue;
            }

            var currentInliers = new List<StarCorrespondence>();
            foreach (var c in correspondences)
            {
                var (px, py) = candidate.Apply(c.Predicted.X, c.Predicted.Y);
                var err = Distance(px, py, c.Detected.X, c.Detected.Y);
                if (err <= _options.InlierThresholdPx)
                {
                    currentInliers.Add(c);
                }
            }

            if (currentInliers.Count > bestInliers.Count)
            {
                bestInliers = currentInliers;
                bestTransform = candidate;
            }
        }

        if (bestInliers.Count < _options.MinInliers)
        {
            return false;
        }

        // Refine using all inliers via Procrustes-like estimate.
        if (!TryFitLeastSquares(bestInliers, out transform, out meanResidualPx))
        {
            return false;
        }

        inliers = bestInliers;
        return true;
    }

    private static (StarCorrespondence A, StarCorrespondence B)? SampleTwoDistinct(IReadOnlyList<StarCorrespondence> all, Random rng)
    {
        if (all.Count < 2)
        {
            return null;
        }

        var i = rng.Next(all.Count);
        var j = rng.Next(all.Count - 1);
        if (j >= i) j++;

        return (all[i], all[j]);
    }

    private static bool TryFitFromTwo(StarCorrespondence a, StarCorrespondence b, out SimilarityTransform2D transform)
    {
        transform = SimilarityTransform2D.Identity;

        var p1x = a.Predicted.X;
        var p1y = a.Predicted.Y;
        var p2x = b.Predicted.X;
        var p2y = b.Predicted.Y;

        var q1x = a.Detected.X;
        var q1y = a.Detected.Y;
        var q2x = b.Detected.X;
        var q2y = b.Detected.Y;

        var dpX = p2x - p1x;
        var dpY = p2y - p1y;
        var dqX = q2x - q1x;
        var dqY = q2y - q1y;

        var lenP = Math.Sqrt((dpX * dpX) + (dpY * dpY));
        var lenQ = Math.Sqrt((dqX * dqX) + (dqY * dqY));
        if (lenP < 1e-6 || lenQ < 1e-6)
        {
            return false;
        }

        var scale = lenQ / lenP;
        var angP = Math.Atan2(dpY, dpX);
        var angQ = Math.Atan2(dqY, dqX);
        var rot = angQ - angP;

        var c = Math.Cos(rot);
        var s = Math.Sin(rot);

        var tx = q1x - (scale * ((c * p1x) - (s * p1y)));
        var ty = q1y - (scale * ((s * p1x) + (c * p1y)));

        transform = new SimilarityTransform2D(scale, rot, tx, ty);
        return true;
    }

    private static bool TryFitLeastSquares(
        IReadOnlyList<StarCorrespondence> inliers,
        out SimilarityTransform2D transform,
        out double meanResidualPx)
    {
        transform = SimilarityTransform2D.Identity;
        meanResidualPx = double.MaxValue;

        if (inliers.Count < 2)
        {
            return false;
        }

        var meanPx = inliers.Average(c => c.Predicted.X);
        var meanPy = inliers.Average(c => c.Predicted.Y);
        var meanQx = inliers.Average(c => c.Detected.X);
        var meanQy = inliers.Average(c => c.Detected.Y);

        double sxx = 0;
        double sxy = 0;
        double normP = 0;

        foreach (var c in inliers)
        {
            var px = c.Predicted.X - meanPx;
            var py = c.Predicted.Y - meanPy;
            var qx = c.Detected.X - meanQx;
            var qy = c.Detected.Y - meanQy;

            sxx += (px * qx) + (py * qy);
            sxy += (px * qy) - (py * qx);
            normP += (px * px) + (py * py);
        }

        if (normP < 1e-9)
        {
            return false;
        }

        var rot = Math.Atan2(sxy, sxx);
        var scale = Math.Sqrt((sxx * sxx) + (sxy * sxy)) / normP;

        var c0 = Math.Cos(rot);
        var s0 = Math.Sin(rot);

        var tx = meanQx - (scale * ((c0 * meanPx) - (s0 * meanPy)));
        var ty = meanQy - (scale * ((s0 * meanPx) + (c0 * meanPy)));

        transform = new SimilarityTransform2D(scale, rot, tx, ty);
        var fitTransform = transform;

        meanResidualPx = inliers.Average(cor =>
        {
            var (x, y) = fitTransform.Apply(cor.Predicted.X, cor.Predicted.Y);
            return Distance(x, y, cor.Detected.X, cor.Detected.Y);
        });

        return true;
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
