namespace AstroFinder.Engine.Primitives;

/// <summary>
/// An equatorial coordinate (right ascension and declination).
/// RA is stored in hours (0–24), Dec is stored in degrees (-90 to +90).
/// </summary>
public readonly record struct EquatorialCoordinate(double RaHours, double DecDegrees)
{
    /// <summary>RA converted to degrees (0–360).</summary>
    public double RaDegrees => RaHours * 15.0;

    /// <summary>RA converted to radians (0–2π).</summary>
    public double RaRadians => RaDegrees * (Math.PI / 180.0);

    /// <summary>Dec converted to radians (-π/2 to +π/2).</summary>
    public double DecRadians => DecDegrees * (Math.PI / 180.0);

    /// <summary>
    /// Creates an equatorial coordinate from degree-based right ascension and declination input.
    /// </summary>
    public static EquatorialCoordinate FromDegrees(double raDegrees, double decDegrees)
    {
        return new EquatorialCoordinate(raDegrees / 15.0, decDegrees);
    }
}
