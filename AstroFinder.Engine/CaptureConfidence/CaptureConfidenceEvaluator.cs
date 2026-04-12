namespace AstroFinder.Engine.CaptureConfidence;

/// <summary>
/// Deterministic heuristic evaluator that classifies whether the current run of frames
/// still looks trustworthy for capture and framing purposes.
/// </summary>
public sealed class CaptureConfidenceEvaluator
{
    /// <summary>
    /// Evaluates the <paramref name="current"/> frame metrics against the supplied <paramref name="baseline"/>.
    /// </summary>
    public CaptureConfidenceAssessment Evaluate(
        FrameQualitySnapshot current,
        CaptureConfidenceBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(baseline);

        if (baseline.SampleCount <= 0)
        {
            throw new ArgumentException("Baseline must contain at least one frame.", nameof(baseline));
        }

        if (!HasComparableData(current, baseline))
        {
            return new CaptureConfidenceAssessment
            {
                State = CaptureConfidenceState.InsufficientData,
                Summary = "Waiting for enough comparable frame metrics.",
                Findings = Array.Empty<CaptureConfidenceFinding>(),
                HealthScore = 1.0,
                BaselineFrameCount = baseline.SampleCount,
            };
        }

        var findings = new List<CaptureConfidenceFinding>();
        var healthScore = 1.0;

        EvaluateStarCount(current, baseline, findings, ref healthScore);
        EvaluateStarSize(current, baseline, findings, ref healthScore);
        EvaluateEccentricity(current, baseline, findings, ref healthScore);
        EvaluateBackground(current, baseline, findings, ref healthScore);
        EvaluateFieldMatch(current, findings, ref healthScore);

        var state = findings.Count == 0
            ? CaptureConfidenceState.Stable
            : findings.Any(f => f.Severity == CaptureConfidenceSeverity.Critical)
                ? CaptureConfidenceState.ProblemDetected
                : CaptureConfidenceState.Degrading;

        return new CaptureConfidenceAssessment
        {
            State = state,
            Summary = BuildSummary(state, findings),
            Findings = findings,
            HealthScore = Math.Clamp(healthScore, 0.0, 1.0),
            BaselineFrameCount = baseline.SampleCount,
        };
    }

    private static bool HasComparableData(FrameQualitySnapshot current, CaptureConfidenceBaseline baseline) =>
        (current.StarCount.HasValue && baseline.MedianStarCount.HasValue)
        || (current.MedianHfrPixels.HasValue && baseline.MedianHfrPixels.HasValue)
        || (current.MedianFwhmPixels.HasValue && baseline.MedianFwhmPixels.HasValue)
        || (current.MedianEccentricity.HasValue && baseline.MedianEccentricity.HasValue)
        || (current.BackgroundLevel.HasValue && baseline.MedianBackgroundLevel.HasValue)
        || current.FieldMatchConfidence.HasValue;

    private static void EvaluateStarCount(
        FrameQualitySnapshot current,
        CaptureConfidenceBaseline baseline,
        ICollection<CaptureConfidenceFinding> findings,
        ref double healthScore)
    {
        if (!current.StarCount.HasValue || !baseline.MedianStarCount.HasValue || baseline.MedianStarCount.Value <= 0)
        {
            return;
        }

        var ratio = current.StarCount.Value / baseline.MedianStarCount.Value;
        if (ratio <= 0.55)
        {
            AddFinding(findings, ref healthScore,
                CaptureConfidenceIssueKind.StarCountDrop,
                CaptureConfidenceSeverity.Critical,
                "Much fewer stars detected — cloud, haze, or framing loss is likely.",
                0.34);
        }
        else if (ratio <= 0.80)
        {
            AddFinding(findings, ref healthScore,
                CaptureConfidenceIssueKind.StarCountDrop,
                CaptureConfidenceSeverity.Warning,
                "Fewer stars detected — monitor for cloud or framing drift.",
                0.15);
        }
    }

    private static void EvaluateStarSize(
        FrameQualitySnapshot current,
        CaptureConfidenceBaseline baseline,
        ICollection<CaptureConfidenceFinding> findings,
        ref double healthScore)
    {
        var currentSize = current.MedianHfrPixels ?? current.MedianFwhmPixels;
        var baselineSize = baseline.MedianHfrPixels ?? baseline.MedianFwhmPixels;
        if (!currentSize.HasValue || !baselineSize.HasValue || baselineSize.Value <= 0)
        {
            return;
        }

        var ratio = currentSize.Value / baselineSize.Value;
        if (ratio >= 1.35)
        {
            AddFinding(findings, ref healthScore,
                CaptureConfidenceIssueKind.StarSizeIncrease,
                CaptureConfidenceSeverity.Critical,
                "Stars are much larger than the recent baseline — focus or seeing has likely worsened.",
                0.28);
        }
        else if (ratio >= 1.15)
        {
            AddFinding(findings, ref healthScore,
                CaptureConfidenceIssueKind.StarSizeIncrease,
                CaptureConfidenceSeverity.Warning,
                "Stars are getting softer — check focus stability.",
                0.12);
        }
    }

    private static void EvaluateEccentricity(
        FrameQualitySnapshot current,
        CaptureConfidenceBaseline baseline,
        ICollection<CaptureConfidenceFinding> findings,
        ref double healthScore)
    {
        if (!current.MedianEccentricity.HasValue || !baseline.MedianEccentricity.HasValue)
        {
            return;
        }

        var delta = current.MedianEccentricity.Value - baseline.MedianEccentricity.Value;
        if (delta >= 0.18)
        {
            AddFinding(findings, ref healthScore,
                CaptureConfidenceIssueKind.EccentricityIncrease,
                CaptureConfidenceSeverity.Critical,
                "Star elongation has increased sharply — tracking or stability may be slipping.",
                0.22);
        }
        else if (delta >= 0.08)
        {
            AddFinding(findings, ref healthScore,
                CaptureConfidenceIssueKind.EccentricityIncrease,
                CaptureConfidenceSeverity.Warning,
                "Stars look less round than before — monitor tracking quality.",
                0.10);
        }
    }

    private static void EvaluateBackground(
        FrameQualitySnapshot current,
        CaptureConfidenceBaseline baseline,
        ICollection<CaptureConfidenceFinding> findings,
        ref double healthScore)
    {
        if (!current.BackgroundLevel.HasValue || !baseline.MedianBackgroundLevel.HasValue || baseline.MedianBackgroundLevel.Value <= 0)
        {
            return;
        }

        var ratio = current.BackgroundLevel.Value / baseline.MedianBackgroundLevel.Value;
        if (ratio >= 1.60)
        {
            AddFinding(findings, ref healthScore,
                CaptureConfidenceIssueKind.BackgroundRise,
                CaptureConfidenceSeverity.Critical,
                "Background brightness has risen sharply — haze, moonlight, or dawn may be intruding.",
                0.24);
        }
        else if (ratio >= 1.25)
        {
            AddFinding(findings, ref healthScore,
                CaptureConfidenceIssueKind.BackgroundRise,
                CaptureConfidenceSeverity.Warning,
                "Background brightness is rising — watch the sky conditions.",
                0.10);
        }
    }

    private static void EvaluateFieldMatch(
        FrameQualitySnapshot current,
        ICollection<CaptureConfidenceFinding> findings,
        ref double healthScore)
    {
        if (!current.FieldMatchConfidence.HasValue)
        {
            return;
        }

        var confidence = current.FieldMatchConfidence.Value;
        if (confidence <= 0.45)
        {
            AddFinding(findings, ref healthScore,
                CaptureConfidenceIssueKind.FieldMatchConfidenceLow,
                CaptureConfidenceSeverity.Critical,
                "Field match is low — the target may no longer be framed as expected.",
                0.30);
        }
        else if (confidence <= 0.70)
        {
            AddFinding(findings, ref healthScore,
                CaptureConfidenceIssueKind.FieldMatchConfidenceLow,
                CaptureConfidenceSeverity.Warning,
                "Field match confidence is slipping — framing drift is possible.",
                0.12);
        }
    }

    private static void AddFinding(
        ICollection<CaptureConfidenceFinding> findings,
        ref double healthScore,
        CaptureConfidenceIssueKind issueKind,
        CaptureConfidenceSeverity severity,
        string message,
        double scorePenalty)
    {
        findings.Add(new CaptureConfidenceFinding(issueKind, severity, message));
        healthScore -= scorePenalty;
    }

    private static string BuildSummary(
        CaptureConfidenceState state,
        IReadOnlyCollection<CaptureConfidenceFinding> findings) => state switch
    {
        CaptureConfidenceState.Stable => "Capture conditions look stable.",
        CaptureConfidenceState.Degrading when findings.Any(f => f.IssueKind == CaptureConfidenceIssueKind.FieldMatchConfidenceLow)
            => "Capture confidence is slipping — framing may be drifting.",
        CaptureConfidenceState.Degrading => "Capture quality is starting to slip.",
        CaptureConfidenceState.ProblemDetected when findings.Any(f => f.IssueKind == CaptureConfidenceIssueKind.FieldMatchConfidenceLow)
            => "Problem detected — the target may no longer be framed correctly.",
        CaptureConfidenceState.ProblemDetected => "Problem detected — current frames may no longer be trustworthy.",
        _ => "Waiting for enough comparable frame metrics.",
    };
}
