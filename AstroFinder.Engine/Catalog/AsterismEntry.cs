namespace AstroFinder.Engine.Catalog;

/// <summary>
/// A recognizable star pattern (e.g., Big Dipper, Orion's Belt).
/// </summary>
public sealed class AsterismEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<string> StarIds { get; init; }
    public double FamiliarityScore { get; init; }
}
