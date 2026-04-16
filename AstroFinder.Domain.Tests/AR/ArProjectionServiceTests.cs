using AstroFinder.Domain.AR;
using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;
using Xunit;

namespace AstroFinder.Domain.Tests.AR;

/// <summary>
/// Integration tests for <see cref="ArProjectionService"/>:
/// exercises the end-to-end pipeline from RA/Dec input through to screen-space output.
/// </summary>
public sealed class ArProjectionServiceTests
{
    private static readonly CameraIntrinsics SquareCamera =
        CameraIntrinsics.FromHorizontalFov(60.0, 1000f, 1000f);

    private static readonly ArProjectionService Service = new();

    // =====================================================================
    // Helpers for building minimal route inputs
    // =====================================================================

    private static ArRouteInput MakeRoute(
        ArStarPoint target,
        double lat = 51.5,
        double lon = -0.1,
        DateTimeOffset? time = null)
    {
        return new ArRouteInput(
            ObserverLatitudeDegrees: lat,
            ObserverLongitudeDegrees: lon,
            ObservationTime: time ?? new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            Target: target,
            AsterismStars: [],
            AsterismSegments: [],
            HopSteps: [],
            BackgroundStars: []);
    }

    private static PoseMatrix MakePoseFromAltAz(double altDeg, double azDeg)
    {
        // Build a camera pose that is looking at the given alt/az direction.
        // Camera convention: Row 0=right, Row 1=up, Row 2=forward.
        // Forward = the ENU direction the camera points at.
        var forward = ArMath.AltAzToEnu(altDeg, azDeg);

        // Up hint: if we''re not looking straight up/down, use ENU up (0,0,1).
        // For near-zenith, use ENU north (0,1,0).
        var upHint = Math.Abs(altDeg) < 85.0
            ? new Vec3(0, 0, 1)
            : new Vec3(0, 1, 0);

        var right = forward.Cross(upHint).Normalized();
        var up = right.Cross(forward).Normalized();

        // PoseMatrix transforms ENU -> camera.
        // Row 0 = right, Row 1 = up, Row 2 = forward (dot products with ENU).
        return new PoseMatrix(new double[]
        {
            right.X, right.Y, right.Z,
            up.X, up.Y, up.Z,
            forward.X, forward.Y, forward.Z
        });
    }

    // =====================================================================
    // Basic projection tests
    // =====================================================================

    [Fact]
    public void Project_targetDirectlyAhead_isVisibleAndCentered()
    {
        // Compute where Betelgeuse is at our test time, then point the camera there.
        var betelgeuse = new ArStarPoint(5.919, 7.407, 0.5, "Betelgeuse", ArOverlayRole.Target);
        var route = MakeRoute(betelgeuse);

        // First, get the alt/az of the target.
        var horiz = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(betelgeuse.RaHours, betelgeuse.DecDegrees),
            route.ObserverLatitudeDegrees,
            route.ObserverLongitudeDegrees,
            route.ObservationTime);

        var pose = MakePoseFromAltAz(horiz.AltitudeDegrees, horiz.AzimuthDegrees);
        var frame = Service.Project(route, pose, SquareCamera);

        Assert.True(frame.Target.InFrontOfCamera, "Target should be in front of camera");
        Assert.NotNull(frame.Target.ScreenPoint);

        // Should be near center (500, 500) on 1000x1000 viewport.
        Assert.InRange(frame.Target.ScreenPoint.Value.XPx, 400f, 600f);
        Assert.InRange(frame.Target.ScreenPoint.Value.YPx, 400f, 600f);
    }

    [Fact]
    public void Project_targetBehindCamera_isNotVisible()
    {
        var betelgeuse = new ArStarPoint(5.919, 7.407, 0.5, "Betelgeuse", ArOverlayRole.Target);
        var route = MakeRoute(betelgeuse);

        var horiz = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(betelgeuse.RaHours, betelgeuse.DecDegrees),
            route.ObserverLatitudeDegrees,
            route.ObserverLongitudeDegrees,
            route.ObservationTime);

        // Point camera in opposite direction.
        var oppositeAz = (horiz.AzimuthDegrees + 180.0) % 360.0;
        var pose = MakePoseFromAltAz(horiz.AltitudeDegrees, oppositeAz);
        var frame = Service.Project(route, pose, SquareCamera);

        Assert.False(frame.Target.InFrontOfCamera, "Target behind camera should not be in front");
    }

    [Fact]
    public void Project_frameContainsOffscreenArrow_whenTargetNotInView()
    {
        var betelgeuse = new ArStarPoint(5.919, 7.407, 0.5, "Betelgeuse", ArOverlayRole.Target);
        var route = MakeRoute(betelgeuse);

        var horiz = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(betelgeuse.RaHours, betelgeuse.DecDegrees),
            route.ObserverLatitudeDegrees,
            route.ObserverLongitudeDegrees,
            route.ObservationTime);

        var oppositeAz = (horiz.AzimuthDegrees + 180.0) % 360.0;
        var pose = MakePoseFromAltAz(horiz.AltitudeDegrees, oppositeAz);
        var frame = Service.Project(route, pose, SquareCamera);

        Assert.NotNull(frame.OffscreenArrow);
        Assert.True(frame.OffscreenArrow.Value.DistanceDeg > 90.0);
    }

    [Fact]
    public void Project_targetOnAxis_noOffscreenArrow()
    {
        var betelgeuse = new ArStarPoint(5.919, 7.407, 0.5, "Betelgeuse", ArOverlayRole.Target);
        var route = MakeRoute(betelgeuse);

        var horiz = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(betelgeuse.RaHours, betelgeuse.DecDegrees),
            route.ObserverLatitudeDegrees,
            route.ObserverLongitudeDegrees,
            route.ObservationTime);

        var pose = MakePoseFromAltAz(horiz.AltitudeDegrees, horiz.AzimuthDegrees);
        var frame = Service.Project(route, pose, SquareCamera);

        Assert.Null(frame.OffscreenArrow);
    }

    [Fact]
    public void Project_frameContainsIntrinsics()
    {
        var target = new ArStarPoint(5.919, 7.407, 0.5, "T", ArOverlayRole.Target);
        var route = MakeRoute(target);
        var frame = Service.Project(route, PoseMatrix.Identity(), SquareCamera);

        Assert.Equal(SquareCamera.WidthPx, frame.Intrinsics.WidthPx);
        Assert.Equal(SquareCamera.HeightPx, frame.Intrinsics.HeightPx);
    }

    [Fact]
    public void Project_multipleStars_populatesAllCollections()
    {
        var target = new ArStarPoint(5.919, 7.407, 0.5, "Target", ArOverlayRole.Target);
        var asterism = new ArStarPoint(6.0, 8.0, 1.0, "A1", ArOverlayRole.AsterismStar);
        var hop = new ArStarPoint(5.5, 6.0, 2.0, "H1", ArOverlayRole.AnchorStar);
        var bg = new ArStarPoint(6.5, 9.0, 3.0, "BG1", ArOverlayRole.BackgroundStar);

        var route = new ArRouteInput(
            ObserverLatitudeDegrees: 51.5,
            ObserverLongitudeDegrees: -0.1,
            ObservationTime: new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            Target: target,
            AsterismStars: [asterism],
            AsterismSegments: [],
            HopSteps: [hop],
            BackgroundStars: [bg]);

        var frame = Service.Project(route, PoseMatrix.Identity(), SquareCamera);

        Assert.Single(frame.AsterismStars);
        Assert.Single(frame.HopSteps);
        Assert.Single(frame.BackgroundStars);
        Assert.Equal("Target", frame.Target.Label);
        Assert.Equal("A1", frame.AsterismStars[0].Label);
    }

    [Fact]
    public void Project_angularDistance_isNonNegative()
    {
        var target = new ArStarPoint(5.919, 7.407, 0.5, "T", ArOverlayRole.Target);
        var route = MakeRoute(target);
        var frame = Service.Project(route, PoseMatrix.Identity(), SquareCamera);

        Assert.True(frame.TargetAngularDistanceDegrees >= 0.0);
    }

    [Fact]
    public void Project_targetOnAxis_showsOnTargetReticle()
    {
        var betelgeuse = new ArStarPoint(5.919, 7.407, 0.5, "Betelgeuse", ArOverlayRole.Target);
        var route = MakeRoute(betelgeuse);

        var horiz = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(betelgeuse.RaHours, betelgeuse.DecDegrees),
            route.ObserverLatitudeDegrees,
            route.ObserverLongitudeDegrees,
            route.ObservationTime);

        var pose = MakePoseFromAltAz(horiz.AltitudeDegrees, horiz.AzimuthDegrees);
        var frame = Service.Project(route, pose, SquareCamera);

        Assert.Equal("On target", frame.CenterReticleText);
    }

    // =====================================================================
    // Pose matrix axis semantics (catches sensor-mapping bugs)
    // =====================================================================

    [Fact]
    public void MakePoseFromAltAz_lookingNorthHorizontal_forwardIsNorth()
    {
        var pose = MakePoseFromAltAz(0.0, 0.0); // altitude=0, azimuth=0 (north)
        var fwd = new Vec3(pose.M[6], pose.M[7], pose.M[8]); // row 2 = forward

        // Camera forward should point north: ENU Y ≈ 1
        Assert.True(fwd.Y > 0.9, $"Forward should point north (Y≈1), got e={fwd.X:F2} n={fwd.Y:F2} u={fwd.Z:F2}");
        Assert.True(Math.Abs(fwd.Z) < 0.1, $"Forward should be horizontal (U≈0), got u={fwd.Z:F2}");
    }

    [Fact]
    public void MakePoseFromAltAz_lookingNorthHorizontal_upIsUp()
    {
        var pose = MakePoseFromAltAz(0.0, 0.0);
        var up = new Vec3(pose.M[3], pose.M[4], pose.M[5]); // row 1 = up

        // Camera up should point toward zenith: ENU Z ≈ 1
        Assert.True(up.Z > 0.9, $"Up should point to zenith (U≈1), got e={up.X:F2} n={up.Y:F2} u={up.Z:F2}");
    }

    [Fact]
    public void MakePoseFromAltAz_lookingDown45North_forwardHasNegativeU()
    {
        var pose = MakePoseFromAltAz(-45.0, 0.0); // tilted down 45° facing north
        var fwd = new Vec3(pose.M[6], pose.M[7], pose.M[8]);

        // Forward should point north-ish AND downward
        Assert.True(fwd.Y > 0.5, $"Forward should have northward component, got n={fwd.Y:F2}");
        Assert.True(fwd.Z < -0.5, $"Forward should point downward (U<-0.5), got u={fwd.Z:F2}");
    }

    [Fact]
    public void MakePoseFromAltAz_lookingUp45North_forwardHasPositiveU()
    {
        var pose = MakePoseFromAltAz(45.0, 0.0); // tilted up 45° facing north
        var fwd = new Vec3(pose.M[6], pose.M[7], pose.M[8]);
        var up = new Vec3(pose.M[3], pose.M[4], pose.M[5]);

        // Forward should point north-ish AND upward
        Assert.True(fwd.Y > 0.5, $"Forward should have northward component, got n={fwd.Y:F2}");
        Assert.True(fwd.Z > 0.5, $"Forward should point upward (U>0.5), got u={fwd.Z:F2}");
        // Up should still have positive U component (top of phone still above horizon)
        Assert.True(up.Z > 0.0, $"Up should have upward component, got u={up.Z:F2}");
    }

    [Fact]
    public void MakePoseFromAltAz_lookingEast_forwardIsEast()
    {
        var pose = MakePoseFromAltAz(0.0, 90.0); // azimuth=90 (east)
        var fwd = new Vec3(pose.M[6], pose.M[7], pose.M[8]);

        Assert.True(fwd.X > 0.9, $"Forward should point east (E≈1), got e={fwd.X:F2} n={fwd.Y:F2} u={fwd.Z:F2}");
    }

    [Fact]
    public void PoseMatrix_multiplyForward_matchesForwardRow()
    {
        // Verify that pose.Multiply(forward_enu) gives cam.Z > 0
        // and that pose.Multiply(right_enu) gives cam.X > 0
        var pose = MakePoseFromAltAz(30.0, 45.0);
        var forward = ArMath.AltAzToEnu(30.0, 45.0);

        var camCoords = pose.Multiply(forward);
        // Object at the direction the camera points should have cam.Z ≈ 1 (forward)
        Assert.True(camCoords.Z > 0.9, $"Object at camera forward should have cam.Z≈1, got {camCoords.Z:F3}");
        Assert.True(Math.Abs(camCoords.X) < 0.1, $"Should be centered X, got {camCoords.X:F3}");
        Assert.True(Math.Abs(camCoords.Y) < 0.1, $"Should be centered Y, got {camCoords.Y:F3}");
    }

    // =====================================================================
    // ARCore pose-pipeline simulation tests
    // =====================================================================

    /// <summary>
    /// Simulates the ARCore pose conversion pipeline from ArCorePoseProvider.
    /// Input is camera-to-world 3x3 (row-major) as extracted from column-major 4x4.
    /// We transpose to get world-to-camera, multiply by ENU→ARCore-world heading matrix,
    /// then flip the Z row.
    /// </summary>
    private static PoseMatrix SimulateArCorePipeline(double[] c2wRowMajor, double headingRad)
    {
        // Transpose c2w to get w2c (rotation matrices are orthogonal).
        var rW2C = new double[9];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                rW2C[r * 3 + c] = c2wRowMajor[c * 3 + r];

        // ENU→ARCore-world matrix from compass heading
        double cosH = Math.Cos(headingRad);
        double sinH = Math.Sin(headingRad);
        var enuToAr = new double[]
        {
             cosH, -sinH,  0.0,
             0.0,   0.0,   1.0,
            -sinH, -cosH,  0.0,
        };

        // ENU-to-camera = world-to-camera * ENU-to-world
        var enuToCam = new double[9];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                enuToCam[r * 3 + c] =
                    rW2C[r * 3 + 0] * enuToAr[0 * 3 + c] +
                    rW2C[r * 3 + 1] * enuToAr[1 * 3 + c] +
                    rW2C[r * 3 + 2] * enuToAr[2 * 3 + c];

        // Flip Z row (ARCore -Z=forward, we use +Z=forward)
        enuToCam[6] = -enuToCam[6];
        enuToCam[7] = -enuToCam[7];
        enuToCam[8] = -enuToCam[8];

        return new PoseMatrix(enuToCam);
    }

    [Fact]
    public void ArCorePipeline_identityPoseFacingNorth_forwardIsNorth()
    {
        // ARCore session started facing north (heading=0).
        // Camera at initial position → identity camera-to-world.
        var cameraPose = new double[]
        {
            1, 0, 0,
            0, 1, 0,
            0, 0, 1,
        };

        var pose = SimulateArCorePipeline(cameraPose, headingRad: 0.0);

        var right = new Vec3(pose.M[0], pose.M[1], pose.M[2]);
        var up    = new Vec3(pose.M[3], pose.M[4], pose.M[5]);
        var fwd   = new Vec3(pose.M[6], pose.M[7], pose.M[8]);

        Assert.True(right.X > 0.9, $"Right should be east (e≈1), got e={right.X:F2} n={right.Y:F2} u={right.Z:F2}");
        Assert.True(up.Z > 0.9, $"Up should be zenith (u≈1), got e={up.X:F2} n={up.Y:F2} u={up.Z:F2}");
        Assert.True(fwd.Y > 0.9, $"Fwd should be north (n≈1), got e={fwd.X:F2} n={fwd.Y:F2} u={fwd.Z:F2}");
    }

    [Fact]
    public void ArCorePipeline_identityPoseFacingEast_forwardIsEast()
    {
        // ARCore session started facing east (heading=π/2).
        var cameraPose = new double[]
        {
            1, 0, 0,
            0, 1, 0,
            0, 0, 1,
        };

        var pose = SimulateArCorePipeline(cameraPose, headingRad: Math.PI / 2);

        var fwd = new Vec3(pose.M[6], pose.M[7], pose.M[8]);
        Assert.True(fwd.X > 0.9, $"Fwd should be east (e≈1), got e={fwd.X:F2} n={fwd.Y:F2} u={fwd.Z:F2}");
    }

    [Fact]
    public void ArCorePipeline_cameraRotated90LeftFromNorth_forwardIsWest()
    {
        // Session started facing north (heading=0). Camera rotates 90° CCW around Y (up).
        // c2w = Ry(+90°) row-major = [0,0,1; 0,1,0; -1,0,0].
        // Turning left from north → looking west.
        var c2w = new double[]
        {
            0, 0, 1,
            0, 1, 0,
           -1, 0, 0,
        };

        var pose = SimulateArCorePipeline(c2w, headingRad: 0.0);

        var fwd = new Vec3(pose.M[6], pose.M[7], pose.M[8]);
        Assert.True(fwd.X < -0.9, $"Fwd should be west (e≈-1), got e={fwd.X:F2} n={fwd.Y:F2} u={fwd.Z:F2}");
    }

    [Fact]
    public void ArCorePipeline_identityPoseFacingSouth_forwardIsSouth()
    {
        // heading=π → facing south.
        var cameraPose = new double[]
        {
            1, 0, 0,
            0, 1, 0,
            0, 0, 1,
        };

        var pose = SimulateArCorePipeline(cameraPose, headingRad: Math.PI);

        var fwd = new Vec3(pose.M[6], pose.M[7], pose.M[8]);
        Assert.True(fwd.Y < -0.9, $"Fwd should be south (n≈-1), got e={fwd.X:F2} n={fwd.Y:F2} u={fwd.Z:F2}");
    }

    [Fact]
    public void ArCorePipeline_facingNorth_projectionCentersOnNorthHorizon()
    {
        // End-to-end: identity pose, heading=0, star at az=0 alt=0 (north horizon)
        // should project near screen center.
        var cameraPose = new double[]
        {
            1, 0, 0,
            0, 1, 0,
            0, 0, 1,
        };

        var pose = SimulateArCorePipeline(cameraPose, headingRad: 0.0);

        var northHorizon = ArMath.AltAzToEnu(0.0, 0.0);
        var screenPt = ArMath.ProjectToScreen(pose, northHorizon, SquareCamera);

        Assert.NotNull(screenPt);
        Assert.InRange(screenPt.Value.XPx, 400f, 600f);
        Assert.InRange(screenPt.Value.YPx, 400f, 600f);
    }
}
