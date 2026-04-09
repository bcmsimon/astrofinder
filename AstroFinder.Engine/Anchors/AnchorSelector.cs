using AstroFinder.Engine.Catalog;
using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Anchors;

/// <summary>
/// Selects the best nearby asterism for navigating to a target.
/// Scoring combines proximity, familiarity, and angular alignment.
/// </summary>
public sealed class AnchorSelector
{
    private const double MaxSearchRadiusDegrees = 40.0;
    private const double ProximityWeight = 0.5;
    private const double FamiliarityWeight = 0.3;
    private const double AlignmentWeight = 0.2;

    public AnchorResult? FindBestAnchor(
        EquatorialCoordinate target,
        IReadOnlyList<AsterismEntry> asterisms,
        Func<string, StarEntry?> starLookup)
    {
        AnchorResult? best = null;

        foreach (var asterism in asterisms)
        {
            foreach (var starId in asterism.StarIds)
            {
                var star = starLookup(starId);
                if (star is null)
                {
                    continue;
                }

                var starCoord = new EquatorialCoordinate(star.RaHours, star.DecDegrees);
                double distance = SphericalGeometry.AngularSeparationDegrees(starCoord, target);

                if (distance > MaxSearchRadiusDegrees)
                {
                    continue;
                }

                double proximityScore = 1.0 - (distance / MaxSearchRadiusDegrees);
                double familiarityScore = Math.Min(asterism.FamiliarityScore, 1.0);
                double alignmentScore = ComputeAlignmentScore(asterism, target, starLookup);

                double totalScore =
                    (proximityScore * ProximityWeight) +
                    (familiarityScore * FamiliarityWeight) +
                    (alignmentScore * AlignmentWeight);

                if (best is null || totalScore > best.Score)
                {
                    best = new AnchorResult
                    {
                        Asterism = asterism,
                        AnchorStar = star,
                        AngularDistanceDegrees = distance,
                        Score = totalScore,
                    };
                }
            }
        }

        return best;
    }

    private static double ComputeAlignmentScore(
        AsterismEntry asterism,
        EquatorialCoordinate target,
        Func<string, StarEntry?> starLookup)
    {
        if (asterism.StarIds.Count < 2)
        {
            return 0.0;
        }

        var star1 = starLookup(asterism.StarIds[0]);
        var star2 = starLookup(asterism.StarIds[1]);

        if (star1 is null || star2 is null)
        {
            return 0.0;
        }

        var coord1 = new EquatorialCoordinate(star1.RaHours, star1.DecDegrees);
        var coord2 = new EquatorialCoordinate(star2.RaHours, star2.DecDegrees);

        double paLine = SphericalGeometry.PositionAngleDegrees(coord1, coord2);
        double paTarget = SphericalGeometry.PositionAngleDegrees(coord2, target);

        double angleDiff = Math.Abs(paLine - paTarget);
        if (angleDiff > 180.0) angleDiff = 360.0 - angleDiff;

        // Best alignment = line extension (angleDiff near 0 or 180)
        double minAngle = Math.Min(angleDiff, 180.0 - angleDiff);
        return 1.0 - (minAngle / 90.0);
    }
}
