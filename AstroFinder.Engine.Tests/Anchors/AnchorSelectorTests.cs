using AstroApps.Equipment.Profiles.Models;
using AstroFinder.Engine.Anchors;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Tests.Anchors;

public class AnchorSelectorTests
{
    private static readonly CatalogStar Dubhe = new()
    {
        Id = "dubhe", DisplayName = "Dubhe", RightAscensionHours = 11.062, DeclinationDeg = 61.751, VisualMagnitude = 1.79,
    };

    private static readonly CatalogStar Merak = new()
    {
        Id = "merak", DisplayName = "Merak", RightAscensionHours = 11.031, DeclinationDeg = 56.382, VisualMagnitude = 2.37,
    };

    private static readonly CatalogAsterism BigDipperPointers = new()
    {
        Id = "big-dipper-pointers",
        DisplayName = "Big Dipper Pointers",
        StarIds = new List<string> { "merak", "dubhe" },
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
                "dubhe" => Dubhe,
                "merak" => Merak,
                _ => null,
            });

        Assert.NotNull(result);
        Assert.Equal("Big Dipper Pointers", result.Asterism.DisplayName);
        Assert.True(result.Score > 0);
    }

    [Fact]
    public void FindBestAnchor_NoAsterisms_ReturnsNull()
    {
        var selector = new AnchorSelector();
        var target = new EquatorialCoordinate(0.0, 0.0);

        var result = selector.FindBestAnchor(
            target,
            Array.Empty<CatalogAsterism>(),
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
                "dubhe" => Dubhe,
                "merak" => Merak,
                _ => null,
            });

        Assert.Null(result);
    }
}
