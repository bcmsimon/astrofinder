using AstroFinder.Engine.Catalog;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Hops;

/// <summary>
/// The complete result of a hop generation: an ordered list of steps
/// from an anchor star to (or near) the target.
/// </summary>
public sealed class HopRoute
{
    public required TargetEntry Target { get; init; }
    public required IReadOnlyList<HopStep> Steps { get; init; }
    public required double TotalAngularDistanceDegrees { get; init; }
}
