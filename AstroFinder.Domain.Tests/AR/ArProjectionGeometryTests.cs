using AstroFinder.Domain.AR;
using Xunit;

namespace AstroFinder.Domain.Tests.AR;

/// <summary>
/// First-principles tests for <see cref="ArMath"/> � the pure math layer
/// ported from the reference AstronomyMath.kt implementation.
/// </summary>
public sealed class ArProjectionGeometryTests
{
    private const double Tolerance = 1e-9;
    private const double AngleTolerance = 0.5;

    private static readonly CameraIntrinsics DefaultIntrinsics =
        CameraIntrinsics.FromHorizontalFov(60.0, 1000f, 1000f);

    // =====================================================================
    // Vec3 basics
    // =====================================================================

    [Fact]
    public void Vec3_Norm_returnsLength()
    {
        var v = new Vec3(3.0, 4.0, 0.0);
        Assert.Equal(5.0, v.Norm(), Tolerance);
    }

    [Fact]
    public void Vec3_Normalized_isUnitLength()
    {
        var v = new Vec3(2.0, 3.0, 6.0);
        Assert.Equal(1.0, v.Normalized().Norm(), Tolerance);
    }

    [Fact]
    public void Vec3_Cross_isRightHanded()
    {
        var x = new Vec3(1, 0, 0);
        var y = new Vec3(0, 1, 0);
        var z = x.Cross(y);
        Assert.Equal(0.0, z.X, Tolerance);
        Assert.Equal(0.0, z.Y, Tolerance);
        Assert.Equal(1.0, z.Z, Tolerance);
    }

    [Fact]
    public void Vec3_Dot_orthogonalIsZero()
    {
        var a = new Vec3(1, 0, 0);
        var b = new Vec3(0, 1, 0);
        Assert.Equal(0.0, a.Dot(b), Tolerance);
    }

    // =====================================================================
    // AltAzToEnu
    // =====================================================================

    [Fact]
    public void AltAzToEnu_returnsUnitVector()
    {
        var v = ArMath.AltAzToEnu(30.0, 45.0);
        Assert.Equal(1.0, v.Norm(), Tolerance);
    }

    [Theory]
    [InlineData(0.0, 0.0)]     // az 0 (north) -> Y=1
    [InlineData(0.0, 90.0)]    // az 90 (east) -> X=1
    [InlineData(0.0, 180.0)]   // az 180 (south) -> Y=-1
    [InlineData(90.0, 0.0)]    // alt 90 (zenith) -> Z=1
    public void AltAzToEnu_cardinalDirections(double alt, double az)
    {
        var v = ArMath.AltAzToEnu(alt, az);
        if (alt == 0.0 && az == 0.0)
        {
            Assert.True(v.Y > 0.9, $"Expected Y~1 for north, got {v}");
            Assert.True(Math.Abs(v.X) < 0.01 && Math.Abs(v.Z) < 0.01);
        }
        else if (alt == 0.0 && az == 90.0)
        {
            Assert.True(v.X > 0.9, $"Expected X~1 for east, got {v}");
        }
        else if (alt == 0.0 && az == 180.0)
        {
            Assert.True(v.Y < -0.9, $"Expected Y~-1 for south, got {v}");
        }
        else if (alt == 90.0)
        {
            Assert.True(v.Z > 0.9, $"Expected Z~1 for zenith, got {v}");
        }
    }

    // =====================================================================
    // PoseMatrix
    // =====================================================================

    [Fact]
    public void PoseMatrix_Identity_passesThrough()
    {
        var input = new Vec3(1.0, 2.0, 3.0);
        var result = PoseMatrix.Identity().Multiply(input);
        Assert.Equal(input.X, result.X, Tolerance);
        Assert.Equal(input.Y, result.Y, Tolerance);
        Assert.Equal(input.Z, result.Z, Tolerance);
    }

    [Fact]
    public void PoseMatrix_Multiply_chainsCorrectly()
    {
        var a = PoseMatrix.Identity();
        var b = PoseMatrix.Identity();
        var result = a.Multiply(b);
        var v = result.Multiply(new Vec3(1, 2, 3));
        Assert.Equal(1.0, v.X, Tolerance);
        Assert.Equal(2.0, v.Y, Tolerance);
        Assert.Equal(3.0, v.Z, Tolerance);
    }

    [Fact]
    public void PoseMatrix_Orthonormalized_preservesIdentity()
    {
        var id = PoseMatrix.Identity().Orthonormalized();
        var v = id.Multiply(new Vec3(5, 6, 7));
        Assert.Equal(5.0, v.X, Tolerance);
        Assert.Equal(6.0, v.Y, Tolerance);
        Assert.Equal(7.0, v.Z, Tolerance);
    }

    // =====================================================================
    // ProjectToScreen � NDC projection
    // =====================================================================

    [Fact]
    public void ProjectToScreen_returnsNull_behindCamera()
    {
        var behind = new Vec3(0.0, 0.0, -1.0);
        var result = ArMath.ProjectToScreen(PoseMatrix.Identity(), behind, DefaultIntrinsics);
        Assert.Null(result);
    }

    [Fact]
    public void ProjectToScreen_directlyAhead_isScreenCenter()
    {
        var ahead = new Vec3(0.0, 0.0, 1.0);
        var result = ArMath.ProjectToScreen(PoseMatrix.Identity(), ahead, DefaultIntrinsics);
        Assert.NotNull(result);
        Assert.Equal(500.0, result.Value.XPx, 1.0);
        Assert.Equal(500.0, result.Value.YPx, 1.0);
    }

    [Fact]
    public void ProjectToScreen_right_isRightOfCenter()
    {
        var right = new Vec3(0.3, 0.0, 1.0).Normalized();
        var result = ArMath.ProjectToScreen(PoseMatrix.Identity(), right, DefaultIntrinsics);
        Assert.NotNull(result);
        Assert.True(result.Value.XPx > 500f, $"Object right of camera should appear right of center, got X={result.Value.XPx}");
    }

    [Fact]
    public void ProjectToScreen_above_isAboveCenter()
    {
        // Camera +Y is up, screen +Y is down -> above should have smaller Y
        var above = new Vec3(0.0, 0.3, 1.0).Normalized();
        var result = ArMath.ProjectToScreen(PoseMatrix.Identity(), above, DefaultIntrinsics);
        Assert.NotNull(result);
        Assert.True(result.Value.YPx < 500f, $"Object above camera should appear above center, got Y={result.Value.YPx}");
    }

    [Fact]
    public void ProjectToScreen_below_isBelowCenter()
    {
        var below = new Vec3(0.0, -0.3, 1.0).Normalized();
        var result = ArMath.ProjectToScreen(PoseMatrix.Identity(), below, DefaultIntrinsics);
        Assert.NotNull(result);
        Assert.True(result.Value.YPx > 500f, $"Object below camera should appear below center, got Y={result.Value.YPx}");
    }

    // =====================================================================
    // IsInFrontOfCamera
    // =====================================================================

    [Fact]
    public void IsInFront_trueForForwardVector()
    {
        Assert.True(ArMath.IsInFrontOfCamera(PoseMatrix.Identity(), new Vec3(0, 0, 1)));
    }

    [Fact]
    public void IsInFront_falseForBackwardVector()
    {
        Assert.False(ArMath.IsInFrontOfCamera(PoseMatrix.Identity(), new Vec3(0, 0, -1)));
    }

    // =====================================================================
    // IsWithinViewport
    // =====================================================================

    [Fact]
    public void IsWithinViewport_centerIsInside()
    {
        Assert.True(ArMath.IsWithinViewport(new ArScreenPoint(500, 500), DefaultIntrinsics));
    }

    [Fact]
    public void IsWithinViewport_outsideIsOutside()
    {
        Assert.False(ArMath.IsWithinViewport(new ArScreenPoint(-10, 500), DefaultIntrinsics));
        Assert.False(ArMath.IsWithinViewport(new ArScreenPoint(500, 1050), DefaultIntrinsics));
    }

    // =====================================================================
    // AngularSeparation
    // =====================================================================

    [Fact]
    public void AngularSeparation_identicalIsZero()
    {
        var a = new Vec3(0, 0, 1);
        Assert.Equal(0.0, ArMath.AngularSeparationDeg(a, a), Tolerance);
    }

    [Fact]
    public void AngularSeparation_orthogonalIs90()
    {
        var a = new Vec3(1, 0, 0);
        var b = new Vec3(0, 1, 0);
        Assert.Equal(90.0, ArMath.AngularSeparationDeg(a, b), 0.01);
    }

    [Fact]
    public void AngularSeparation_oppositeIs180()
    {
        var a = new Vec3(0, 0, 1);
        var b = new Vec3(0, 0, -1);
        Assert.Equal(180.0, ArMath.AngularSeparationDeg(a, b), 0.01);
    }

    // =====================================================================
    // OffscreenArrowGuidance
    // =====================================================================

    [Fact]
    public void OffscreenArrow_onAxis_returnsNull()
    {
        // Camera looking directly at the target -> null
        var enu = new Vec3(0, 0, 1);
        var arrow = ArMath.OffscreenArrowGuidance(PoseMatrix.Identity(), enu, 2.0);
        Assert.Null(arrow);
    }

    [Fact]
    public void OffscreenArrow_behindCamera_returnsGuidance()
    {
        var behind = new Vec3(0, 0, -1);
        var arrow = ArMath.OffscreenArrowGuidance(PoseMatrix.Identity(), behind, 2.0);
        Assert.NotNull(arrow);
        Assert.True(arrow.Value.DistanceDeg > 90.0);
    }

    // =====================================================================
    // JulianDate
    // =====================================================================

    [Fact]
    public void JulianDate_unixEpoch()
    {
        var jd = ArMath.JulianDate(0L);
        Assert.Equal(2440587.5, jd, Tolerance);
    }

    // =====================================================================
    // RaDecToAltAz  � sanity check
    // =====================================================================

    [Fact]
    public void RaDecToAltAz_returnsSaneRanges()
    {
        // 2026-01-15T00:00:00Z
        long utcMs = 1768435200000L;
        var (alt, az) = ArMath.RaDecToAltAz(5.0, 20.0, 51.5, -0.1, utcMs);
        Assert.InRange(az, 0.0, 360.0);
        Assert.InRange(alt, -90.0, 90.0);
    }

    // =====================================================================
    // SmoothPose
    // =====================================================================

    [Fact]
    public void SmoothPose_identitySmoothedWithItself_isIdentity()
    {
        var id = PoseMatrix.Identity();
        var smoothed = ArMath.SmoothPose(id, id, 0.5);
        var v = smoothed.Multiply(new Vec3(1, 0, 0));
        Assert.Equal(1.0, v.X, 1e-6);
        Assert.Equal(0.0, v.Y, 1e-6);
        Assert.Equal(0.0, v.Z, 1e-6);
    }

    // =====================================================================
    // CameraIntrinsics factory
    // =====================================================================

    [Fact]
    public void CameraIntrinsics_FromHorizontalFov_computesVerticalFov()
    {
        var cam = CameraIntrinsics.FromHorizontalFov(60.0, 1080f, 1920f);
        // For a 9:16 aspect ratio, vertical FOV should be larger than horizontal
        Assert.True(cam.VerticalFovDeg > cam.HorizontalFovDeg,
            $"Vertical FOV {cam.VerticalFovDeg} should exceed horizontal {cam.HorizontalFovDeg} for portrait screen");
    }

    [Fact]
    public void CameraIntrinsics_Default_hasReasonableValues()
    {
        var d = CameraIntrinsics.Default;
        Assert.True(d.HorizontalFovDeg > 30 && d.HorizontalFovDeg < 120);
        Assert.True(d.WidthPx > 0);
        Assert.True(d.HeightPx > 0);
    }
}
