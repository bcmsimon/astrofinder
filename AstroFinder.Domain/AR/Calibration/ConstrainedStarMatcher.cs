namespace AstroFinder.Domain.AR.Calibration;

/// <summary>
/// Matches predicted catalog stars to detected blobs using constrained local search.
/// </summary>
public sealed class ConstrainedStarMatcher
{
    public sealed record Options(
        double SearchRadiusPx = 64.0,
        int MaxMatches = 40,
        int MinMatches = 4,
        double BrightnessOrderWeight = 0.35,
        double GeometryWeight = 0.25,
        double DistanceWeight = 0.40);

    private readonly Options _options;

    public ConstrainedStarMatcher(Options? options = null)
    {
        _options = options ?? new Options();
    }

    public IReadOnlyList<StarCorrespondence> Match(
        IReadOnlyList<PredictedStarPoint> predicted,
        IReadOnlyList<DetectedStarPoint> detected)
    {
        if (predicted.Count == 0 || detected.Count == 0)
        {
            return [];
        }

        var predictedOrdered = predicted
            .OrderBy(p => p.Magnitude)
            .Take(_options.MaxMatches)
            .ToList();

        var detectedOrdered = detected
            .OrderByDescending(d => d.PeakIntensity)
            .Take(Math.Max(_options.MaxMatches, predictedOrdered.Count * 2))
            .ToList();

        var usedDetections = new HashSet<int>();
        var matches = new List<StarCorrespondence>();

        for (var pi = 0; pi < predictedOrdered.Count; pi++)
        {
            var p = predictedOrdered[pi];
            double bestScore = double.MaxValue;
            var bestIdx = -1;

            for (var di = 0; di < detectedOrdered.Count; di++)
            {
                if (usedDetections.Contains(di))
                {
                    continue;
                }

                var d = detectedOrdered[di];
                var dx = d.X - p.X;
                var dy = d.Y - p.Y;
                var dist = Math.Sqrt((dx * dx) + (dy * dy));
                if (dist > _options.SearchRadiusPx)
                {
                    continue;
                }

                var expectedRank = pi / (double)Math.Max(1, predictedOrdered.Count - 1);
                var observedRank = di / (double)Math.Max(1, detectedOrdered.Count - 1);
                var brightnessPenalty = Math.Abs(expectedRank - observedRank);

                var predictedNn = NearestNeighborDistance(predictedOrdered, p.X, p.Y, p.Index);
                var detectedNn = NearestNeighborDistance(detectedOrdered, d.X, d.Y, di);
                var geometryPenalty = Math.Abs(predictedNn - detectedNn) / Math.Max(_options.SearchRadiusPx, 1.0);

                var score = (_options.DistanceWeight * (dist / _options.SearchRadiusPx))
                    + (_options.BrightnessOrderWeight * brightnessPenalty)
                    + (_options.GeometryWeight * geometryPenalty);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestIdx = di;
                }
            }

            if (bestIdx >= 0)
            {
                usedDetections.Add(bestIdx);
                matches.Add(new StarCorrespondence(p, detectedOrdered[bestIdx]));
            }
        }

        if (matches.Count < _options.MinMatches)
        {
            return [];
        }

        return matches;
    }

    private static double NearestNeighborDistance(IReadOnlyList<PredictedStarPoint> points, double x, double y, int selfIndex)
    {
        var best = double.MaxValue;
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].Index == selfIndex)
            {
                continue;
            }

            var dx = points[i].X - x;
            var dy = points[i].Y - y;
            var dist = Math.Sqrt((dx * dx) + (dy * dy));
            if (dist < best)
            {
                best = dist;
            }
        }

        return double.IsFinite(best) ? best : 0.0;
    }

    private static double NearestNeighborDistance(IReadOnlyList<DetectedStarPoint> points, double x, double y, int selfIndex)
    {
        var best = double.MaxValue;
        for (var i = 0; i < points.Count; i++)
        {
            if (i == selfIndex)
            {
                continue;
            }

            var dx = points[i].X - x;
            var dy = points[i].Y - y;
            var dist = Math.Sqrt((dx * dx) + (dy * dy));
            if (dist < best)
            {
                best = dist;
            }
        }

        return double.IsFinite(best) ? best : 0.0;
    }
}
