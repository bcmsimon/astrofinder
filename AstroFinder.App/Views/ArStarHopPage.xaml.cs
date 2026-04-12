using AstroFinder.App.Services;
using AstroFinder.Domain.AR;

namespace AstroFinder.App.Views;

/// <summary>
/// Displays a live sky overlay driven by the device's compass and accelerometer.
/// The star-hop route computed in <see cref="StarMapPage"/> is reused as-is;
/// only the rendering coordinate system changes from chart projection to camera-frame projection.
/// </summary>
public partial class ArStarHopPage : ContentPage
{
    private readonly StarMapData _data;
    private readonly IDeviceOrientationService _orientationService;
    private readonly ObserverOrientationService _observerOrientationService;
    private readonly ArProjectionService _projectionService = new();
    private readonly ArOverlayDrawable _overlayDrawable = new();
    private CancellationTokenSource? _cameraCts;

    private ArRouteInput? _routeInput;
    private CameraViewport _viewport = CameraViewport.Default;
    private bool _calibrationHintShown;
    private bool _isNightMode;

    public ArStarHopPage(
        StarMapData data,
        IDeviceOrientationService deviceOrientationService,
        ObserverOrientationService observerOrientationService)
    {
        _data = data;
        _orientationService = deviceOrientationService;
        _observerOrientationService = observerOrientationService;

        InitializeComponent();

        // Attach the drawable immediately so Draw() is wired before the first Invalidate().
        OverlayView.Drawable = _overlayDrawable;
        _overlayDrawable.SetNightMode(false);
    }

    // -------------------------------------------------------------------------
    // Page lifecycle
    // -------------------------------------------------------------------------

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        WireWindowLifecycle();

        // Resolve observer context: prefer fresh location, fall back to what
        // is already embedded in StarMapData (from when the map was built).
        await ResolveRouteInputAsync();

        // Start orientation sensors.
        var sensorsStarted = await _orientationService.StartAsync();
        if (!sensorsStarted)
        {
            ShowStatus("Compass sensor not available on this device.\nOverlay cannot be oriented.");
            return;
        }

        _orientationService.PoseChanged -= OnPoseChanged;
        _orientationService.PoseChanged += OnPoseChanged;

        await RestartCameraPreviewIfAllowedAsync();
        ShowCalibrationHint();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        UnwireWindowLifecycle();
        _orientationService.PoseChanged -= OnPoseChanged;
        _orientationService.Stop();
        StopCameraPreviewSafe();
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
        catch (OperationCanceledException)
        {
            // Page was closed before camera started — expected.
        }
        catch (Exception)
        {
            // Camera unavailable; overlay still works over the dark background.
        }
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
        catch (Exception)
        {
            // Ignore stop failures during suspend/resume transitions.
        }
    }

    private void WireWindowLifecycle()
    {
        if (Window is null)
        {
            return;
        }

        Window.Stopped -= OnWindowStopped;
        Window.Resumed -= OnWindowResumed;
        Window.Stopped += OnWindowStopped;
        Window.Resumed += OnWindowResumed;
    }

    private void UnwireWindowLifecycle()
    {
        if (Window is null)
        {
            return;
        }

        Window.Stopped -= OnWindowStopped;
        Window.Resumed -= OnWindowResumed;
    }

    private void OnWindowStopped(object? sender, EventArgs e)
    {
        StopCameraPreviewSafe();
    }

    private async void OnWindowResumed(object? sender, EventArgs e)
    {
        await RestartCameraPreviewIfAllowedAsync();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width > 0 && height > 0)
        {
            // Use a wider default phone-camera horizontal FOV so the overlay reacts
            // less aggressively to tiny hand movements and better matches typical
            // modern phone main lenses.
            _viewport = new CameraViewport(width, height, 72.0);
        }
    }

    // -------------------------------------------------------------------------
    // Sensor event
    // -------------------------------------------------------------------------

    private void OnPoseChanged(object? sender, DevicePose pose)
    {
        if (_routeInput is null) return;

        // Always use the current clock for the observation time so the
        // Alt/Az conversion tracks the sky rotation in real time.
        var liveInput = _routeInput with { ObservationTime = DateTimeOffset.Now };

        var frame = _projectionService.Project(liveInput, pose, _viewport);

        HeadingLabel.Text = $"↑ {pose.HeadingDegrees:F0}°  •  {pose.PitchDegrees:F0}° elev";

        _overlayDrawable.Update(frame);
        OverlayView.Invalidate();
    }

    // -------------------------------------------------------------------------
    // Location resolution
    // -------------------------------------------------------------------------

    private async Task ResolveRouteInputAsync()
    {
        double lat, lon;

        // Prefer a fresh GPS fix for real-time accuracy.
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
            // Fall back to the location stored when the star-hop map was built.
            lat = _data.ObserverLatitudeDeg.Value;
            lon = _data.ObserverLongitudeDeg.Value;
            ShowStatus("Using stored location.\nEnable location services for best accuracy.");
        }
        else
        {
            ShowStatus("Location unavailable.\nEnable location services for an accurate sky overlay.");
            // Still build the route input with a clearly incorrect location so the
            // overlay at least renders (directional overlay will be inaccurate).
            lat = 0.0;
            lon = 0.0;
        }

        _routeInput = BuildRouteInput(_data, lat, lon);
    }

    // -------------------------------------------------------------------------
    // Data conversion: StarMapData → ArRouteInput
    // -------------------------------------------------------------------------

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

        // Auto-dismiss after 7 seconds.
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
        // Reset and re-show the calibration hint.
        _calibrationHintShown = false;
        ShowCalibrationHint();
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
