using AstroFinder.Engine.Anchors;
using AstroFinder.Engine.Catalog;
using AstroFinder.Engine.Hops;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Domain;

/// <summary>
/// Represents the result of a complete target finding session:
/// the selected target, anchor, and generated hop route.
/// </summary>
public sealed class FinderSession
{
    public required TargetEntry Target { get; init; }
    public required AnchorResult? Anchor { get; init; }
    public required HopRoute? Route { get; init; }
    public required RelativePosition? RelativePosition { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
