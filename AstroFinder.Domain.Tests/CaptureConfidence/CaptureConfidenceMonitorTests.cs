using AstroFinder.Domain.CaptureConfidence;
using AstroFinder.Engine.CaptureConfidence;

namespace AstroFinder.Domain.Tests.CaptureConfidence;

public class CaptureConfidenceMonitorTests
{
    [Fact]
    public void AddSnapshot_WhileCollectingBaseline_ReturnsInsufficientData()
    {
        var monitor = new CaptureConfidenceMonitor(baselineFrameCount: 3, historyLimit: 6);

        var first = monitor.AddSnapshot(CreateSnapshot(0, starCount: 100, hfr: 2.0, background: 1200, fieldMatch: 0.95));
        var second = monitor.AddSnapshot(CreateSnapshot(1, starCount: 99, hfr: 2.0, background: 1210, fieldMatch: 0.95));
        var third = monitor.AddSnapshot(CreateSnapshot(2, starCount: 101, hfr: 1.95, background: 1195, fieldMatch: 0.96));

        Assert.Equal(CaptureConfidenceState.InsufficientData, first.State);
        Assert.Equal(CaptureConfidenceState.InsufficientData, second.State);
        Assert.Equal(CaptureConfidenceState.InsufficientData, third.State);
        Assert.Equal(3, monitor.SnapshotCount);
    }

    [Fact]
    public void AddSnapshot_AfterBaselineBuilt_EvaluatesAgainstPreviousFrames()
    {
        var monitor = new CaptureConfidenceMonitor(baselineFrameCount: 3, historyLimit: 6);

        monitor.AddSnapshot(CreateSnapshot(0, starCount: 100, hfr: 2.0, background: 1200, fieldMatch: 0.95));
        monitor.AddSnapshot(CreateSnapshot(1, starCount: 98, hfr: 2.05, background: 1210, fieldMatch: 0.94));
        monitor.AddSnapshot(CreateSnapshot(2, starCount: 101, hfr: 1.98, background: 1190, fieldMatch: 0.96));

        var result = monitor.AddSnapshot(CreateSnapshot(3, starCount: 48, hfr: 2.65, background: 1710, fieldMatch: 0.35, eccentricity: 0.40));

        Assert.Equal(CaptureConfidenceState.ProblemDetected, result.State);
        Assert.Contains(result.Findings, f => f.IssueKind == CaptureConfidenceIssueKind.FieldMatchConfidenceLow);
        Assert.Contains(result.Findings, f => f.IssueKind == CaptureConfidenceIssueKind.StarCountDrop);
    }

    private static FrameQualitySnapshot CreateSnapshot(
        int minuteOffset,
        int starCount,
        double hfr,
        double background,
        double fieldMatch,
        double eccentricity = 0.18) => new()
        {
            CapturedAtUtc = new DateTimeOffset(2026, 4, 11, 21, minuteOffset, 0, TimeSpan.Zero),
            StarCount = starCount,
            MedianHfrPixels = hfr,
            MedianEccentricity = eccentricity,
            BackgroundLevel = background,
            FieldMatchConfidence = fieldMatch,
        };
}
