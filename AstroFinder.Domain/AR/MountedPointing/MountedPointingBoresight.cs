namespace AstroFinder.Domain.AR.MountedPointing;

/// <summary>
/// Fixed boresight offset between phone camera axis and tracker/main camera axis.
/// Positive yaw means the main camera points to the right of the phone camera.
/// Positive pitch means the main camera points above the phone camera.
/// </summary>
public sealed record MountedPointingBoresight(
    double YawOffsetDeg,
    double PitchOffsetDeg)
{
    public static MountedPointingBoresight Zero { get; } = new(0.0, 0.0);
}
