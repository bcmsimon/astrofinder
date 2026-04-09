using AstroFinder.Engine.Anchors;
using AstroFinder.Engine.Catalog;
using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Hops;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Domain;

/// <summary>
/// Orchestrates the target finding workflow: catalog lookup,
/// anchor selection, hop generation, and relative position computation.
/// </summary>
public sealed class FinderOrchestrator
{
    private readonly ICatalogProvider _catalogProvider;
    private readonly AnchorSelector _anchorSelector;
    private readonly HopGenerator _hopGenerator;

    public FinderOrchestrator(
        ICatalogProvider catalogProvider,
        AnchorSelector anchorSelector,
        HopGenerator hopGenerator)
    {
        _catalogProvider = catalogProvider;
        _anchorSelector = anchorSelector;
        _hopGenerator = hopGenerator;
    }

    /// <summary>
    /// Generates a complete finder session for the specified target.
    /// </summary>
    public FinderSession CreateSession(string targetId)
    {
        var target = _catalogProvider.FindTargetById(targetId);
        if (target is null)
        {
            throw new ArgumentException($"Target '{targetId}' not found in catalog.", nameof(targetId));
        }

        var targetCoord = new EquatorialCoordinate(target.RightAscensionHours, target.DeclinationDeg);

        // Find best anchor asterism
        var anchor = _anchorSelector.FindBestAnchor(
            targetCoord,
            _catalogProvider.GetAsterisms(),
            _catalogProvider.FindStarByHipparcosId);

        // Generate hop route
        HopRoute? route = null;
        if (anchor is not null)
        {
            route = _hopGenerator.GenerateRoute(
                anchor.AnchorStar,
                target,
                _catalogProvider.GetStars());
        }

        // Compute relative position from anchor star
        RelativePosition? relativePos = null;
        if (anchor is not null)
        {
            var anchorCoord = new EquatorialCoordinate(anchor.AnchorStar.RightAscensionHours, anchor.AnchorStar.DeclinationDeg);
            relativePos = SphericalGeometry.ComputeRelativePosition(anchorCoord, targetCoord);
        }

        return new FinderSession
        {
            Target = target,
            Anchor = anchor,
            Route = route,
            RelativePosition = relativePos,
        };
    }
}
