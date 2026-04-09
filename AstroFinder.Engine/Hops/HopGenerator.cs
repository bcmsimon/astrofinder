using AstroApps.Equipment.Profiles.Models;
using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Hops;

/// <summary>
/// Generates simple, human-friendly star-hopping routes from an anchor star to a target.
/// </summary>
public sealed class HopGenerator
{
    private const double MaxStepDistanceDegrees = 10.0;
    private const double TargetProximityDegrees = 1.0;

    /// <summary>
    /// Generates a hopping route from a starting star toward a target,
    /// selecting intermediate bright stars as waypoints.
    /// </summary>
    public HopRoute GenerateRoute(
        CatalogStar startStar,
        CatalogTarget target,
        IReadOnlyList<CatalogStar> availableStars,
        double magnitudeLimit = 5.0)
    {
        var targetCoord = new EquatorialCoordinate(target.RightAscensionHours, target.DeclinationDeg);
        var steps = new List<HopStep>();
        var current = startStar;
        var visited = new HashSet<string> { startStar.Id };
        double totalDistance = 0.0;

        const int maxSteps = 10;

        for (int i = 0; i < maxSteps; i++)
        {
            var currentCoord = new EquatorialCoordinate(current.RightAscensionHours, current.DeclinationDeg);
            double distanceToTarget = SphericalGeometry.AngularSeparationDegrees(currentCoord, targetCoord);

            if (distanceToTarget <= TargetProximityDegrees)
            {
                break;
            }

            var next = FindBestNextHop(current, targetCoord, availableStars, visited, magnitudeLimit);
            if (next is null)
            {
                break;
            }

            var nextCoord = new EquatorialCoordinate(next.RightAscensionHours, next.DeclinationDeg);
            double stepDistance = SphericalGeometry.AngularSeparationDegrees(currentCoord, nextCoord);

            string instruction = FormatInstruction(current, next, stepDistance);

            steps.Add(new HopStep
            {
                FromStar = current,
                ToStar = next,
                AngularDistanceDegrees = stepDistance,
                Instruction = instruction,
            });

            totalDistance += stepDistance;
            visited.Add(next.Id);
            current = next;
        }

        return new HopRoute
        {
            Target = target,
            Steps = steps,
            TotalAngularDistanceDegrees = totalDistance,
        };
    }

    private static CatalogStar? FindBestNextHop(
        CatalogStar current,
        EquatorialCoordinate target,
        IReadOnlyList<CatalogStar> stars,
        HashSet<string> visited,
        double magnitudeLimit)
    {
        var currentCoord = new EquatorialCoordinate(current.RightAscensionHours, current.DeclinationDeg);
        double currentDistToTarget = SphericalGeometry.AngularSeparationDegrees(currentCoord, target);

        CatalogStar? bestCandidate = null;
        double bestScore = double.MinValue;

        foreach (var star in stars)
        {
            if (visited.Contains(star.Id) || star.VisualMagnitude > magnitudeLimit)
            {
                continue;
            }

            var starCoord = new EquatorialCoordinate(star.RightAscensionHours, star.DeclinationDeg);
            double distFromCurrent = SphericalGeometry.AngularSeparationDegrees(currentCoord, starCoord);

            if (distFromCurrent > MaxStepDistanceDegrees || distFromCurrent < 0.5)
            {
                continue;
            }

            double distToTarget = SphericalGeometry.AngularSeparationDegrees(starCoord, target);

            // Prefer stars that are closer to the target and reasonably bright
            double progressScore = (currentDistToTarget - distToTarget) / currentDistToTarget;
            double brightnessScore = 1.0 - (star.VisualMagnitude / magnitudeLimit);
            double score = (progressScore * 0.7) + (brightnessScore * 0.3);

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = star;
            }
        }

        return bestCandidate;
    }

    private static string FormatInstruction(CatalogStar from, CatalogStar to, double distance)
    {
        string fromName = string.IsNullOrEmpty(from.DisplayName) ? from.Id : from.DisplayName;
        string toName = string.IsNullOrEmpty(to.DisplayName) ? to.Id : to.DisplayName;
        string distText = distance < 1.0
            ? $"{distance * 60.0:F0} arcmin"
            : $"{distance:F1}°";

        return $"From {fromName}, hop {distText} to {toName}";
    }
}
