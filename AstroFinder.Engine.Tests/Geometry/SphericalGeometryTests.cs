using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Tests.Geometry;

public class SphericalGeometryTests
{
    [Fact]
    public void AngularSeparation_SamePoint_ReturnsZero()
    {
        var coord = new EquatorialCoordinate(12.0, 45.0);
        double result = SphericalGeometry.AngularSeparationDegrees(coord, coord);
        Assert.Equal(0.0, result, precision: 10);
    }

    [Fact]
    public void AngularSeparation_Poles_Returns180()
    {
        var northPole = new EquatorialCoordinate(0.0, 90.0);
        var southPole = new EquatorialCoordinate(0.0, -90.0);
        double result = SphericalGeometry.AngularSeparationDegrees(northPole, southPole);
        Assert.Equal(180.0, result, precision: 10);
    }

    [Fact]
    public void AngularSeparation_KnownPair_Polaris_to_Dubhe()
    {
        // Polaris: RA ~2.53h, Dec ~89.26°
        // Dubhe: RA ~11.06h, Dec ~61.75°
        var polaris = new EquatorialCoordinate(2.53, 89.26);
        var dubhe = new EquatorialCoordinate(11.06, 61.75);
        double sep = SphericalGeometry.AngularSeparationDegrees(polaris, dubhe);

        // Expected separation ~28.7° (approximate)
        Assert.InRange(sep, 27.0, 30.0);
    }

    [Fact]
    public void PositionAngle_DueNorth_ReturnsZero()
    {
        var from = new EquatorialCoordinate(6.0, 20.0);
        var to = new EquatorialCoordinate(6.0, 25.0);
        double pa = SphericalGeometry.PositionAngleDegrees(from, to);

        // Due north should be ~0°
        Assert.InRange(pa, 0.0, 1.0);
    }

    [Fact]
    public void PositionAngle_DueSouth_Returns180()
    {
        var from = new EquatorialCoordinate(6.0, 25.0);
        var to = new EquatorialCoordinate(6.0, 20.0);
        double pa = SphericalGeometry.PositionAngleDegrees(from, to);

        Assert.InRange(pa, 179.0, 181.0);
    }

    [Fact]
    public void ComputeRelativePosition_ReturnsConsistentValues()
    {
        var reference = new EquatorialCoordinate(10.0, 40.0);
        var target = new EquatorialCoordinate(10.5, 42.0);

        var result = SphericalGeometry.ComputeRelativePosition(reference, target);

        Assert.True(result.DeltaDecDegrees > 0, "Target is north, so delta dec should be positive");
        Assert.True(result.AngularSeparationDegrees > 0);
        Assert.InRange(result.PositionAngleDegrees, 0, 360);
    }
}
