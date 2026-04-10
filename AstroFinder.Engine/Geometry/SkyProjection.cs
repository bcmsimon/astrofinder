using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Geometry;

/// <summary>
/// Deterministic sky-orientation helpers for converting equatorial coordinates
/// into the observer's local horizontal frame using time and longitude.
/// </summary>
public static class SkyProjection
{
    private const double DegreesPerHour = 15.0;
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    public static HorizontalCoordinate EquatorialToHorizontal(
        EquatorialCoordinate coordinate,
        double observerLatitudeDegrees,
        double observerLongitudeDegrees,
        DateTimeOffset observationTime)
    {
        var latitudeRad = observerLatitudeDegrees * DegToRad;
        var declinationRad = coordinate.DecDegrees * DegToRad;
        var localSiderealTimeDegrees = LocalSiderealTimeDegrees(observationTime, observerLongitudeDegrees);
        var hourAngleDegrees = NormalizeDegreesSigned(localSiderealTimeDegrees - coordinate.RaDegrees);
        var hourAngleRad = hourAngleDegrees * DegToRad;

        var sinAltitude =
            (Math.Sin(declinationRad) * Math.Sin(latitudeRad)) +
            (Math.Cos(declinationRad) * Math.Cos(latitudeRad) * Math.Cos(hourAngleRad));

        var altitudeDegrees = Math.Asin(Math.Clamp(sinAltitude, -1.0, 1.0)) * RadToDeg;

        var azimuthDegrees = NormalizeDegrees(
            (Math.Atan2(
                Math.Sin(hourAngleRad),
                (Math.Cos(hourAngleRad) * Math.Sin(latitudeRad)) -
                (Math.Tan(declinationRad) * Math.Cos(latitudeRad))) * RadToDeg) + 180.0);

        return new HorizontalCoordinate(altitudeDegrees, azimuthDegrees);
    }

    public static double LocalSiderealTimeDegrees(DateTimeOffset observationTime, double observerLongitudeDegrees)
    {
        var utc = observationTime.ToUniversalTime();
        var julianDate = utc.UtcDateTime.ToOADate() + 2415018.5;
        var julianCenturies = (julianDate - 2451545.0) / 36525.0;

        var gmstDegrees = 280.46061837 +
                          (360.98564736629 * (julianDate - 2451545.0)) +
                          (0.000387933 * julianCenturies * julianCenturies) -
                          ((julianCenturies * julianCenturies * julianCenturies) / 38710000.0);

        return NormalizeDegrees(gmstDegrees + observerLongitudeDegrees);
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    private static double NormalizeDegreesSigned(double degrees)
    {
        var normalized = NormalizeDegrees(degrees);
        return normalized > 180.0 ? normalized - 360.0 : normalized;
    }
}
