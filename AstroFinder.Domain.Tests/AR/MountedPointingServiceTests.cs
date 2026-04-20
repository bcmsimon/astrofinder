using AstroFinder.Domain.AR.Calibration;
using AstroFinder.Domain.AR.MountedPointing;

namespace AstroFinder.Domain.Tests.AR;

public sealed class MountedPointingServiceTests
{
    private static readonly CameraIntrinsics TestIntrinsics =
        CameraIntrinsics.FromHorizontalFov(60.0, 1000, 1000);

    [Fact]
    public void Solve_SuccessfulSeededSolve_ReturnsReliableGuidance()
    {
        var seeded = BuildSeededFrame(new ArScreenPoint(500, 500));
        var image = BuildSyntheticGrayFrame(seeded, tx: 32.0, ty: -20.0);
        var service = new MountedPointingService();

        var result = service.Solve(new MountedPointingInput(
            seeded,
            image,
            MountedPointingBoresight.Zero));

        Assert.True(result.IsSolved);
        Assert.True(result.CanGuideReliably);
        Assert.True(result.Confidence >= 0.35);
        Assert.True(result.AngularErrorToTargetDeg > 0.1);
        Assert.Contains("Move", result.GuidanceText);
    }

    [Fact]
    public void Solve_InsufficientMatches_ReturnsConservativeFailure()
    {
        var seeded = BuildSeededFrame(new ArScreenPoint(500, 500));
        var image = BuildSparseGrayFrame((520, 490), (300, 220));
        var service = new MountedPointingService();

        var result = service.Solve(new MountedPointingInput(
            seeded,
            image,
            MountedPointingBoresight.Zero));

        Assert.False(result.IsSolved);
        Assert.False(result.CanGuideReliably);
        Assert.Equal(0.0, result.Confidence);
        Assert.Contains("Solve confidence is too low", result.GuidanceText);
        Assert.Contains(result.Warnings, warning => warning.Contains("Insufficient detected stars", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Solve_BoresightOffsets_AdjustMainCameraError()
    {
        var seeded = BuildSeededFrame(new ArScreenPoint(500, 500));
        var image = BuildSyntheticGrayFrame(seeded, tx: 30.0, ty: -16.0);

        var noOffset = new MountedPointingService().Solve(new MountedPointingInput(
            seeded,
            image,
            MountedPointingBoresight.Zero));

        var withOffset = new MountedPointingService().Solve(new MountedPointingInput(
            seeded,
            image,
            new MountedPointingBoresight(
                noOffset.MainCameraHorizontalErrorDeg,
                noOffset.MainCameraVerticalErrorDeg)));

        Assert.True(noOffset.CanGuideReliably);
        Assert.True(withOffset.CanGuideReliably);
        Assert.True(Math.Abs(withOffset.MainCameraHorizontalErrorDeg) < 0.25);
        Assert.True(Math.Abs(withOffset.MainCameraVerticalErrorDeg) < 0.25);
        Assert.True(withOffset.AngularErrorToTargetDeg < noOffset.AngularErrorToTargetDeg);
    }

    [Fact]
    public void Solve_GuidanceText_UsesPlainActionableLanguage()
    {
        var seeded = BuildSeededFrame(new ArScreenPoint(500, 500));
        var image = BuildSyntheticGrayFrame(seeded, tx: 64.0, ty: -10.0);
        var service = new MountedPointingService();

        var result = service.Solve(new MountedPointingInput(
            seeded,
            image,
            MountedPointingBoresight.Zero));

        Assert.True(result.CanGuideReliably);
        Assert.Equal("Move right and slightly up.", result.GuidanceText);
    }

    [Fact]
    public void Solve_TargetUnavailable_DegradesSafely()
    {
        var seeded = BuildSeededFrame(targetScreenPoint: null);
        var image = BuildSyntheticGrayFrame(seeded, tx: 20.0, ty: -12.0);
        var service = new MountedPointingService();

        var result = service.Solve(new MountedPointingInput(
            seeded,
            image,
            MountedPointingBoresight.Zero));

        Assert.False(result.IsSolved);
        Assert.False(result.CanGuideReliably);
        Assert.Contains("Solve confidence is too low", result.GuidanceText);
        Assert.Contains(result.Warnings, warning => warning.Contains("Target is not visible", StringComparison.OrdinalIgnoreCase));
    }

    private static ArOverlayFrame BuildSeededFrame(ArScreenPoint? targetScreenPoint)
    {
        var stars = new List<ProjectedSkyObject>
        {
            MakeStar("S1", 220, 220, ArOverlayRole.BackgroundStar, 2.0),
            MakeStar("S2", 780, 220, ArOverlayRole.BackgroundStar, 2.1),
            MakeStar("S3", 260, 760, ArOverlayRole.BackgroundStar, 2.2),
            MakeStar("S4", 740, 760, ArOverlayRole.BackgroundStar, 2.3),
            MakeStar("S5", 500, 300, ArOverlayRole.AsterismStar, 1.8),
            MakeStar("S6", 340, 520, ArOverlayRole.HopStar, 2.4),
            MakeStar("S7", 620, 540, ArOverlayRole.HopStar, 2.5),
        };

        var target = new ProjectedSkyObject(
            Id: "Target",
            Label: "Target",
            ScreenPoint: targetScreenPoint,
            InFrontOfCamera: true,
            WithinViewport: targetScreenPoint.HasValue,
            AltitudeDeg: 45.0,
            AzimuthDeg: 130.0,
            DirectionEnu: new Vec3(0.3, 0.7, 0.5),
            Role: ArOverlayRole.Target,
            Magnitude: 8.0);

        return new ArOverlayFrame(
            Target: target,
            AsterismStars: stars.Where(s => s.Role == ArOverlayRole.AsterismStar).ToList(),
            AsterismSegments: [],
            HopSteps: stars.Where(s => s.Role == ArOverlayRole.HopStar).ToList(),
            BackgroundStars: stars.Where(s => s.Role == ArOverlayRole.BackgroundStar).ToList(),
            Intrinsics: TestIntrinsics,
            OffscreenArrow: null,
            TargetAngularDistanceDegrees: 0.0,
            CenterReticleText: null,
            RouteSegments: []);
    }

    private static ProjectedSkyObject MakeStar(string id, float x, float y, ArOverlayRole role, double magnitude) =>
        new(
            Id: id,
            Label: id,
            ScreenPoint: new ArScreenPoint(x, y),
            InFrontOfCamera: true,
            WithinViewport: true,
            AltitudeDeg: 40.0,
            AzimuthDeg: 120.0,
            DirectionEnu: new Vec3(0.4, 0.6, 0.5),
            Role: role,
            Magnitude: magnitude);

    private static GrayImageFrame BuildSyntheticGrayFrame(ArOverlayFrame seededFrame, double tx, double ty)
    {
        var buffer = new byte[(int)(seededFrame.Intrinsics.WidthPx * seededFrame.Intrinsics.HeightPx)];
        var predicted = new List<ProjectedSkyObject>();
        predicted.AddRange(seededFrame.AsterismStars);
        predicted.AddRange(seededFrame.HopSteps);
        predicted.AddRange(seededFrame.BackgroundStars);
        predicted.Add(seededFrame.Target);

        foreach (var p in predicted)
        {
            if (!p.ScreenPoint.HasValue)
            {
                continue;
            }

            var x = (int)Math.Round(p.ScreenPoint.Value.XPx + tx);
            var y = (int)Math.Round(p.ScreenPoint.Value.YPx + ty);
            DrawBrightBlob(buffer, (int)seededFrame.Intrinsics.WidthPx, (int)seededFrame.Intrinsics.HeightPx, x, y);
        }

        return new GrayImageFrame((int)seededFrame.Intrinsics.WidthPx, (int)seededFrame.Intrinsics.HeightPx, buffer);
    }

    private static GrayImageFrame BuildSparseGrayFrame(params (int X, int Y)[] points)
    {
        const int size = 1000;
        var buffer = new byte[size * size];
        foreach (var p in points)
        {
            DrawBrightBlob(buffer, size, size, p.X, p.Y);
        }

        return new GrayImageFrame(size, size, buffer);
    }

    private static void DrawBrightBlob(byte[] buffer, int width, int height, int x, int y)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var xx = x + dx;
                var yy = y + dy;
                if (xx < 1 || yy < 1 || xx >= width - 1 || yy >= height - 1)
                {
                    continue;
                }

                var idx = (yy * width) + xx;
                buffer[idx] = (byte)((dx == 0 && dy == 0) ? 255 : 210);
            }
        }
    }
}
