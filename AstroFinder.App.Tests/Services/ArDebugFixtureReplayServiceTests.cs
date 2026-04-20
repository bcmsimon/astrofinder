using AstroFinder.App.Services;
using AstroFinder.Domain.AR;
using AstroFinder.Domain.AR.MountedPointing;

namespace AstroFinder.App.Tests.Services;

public sealed class ArDebugFixtureReplayServiceTests
{
    [Fact]
    public void TryBuildFrame_ReliableOffsetFixture_ProducesReliableSolve()
    {
        var replayService = CreateReplayService();
        replayService.SetSelectedFixture("reliable-offset");
        replayService.SetEnabled(true);

        var seeded = BuildSeededFrame(new ArScreenPoint(500, 500));

        var ok = replayService.TryBuildFrame(seeded, out var frame);
        var result = new MountedPointingService().Solve(new MountedPointingInput(
            seeded,
            frame,
            MountedPointingBoresight.Zero));

        replayService.SetEnabled(false);

        Assert.True(ok);
        Assert.True(result.CanGuideReliably);
        Assert.Contains("right", result.GuidanceText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("up", result.GuidanceText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryBuildFrame_SparseFixture_DegradesSafely()
    {
        var replayService = CreateReplayService();
        replayService.SetSelectedFixture("sparse-low-confidence");
        replayService.SetEnabled(true);

        var seeded = BuildSeededFrame(new ArScreenPoint(500, 500));

        var ok = replayService.TryBuildFrame(seeded, out var frame);
        var result = new MountedPointingService().Solve(new MountedPointingInput(
            seeded,
            frame,
            MountedPointingBoresight.Zero));

        replayService.SetEnabled(false);

        Assert.True(ok);
        Assert.False(result.CanGuideReliably);
        Assert.Contains("Solve confidence is too low", result.GuidanceText);
    }

    private static ArOverlayFrame BuildSeededFrame(ArScreenPoint? targetScreenPoint)
    {
        var intrinsics = CameraIntrinsics.FromHorizontalFov(60.0, 1000, 1000);
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
            Intrinsics: intrinsics,
            OffscreenArrow: null,
            TargetAngularDistanceDegrees: 0.0,
            CenterReticleText: null,
            RouteSegments: []);
    }

    private static ArDebugFixtureReplayService CreateReplayService()
    {
        var booleanStore = new Dictionary<string, bool>(StringComparer.Ordinal);
        var stringStore = new Dictionary<string, string>(StringComparer.Ordinal);

        return new ArDebugFixtureReplayService(
            getBoolean: (key, defaultValue) => booleanStore.TryGetValue(key, out var value) ? value : defaultValue,
            setBoolean: (key, value) => booleanStore[key] = value,
            getString: (key, defaultValue) => stringStore.TryGetValue(key, out var value) ? value : defaultValue,
            setString: (key, value) => stringStore[key] = value);
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
}