using AstroFinder.Domain.AR;
using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Domain.Tests.AR;

public class ArProjectionServiceTests
{
    private static readonly double ObserverLat = 51.5;
    private static readonly double ObserverLon = 0.0;
    // A time when Polaris is well above the horizon for the test observer.
    private static readonly DateTimeOffset ObservationTime =
        new(2026, 4, 10, 22, 0, 0, TimeSpan.Zero);

    private static readonly CameraViewport SquareViewport = new(1000.0, 1000.0, 60.0);
    private static readonly ArProjectionService Service = new();

    // ------------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal route input containing only a single target star.
    /// </summary>
    private static ArRouteInput SingleStarRoute(ArStarPoint target, DateTimeOffset? time = null) =>
        new(
            ObserverLatitudeDegrees: ObserverLat,
            ObserverLongitudeDegrees: ObserverLon,
            ObservationTime: time ?? ObservationTime,
            Target: target,
            AsterismStars: [],
            AsterismSegments: [],
            HopSteps: [],
            BackgroundStars: []);

    // ------------------------------------------------------------------
    // Test 1: Determinism — identical inputs produce identical output.
    // ------------------------------------------------------------------

    [Fact]
    public void Project_SameInputs_ProduceIdenticalOutput()
    {
        var star = new ArStarPoint(2.5298, 89.26, 2.0, "Polaris", ArOverlayRole.Target);
        var route = SingleStarRoute(star);
        var horizontal = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(star.RaHours, star.DecDegrees),
            ObserverLat, ObserverLon, ObservationTime);
        var pose = new DevicePose(horizontal.AzimuthDegrees, horizontal.AltitudeDegrees);

        var frame1 = Service.Project(route, pose, SquareViewport);
        var frame2 = Service.Project(route, pose, SquareViewport);

        Assert.Equal(frame1.Target.ScreenX, frame2.Target.ScreenX);
        Assert.Equal(frame1.Target.ScreenY, frame2.Target.ScreenY);
        Assert.Equal(frame1.Target.IsVisible, frame2.Target.IsVisible);
    }

    // ------------------------------------------------------------------
    // Test 2: Star on the device's exact pointing axis → appears at screen centre.
    // ------------------------------------------------------------------

    [Fact]
    public void Project_StarAtDevicePointingAxis_AppearsAtScreenCentre()
    {
        // Polaris (RA 2.5298 h, Dec +89.26°) is permanently above the horizon
        // at 51.5°N and maps to a known alt/az at the test time.
        var star = new ArStarPoint(2.5298, 89.26, 2.0, "Polaris", ArOverlayRole.Target);
        var route = SingleStarRoute(star);

        // Point the device exactly where Polaris is.
        var horizontal = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(star.RaHours, star.DecDegrees),
            ObserverLat, ObserverLon, ObservationTime);
        var pose = new DevicePose(horizontal.AzimuthDegrees, horizontal.AltitudeDegrees);

        var frame = Service.Project(route, pose, SquareViewport);

        // With dAz = 0 and dAlt = 0 the star must land on screen centre (500, 500).
        Assert.InRange(frame.Target.ScreenX, 490.0, 510.0);
        Assert.InRange(frame.Target.ScreenY, 490.0, 510.0);
        Assert.True(frame.Target.IsVisible);
    }

    // ------------------------------------------------------------------
    // Test 3: Star 180° opposite to the device heading → not visible.
    // ------------------------------------------------------------------

    [Fact]
    public void Project_StarBehindDevice_NotVisible()
    {
        // Use Polaris: guaranteed above the horizon from 51.5°N but we will
        // point the device 180° away so it is outside the FOV.
        var star = new ArStarPoint(2.5298, 89.26, 2.0, "Polaris", ArOverlayRole.Target);
        var route = SingleStarRoute(star);

        var horizontal = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(star.RaHours, star.DecDegrees),
            ObserverLat, ObserverLon, ObservationTime);

        // Point the device directly opposite (180° azimuth away) at the same pitch.
        var oppositeHeading = (horizontal.AzimuthDegrees + 180.0) % 360.0;
        var pose = new DevicePose(oppositeHeading, horizontal.AltitudeDegrees);

        var frame = Service.Project(route, pose, SquareViewport);

        Assert.False(frame.Target.IsVisible);
    }

    // ------------------------------------------------------------------
    // Test 4: Below-horizon object → IsVisible is false regardless of device pose.
    // ------------------------------------------------------------------

    [Fact]
    public void Project_BelowHorizonStar_NotVisible()
    {
        // Sigma Octantis (south polar star, Dec ≈ –88.97°).
        // Viewed from 51.5°N it is permanently below the horizon.
        var southPole = new ArStarPoint(21.0785, -88.97, 5.5, "SigOct", ArOverlayRole.Target);
        var route = SingleStarRoute(southPole);

        var horizontal = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(southPole.RaHours, southPole.DecDegrees),
            ObserverLat, ObserverLon, ObservationTime);

        // Even if we point the device at its computed position, it should be rejected
        // because its altitude is below the –5° threshold.
        var pose = new DevicePose(horizontal.AzimuthDegrees, horizontal.AltitudeDegrees);

        var frame = Service.Project(route, pose, SquareViewport);

        Assert.True(horizontal.AltitudeDegrees < -5.0, "Sanity: south-polar star must be below horizon from 51.5°N");
        Assert.False(frame.Target.IsVisible);
    }

    // ------------------------------------------------------------------
    // Test 5: Azimuth wrap-around — star just west of north is placed
    //         correctly to the left of a north-pointing device.
    // ------------------------------------------------------------------

    [Fact]
    public void Project_AzimuthWrapAroundNorth_PlacedLeftOfCentre()
    {
        // We need a known star whose azimuth is predictable.
        // Use Polaris and derive its position, then construct a synthetic
        // test: point device at Az=5°, observe a star at Az=355° (10° to the left).
        // The star should be at screenX < 500 (left of centre).
        //
        // Because we cannot trivially construct a star at exactly Az=355° from
        // equatorial coords, we verify the azimuth-wrap invariant via the service
        // directly by comparing two orientations.

        var star = new ArStarPoint(2.5298, 89.26, 2.0, "Polaris", ArOverlayRole.Target);
        var route = SingleStarRoute(star);

        var horizontal = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(star.RaHours, star.DecDegrees),
            ObserverLat, ObserverLon, ObservationTime);

        // Pose A: device heading is 10° clockwise of the star's azimuth.
        // The star is 10° to the LEFT of the device heading → screenX < centre.
        var poseWithStarToLeft = new DevicePose(
            (horizontal.AzimuthDegrees + 10.0) % 360.0,
            horizontal.AltitudeDegrees);

        // Pose B: device heading is 10° counter-clockwise of the star's azimuth.
        // The star is 10° to the RIGHT of the device heading → screenX > centre.
        var poseWithStarToRight = new DevicePose(
            (horizontal.AzimuthDegrees - 10.0 + 360.0) % 360.0,
            horizontal.AltitudeDegrees);

        var frameLeft = Service.Project(route, poseWithStarToLeft, SquareViewport);
        var frameRight = Service.Project(route, poseWithStarToRight, SquareViewport);

        Assert.True(frameLeft.Target.ScreenX < 500.0, "Star to the left of device heading must yield ScreenX < centre");
        Assert.True(frameRight.Target.ScreenX > 500.0, "Star to the right of device heading must yield ScreenX > centre");
    }

    // ------------------------------------------------------------------
    // Test 6: Multiple route points (anchor + hop + target) are all projected.
    // ------------------------------------------------------------------

    [Fact]
    public void Project_MultipleRoutePoints_AllProjected()
    {
        var anchor = new ArStarPoint(2.5298, 89.26, 2.0, "Polaris", ArOverlayRole.AnchorStar);
        var target = new ArStarPoint(3.0, 85.0, 5.0, "Target", ArOverlayRole.Target);

        var horizontal = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(anchor.RaHours, anchor.DecDegrees),
            ObserverLat, ObserverLon, ObservationTime);
        var pose = new DevicePose(horizontal.AzimuthDegrees, horizontal.AltitudeDegrees);

        var route = new ArRouteInput(
            ObserverLatitudeDegrees: ObserverLat,
            ObserverLongitudeDegrees: ObserverLon,
            ObservationTime: ObservationTime,
            Target: target,
            AsterismStars: [new ArStarPoint(2.5298, 89.26, 2.0, "PolAst", ArOverlayRole.AsterismStar)],
            AsterismSegments: [],
            HopSteps: [anchor],
            BackgroundStars: [new ArStarPoint(4.0, 80.0, 6.5, "BG", ArOverlayRole.BackgroundStar)]);

        var frame = Service.Project(route, pose, SquareViewport);

        Assert.Single(frame.AsterismStars);
        Assert.Single(frame.HopSteps);
        Assert.Single(frame.BackgroundStars);
        Assert.NotNull(frame.Target);
    }

    // ------------------------------------------------------------------
    // Test 7: Target at same az/alt as device → distance ≈ 0.
    // ------------------------------------------------------------------

    [Fact]
    public void Project_TargetAtDevicePointing_DistanceNearZero()
    {
        var star = new ArStarPoint(2.5298, 89.26, 2.0, "Polaris", ArOverlayRole.Target);
        var route = SingleStarRoute(star);

        var horizontal = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(star.RaHours, star.DecDegrees),
            ObserverLat, ObserverLon, ObservationTime);
        var pose = new DevicePose(horizontal.AzimuthDegrees, horizontal.AltitudeDegrees);

        var frame = Service.Project(route, pose, SquareViewport);

        Assert.InRange(frame.TargetAngularDistanceDegrees, 0.0, 1.0);
    }

    // ------------------------------------------------------------------
    // Test 8: Target directly above device pointing → bearing ≈ 0° (up).
    // ------------------------------------------------------------------

    [Fact]
    public void Project_TargetDirectlyAbove_BearingNearZero()
    {
        var star = new ArStarPoint(2.5298, 89.26, 2.0, "Polaris", ArOverlayRole.Target);
        var route = SingleStarRoute(star);

        var horizontal = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(star.RaHours, star.DecDegrees),
            ObserverLat, ObserverLon, ObservationTime);

        // Point the device 10° below the star (same azimuth) → star is directly above.
        var pose = new DevicePose(horizontal.AzimuthDegrees, horizontal.AltitudeDegrees - 10.0);

        var frame = Service.Project(route, pose, SquareViewport);

        // Bearing should be near 0° (up) or near 360° (equivalent).
        var normBearing = frame.TargetBearingDegrees % 360.0;
        Assert.True(normBearing < 5.0 || normBearing > 355.0,
            $"Expected bearing near 0° (up), got {normBearing:F1}°");
        Assert.True(frame.TargetAngularDistanceDegrees > 5.0);
    }

    // ------------------------------------------------------------------
    // Test 9: Target directly to the right → bearing ≈ 90°.
    // ------------------------------------------------------------------

    [Fact]
    public void Project_TargetDirectlyRight_BearingNear90()
    {
        var star = new ArStarPoint(2.5298, 89.26, 2.0, "Polaris", ArOverlayRole.Target);
        var route = SingleStarRoute(star);

        var horizontal = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(star.RaHours, star.DecDegrees),
            ObserverLat, ObserverLon, ObservationTime);

        // Point the device 10° to the left of the star (same altitude) → star is to the right.
        var poseHeading = (horizontal.AzimuthDegrees - 10.0 + 360.0) % 360.0;
        var pose = new DevicePose(poseHeading, horizontal.AltitudeDegrees);

        var frame = Service.Project(route, pose, SquareViewport);

        Assert.InRange(frame.TargetBearingDegrees, 80.0, 100.0);
    }

    // ------------------------------------------------------------------
    // Test 10: Target 180° behind → distance large, bearing is valid.
    // ------------------------------------------------------------------

    [Fact]
    public void Project_TargetBehind_DistanceLargeAndBearingValid()
    {
        var star = new ArStarPoint(2.5298, 89.26, 2.0, "Polaris", ArOverlayRole.Target);
        var route = SingleStarRoute(star);

        var horizontal = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(star.RaHours, star.DecDegrees),
            ObserverLat, ObserverLon, ObservationTime);

        var oppositeHeading = (horizontal.AzimuthDegrees + 180.0) % 360.0;
        var pose = new DevicePose(oppositeHeading, horizontal.AltitudeDegrees);

        var frame = Service.Project(route, pose, SquareViewport);

        Assert.True(frame.TargetAngularDistanceDegrees > 100.0,
            $"Expected large angular distance, got {frame.TargetAngularDistanceDegrees:F1}°");
        Assert.False(double.IsNaN(frame.TargetBearingDegrees));
        Assert.InRange(frame.TargetBearingDegrees, 0.0, 360.0);
    }

    // ------------------------------------------------------------------
    // Test 11: Azimuth near 0°/360° wrap → bearing remains stable.
    // ------------------------------------------------------------------

    [Fact]
    public void Project_AzimuthWrap_BearingStable()
    {
        var star = new ArStarPoint(2.5298, 89.26, 2.0, "Polaris", ArOverlayRole.Target);
        var route = SingleStarRoute(star);

        var horizontal = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(star.RaHours, star.DecDegrees),
            ObserverLat, ObserverLon, ObservationTime);

        // Two device headings that straddle the star and are both slightly
        // to the left → bearing should be similar (both pointing right).
        var headingA = (horizontal.AzimuthDegrees - 5.0 + 360.0) % 360.0;
        var headingB = (horizontal.AzimuthDegrees - 6.0 + 360.0) % 360.0;

        var frameA = Service.Project(route, new DevicePose(headingA, horizontal.AltitudeDegrees), SquareViewport);
        var frameB = Service.Project(route, new DevicePose(headingB, horizontal.AltitudeDegrees), SquareViewport);

        // Both bearings should be rightward (~90°) and within a few degrees of each other.
        var diff = Math.Abs(frameA.TargetBearingDegrees - frameB.TargetBearingDegrees);
        if (diff > 180.0) diff = 360.0 - diff;
        Assert.True(diff < 10.0,
            $"Expected similar bearings, got {frameA.TargetBearingDegrees:F1}° and {frameB.TargetBearingDegrees:F1}° (diff={diff:F1}°)");
    }
}
