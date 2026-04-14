namespace AstroFinder.Domain.AR;

/// <summary>
/// Guidance for rendering an off-screen arrow pointing toward the target.
/// </summary>
public readonly record struct ArrowGuidance(
    /// <summary>
    /// Arrow angle in screen space: 0 = right, +π/2 = down, -π/2 = up.
    /// </summary>
    float AngleRadians,
    /// <summary>
    /// Angular distance from the camera forward axis to the target, in degrees.
    /// </summary>
    double DistanceDeg,
    /// <summary>
    /// Horizontal hint: positive = target is to the right.
    /// </summary>
    double HorizontalHintDeg,
    /// <summary>
    /// Vertical hint: positive = target is above.
    /// </summary>
    double VerticalHintDeg);
