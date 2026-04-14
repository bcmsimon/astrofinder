namespace AstroFinder.Domain.AR.Calibration;

/// <summary>
/// Smooths calibration transforms over time and degrades gracefully when confidence drops.
/// </summary>
public sealed class TemporalCalibrationFilter
{
    private SimilarityTransform2D _current = SimilarityTransform2D.Identity;
    private double _confidence;
    private bool _initialized;

    public SimilarityTransform2D CurrentTransform => _current;
    public double CurrentConfidence => _confidence;

    public SimilarityTransform2D Update(SimilarityTransform2D measurement, double confidence)
    {
        confidence = Math.Clamp(confidence, 0.0, 1.0);

        if (!_initialized)
        {
            _current = measurement;
            _confidence = confidence;
            _initialized = true;
            return _current;
        }

        // Fast response for high confidence, slower for low confidence.
        var alpha = 0.10 + (0.45 * confidence);
        _current = _current.LerpTo(measurement, alpha);
        _confidence = (0.85 * _confidence) + (0.15 * confidence);

        return _current;
    }

    public void DecayWithoutMeasurement()
    {
        if (!_initialized)
        {
            return;
        }

        _confidence *= 0.85;

        // Pull back toward identity when correction confidence fades.
        var fallbackAlpha = 0.08;
        _current = _current.LerpTo(SimilarityTransform2D.Identity, fallbackAlpha);
    }

    public SimilarityTransform2D GetBlendedTransform()
    {
        var blend = Math.Clamp(_confidence, 0.0, 1.0);
        return SimilarityTransform2D.Identity.LerpTo(_current, blend);
    }
}
