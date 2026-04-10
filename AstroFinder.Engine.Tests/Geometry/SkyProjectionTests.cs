using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Tests.Geometry;

public class SkyProjectionTests
{
    [Fact]
    public void EquatorialToHorizontal_WhenTargetOnMeridianAtObserverLatitude_IsNearZenith()
    {
        var observationTime = new DateTimeOffset(2026, 4, 10, 22, 0, 0, TimeSpan.Zero);
        const double latitude = 52.0;
        const double longitude = 0.0;
        var lstDegrees = SkyProjection.LocalSiderealTimeDegrees(observationTime, longitude);
        var coordinate = new EquatorialCoordinate(lstDegrees / 15.0, latitude);

        var horizontal = SkyProjection.EquatorialToHorizontal(coordinate, latitude, longitude, observationTime);

        Assert.InRange(horizontal.AltitudeDegrees, 89.0, 90.0);
    }

    [Fact]
    public void EquatorialToHorizontal_WhenTargetOnSouthernMeridian_HasSouthernAzimuth()
    {
        var observationTime = new DateTimeOffset(2026, 4, 10, 22, 0, 0, TimeSpan.Zero);
        const double latitude = 52.0;
        const double longitude = 0.0;
        var lstDegrees = SkyProjection.LocalSiderealTimeDegrees(observationTime, longitude);
        var coordinate = new EquatorialCoordinate(lstDegrees / 15.0, 0.0);

        var horizontal = SkyProjection.EquatorialToHorizontal(coordinate, latitude, longitude, observationTime);

        Assert.InRange(horizontal.AzimuthDegrees, 170.0, 190.0);
        Assert.InRange(horizontal.AltitudeDegrees, 35.0, 40.5);
    }
}
