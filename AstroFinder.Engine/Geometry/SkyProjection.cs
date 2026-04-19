using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Geometry;

/// <summary>
/// Deterministic sky-orientation helpers for converting equatorial coordinates
/// into the observer's local horizontal frame using time and longitude.
/// </summary>
public static class SkyProjection
{
    private const double DegreesPerHour = 15.0;

    public static HorizontalCoordinate EquatorialToHorizontal(
        EquatorialCoordinate coordinate,
        double observerLatitudeDegrees,
        double observerLongitudeDegrees,
        DateTimeOffset observationTime)
    {
        return SkyOrientationService.GetHorizontalCoordinate(
            new ObserverLocation(observerLatitudeDegrees, observerLongitudeDegrees),
            coordinate,
            observationTime.UtcDateTime);
    }

    public static double LocalSiderealTimeDegrees(DateTimeOffset observationTime, double observerLongitudeDegrees)
    {
        var localSiderealTimeRadians = SkyOrientationService.GetLocalSiderealTimeRadians(
            observationTime.UtcDateTime,
            observerLongitudeDegrees);

        return SkyOrientationService.RadiansToDegrees(localSiderealTimeRadians);
    }
}
