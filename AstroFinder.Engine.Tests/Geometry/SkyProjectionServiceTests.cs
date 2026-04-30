using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Tests.Geometry;

/// <summary>
/// Deterministic unit tests for <see cref="SkyProjectionService"/>.
/// All fixtures use known RA/Dec objects, observer positions, and UTC times.
/// </summary>
public class SkyProjectionServiceTests
{
    private readonly SkyProjectionService _svc = new();

    // Polaris: RA = 37.95°, Dec = +89.26°
    private const double PolarisRaDeg = 37.95;
    private const double PolarisDecDeg = 89.26;

    // Observer: 51.5°N, 0°W — near Greenwich, 2024-01-01 00:00 UTC
    private static readonly ObserverContext LondonObserver = new()
    {
        LatitudeDegrees = 51.5,
        LongitudeDegrees = 0.0,
        UtcTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void Polaris_NearZenithNorthernObserver_ProjectsNearCentre()
    {
        // Observer at 89°N: Polaris is within 1° of the zenith.
        // Compute the actual Polaris alt/az and use it as the pointing direction
        // so the test is not sensitive to sidereal time at a fixed UTC.
        var polarObserver = new ObserverContext
        {
            LatitudeDegrees = 89.0,
            LongitudeDegrees = 0.0,
            UtcTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var coordEq = EquatorialCoordinate.FromDegrees(PolarisRaDeg, PolarisDecDeg);
        var observerLocation = new ObserverLocation(polarObserver.LatitudeDegrees, polarObserver.LongitudeDegrees);
        var polarisHoriz = SkyOrientationService.GetHorizontalCoordinate(observerLocation, coordEq, polarObserver.UtcTime);

        // Point camera directly at Polaris — should project to screen centre.
        var orientation = new SkyOrientation
        {
            AzimuthDegrees = polarisHoriz.AzimuthDegrees,
            AltitudeDegrees = polarisHoriz.AltitudeDegrees,
            RollDegrees = 0,
        };

        var result = _svc.Project(PolarisRaDeg, PolarisDecDeg, orientation, polarObserver, fovDegrees: 60);

        Assert.NotNull(result);
        // Altitude for 89°N observer is ~89° → near zenith → projection is near screen centre.
        Assert.InRange(result!.Value.X, 0.3f, 0.7f);
        Assert.InRange(result.Value.Y, 0.3f, 0.7f);
    }

    [Fact]
    public void ObjectAtExactPointingDirection_ProjectsToScreenCentre()
    {
        // Pick a target object that is at a known alt/az for the observer, then point there.
        // We compute Polaris' alt/az for the observer and use that as the pointing direction.
        var coordEq = EquatorialCoordinate.FromDegrees(PolarisRaDeg, PolarisDecDeg);
        var observerLocation = new ObserverLocation(LondonObserver.LatitudeDegrees, LondonObserver.LongitudeDegrees);
        var objHoriz = SkyOrientationService.GetHorizontalCoordinate(observerLocation, coordEq, LondonObserver.UtcTime);

        var orientation = new SkyOrientation
        {
            AzimuthDegrees = objHoriz.AzimuthDegrees,
            AltitudeDegrees = objHoriz.AltitudeDegrees,
            RollDegrees = 0,
        };

        var result = _svc.Project(PolarisRaDeg, PolarisDecDeg, orientation, LondonObserver, fovDegrees: 60);

        Assert.NotNull(result);
        // Should be very close to screen centre (0.5, 0.5).
        Assert.InRange(result!.Value.X, 0.45f, 0.55f);
        Assert.InRange(result.Value.Y, 0.45f, 0.55f);
    }

    [Fact]
    public void ObjectMoreThan90DegAway_ReturnsNull()
    {
        // Polaris is near az≈0 for London. Camera pointing at az=100 → ~100° offset → outside FOV.
        var farOrientation = new SkyOrientation { AzimuthDegrees = 100, AltitudeDegrees = 45, RollDegrees = 0 };

        var result = _svc.Project(PolarisRaDeg, PolarisDecDeg, farOrientation, LondonObserver, fovDegrees: 60);

        Assert.Null(result);
    }

    [Fact]
    public void ObjectOppositePointingDirection_ReturnsNull()
    {
        // Point directly opposite to where Polaris is → must return null.
        var coordEq = EquatorialCoordinate.FromDegrees(PolarisRaDeg, PolarisDecDeg);
        var observerLocation = new ObserverLocation(LondonObserver.LatitudeDegrees, LondonObserver.LongitudeDegrees);
        var polarisHoriz = SkyOrientationService.GetHorizontalCoordinate(observerLocation, coordEq, LondonObserver.UtcTime);

        var oppositeOrientation = new SkyOrientation
        {
            AzimuthDegrees = (polarisHoriz.AzimuthDegrees + 180.0) % 360.0,
            AltitudeDegrees = -polarisHoriz.AltitudeDegrees,
            RollDegrees = 0,
        };

        var result = _svc.Project(PolarisRaDeg, PolarisDecDeg, oppositeOrientation, LondonObserver, fovDegrees: 60);

        Assert.Null(result);
    }

    [Fact]
    public void ObjectSameAzimuthHigherAlt_ScreenXIsNearHalfAndYAboveCentre()
    {
        // Camera points at same azimuth as Polaris but 20° lower in altitude.
        // Polaris: x ≈ 0.5 (no horizontal offset), y < 0.5 (above screen centre).
        var coordEq = EquatorialCoordinate.FromDegrees(PolarisRaDeg, PolarisDecDeg);
        var observerLocation = new ObserverLocation(LondonObserver.LatitudeDegrees, LondonObserver.LongitudeDegrees);
        var polarisHoriz = SkyOrientationService.GetHorizontalCoordinate(observerLocation, coordEq, LondonObserver.UtcTime);

        var orientation = new SkyOrientation
        {
            AzimuthDegrees = polarisHoriz.AzimuthDegrees,
            AltitudeDegrees = polarisHoriz.AltitudeDegrees - 20.0,
            RollDegrees = 0,
        };

        var result = _svc.Project(PolarisRaDeg, PolarisDecDeg, orientation, LondonObserver, fovDegrees: 60);

        Assert.NotNull(result);
        Assert.InRange(result!.Value.X, 0.4f, 0.6f);
        Assert.True(result.Value.Y < 0.5f);
    }

    [Fact]
    public void LstMidnightWrap_DoesNotThrow()
    {
        // Confirm no exception at the 23:59:59 sidereal wrap boundary.
        var midnightObserver = new ObserverContext
        {
            LatitudeDegrees = 51.5,
            LongitudeDegrees = 0.0,
            UtcTime = new DateTime(2024, 1, 1, 23, 59, 59, DateTimeKind.Utc),
        };
        var orientation = new SkyOrientation { AzimuthDegrees = 0, AltitudeDegrees = 89, RollDegrees = 0 };

        var ex = Record.Exception(() =>
            _svc.Project(PolarisRaDeg, PolarisDecDeg, orientation, midnightObserver, fovDegrees: 60));

        Assert.Null(ex);
    }
}
