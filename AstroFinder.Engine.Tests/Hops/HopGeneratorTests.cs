using AstroFinder.Engine.Catalog;
using AstroFinder.Engine.Hops;

namespace AstroFinder.Engine.Tests.Hops;

public class HopGeneratorTests
{
    private static readonly StarEntry StarA = new()
    {
        Id = "S1", Name = "Alpha", RaHours = 10.0, DecDegrees = 60.0, Magnitude = 2.0,
    };

    private static readonly StarEntry StarB = new()
    {
        Id = "S2", Name = "Beta", RaHours = 10.2, DecDegrees = 63.0, Magnitude = 3.0,
    };

    private static readonly StarEntry StarC = new()
    {
        Id = "S3", Name = "Gamma", RaHours = 10.3, DecDegrees = 66.0, Magnitude = 3.5,
    };

    private static readonly TargetEntry TargetM81 = new()
    {
        Id = "M81", Name = "M81", CommonName = "Bode's Galaxy",
        RaHours = 9.93, DecDegrees = 69.07, ObjectType = "Galaxy",
    };

    [Fact]
    public void GenerateRoute_WithAvailableStars_ProducesSteps()
    {
        var generator = new HopGenerator();
        var stars = new List<StarEntry> { StarA, StarB, StarC };

        var route = generator.GenerateRoute(StarA, TargetM81, stars);

        Assert.NotNull(route);
        Assert.Equal("M81", route.Target.Id);
        Assert.True(route.Steps.Count > 0, "Should produce at least one hop step");
    }

    [Fact]
    public void GenerateRoute_NoAvailableStars_ReturnsEmptySteps()
    {
        var generator = new HopGenerator();

        var route = generator.GenerateRoute(StarA, TargetM81, Array.Empty<StarEntry>());

        Assert.NotNull(route);
        Assert.Empty(route.Steps);
    }

    [Fact]
    public void GenerateRoute_TotalDistance_IsPositive()
    {
        var generator = new HopGenerator();
        var stars = new List<StarEntry> { StarA, StarB, StarC };

        var route = generator.GenerateRoute(StarA, TargetM81, stars);

        if (route.Steps.Count > 0)
        {
            Assert.True(route.TotalAngularDistanceDegrees > 0);
        }
    }
}
