using AstroFinder.Domain.AR.Calibration;

namespace AstroFinder.Domain.AR.MountedPointing;

/// <summary>
/// Seeded mounted-phone solve service that estimates pointing correction
/// and emits conservative target adjustment guidance.
/// </summary>
public sealed class MountedPointingService
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    private readonly StarDetector _starDetector;
    private readonly ArOverlayCalibrationPipeline _calibrationPipeline;
    private readonly MountedPointingSolveOptions _options;

    public MountedPointingService(
        StarDetector? starDetector = null,
        ArOverlayCalibrationPipeline? calibrationPipeline = null,
        MountedPointingSolveOptions? options = null)
    {
        _starDetector = starDetector ?? new StarDetector();
        _calibrationPipeline = calibrationPipeline ?? new ArOverlayCalibrationPipeline();
        _options = options ?? new MountedPointingSolveOptions();
    }

    public MountedPointingSolveResult Solve(MountedPointingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var warnings = new List<string>();
        var diagnostics = new List<string>();

        var detectedStars = _starDetector.Detect(input.CameraFrame);
        diagnostics.Add($"detected={detectedStars.Count}");

        if (detectedStars.Count < _options.MinDetectedStars)
        {
            warnings.Add("Insufficient detected stars for a reliable solve.");
            return BuildFailedResult(input, detectedStars.Count, warnings, diagnostics);
        }

        var calibration = _calibrationPipeline.Calibrate(
            input.SeededFrame,
            detectedStars,
            out var correctedFrame);

        diagnostics.Add($"matched={calibration.MatchedCount}");
        diagnostics.Add($"inliers={calibration.InlierCount}");
        diagnostics.Add($"residualPx={calibration.MeanResidualPx:F2}");
        diagnostics.Add($"spread={calibration.SpreadScore:F2}");
        diagnostics.Add($"confidence={calibration.Confidence:F2}");

        var canGuideReliably = calibration.UsedCorrection
            && calibration.MatchedCount >= _options.MinMatchedStars
            && calibration.InlierCount >= _options.MinInlierStars
            && calibration.Confidence >= _options.MinConfidence
            && calibration.MeanResidualPx <= _options.MaxMeanResidualPx
            && calibration.SpreadScore >= _options.MinSpreadScore;

        if (!calibration.UsedCorrection)
        {
            warnings.Add("Calibration correction was not accepted.");
        }

        if (calibration.MatchedCount < _options.MinMatchedStars)
        {
            warnings.Add("Too few star matches for reliable correction.");
        }

        if (calibration.InlierCount < _options.MinInlierStars)
        {
            warnings.Add("Too few inlier star matches for reliable correction.");
        }

        if (calibration.Confidence < _options.MinConfidence)
        {
            warnings.Add("Solve confidence below reliability threshold.");
        }

        if (calibration.MeanResidualPx > _options.MaxMeanResidualPx)
        {
            warnings.Add("Solve residual too high for reliable guidance.");
        }

        if (calibration.SpreadScore < _options.MinSpreadScore)
        {
            warnings.Add("Star match spread is too narrow for stable correction.");
        }

        var errorSourcePoint = correctedFrame.Target.ScreenPoint ?? input.SeededFrame.Target.ScreenPoint;
        if (!errorSourcePoint.HasValue)
        {
            canGuideReliably = false;
            warnings.Add("Target is not visible in predicted frame.");
        }

        var (phoneHorizontalErrorDeg, phoneVerticalErrorDeg) = errorSourcePoint.HasValue
            ? ToAngularError(errorSourcePoint.Value, correctedFrame.Intrinsics)
            : (0.0, 0.0);

        var mainHorizontalErrorDeg = phoneHorizontalErrorDeg - input.Boresight.YawOffsetDeg;
        var mainVerticalErrorDeg = phoneVerticalErrorDeg - input.Boresight.PitchOffsetDeg;
        var angularErrorDeg = AngularError(mainHorizontalErrorDeg, mainVerticalErrorDeg);

        if (angularErrorDeg <= _options.OnTargetThresholdDeg)
        {
            diagnostics.Add("status=near-target");
        }

        var guidance = MountedPointingGuidancePolicy.Build(
            canGuideReliably,
            mainHorizontalErrorDeg,
            mainVerticalErrorDeg,
            angularErrorDeg,
            _options);

        return new MountedPointingSolveResult(
            IsSolved: canGuideReliably,
            CanGuideReliably: canGuideReliably,
            Confidence: calibration.Confidence,
            MainCameraHorizontalErrorDeg: mainHorizontalErrorDeg,
            MainCameraVerticalErrorDeg: mainVerticalErrorDeg,
            AngularErrorToTargetDeg: angularErrorDeg,
            GuidanceText: guidance,
            Diagnostics: diagnostics,
            Warnings: warnings,
            CorrectedFrame: correctedFrame,
            Calibration: calibration);
    }

    private MountedPointingSolveResult BuildFailedResult(
        MountedPointingInput input,
        int detectedCount,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> diagnostics)
    {
        var targetPoint = input.SeededFrame.Target.ScreenPoint;
        var (phoneHorizontalErrorDeg, phoneVerticalErrorDeg) = targetPoint.HasValue
            ? ToAngularError(targetPoint.Value, input.SeededFrame.Intrinsics)
            : (0.0, 0.0);

        var mainHorizontalErrorDeg = phoneHorizontalErrorDeg - input.Boresight.YawOffsetDeg;
        var mainVerticalErrorDeg = phoneVerticalErrorDeg - input.Boresight.PitchOffsetDeg;
        var angularErrorDeg = AngularError(mainHorizontalErrorDeg, mainVerticalErrorDeg);

        var allDiagnostics = diagnostics.Concat([$"detected={detectedCount}"]).ToList();

        return new MountedPointingSolveResult(
            IsSolved: false,
            CanGuideReliably: false,
            Confidence: 0.0,
            MainCameraHorizontalErrorDeg: mainHorizontalErrorDeg,
            MainCameraVerticalErrorDeg: mainVerticalErrorDeg,
            AngularErrorToTargetDeg: angularErrorDeg,
            GuidanceText: MountedPointingGuidancePolicy.Build(
                false,
                mainHorizontalErrorDeg,
                mainVerticalErrorDeg,
                angularErrorDeg,
                _options),
            Diagnostics: allDiagnostics,
            Warnings: warnings,
            CorrectedFrame: input.SeededFrame,
            Calibration: null);
    }

    private static (double HorizontalErrorDeg, double VerticalErrorDeg) ToAngularError(
        ArScreenPoint point,
        CameraIntrinsics intrinsics)
    {
        var xNdc = ((2.0 * point.XPx) / intrinsics.WidthPx) - 1.0;
        var yNdc = 1.0 - ((2.0 * point.YPx) / intrinsics.HeightPx);

        var tanHalfH = Math.Tan(0.5 * intrinsics.HorizontalFovDeg * DegToRad);
        var tanHalfV = Math.Tan(0.5 * intrinsics.VerticalFovDeg * DegToRad);

        var horizontalDeg = Math.Atan(xNdc * tanHalfH) * RadToDeg;
        var verticalDeg = Math.Atan(yNdc * tanHalfV) * RadToDeg;
        return (horizontalDeg, verticalDeg);
    }

    private static double AngularError(double horizontalDeg, double verticalDeg)
    {
        var tanX = Math.Tan(horizontalDeg * DegToRad);
        var tanY = Math.Tan(verticalDeg * DegToRad);
        return Math.Atan(Math.Sqrt((tanX * tanX) + (tanY * tanY))) * RadToDeg;
    }
}
