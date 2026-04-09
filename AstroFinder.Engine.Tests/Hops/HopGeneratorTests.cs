using AstroApps.Equipment.Profiles.Models;
using AstroFinder.Engine.Hops;

namespace AstroFinder.Engine.Tests.Hops;

public class HopGeneratorTests
{
    private static readonly CatalogStar StarA = new()
    {
        Id = "S1", DisplayName = "Alpha", RightAscensionHours = 10.0, DeclinationDeg = 60.0, VisualMagnitude = 2.0,
    };

    private static readonly CatalogStar StarB = new()
    {
        Id = "S2", DisplayName = "Beta", RightAscensionHours = 10.2, DeclinationDeg = 63.0, VisualMagnitude = 3.0,
    };

    private static readonly CatalogStar StarC = new()
    {
        Id = "S3", DisplayName = "Gamma", RightAscensionHours = 10.3, DeclinationDeg = 66.0, VisualMagnitude = 3.5,
    };

    private static readonly CatalogTarget TargetM81 = new()
    {
        Id = "M81", DisplayName = "M81",
        RightAscensionHours = 9.93, DeclinationDeg = 69.07,
        Category = AstroApps.Equipment.Profiles.Enums.ShootingTargetCategory.Galaxy,
    };

    [Fact]
    public void GenerateRoute_WithAvailableStars_ProducesSteps()
    {
        var generator = new HopGenerator();
        var stars = new List<CatalogStar> { StarA, StarB, StarC };

        var route = generator.GenerateRoute(StarA, TargetM81, stars);

        Assert.NotNull(route);
        Assert.Equal("M81", route.Target.Id);
        Assert.True(route.Steps.Count > 0, "Should produce at least one hop step");
    }

    [Fact]
    public void GenerateRoute_NoAvailableStars_ReturnsEmptySteps()
    {
        var generator = new HopGenerator();

        var route = generator.GenerateRoute(StarA, TargetM81, Array.Empty<CatalogStar>());

        Assert.NotNull(route);
        Assert.Empty(route.Steps);
    }

    [Fact]
    public void GenerateRoute_TotalDistance_IsPositive()
    {
        var generator = new HopGenerator();
        var stars = new List<CatalogStar> { StarA, StarB, StarC };

        var route = generator.GenerateRoute(StarA, TargetM81, stars);

        if (route.Steps.Count > 0)
        {
            Assert.True(route.TotalAngularDistanceDegrees > 0);
        }
    }
}
