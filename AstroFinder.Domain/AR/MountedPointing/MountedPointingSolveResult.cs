using AstroFinder.Domain.AR.Calibration;

namespace AstroFinder.Domain.AR.MountedPointing;

/// <summary>
/// Mounted-pointing solve output and guidance payload.
/// </summary>
public sealed record MountedPointingSolveResult(
    bool IsSolved,
    bool CanGuideReliably,
    double Confidence,
    double MainCameraHorizontalErrorDeg,
    double MainCameraVerticalErrorDeg,
    double AngularErrorToTargetDeg,
    string GuidanceText,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Warnings,
    ArOverlayFrame CorrectedFrame,
    CalibrationResult? Calibration);
