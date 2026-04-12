using AstroFinder.Engine.CaptureConfidence;

namespace AstroFinder.Engine.Tests.CaptureConfidence;

public class CaptureConfidenceEvaluatorTests
{
    private static readonly CaptureConfidenceBaseline StableBaseline = CaptureConfidenceBaseline.Create(
    [
        new FrameQualitySnapshot { CapturedAtUtc = new DateTimeOffset(2026, 4, 11, 20, 0, 0, TimeSpan.Zero), StarCount = 102, MedianHfrPixels = 2.0, MedianEccentricity = 0.18, BackgroundLevel = 1200, FieldMatchConfidence = 0.95 },
        new FrameQualitySnapshot { CapturedAtUtc = new DateTimeOffset(2026, 4, 11, 20, 1, 0, TimeSpan.Zero), StarCount = 98, MedianHfrPixels = 2.1, MedianEccentricity = 0.19, BackgroundLevel = 1190, FieldMatchConfidence = 0.94 },
        new FrameQualitySnapshot { CapturedAtUtc = new DateTimeOffset(2026, 4, 11, 20, 2, 0, TimeSpan.Zero), StarCount = 100, MedianHfrPixels = 2.0, MedianEccentricity = 0.18, BackgroundLevel = 1210, FieldMatchConfidence = 0.96 },
        new FrameQualitySnapshot { CapturedAtUtc = new DateTimeOffset(2026, 4, 11, 20, 3, 0, TimeSpan.Zero), StarCount = 101, MedianHfrPixels = 2.05, MedianEccentricity = 0.17, BackgroundLevel = 1185, FieldMatchConfidence = 0.95 },
        new FrameQualitySnapshot { CapturedAtUtc = new DateTimeOffset(2026, 4, 11, 20, 4, 0, TimeSpan.Zero), StarCount = 99, MedianHfrPixels = 1.95, MedianEccentricity = 0.18, BackgroundLevel = 1205, FieldMatchConfidence = 0.95 },
    ]);

    private readonly CaptureConfidenceEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_FrameNearBaseline_ReturnsStable()
    {
        var current = new FrameQualitySnapshot
        {
            CapturedAtUtc = new DateTimeOffset(2026, 4, 11, 20, 5, 0, TimeSpan.Zero),
            StarCount = 97,
            MedianHfrPixels = 2.08,
            MedianEccentricity = 0.19,
            BackgroundLevel = 1215,
            FieldMatchConfidence = 0.92,
        };

        var result = _evaluator.Evaluate(current, StableBaseline);

        Assert.Equal(CaptureConfidenceState.Stable, result.State);
        Assert.Empty(result.Findings);
        Assert.True(result.HealthScore > 0.85);
    }

    [Fact]
    public void Evaluate_SofterStarsAndBrighterBackground_ReturnsDegrading()
    {
        var current = new FrameQualitySnapshot
        {
            CapturedAtUtc = new DateTimeOffset(2026, 4, 11, 20, 6, 0, TimeSpan.Zero),
            StarCount = 84,
            MedianHfrPixels = 2.34,
            MedianEccentricity = 0.24,
            BackgroundLevel = 1525,
            FieldMatchConfidence = 0.82,
        };

        var result = _evaluator.Evaluate(current, StableBaseline);

        Assert.Equal(CaptureConfidenceState.Degrading, result.State);
        Assert.Contains(result.Findings, f => f.IssueKind == CaptureConfidenceIssueKind.StarSizeIncrease);
        Assert.Contains(result.Findings, f => f.IssueKind == CaptureConfidenceIssueKind.BackgroundRise);
    }

    [Fact]
    public void Evaluate_StarCountCollapseAndLowFieldMatch_ReturnsProblemDetected()
    {
        var current = new FrameQualitySnapshot
        {
            CapturedAtUtc = new DateTimeOffset(2026, 4, 11, 20, 7, 0, TimeSpan.Zero),
            StarCount = 45,
            MedianHfrPixels = 2.7,
            MedianEccentricity = 0.41,
            BackgroundLevel = 1700,
            FieldMatchConfidence = 0.32,
        };

        var result = _evaluator.Evaluate(current, StableBaseline);

        Assert.Equal(CaptureConfidenceState.ProblemDetected, result.State);
        Assert.Contains(result.Findings, f => f.IssueKind == CaptureConfidenceIssueKind.StarCountDrop && f.Severity == CaptureConfidenceSeverity.Critical);
        Assert.Contains(result.Findings, f => f.IssueKind == CaptureConfidenceIssueKind.FieldMatchConfidenceLow && f.Severity == CaptureConfidenceSeverity.Critical);
        Assert.True(result.HealthScore < 0.5);
    }
}
