using AstroFinder.Domain.AR;
using AstroFinder.Domain.AR.Calibration;

namespace AstroFinder.App.Services;

public sealed record ArDebugFixtureOption(string Id, string DisplayName, string Description);

public sealed class ArDebugFixtureReplayService
{
    private const string EnabledKey = "astrofinder.debug-ar-fixture.enabled";
    private const string FixtureKey = "astrofinder.debug-ar-fixture.selected";
    private const string ReliableOffsetFixtureId = "reliable-offset";
    private const string SparseFixtureId = "sparse-low-confidence";

    private static readonly IReadOnlyList<ArDebugFixtureOption> FixtureOptions =
    [
        new ArDebugFixtureOption(
            ReliableOffsetFixtureId,
            "Reliable offset",
            "Feeds a stable shifted star field so the AR solver should return directional guidance."),
        new ArDebugFixtureOption(
            SparseFixtureId,
            "Sparse low-confidence",
            "Feeds too few stars so the AR solver should degrade safely with a low-confidence warning.")
    ];

    private readonly Func<string, bool, bool> _getBoolean;
    private readonly Action<string, bool> _setBoolean;
    private readonly Func<string, string, string> _getString;
    private readonly Action<string, string> _setString;

    public ArDebugFixtureReplayService(
        Func<string, bool, bool>? getBoolean = null,
        Action<string, bool>? setBoolean = null,
        Func<string, string, string>? getString = null,
        Action<string, string>? setString = null)
    {
        _getBoolean = getBoolean ?? ((key, defaultValue) => Preferences.Default.Get(key, defaultValue));
        _setBoolean = setBoolean ?? ((key, value) => Preferences.Default.Set(key, value));
        _getString = getString ?? ((key, defaultValue) => Preferences.Default.Get(key, defaultValue));
        _setString = setString ?? ((key, value) => Preferences.Default.Set(key, value));
    }

    public bool IsAvailable
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    public bool IsEnabled => IsAvailable && _getBoolean(EnabledKey, false);

    public string SelectedFixtureId
    {
        get
        {
            var stored = _getString(FixtureKey, ReliableOffsetFixtureId);
            return FixtureOptions.Any(option => option.Id == stored)
                ? stored
                : ReliableOffsetFixtureId;
        }
    }

    public string SelectedFixtureDisplayName =>
        FixtureOptions.First(option => option.Id == SelectedFixtureId).DisplayName;

    public IReadOnlyList<ArDebugFixtureOption> GetAvailableFixtures() => FixtureOptions;

    public void SetEnabled(bool enabled)
    {
        _setBoolean(EnabledKey, IsAvailable && enabled);
    }

    public void SetSelectedFixture(string fixtureId)
    {
        var normalized = FixtureOptions.Any(option => option.Id == fixtureId)
            ? fixtureId
            : ReliableOffsetFixtureId;

        _setString(FixtureKey, normalized);
    }

    public bool TryBuildFrame(ArOverlayFrame seededFrame, out GrayImageFrame frame)
    {
        ArgumentNullException.ThrowIfNull(seededFrame);

        if (!IsEnabled)
        {
            frame = new GrayImageFrame(0, 0, []);
            return false;
        }

        frame = SelectedFixtureId switch
        {
            SparseFixtureId => BuildSparseFrame(seededFrame),
            _ => BuildReliableOffsetFrame(seededFrame)
        };

        return frame.Width > 0 && frame.Height > 0;
    }

    private static GrayImageFrame BuildReliableOffsetFrame(ArOverlayFrame seededFrame)
    {
        var width = (int)Math.Round(seededFrame.Intrinsics.WidthPx);
        var height = (int)Math.Round(seededFrame.Intrinsics.HeightPx);
        if (width <= 0 || height <= 0)
        {
            return new GrayImageFrame(0, 0, []);
        }

        var buffer = new byte[width * height];
        var tx = Math.Clamp(width * 0.04, 6.0, 48.0);
        var ty = -Math.Clamp(height * 0.018, 4.0, 24.0);

        foreach (var point in EnumerateProjectedPoints(seededFrame))
        {
            if (!point.ScreenPoint.HasValue)
            {
                continue;
            }

            var x = (int)Math.Round(point.ScreenPoint.Value.XPx + tx);
            var y = (int)Math.Round(point.ScreenPoint.Value.YPx + ty);
            DrawBrightBlob(buffer, width, height, x, y);
        }

        return new GrayImageFrame(width, height, buffer);
    }

    private static GrayImageFrame BuildSparseFrame(ArOverlayFrame seededFrame)
    {
        var width = (int)Math.Round(seededFrame.Intrinsics.WidthPx);
        var height = (int)Math.Round(seededFrame.Intrinsics.HeightPx);
        if (width <= 0 || height <= 0)
        {
            return new GrayImageFrame(0, 0, []);
        }

        var buffer = new byte[width * height];
        var projected = EnumerateProjectedPoints(seededFrame)
            .Where(point => point.ScreenPoint.HasValue)
            .Take(2)
            .ToList();

        foreach (var point in projected)
        {
            var x = (int)Math.Round(point.ScreenPoint!.Value.XPx + 10.0);
            var y = (int)Math.Round(point.ScreenPoint.Value.YPx - 6.0);
            DrawBrightBlob(buffer, width, height, x, y);
        }

        return new GrayImageFrame(width, height, buffer);
    }

    private static IEnumerable<ProjectedSkyObject> EnumerateProjectedPoints(ArOverlayFrame seededFrame)
    {
        foreach (var star in seededFrame.AsterismStars)
        {
            yield return star;
        }

        foreach (var star in seededFrame.HopSteps)
        {
            yield return star;
        }

        foreach (var star in seededFrame.BackgroundStars.Take(24))
        {
            yield return star;
        }

        yield return seededFrame.Target;
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

                buffer[(yy * width) + xx] = (byte)((dx == 0 && dy == 0) ? 255 : 210);
            }
        }
    }
}