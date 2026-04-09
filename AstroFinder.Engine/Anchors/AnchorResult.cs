using AstroFinder.Engine.Catalog;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Anchors;

/// <summary>
/// Result of an anchor selection: the best nearby asterism or bright-star
/// pattern for navigating to a target.
/// </summary>
public sealed class AnchorResult
{
    public required AsterismEntry Asterism { get; init; }
    public required StarEntry AnchorStar { get; init; }
    public required double AngularDistanceDegrees { get; init; }
    public required double Score { get; init; }
}
