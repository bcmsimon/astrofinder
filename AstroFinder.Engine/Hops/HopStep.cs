using AstroApps.Equipment.Profiles.Models;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Hops;

/// <summary>
/// A single step in a star-hopping route.
/// </summary>
public sealed class HopStep
{
    public required CatalogStar FromStar { get; init; }
    public required CatalogStar ToStar { get; init; }
    public required double AngularDistanceDegrees { get; init; }
    public required string Instruction { get; init; }
}
