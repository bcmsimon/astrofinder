using AstroFinder.Engine.Catalog;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Hops;

/// <summary>
/// A single step in a star-hopping route.
/// </summary>
public sealed class HopStep
{
    public required StarEntry FromStar { get; init; }
    public required StarEntry ToStar { get; init; }
    public required double AngularDistanceDegrees { get; init; }
    public required string Instruction { get; init; }
}
