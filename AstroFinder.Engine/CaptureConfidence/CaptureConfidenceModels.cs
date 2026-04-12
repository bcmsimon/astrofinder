namespace AstroFinder.Engine.CaptureConfidence;

/// <summary>
/// High-level trust state for the current capture conditions.
/// </summary>
public enum CaptureConfidenceState
{
    /// <summary>
    /// There is not yet enough data to compare the current frame against a stable baseline.
    /// </summary>
    InsufficientData,

    /// <summary>
    /// Current conditions are broadly consistent with the recent baseline.
    /// </summary>
    Stable,

    /// <summary>
    /// Conditions are starting to drift and should be watched.
    /// </summary>
    Degrading,

    /// <summary>
    /// One or more metrics strongly suggest that the current capture data is no longer trustworthy.
    /// </summary>
    ProblemDetected,
}

/// <summary>
/// Severity attached to an individual capture-confidence finding.
/// </summary>
public enum CaptureConfidenceSeverity
{
    Info,
    Warning,
    Critical,
}

/// <summary>
/// Machine-readable reason code for a capture-confidence finding.
/// </summary>
public enum CaptureConfidenceIssueKind
{
    StarCountDrop,
    StarSizeIncrease,
    EccentricityIncrease,
    BackgroundRise,
    FieldMatchConfidenceLow,
}

/// <summary>
/// Per-frame quality metrics produced by the shared analysis pipeline.
/// AstroFinder consumes these values without owning the image-analysis algorithms.
/// </summary>
public sealed record FrameQualitySnapshot
{
    /// <summary>
    /// UTC timestamp for the frame that produced these metrics.
    /// </summary>
    public required DateTimeOffset CapturedAtUtc { get; init; }

    /// <summary>
    /// Number of usable stars detected in the frame.
    /// </summary>
    public int? StarCount { get; init; }

    /// <summary>
    /// Median half-flux radius of the measured stars, in pixels.
    /// Lower values are usually better.
    /// </summary>
    public double? MedianHfrPixels { get; init; }

    /// <summary>
    /// Median full-width half-maximum of the measured stars, in pixels.
    /// Lower values are usually better.
    /// </summary>
    public double? MedianFwhmPixels { get; init; }

    /// <summary>
    /// Median star eccentricity for the frame (0 = round, larger = more elongated).
    /// </summary>
    public double? MedianEccentricity { get; init; }

    /// <summary>
    /// Relative background level for the frame. Units are source-specific but should be comparable within a session.
    /// </summary>
    public double? BackgroundLevel { get; init; }

    /// <summary>
    /// Optional 0-1 confidence that the current field still matches the expected target/anchor pattern.
    /// </summary>
    public double? FieldMatchConfidence { get; init; }

    /// <summary>
    /// Optional logical identifier of the source that produced the frame.
    /// </summary>
    public string? SourceId { get; init; }
}

/// <summary>
/// Rolling baseline derived from recent "known good" frames.
/// </summary>
public sealed record CaptureConfidenceBaseline
{
    /// <summary>
    /// Number of snapshots used to construct the baseline.
    /// </summary>
    public required int SampleCount { get; init; }

    public double? MedianStarCount { get; init; }
    public double? MedianHfrPixels { get; init; }
    public double? MedianFwhmPixels { get; init; }
    public double? MedianEccentricity { get; init; }
    public double? MedianBackgroundLevel { get; init; }

    /// <summary>
    /// Creates a baseline using the medians of the supplied snapshots.
    /// </summary>
    public static CaptureConfidenceBaseline Create(IEnumerable<FrameQualitySnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        var list = snapshots.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("At least one snapshot is required to build a baseline.", nameof(snapshots));
        }

        return new CaptureConfidenceBaseline
        {
            SampleCount = list.Count,
            MedianStarCount = Median(list.Select(s => s.StarCount)),
            MedianHfrPixels = Median(list.Select(s => s.MedianHfrPixels)),
            MedianFwhmPixels = Median(list.Select(s => s.MedianFwhmPixels)),
            MedianEccentricity = Median(list.Select(s => s.MedianEccentricity)),
            MedianBackgroundLevel = Median(list.Select(s => s.BackgroundLevel)),
        };
    }

    private static double? Median(IEnumerable<int?> values) =>
        Median(values.Where(v => v.HasValue).Select(v => (double)v!.Value));

    private static double? Median(IEnumerable<double?> values) =>
        Median(values.Where(v => v.HasValue).Select(v => v!.Value));

    private static double? Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(v => v).ToArray();
        if (ordered.Length == 0)
        {
            return null;
        }

        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2.0
            : ordered[middle];
    }
}

/// <summary>
/// One concrete reason why AstroFinder believes the current data is stable, degrading, or in trouble.
/// </summary>
public sealed record CaptureConfidenceFinding(
    CaptureConfidenceIssueKind IssueKind,
    CaptureConfidenceSeverity Severity,
    string Message);

/// <summary>
/// Result of evaluating the current frame against the recent session baseline.
/// </summary>
public sealed record CaptureConfidenceAssessment
{
    public required CaptureConfidenceState State { get; init; }
    public required string Summary { get; init; }
    public required IReadOnlyList<CaptureConfidenceFinding> Findings { get; init; }

    /// <summary>
    /// Simple 0-1 health score for UI badges or progress bars. Higher is better.
    /// </summary>
    public double HealthScore { get; init; }

    /// <summary>
    /// Number of frames used for the comparison baseline.
    /// </summary>
    public int BaselineFrameCount { get; init; }
}
