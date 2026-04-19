namespace AstroFinder.Engine.Primitives;

/// <summary>
/// Observer latitude and longitude in degrees.
/// Longitude is positive east of Greenwich and negative west of Greenwich.
/// </summary>
public readonly record struct ObserverLocation(double LatitudeDegrees, double LongitudeDegrees)
{
    /// <summary>Latitude converted to radians.</summary>
    public double LatitudeRadians => LatitudeDegrees * (Math.PI / 180.0);

    /// <summary>Longitude converted to radians.</summary>
    public double LongitudeRadians => LongitudeDegrees * (Math.PI / 180.0);
}