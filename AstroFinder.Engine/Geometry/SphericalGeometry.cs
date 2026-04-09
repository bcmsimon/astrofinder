using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Geometry;

/// <summary>
/// Spherical astronomy calculations for sky navigation.
/// </summary>
public static class SphericalGeometry
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    /// <summary>
    /// Computes the angular separation between two equatorial coordinates
    /// using the Vincenty formula (numerically stable for all separations).
    /// </summary>
    public static double AngularSeparationDegrees(EquatorialCoordinate a, EquatorialCoordinate b)
    {
        double ra1 = a.RaDegrees * DegToRad;
        double dec1 = a.DecDegrees * DegToRad;
        double ra2 = b.RaDegrees * DegToRad;
        double dec2 = b.DecDegrees * DegToRad;
        double dRa = ra2 - ra1;

        double sinDec1 = Math.Sin(dec1);
        double cosDec1 = Math.Cos(dec1);
        double sinDec2 = Math.Sin(dec2);
        double cosDec2 = Math.Cos(dec2);
        double cosDRa = Math.Cos(dRa);
        double sinDRa = Math.Sin(dRa);

        double numerator = Math.Sqrt(
            Math.Pow(cosDec2 * sinDRa, 2) +
            Math.Pow(cosDec1 * sinDec2 - sinDec1 * cosDec2 * cosDRa, 2));
        double denominator = sinDec1 * sinDec2 + cosDec1 * cosDec2 * cosDRa;

        return Math.Atan2(numerator, denominator) * RadToDeg;
    }

    /// <summary>
    /// Computes the position angle from coordinate A to coordinate B,
    /// measured north through east (0–360 degrees).
    /// </summary>
    public static double PositionAngleDegrees(EquatorialCoordinate from, EquatorialCoordinate to)
    {
        double ra1 = from.RaDegrees * DegToRad;
        double dec1 = from.DecDegrees * DegToRad;
        double ra2 = to.RaDegrees * DegToRad;
        double dec2 = to.DecDegrees * DegToRad;
        double dRa = ra2 - ra1;

        double y = Math.Sin(dRa) * Math.Cos(dec2);
        double x = Math.Cos(dec1) * Math.Sin(dec2) - Math.Sin(dec1) * Math.Cos(dec2) * Math.Cos(dRa);

        double pa = Math.Atan2(y, x) * RadToDeg;
        return (pa + 360.0) % 360.0;
    }

    /// <summary>
    /// Computes the full relative position between a reference and target coordinate.
    /// </summary>
    public static RelativePosition ComputeRelativePosition(EquatorialCoordinate reference, EquatorialCoordinate target)
    {
        double deltaRa = (target.RaDegrees - reference.RaDegrees);
        if (deltaRa > 180.0) deltaRa -= 360.0;
        if (deltaRa < -180.0) deltaRa += 360.0;

        double deltaDec = target.DecDegrees - reference.DecDegrees;
        double separation = AngularSeparationDegrees(reference, target);
        double pa = PositionAngleDegrees(reference, target);

        return new RelativePosition(deltaRa, deltaDec, separation, pa);
    }
}
