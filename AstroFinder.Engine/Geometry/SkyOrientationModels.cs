using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Geometry;

/// <summary>
/// A plotted point for a local chart after projection and rotation have been applied.
/// </summary>
/// <param name="Label">Optional display label.</param>
/// <param name="Coordinate">Original equatorial coordinate.</param>
/// <param name="Position">Projected chart-space position in radians.</param>
public readonly record struct StarPlotPoint(string? Label, EquatorialCoordinate Coordinate, PointD Position);

/// <summary>
/// Output of an observer-specific sky-orientation calculation.
/// </summary>
public readonly record struct SkyOrientationResult(
    double JulianDate,
    double GreenwichSiderealTimeRadians,
    double LocalSiderealTimeRadians,
    double HourAngleRadians,
    double ParallacticAngleRadians,
    double DisplayRotationRadians,
    double AltitudeRadians,
    bool IsAboveHorizon,
    bool IsNearZenithSensitive,
    PointD NorthDirection)
{
    /// <summary>Greenwich sidereal time in degrees.</summary>
    public double GreenwichSiderealTimeDegrees => SkyOrientationService.RadiansToDegrees(GreenwichSiderealTimeRadians);

    /// <summary>Local sidereal time in degrees.</summary>
    public double LocalSiderealTimeDegrees => SkyOrientationService.RadiansToDegrees(LocalSiderealTimeRadians);

    /// <summary>Hour angle in degrees.</summary>
    public double HourAngleDegrees => SkyOrientationService.RadiansToDegrees(HourAngleRadians);

    /// <summary>Parallactic angle in degrees.</summary>
    public double ParallacticAngleDegrees => SkyOrientationService.RadiansToDegrees(ParallacticAngleRadians);

    /// <summary>The chart display rotation in degrees.</summary>
    public double DisplayRotationDegrees => SkyOrientationService.RadiansToDegrees(DisplayRotationRadians);

    /// <summary>Altitude in degrees above the horizon.</summary>
    public double AltitudeDegrees => SkyOrientationService.RadiansToDegrees(AltitudeRadians);
}