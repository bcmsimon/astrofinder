using AstroFinder.App.Services;
using AstroFinder.App.ViewModels;
using AstroFinder.Domain.AR;
using AstroFinder.Domain.AR.Calibration;
using AstroFinder.Domain.AR.MountedPointing;
using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.App.Views;

/// <summary>
/// Displays a live sky overlay driven by ARCore camera pose tracking.
/// The hop map is rendered to a bitmap and placed as a textured quad
/// in ARCore world space, so ARCore's own view/projection matrices
/// handle all the world-lock tracking.
/// </summary>
public partial class ArStarHopPage : ContentPage
{
    private readonly StarMapData _data;
    private readonly IDeviceOrientationService _orientationService;
    private readonly ObserverOrientationService _observerOrientationService;
    private readonly ArProjectionService _projectionService = new();
    private readonly MountedPointingService _mountedPointingService = new();
    private readonly ArOverlayDrawable _overlayDrawable = new();
    private readonly MountedPointingBoresight _boresight = MountedPointingBoresight.Zero;

    private ArRouteInput? _routeInput;
    private CameraIntrinsics _intrinsics = CameraIntrinsics.Default;
    private bool _calibrationHintShown;
    private bool _isNightMode;
    private bool _mapOverlaySet;
    private DateTimeOffset _lastLowConfidenceBannerAt;
#if ANDROID
    private float[]? _lastModelMatrix;
#endif

    public ArStarHopPage(
        StarMapData data,
        IDeviceOrientationService deviceOrientationService,
        ObserverOrientationService observerOrientationService)
    {
        _data = data;
        _orientationService = deviceOrientationService;
        _observerOrientationService = observerOrientationService;

        InitializeComponent();

        OverlayView.Drawable = _overlayDrawable;
        _overlayDrawable.SetNightMode(false);

        ArCamera.StatusMessage += OnArStatusMessage;
    }

    // -------------------------------------------------------------------------
    // Page lifecycle
    // -------------------------------------------------------------------------

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        DiagHudLabel.IsVisible = Preferences.Default.Get(SettingsPageViewModel.ShowArDebugHudKey, false);

        await ResolveRouteInputAsync();

        ArCamera.ResumeSession();

        var sensorsStarted = await _orientationService.StartAsync();
        if (!sensorsStarted)
        {
            ShowStatus("AR tracking not available on this device.");
            return;
        }

        _orientationService.PoseChanged -= OnPoseChanged;
        _orientationService.PoseChanged += OnPoseChanged;

        ShowCalibrationHint();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _orientationService.PoseChanged -= OnPoseChanged;
        _orientationService.Stop();
        ArCamera.PauseSession();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width > 0 && height > 0)
        {
            _intrinsics = CameraIntrinsics.FromHorizontalFov(72.0, (float)width, (float)height);
        }
    }

    // -------------------------------------------------------------------------
    // Sensor event
    // -------------------------------------------------------------------------

    private string GetFullDiagnostics()
    {
#if ANDROID
        if (_orientationService is AstroFinder.App.Platforms.Android.Ar.ArCorePoseProvider provider)
        {
            var d = provider.GetDiagnostics();
            var pose = d.Raw4x4;
            // ARCore pose = camera-to-world. translation is at [12], [13], [14] in column-major
            var cpX = pose != null && pose.Length >= 16 ? pose[12] : 0f;
            var cpY = pose != null && pose.Length >= 16 ? pose[13] : 0f;
            var cpZ = pose != null && pose.Length >= 16 ? pose[14] : 0f;
            
            // Map matrix translation
            var mpX = _lastModelMatrix != null && _lastModelMatrix.Length >= 16 ? _lastModelMatrix[12] : 0f;
            var mpY = _lastModelMatrix != null && _lastModelMatrix.Length >= 16 ? _lastModelMatrix[13] : 0f;
            var mpZ = _lastModelMatrix != null && _lastModelMatrix.Length >= 16 ? _lastModelMatrix[14] : 0f;

            // Map matrix forward (from column 2)
            var mfX = _lastModelMatrix != null && _lastModelMatrix.Length >= 16 ? _lastModelMatrix[8] : 0f;
            var mfY = _lastModelMatrix != null && _lastModelMatrix.Length >= 16 ? _lastModelMatrix[9] : 0f;
            var mfZ = _lastModelMatrix != null && _lastModelMatrix.Length >= 16 ? _lastModelMatrix[10] : 0f;

            return $"Hdg: {d.HeadingDeg:F1}° (Locked: {provider.LockedHeadingRad.HasValue})\n" +
                   $"DispPitch: {d.ArPitchDeg:F1}° SensPitch: {d.SensorPitchDeg:F1}°\n" +
                   $"CamPos: ({cpX:F2}, {cpY:F2}, {cpZ:F2})\n" +
                   $"MapPos: ({mpX:F2}, {mpY:F2}, {mpZ:F2})\n" +
                   $"MapFwd: ({mfX:F2}, {mfY:F2}, {mfZ:F2})\n" +
                   $"Map overlay: {(_mapOverlaySet ? "ON" : "waiting")}";
        }
#endif
        return "";
    }

    private void OnPoseChanged(object? sender, PoseMatrix pose)
    {
        if (_routeInput is null) return;

        // --- Set up the bitmap map overlay on first tracking frame ---
        TrySetMapOverlay();

        // --- Lightweight diagnostics only (no per-frame projection) ---
        var liveInput = _routeInput with { ObservationTime = DateTimeOffset.Now };
        var frame = _projectionService.Project(liveInput, pose, _intrinsics);
        var guidanceResult = SolveMountedPointing(frame);
        var displayFrame = guidanceResult?.CorrectedFrame ?? frame;

        HeadingLabel.Text = guidanceResult is null
            ? $"{frame.TargetAngularDistanceDegrees:F0}\u00B0 to target"
            : guidanceResult.CanGuideReliably
                ? $"{guidanceResult.GuidanceText} ({guidanceResult.AngularErrorToTargetDeg:F1}\u00B0)"
                : guidanceResult.GuidanceText;

        if (DiagHudLabel.IsVisible)
        {
            var calibrationDiag = guidanceResult?.Calibration is null
                ? string.Empty
                : $"\nSolve conf: {guidanceResult.Confidence:F2}  match/inlier: {guidanceResult.Calibration.MatchedCount}/{guidanceResult.Calibration.InlierCount}";
            DiagHudLabel.Text = GetFullDiagnostics() + calibrationDiag;
        }

        if (guidanceResult is not null
            && !guidanceResult.CanGuideReliably
            && DateTimeOffset.UtcNow - _lastLowConfidenceBannerAt > TimeSpan.FromSeconds(3))
        {
            ShowStatus(guidanceResult.GuidanceText);
            _lastLowConfidenceBannerAt = DateTimeOffset.UtcNow;
        }

        // Still update the old overlay for off-screen arrow guidance only.
        _overlayDrawable.Update(displayFrame);
        OverlayView.Invalidate();
    }

    private MountedPointingSolveResult? SolveMountedPointing(ArOverlayFrame seededFrame)
    {
        if (!ArCamera.TryGetLatestGrayFrame(out var cameraFrame))
        {
            return null;
        }

        return _mountedPointingService.Solve(new MountedPointingInput(
            seededFrame,
            cameraFrame,
            _boresight));
    }

    /// <summary>
    /// Renders the hop map bitmap and places it in ARCore world space.
    /// Called once when heading is locked and location is available.
    /// </summary>
    private void TrySetMapOverlay()
    {
        if (_mapOverlaySet || _routeInput is null) return;

#if ANDROID
        if (_orientationService is not AstroFinder.App.Platforms.Android.Ar.ArCorePoseProvider provider)
            return;

        var headingRad = provider.LockedHeadingRad;
        if (!headingRad.HasValue) return; // not tracking yet

        // Render the hop map to a bitmap.
        var result = Platforms.Android.Ar.MapBitmapRenderer.Render(_data);

        // Compute the model matrix placing the quad at the map center's sky position.
        var modelMatrix = Platforms.Android.Ar.MapPlacement.ComputeModelMatrix(
            result.CenterRaHours,
            result.CenterDecDeg,
            result.AngularWidthDeg,
            _routeInput.ObserverLatitudeDegrees,
            _routeInput.ObserverLongitudeDegrees,
            headingRad.Value,
            DateTimeOffset.UtcNow);

        if (modelMatrix == null)
        {
            ShowStatus("Map center is below the horizon.");
            return;
        }

        _lastModelMatrix = modelMatrix;

        // Send the bitmap + placement to the AR camera view.
        ArCamera.SetMapOverlay(result.Bitmap, modelMatrix, _isNightMode ? 0.55f : 0.80f);
        _mapOverlaySet = true;
        _overlayDrawable.IsMapOverlayActive = true;
        System.Diagnostics.Debug.WriteLine($"[ArStarHopPage] Map overlay set: center RA={result.CenterRaHours:F2}h Dec={result.CenterDecDeg:F1}° width={result.AngularWidthDeg:F1}°");
#endif
    }

    // -------------------------------------------------------------------------
    // Location resolution
    // -------------------------------------------------------------------------

    private async Task ResolveRouteInputAsync()
    {
        double lat, lon;

        var freshContext = await _observerOrientationService.TryGetObserverContextAsync();
        if (freshContext is not null)
        {
            lat = freshContext.LatitudeDegrees;
            lon = freshContext.LongitudeDegrees;
        }
        else if (_data.UseObserverOrientation
              && _data.ObserverLatitudeDeg.HasValue
              && _data.ObserverLongitudeDeg.HasValue)
        {
            lat = _data.ObserverLatitudeDeg.Value;
            lon = _data.ObserverLongitudeDeg.Value;
            ShowStatus("Using stored location.\nEnable location services for best accuracy.");
        }
        else
        {
            ShowStatus("Location unavailable.\nEnable location services for an accurate sky overlay.");
            lat = 0.0;
            lon = 0.0;
        }

        _routeInput = BuildRouteInput(_data, lat, lon);
    }

    private static ArRouteInput BuildRouteInput(StarMapData data, double lat, double lon)
    {
        static ArStarPoint Map(StarMapPoint p, ArOverlayRole role) =>
            new(p.RaHours, p.DecDeg, p.Magnitude, p.Label, role);

        return new ArRouteInput(
            ObserverLatitudeDegrees: lat,
            ObserverLongitudeDegrees: lon,
            ObservationTime: DateTimeOffset.Now,
            Target: Map(data.Target, ArOverlayRole.Target),
            AsterismStars: data.AsterismStars
                .Select(s => Map(s, ArOverlayRole.AsterismStar))
                .ToList(),
            AsterismSegments: data.AsterismSegments,
            HopSteps: data.HopSteps
                .Select((s, i) => Map(s, i == 0 ? ArOverlayRole.AnchorStar : ArOverlayRole.HopStar))
                .ToList(),
            BackgroundStars: data.BackgroundStars
                .Select(s => Map(s, ArOverlayRole.BackgroundStar))
                .ToList());
    }

    // -------------------------------------------------------------------------
    // UI helpers
    // -------------------------------------------------------------------------

    private void ShowStatus(string message)
    {
        StatusLabel.Text = message;
        StatusBanner.IsVisible = true;
    }

    private void ShowCalibrationHint()
    {
        if (_calibrationHintShown) return;
        _calibrationHintShown = true;
        CalibrationHint.IsVisible = true;

        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(7), () =>
        {
            CalibrationHint.IsVisible = false;
        });
    }

    private void OnArStatusMessage(object? sender, string message)
    {
        ShowStatus(message);
        if (message == "ARCore tracking: Tracking" && _mapOverlaySet)
        {
            // ARCore frequently drifts or changes its world coordinates when recovering from a Paused state.
            // Recalibrate the compass and re-anchor the map to prevent wild jumps in position.
            OnCalibrateClicked(this, EventArgs.Empty);
        }
    }

    // -------------------------------------------------------------------------
    // Button handlers
    // -------------------------------------------------------------------------

    private async void OnCloseClicked(object? sender, EventArgs e) =>
        await Navigation.PopModalAsync();

    private void OnCalibrateClicked(object? sender, EventArgs e)
    {
        _orientationService.Recenter();
        // Reset map overlay so it will be re-computed with new heading.
        _mapOverlaySet = false;
#if ANDROID
        _lastModelMatrix = null;
#endif
        _overlayDrawable.IsMapOverlayActive = false;
        ArCamera.ClearMapOverlay();
        ShowStatus("Heading recalibrated from compass. Map will re-anchor.");
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(3), () => StatusBanner.IsVisible = false);
    }

    private void OnNightModeClicked(object? sender, EventArgs e)
    {
        _isNightMode = !_isNightMode;
        NightFilterOverlay.IsVisible = _isNightMode;
        ArCamera.Opacity = _isNightMode ? 0.24 : 1.0;
        NightModeButton.Text = _isNightMode ? "Day AR" : "Night AR";

        _overlayDrawable.SetNightMode(_isNightMode);
        OverlayView.Invalidate();
    }
}
