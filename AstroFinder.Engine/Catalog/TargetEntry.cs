namespace AstroFinder.Engine.Catalog;

/// <summary>
/// A deep-sky target from the target catalog.
/// </summary>
public sealed class TargetEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? CommonName { get; init; }
    public required double RaHours { get; init; }
    public required double DecDegrees { get; init; }
    public required string ObjectType { get; init; }
    public double? MagnitudeV { get; init; }
    public double? SizeArcminutes { get; init; }
    public string? Constellation { get; init; }
}
