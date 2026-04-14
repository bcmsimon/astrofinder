using AstroFinder.App.Services;
using AstroFinder.Domain.AR;
using AstroFinder.Domain.AR.Calibration;
using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.App.Views;

/// <summary>
/// Displays a live sky overlay driven by a <see cref="PoseMatrix"/>-based sensor pipeline.
/// </summary>
public partial class ArStarHopPage : ContentPage
{
    private readonly StarMapData _data;
    private readonly IDeviceOrientationService _orientationService;
    private readonly ObserverOrientationService _observerOrientationService;
    private readonly IArFrameSource _arFrameSource;
    private readonly ArProjectionService _projectionService = new();
    private readonly ArOverlayCalibrationPipeline _calibrationPipeline = new();
    private readonly StarDetector _starDetector = new();
    private readonly ArOverlayDrawable _overlayDrawable = new();
    private CancellationTokenSource? _cameraCts;
    private CancellationTokenSource? _detectionLoopCts;

    private ArRouteInput? _routeInput;
    private CameraIntrinsics _intrinsics = CameraIntrinsics.Default;
    private bool _calibrationHintShown;
    private bool _isNightMode;
    private bool _useCalibratedMode;
    private IReadOnlyList<DetectedStarPoint> _latestDetections = [];
    private readonly object _detectionsGate = new();

    public ArStarHopPage(
        StarMapData data,
        IDeviceOrientationService deviceOrientationService,
        ObserverOrientationService observerOrientationService,
        IArFrameSource arFrameSource)
    {
        _data = data;
        _orientationService = deviceOrientationService;
        _observerOrientationService = observerOrientationService;
        _arFrameSource = arFrameSource;

        InitializeComponent();

        OverlayView.Drawable = _overlayDrawable;
        _overlayDrawable.SetNightMode(false);
        UpdateModeButton();
    }

    // -------------------------------------------------------------------------
    // Page lifecycle
    // -------------------------------------------------------------------------

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _arFrameSource.Attach(CameraPreview);
        _arFrameSource.SetActive(_useCalibratedMode);

        WireWindowLifecycle();

        await ResolveRouteInputAsync();

        var sensorsStarted = await _orientationService.StartAsync();
        if (!sensorsStarted)
        {
            ShowStatus("Compass sensor not available on this device.\nOverlay cannot be oriented.");
            return;
        }

        _orientationService.PoseChanged -= OnPoseChanged;
        _orientationService.PoseChanged += OnPoseChanged;

        await RestartCameraPreviewIfAllowedAsync();
        StartDetectionLoop();
        ShowCalibrationHint();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        UnwireWindowLifecycle();
        _orientationService.PoseChanged -= OnPoseChanged;
        _orientationService.Stop();
        _arFrameSource.SetActive(false);
        _arFrameSource.Detach();
        StopCameraPreviewSafe();
        StopDetectionLoop();
    }

    private async Task RestartCameraPreviewIfAllowedAsync()
    {
        StopCameraPreviewSafe();

        var cameraPermission = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (cameraPermission == PermissionStatus.Granted)
        {
            _cameraCts = new CancellationTokenSource();
            _ = StartCameraAsync(_cameraCts.Token);
        }
    }

    private async Task StartCameraAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CameraPreview.StartCameraPreview(cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    private void StopCameraPreviewSafe()
    {
        try
        {
            _cameraCts?.Cancel();
            _cameraCts?.Dispose();
            _cameraCts = null;
            CameraPreview.StopCameraPreview();
        }
        catch (Exception) { }
    }

    private void WireWindowLifecycle()
    {
        if (Window is null) return;
        Window.Stopped -= OnWindowStopped;
        Window.Resumed -= OnWindowResumed;
        Window.Stopped += OnWindowStopped;
        Window.Resumed += OnWindowResumed;
    }

    private void UnwireWindowLifecycle()
    {
        if (Window is null) return;
        Window.Stopped -= OnWindowStopped;
        Window.Resumed -= OnWindowResumed;
    }

    private void OnWindowStopped(object? sender, EventArgs e) => StopCameraPreviewSafe();

    private async void OnWindowResumed(object? sender, EventArgs e) =>
        await RestartCameraPreviewIfAllowedAsync();

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

    private void OnPoseChanged(object? sender, PoseMatrix pose)
    {
        if (_routeInput is null) return;

        var liveInput = _routeInput with { ObservationTime = DateTimeOffset.Now };

        var frame = _projectionService.Project(liveInput, pose, _intrinsics);
        CalibrationResult? calibration = null;

        if (_useCalibratedMode)
        {
            IReadOnlyList<DetectedStarPoint> detections;
            lock (_detectionsGate) { detections = _latestDetections; }

            calibration = _calibrationPipeline.Calibrate(frame, detections, out var corrected);
            frame = corrected;
        }

        var headingText = $"{frame.TargetAngularDistanceDegrees:F0}\u00B0 to target";
        if (calibration is not null)
            headingText += $"  |  Cal {calibration.Confidence:0.00}";
        HeadingLabel.Text = headingText;

        // --- Diagnostics HUD ---
        var t = frame.Target;
        // Pose matrix rows: row0=right, row1=up, row2=forward (in ENU coords)
        var right = new Vec3(pose.M[0], pose.M[1], pose.M[2]);
        var up    = new Vec3(pose.M[3], pose.M[4], pose.M[5]);
        var fwd   = new Vec3(pose.M[6], pose.M[7], pose.M[8]);
        var camEnu = pose.Multiply(t.DirectionEnu);  // target in camera space

        var visStars = frame.AsterismStars.Count(s => s.InFrontOfCamera && s.ScreenPoint.HasValue);
        var visHops = frame.HopSteps.Count(s => s.InFrontOfCamera && s.ScreenPoint.HasValue);

        var sp = t.ScreenPoint.HasValue ? $"{t.ScreenPoint.Value.XPx:F0},{t.ScreenPoint.Value.YPx:F0}" : "null";

        DiagHudLabel.Text =
            $"Tgt: alt={t.AltitudeDeg:F1} az={t.AzimuthDeg:F1} | scr={sp}\n" +
            $"CamSpace: x={camEnu.X:F3} y={camEnu.Y:F3} z={camEnu.Z:F3}\n" +
            $"Right(ENU): e={right.X:F2} n={right.Y:F2} u={right.Z:F2}\n" +
            $"Up(ENU):    e={up.X:F2} n={up.Y:F2} u={up.Z:F2}\n" +
            $"Fwd(ENU):   e={fwd.X:F2} n={fwd.Y:F2} u={fwd.Z:F2}\n" +
            $"Vis: {visStars}/{frame.AsterismStars.Count} ast, {visHops}/{frame.HopSteps.Count} hop";

        _overlayDrawable.Update(frame);
        OverlayView.Invalidate();
    }

    private void StartDetectionLoop()
    {
        StopDetectionLoop();

        _detectionLoopCts = new CancellationTokenSource();
        var ct = _detectionLoopCts.Token;

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_useCalibratedMode && _arFrameSource.TryGetLatestFrame(out var frame) && frame.Width > 0 && frame.Height > 0)
                    {
                        var detections = _starDetector.Detect(frame);
                        lock (_detectionsGate) { _latestDetections = detections; }
                    }
                    else if (!_useCalibratedMode)
                    {
                        lock (_detectionsGate) { _latestDetections = []; }
                    }
                }
                catch { }

                await Task.Delay(TimeSpan.FromMilliseconds(350), ct).ConfigureAwait(false);
            }
        }, ct);
    }

    private void StopDetectionLoop()
    {
        try
        {
            _detectionLoopCts?.Cancel();
            _detectionLoopCts?.Dispose();
            _detectionLoopCts = null;
        }
        catch { }
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

    // -------------------------------------------------------------------------
    // Button handlers
    // -------------------------------------------------------------------------

    private async void OnCloseClicked(object? sender, EventArgs e) =>
        await Navigation.PopModalAsync();

    private void OnCalibrateClicked(object? sender, EventArgs e)
    {
        _orientationService.Recenter();
        ShowStatus("Heading recalibrated.");
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(3), () => StatusBanner.IsVisible = false);
    }

    private void OnModeClicked(object? sender, EventArgs e)
    {
        _useCalibratedMode = !_useCalibratedMode;
        _arFrameSource.SetActive(_useCalibratedMode);
        UpdateModeButton();

        if (!_useCalibratedMode)
        {
            lock (_detectionsGate) { _latestDetections = []; }
        }
    }

    private void UpdateModeButton()
    {
        ModeButton.Text = _useCalibratedMode ? "Cal AR" : "Sensor AR";
    }

    private void OnNightModeClicked(object? sender, EventArgs e)
    {
        _isNightMode = !_isNightMode;
        NightFilterOverlay.IsVisible = _isNightMode;
        CameraPreview.Opacity = _isNightMode ? 0.24 : 1.0;
        NightModeButton.Text = _isNightMode ? "Day AR" : "Night AR";

        _overlayDrawable.SetNightMode(_isNightMode);
        OverlayView.Invalidate();
    }
}
