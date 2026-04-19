using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Geometry;

/// <summary>
/// Deterministic helper methods for orienting a small equatorial chart so it matches
/// the observer's sky view for a given location and UTC time.
/// All trigonometry is performed in radians internally.
/// </summary>
public static class SkyOrientationService
{
    private const double DegreesPerCircle = 360.0;
    private const double RadiansPerCircle = Math.PI * 2.0;
    private const double DegreesToRadiansFactor = Math.PI / 180.0;
    private const double RadiansToDegreesFactor = 180.0 / Math.PI;
    private const double J2000JulianDate = 2451545.0;
    private const double ZenithSensitivityThresholdRadians = 1.0 * DegreesToRadiansFactor;

    /// <summary>
    /// Converts degrees to radians.
    /// </summary>
    public static double DegreesToRadians(double degrees) => degrees * DegreesToRadiansFactor;

    /// <summary>
    /// Converts radians to degrees.
    /// </summary>
    public static double RadiansToDegrees(double radians) => radians * RadiansToDegreesFactor;

    /// <summary>
    /// Normalizes an angle into the range [0, 2π).
    /// </summary>
    public static double NormalizeRadians(double radians)
    {
        var normalized = radians % RadiansPerCircle;
        return normalized < 0.0 ? normalized + RadiansPerCircle : normalized;
    }

    /// <summary>
    /// Normalizes an angle into the range [0, 360).
    /// </summary>
    public static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % DegreesPerCircle;
        return normalized < 0.0 ? normalized + DegreesPerCircle : normalized;
    }

    /// <summary>
    /// Converts a UTC timestamp to Julian Date.
    /// </summary>
    public static double ToJulianDate(DateTime utc)
    {
        var normalizedUtc = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
        var year = normalizedUtc.Year;
        var month = normalizedUtc.Month;

        if (month <= 2)
        {
            year -= 1;
            month += 12;
        }

        var dayFraction = normalizedUtc.TimeOfDay.TotalDays;
        var day = normalizedUtc.Day + dayFraction;
        var century = year / 100;
        var leapCenturyCorrection = 2 - century + (century / 4);

        return Math.Floor(365.25 * (year + 4716))
             + Math.Floor(30.6001 * (month + 1))
             + day
             + leapCenturyCorrection
             - 1524.5;
    }

    /// <summary>
    /// Computes Greenwich Sidereal Time in radians for a UTC timestamp.
    /// </summary>
    public static double GetGreenwichSiderealTimeRadians(DateTime utc)
    {
        var julianDate = ToJulianDate(utc);
        var julianCenturies = (julianDate - J2000JulianDate) / 36525.0;

        var gmstDegrees = 280.46061837
                        + (360.98564736629 * (julianDate - J2000JulianDate))
                        + (0.000387933 * julianCenturies * julianCenturies)
                        - ((julianCenturies * julianCenturies * julianCenturies) / 38710000.0);

        return DegreesToRadians(NormalizeDegrees(gmstDegrees));
    }

    /// <summary>
    /// Computes Local Sidereal Time in radians.
    /// Longitude is positive east of Greenwich.
    /// </summary>
    public static double GetLocalSiderealTimeRadians(DateTime utc, double longitudeDeg)
    {
        return NormalizeRadians(GetGreenwichSiderealTimeRadians(utc) + DegreesToRadians(longitudeDeg));
    }

    /// <summary>
    /// Computes hour angle in radians and normalizes it into the range [-π, π].
    /// </summary>
    public static double GetHourAngleRadians(double lstRad, double raRad)
    {
        return NormalizeSignedRadians(lstRad - raRad);
    }

    /// <summary>
    /// Computes the parallactic angle in radians.
    /// The returned angle is the angle between celestial north and the local vertical toward zenith.
    /// </summary>
    public static double GetParallacticAngleRadians(double latitudeRad, double declinationRad, double hourAngleRad)
    {
        return Math.Atan2(
            Math.Sin(hourAngleRad),
            (Math.Tan(latitudeRad) * Math.Cos(declinationRad)) - (Math.Sin(declinationRad) * Math.Cos(hourAngleRad)));
    }

    /// <summary>
    /// Projects a small field around the supplied chart center using a tangent-plane approximation.
    /// For wider fields, a proper gnomonic projection is more accurate.
    /// </summary>
    public static PointD ProjectSmallField(EquatorialCoordinate coordinate, EquatorialCoordinate chartCenter)
    {
        var deltaRaRadians = NormalizeSignedRadians(coordinate.RaRadians - chartCenter.RaRadians);
        var centerDeclinationRadians = chartCenter.DecRadians;
        var x = deltaRaRadians * Math.Cos(centerDeclinationRadians);
        var y = coordinate.DecRadians - centerDeclinationRadians;
        return new PointD(x, y);
    }

    /// <summary>
    /// Rotates a projected chart-space point by <paramref name="thetaRad"/>.
    /// Positive angles are counter-clockwise in mathematical coordinates.
    /// </summary>
    public static PointD RotatePoint(PointD point, double thetaRad)
    {
        var cosTheta = Math.Cos(thetaRad);
        var sinTheta = Math.Sin(thetaRad);

        return new PointD(
            (point.X * cosTheta) - (point.Y * sinTheta),
            (point.X * sinTheta) + (point.Y * cosTheta));
    }

    /// <summary>
    /// Computes observer-specific orientation values for a chart centered on <paramref name="target"/>.
    /// Set <paramref name="invertForScreenCoordinates"/> to switch between rotation = q and rotation = -q.
    /// </summary>
    public static SkyOrientationResult GetChartOrientation(
        ObserverLocation observer,
        DateTime utc,
        EquatorialCoordinate target,
        bool invertForScreenCoordinates = false)
    {
        var normalizedUtc = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
        var julianDate = ToJulianDate(normalizedUtc);
        var gstRad = GetGreenwichSiderealTimeRadians(normalizedUtc);
        var lstRad = GetLocalSiderealTimeRadians(normalizedUtc, observer.LongitudeDegrees);
        var hourAngleRad = GetHourAngleRadians(lstRad, target.RaRadians);
        var parallacticAngleRad = GetParallacticAngleRadians(observer.LatitudeRadians, target.DecRadians, hourAngleRad);
        var displayRotationRad = invertForScreenCoordinates ? -parallacticAngleRad : parallacticAngleRad;
        var altitudeRad = GetAltitudeRadians(observer.LatitudeRadians, target.DecRadians, hourAngleRad);
        var isNearZenithSensitive = Math.Abs((Math.PI / 2.0) - altitudeRad) <= ZenithSensitivityThresholdRadians;
        var northDirection = RotatePoint(new PointD(0.0, 1.0), displayRotationRad);

        return new SkyOrientationResult(
            julianDate,
            gstRad,
            lstRad,
            hourAngleRad,
            parallacticAngleRad,
            displayRotationRad,
            altitudeRad,
            altitudeRad > 0.0,
            isNearZenithSensitive,
            northDirection);
    }

    /// <summary>
    /// Converts an equatorial coordinate into a local horizontal coordinate for the supplied observer and UTC time.
    /// </summary>
    public static HorizontalCoordinate GetHorizontalCoordinate(ObserverLocation observer, EquatorialCoordinate coordinate, DateTime utc)
    {
        var normalizedUtc = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
        var lstRad = GetLocalSiderealTimeRadians(normalizedUtc, observer.LongitudeDegrees);
        var hourAngleRad = GetHourAngleRadians(lstRad, coordinate.RaRadians);
        var altitudeRad = GetAltitudeRadians(observer.LatitudeRadians, coordinate.DecRadians, hourAngleRad);
        var azimuthRad = NormalizeRadians(
            Math.Atan2(
                Math.Sin(hourAngleRad),
                (Math.Cos(hourAngleRad) * Math.Sin(observer.LatitudeRadians)) -
                (Math.Tan(coordinate.DecRadians) * Math.Cos(observer.LatitudeRadians))) + Math.PI);

        return new HorizontalCoordinate(RadiansToDegrees(altitudeRad), RadiansToDegrees(azimuthRad));
    }

    private static double NormalizeSignedRadians(double radians)
    {
        var normalized = NormalizeRadians(radians);
        return normalized > Math.PI ? normalized - RadiansPerCircle : normalized;
    }

    private static double GetAltitudeRadians(double latitudeRad, double declinationRad, double hourAngleRad)
    {
        var sinAltitude = (Math.Sin(declinationRad) * Math.Sin(latitudeRad))
                        + (Math.Cos(declinationRad) * Math.Cos(latitudeRad) * Math.Cos(hourAngleRad));

        return Math.Asin(Math.Clamp(sinAltitude, -1.0, 1.0));
    }
}