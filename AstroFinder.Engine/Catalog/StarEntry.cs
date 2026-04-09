namespace AstroFinder.Engine.Catalog;

/// <summary>
/// A star from the star catalog with position and magnitude.
/// </summary>
public sealed class StarEntry
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public required double RaHours { get; init; }
    public required double DecDegrees { get; init; }
    public required double Magnitude { get; init; }
    public string? ConstellationAbbreviation { get; init; }
}
