using AstroFinder.Engine.CaptureConfidence;

namespace AstroFinder.Domain.CaptureConfidence;

/// <summary>
/// Maintains a rolling session baseline and evaluates each new frame snapshot against it.
/// </summary>
public sealed class CaptureConfidenceMonitor
{
    private readonly Queue<FrameQualitySnapshot> _history = new();
    private readonly CaptureConfidenceEvaluator _evaluator;
    private readonly int _baselineFrameCount;
    private readonly int _historyLimit;

    public CaptureConfidenceMonitor(
        CaptureConfidenceEvaluator? evaluator = null,
        int baselineFrameCount = 5,
        int historyLimit = 12)
    {
        if (baselineFrameCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(baselineFrameCount), "At least two baseline frames are required.");
        }

        if (historyLimit < baselineFrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(historyLimit), "History limit must be at least as large as the baseline frame count.");
        }

        _evaluator = evaluator ?? new CaptureConfidenceEvaluator();
        _baselineFrameCount = baselineFrameCount;
        _historyLimit = historyLimit;
    }

    /// <summary>
    /// Number of snapshots currently retained in the rolling history.
    /// </summary>
    public int SnapshotCount => _history.Count;

    /// <summary>
    /// Adds a new snapshot and returns the current capture-confidence assessment.
    /// </summary>
    public CaptureConfidenceAssessment AddSnapshot(FrameQualitySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (_history.Count < _baselineFrameCount)
        {
            _history.Enqueue(snapshot);
            TrimHistory();

            return new CaptureConfidenceAssessment
            {
                State = CaptureConfidenceState.InsufficientData,
                Summary = $"Collecting baseline frames ({Math.Min(_history.Count, _baselineFrameCount)}/{_baselineFrameCount}).",
                Findings = Array.Empty<CaptureConfidenceFinding>(),
                HealthScore = 1.0,
                BaselineFrameCount = _history.Count,
            };
        }

        var baseline = CaptureConfidenceBaseline.Create(_history.TakeLast(_baselineFrameCount));
        var assessment = _evaluator.Evaluate(snapshot, baseline);

        _history.Enqueue(snapshot);
        TrimHistory();

        return assessment;
    }

    private void TrimHistory()
    {
        while (_history.Count > _historyLimit)
        {
            _history.Dequeue();
        }
    }
}
