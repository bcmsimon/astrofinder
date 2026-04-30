using AstroFinder.App.Controls;
using AstroFinder.App.Services;
using AstroFinder.App.ViewModels;
using AstroFinder.Domain.AR;
using AstroFinder.Domain.AR.Calibration;
using AstroFinder.Domain.AR.MountedPointing;
using CommunityToolkit.Maui.Views;

namespace AstroFinder.App.Views;

/// <summary>
/// Full-screen sky AR overlay page.
/// Stacks a live camera preview (Layer 1) with a transparent
/// <see cref="SkyOverlayView"/> (Layer 2) and minimal HUD controls (Layer 3).
/// </summary>
public partial class SkyOverlayPage : ContentPage
{
    private readonly SkyOverlayViewModel _viewModel;
    private readonly ICameraPreviewService _cameraPreviewService;
    private readonly CommunityToolkitCameraPreviewService? _cameraService;
    private readonly IArFrameSource _frameSource;
    private readonly ArDebugFixtureReplayService _fixtureService;
    private readonly LabelScaleService _labelScaleService;
    private readonly MountedPointingService _solveService = new();
    private string _targetLabel = string.Empty;

    public SkyOverlayPage(
        SkyOverlayViewModel viewModel,
        ICameraPreviewService cameraPreviewService,
        IArFrameSource frameSource,
        ArDebugFixtureReplayService fixtureService,
        LabelScaleService labelScaleService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _cameraPreviewService = cameraPreviewService;
        _frameSource = frameSource;
        _fixtureService = fixtureService;
        _labelScaleService = labelScaleService;

        _viewModel.AttachOverlayView(SkyOverlay);
        _viewModel.DiagnosticsChanged = text => DiagnosticsLabel.Text = text;
        _viewModel.CalibrationNeededChanged = needed =>
        {
            CalibrationOverlay.IsVisible = needed;
        };
        _viewModel.TargetGuideChanged = angle =>
        {
            if (angle.HasValue)
                TargetGuide.SetAngle(angle.Value);
            else
                TargetGuide.Hide();
        };

        _cameraService = cameraPreviewService as CommunityToolkitCameraPreviewService;
        _cameraService?.Attach(CameraPreview);
        _frameSource.Attach(CameraPreview);

        PinchCapture.PinchUpdated = OnNativePinchUpdated;
    }

    /// <summary>
    /// Sets the navigation target that will be rendered as a red highlighted cross-hair.
    /// Call this immediately after resolving the page from DI, before pushing it.
    /// </summary>
    public void SetTarget(double raHours, double decDeg, string label)
    {
        _targetLabel = label;
        _viewModel.SetTarget(raHours, decDeg, label);
    }

    /// <summary>
    /// Sets the reference/anchor star that will also be rendered as a red highlighted cross-hair.
    /// Call this immediately after resolving the page from DI, before pushing it.
    /// </summary>
    public void SetReference(double raHours, double decDeg, string label) =>
        _viewModel.SetReference(raHours, decDeg, label);

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Restore persisted toggle states.
        _viewModel.IsNightMode = Preferences.Get("SkyOverlay.NightMode", false);
        ApplyNightModeVisuals();
        SkyOverlay.SetLabelScale(_labelScaleService.LabelScale);

        _frameSource.SetActive(true);
        await _viewModel.StartAsync();
    }

    protected override async void OnDisappearing()
    {
        _frameSource.SetActive(false);
        _frameSource.Detach();
        await _viewModel.StopAsync();
        _cameraService?.Detach();
        base.OnDisappearing();
    }

    private async void OnCloseClicked(object? sender, EventArgs e) =>
        await Navigation.PopModalAsync();

    private void OnNightModeToggled(object? sender, EventArgs e)
    {
        _viewModel.IsNightMode = !_viewModel.IsNightMode;
        Preferences.Set("SkyOverlay.NightMode", _viewModel.IsNightMode);
        ApplyNightModeVisuals();
    }

    private async void OnSolveClicked(object? sender, EventArgs e)
    {
        var snapshot = _viewModel.LatestProjectedSnapshot;
        if (snapshot is null || snapshot.Count == 0)
        {
            await DisplayAlert("Solve", "No sky projection yet. Wait for sensor lock.", "OK");
            return;
        }

        var viewWidth = SkyOverlay.Width;
        var viewHeight = SkyOverlay.Height;
        if (viewWidth <= 0 || viewHeight <= 0)
            return;

        var seededFrame = BuildSeededFrame(snapshot, viewWidth, viewHeight);

        if (!_frameSource.TryGetLatestFrame(out var cameraFrame))
            _fixtureService.TryBuildFrame(seededFrame, out cameraFrame);

        var result = _solveService.Solve(new MountedPointingInput(
            seededFrame, cameraFrame, MountedPointingBoresight.Zero));

        var message = result.CanGuideReliably
            ? $"{result.GuidanceText}\n\nError: {result.AngularErrorToTargetDeg:F1}°  ·  Confidence: {result.Confidence:P0}"
            : $"Solve not reliable.\n{result.GuidanceText}\n\n{string.Join("\n", result.Warnings)}";

        await DisplayAlert("Pointing Solve", message, "OK");
    }

    private ArOverlayFrame BuildSeededFrame(
        IReadOnlyList<ProjectedSkyItem> items,
        double viewWidth,
        double viewHeight)
    {
        var fovDeg = _cameraPreviewService.HorizontalFovDegrees;
        var intrinsics = CameraIntrinsics.FromHorizontalFov(fovDeg, (int)viewWidth, (int)viewHeight);

        var asterismStars = new List<ProjectedSkyObject>();
        var backgroundStars = new List<ProjectedSkyObject>();
        ProjectedSkyObject? target = null;
        var idx = 0;

        foreach (var item in items)
        {
            var px = item.ScreenPosition.X * (float)viewWidth;
            var py = item.ScreenPosition.Y * (float)viewHeight;
            var screenPt = new ArScreenPoint(px, py);
            bool inView = item.ScreenPosition.X is >= 0 and <= 1
                       && item.ScreenPosition.Y is >= 0 and <= 1;

            var role = item.IsHighlighted
                ? (string.Equals(item.Label, _targetLabel, StringComparison.Ordinal)
                    ? ArOverlayRole.Target
                    : ArOverlayRole.AsterismStar)
                : ArOverlayRole.BackgroundStar;

            var obj = new ProjectedSkyObject(
                Id: $"s{idx++}",
                Label: item.Label,
                ScreenPoint: screenPt,
                InFrontOfCamera: inView,
                WithinViewport: inView,
                AltitudeDeg: 0.0,
                AzimuthDeg: 0.0,
                DirectionEnu: new Vec3(0.0, 0.0, 1.0),
                Role: role,
                Magnitude: item.Magnitude);

            if (role == ArOverlayRole.Target)
                target = obj;
            else if (role == ArOverlayRole.AsterismStar)
                asterismStars.Add(obj);
            else
                backgroundStars.Add(obj);
        }

        target ??= new ProjectedSkyObject(
            Id: "target",
            Label: _targetLabel,
            ScreenPoint: null,
            InFrontOfCamera: false,
            WithinViewport: false,
            AltitudeDeg: 0.0,
            AzimuthDeg: 0.0,
            DirectionEnu: new Vec3(0.0, 0.0, 1.0),
            Role: ArOverlayRole.Target,
            Magnitude: 0.0);

        return new ArOverlayFrame(
            Target: target,
            AsterismStars: asterismStars,
            AsterismSegments: [],
            HopSteps: [],
            BackgroundStars: backgroundStars,
            Intrinsics: intrinsics,
            OffscreenArrow: null,
            TargetAngularDistanceDegrees: 0.0,
            CenterReticleText: null,
            RouteSegments: []);
    }

    private void ApplyNightModeVisuals()
    {
        NightModeDim.IsVisible = _viewModel.IsNightMode;
        NightModeButton.BackgroundColor = Colors.Transparent;
        NightModeButton.TextColor = (Color)Application.Current!.Resources["ColorPrimary"];
        NightModeButton.FontAttributes = _viewModel.IsNightMode ? FontAttributes.Bold : FontAttributes.None;
    }

    private float _pinchScale = 1f;
    private float _pinchStartScale = 1f;

    private void OnNativePinchUpdated(float cumulativeScale, bool isStart)
    {
        if (isStart)
        {
            _pinchStartScale = _pinchScale;
        }
        else
        {
            _pinchScale = Math.Clamp(_pinchStartScale * cumulativeScale, 0.5f, 3.0f);
            SkyOverlay.SetLabelScale(_pinchScale);
            SkyOverlay.Invalidate();
        }
    }
}
