using AstroFinder.Engine.Anchors;
using AstroFinder.Engine.Catalog;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Tests.Anchors;

public class AnchorSelectorTests
{
    private static readonly StarEntry Dubhe = new()
    {
        Id = "HIP54061", Name = "Dubhe", RaHours = 11.062, DecDegrees = 61.751, Magnitude = 1.79,
    };

    private static readonly StarEntry Merak = new()
    {
        Id = "HIP53910", Name = "Merak", RaHours = 11.031, DecDegrees = 56.382, Magnitude = 2.37,
    };

    private static readonly AsterismEntry BigDipperPointers = new()
    {
        Id = "big-dipper-pointers",
        Name = "Big Dipper Pointers",
        StarIds = new[] { "HIP53910", "HIP54061" },
        FamiliarityScore = 1.0,
    };

    [Fact]
    public void FindBestAnchor_NearbyTarget_ReturnsResult()
    {
        var selector = new AnchorSelector();
        // M81: RA ~9.93h, Dec ~69.07°
        var target = new EquatorialCoordinate(9.93, 69.07);

        var result = selector.FindBestAnchor(
            target,
            new[] { BigDipperPointers },
            id => id switch
            {
                "HIP54061" => Dubhe,
                "HIP53910" => Merak,
                _ => null,
            });

        Assert.NotNull(result);
        Assert.Equal("Big Dipper Pointers", result.Asterism.Name);
        Assert.True(result.Score > 0);
    }

    [Fact]
    public void FindBestAnchor_NoAsterisms_ReturnsNull()
    {
        var selector = new AnchorSelector();
        var target = new EquatorialCoordinate(0.0, 0.0);

        var result = selector.FindBestAnchor(
            target,
            Array.Empty<AsterismEntry>(),
            _ => null);

        Assert.Null(result);
    }

    [Fact]
    public void FindBestAnchor_TooFarAway_ReturnsNull()
    {
        var selector = new AnchorSelector();
        // Target far from Dubhe/Merak
        var target = new EquatorialCoordinate(0.0, -60.0);

        var result = selector.FindBestAnchor(
            target,
            new[] { BigDipperPointers },
            id => id switch
            {
                "HIP54061" => Dubhe,
                "HIP53910" => Merak,
                _ => null,
            });

        Assert.Null(result);
    }
}
