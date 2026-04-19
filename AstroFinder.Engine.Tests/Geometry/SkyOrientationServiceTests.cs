using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Tests.Geometry;

public class SkyOrientationServiceTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void NormalizeDegrees_WrapsIntoUnsignedRange()
    {
        Assert.Equal(315.0, SkyOrientationService.NormalizeDegrees(-45.0), 10);
        Assert.Equal(5.0, SkyOrientationService.NormalizeDegrees(725.0), 10);
    }

    [Fact]
    public void NormalizeRadians_WrapsIntoUnsignedRange()
    {
        var normalized = SkyOrientationService.NormalizeRadians(-(Math.PI / 2.0));

        Assert.InRange(normalized, (Math.PI * 1.5) - Tolerance, (Math.PI * 1.5) + Tolerance);
    }

    [Fact]
    public void ToJulianDate_ForJ2000NoonUtc_MatchesReferenceEpoch()
    {
        var utc = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(2451545.0, SkyOrientationService.ToJulianDate(utc), 10);
    }

    [Fact]
    public void GetGreenwichSiderealTimeRadians_ForJ2000NoonUtc_IsSanityChecked()
    {
        var utc = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var gstDegrees = SkyOrientationService.RadiansToDegrees(
            SkyOrientationService.GetGreenwichSiderealTimeRadians(utc));

        Assert.InRange(gstDegrees, 280.45, 280.47);
    }

    [Fact]
    public void GetHourAngleRadians_NormalizesIntoSignedRange()
    {
        var lst = SkyOrientationService.DegreesToRadians(10.0);
        var ra = SkyOrientationService.DegreesToRadians(350.0);

        var hourAngleDegrees = SkyOrientationService.RadiansToDegrees(
            SkyOrientationService.GetHourAngleRadians(lst, ra));

        Assert.InRange(hourAngleDegrees, 19.999999, 20.000001);
    }

    [Fact]
    public void ProjectSmallField_UsesLocalApproximationAndRotatePoint_RotatesCounterClockwise()
    {
        var center = EquatorialCoordinate.FromDegrees(150.0, 30.0);
        var star = EquatorialCoordinate.FromDegrees(151.0, 31.0);

        var projected = SkyOrientationService.ProjectSmallField(star, center);
        var rotated = SkyOrientationService.RotatePoint(projected, Math.PI / 2.0);

        Assert.InRange(projected.X, 0.01511, 0.01512);
        Assert.InRange(projected.Y, 0.01745, 0.01746);
        Assert.InRange(rotated.X, -0.01746, -0.01745);
        Assert.InRange(rotated.Y, 0.01511, 0.01512);
    }

    [Fact]
    public void GetChartOrientation_ForM81_ReturnsFiniteAnglesAndAboveHorizon()
    {
        var observer = new ObserverLocation(51.5074, -0.1278);
        var utc = new DateTime(2026, 4, 19, 22, 0, 0, DateTimeKind.Utc);
        var m81 = EquatorialCoordinate.FromDegrees(148.8882, 69.0653);

        var orientation = SkyOrientationService.GetChartOrientation(observer, utc, m81);

        Assert.True(double.IsFinite(orientation.ParallacticAngleRadians));
        Assert.True(double.IsFinite(orientation.DisplayRotationRadians));
        Assert.InRange(orientation.ParallacticAngleDegrees, -180.0, 180.0);
        Assert.True(orientation.IsAboveHorizon);
        Assert.InRange(orientation.AltitudeDegrees, 20.0, 90.0);

        var northLength = Math.Sqrt(
            (orientation.NorthDirection.X * orientation.NorthDirection.X) +
            (orientation.NorthDirection.Y * orientation.NorthDirection.Y));

        Assert.InRange(northLength, 0.999999, 1.000001);
    }

    [Fact]
    public void GetChartOrientation_InvertFlagFlipsDisplayRotationSignOnly()
    {
        var observer = new ObserverLocation(51.5074, -0.1278);
        var utc = new DateTime(2026, 4, 19, 22, 0, 0, DateTimeKind.Utc);
        var m81 = EquatorialCoordinate.FromDegrees(148.8882, 69.0653);

        var qOrientation = SkyOrientationService.GetChartOrientation(observer, utc, m81, invertForScreenCoordinates: false);
        var negQOrientation = SkyOrientationService.GetChartOrientation(observer, utc, m81, invertForScreenCoordinates: true);

        // This is the UI verification rule: if the rendered chart rotates opposite to the real sky,
        // keep the same parallactic angle and flip only the display rotation sign.
        Assert.InRange(
            Math.Abs(qOrientation.ParallacticAngleRadians - negQOrientation.ParallacticAngleRadians),
            0.0,
            Tolerance);

        Assert.InRange(
            Math.Abs(qOrientation.DisplayRotationRadians + negQOrientation.DisplayRotationRadians),
            0.0,
            Tolerance);
    }
}