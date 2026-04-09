namespace AstroFinder.Engine.Primitives;

/// <summary>
/// An equatorial coordinate (right ascension and declination).
/// RA is in hours (0–24), Dec is in degrees (-90 to +90).
/// </summary>
public readonly record struct EquatorialCoordinate(double RaHours, double DecDegrees)
{
    /// <summary>RA converted to degrees (0–360).</summary>
    public double RaDegrees => RaHours * 15.0;
}
