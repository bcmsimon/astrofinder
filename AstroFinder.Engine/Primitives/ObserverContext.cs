namespace AstroFinder.Engine.Primitives;

/// <summary>
/// Observer location and UTC time used for sky-projection calculations.
/// </summary>
public sealed class ObserverContext
{
    /// <summary>Observer latitude in degrees, positive north.</summary>
    public double LatitudeDegrees { get; init; }

    /// <summary>Observer longitude in degrees, positive east of Greenwich.</summary>
    public double LongitudeDegrees { get; init; }

    /// <summary>Observation time in UTC.</summary>
    public DateTime UtcTime { get; init; }
}
