using AstroFinder.Domain.AR.Calibration;

namespace AstroFinder.Domain.AR.MountedPointing;

/// <summary>
/// Inputs for a single mounted-pointing solve step.
/// </summary>
public sealed record MountedPointingInput(
    ArOverlayFrame SeededFrame,
    GrayImageFrame CameraFrame,
    MountedPointingBoresight Boresight);
