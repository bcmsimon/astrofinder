using AstroFinder.App.Controls;
using AstroFinder.App.Services;
using AstroFinder.Engine.Catalog;
using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.App.ViewModels;

/// <summary>
/// Drives the sky-overlay AR view.
/// Subscribes to <see cref="ISkyOrientationService"/>, projects catalog objects via
/// <see cref="ISkyProjectionService"/>, and pushes <see cref="ProjectedSkyItem"/> lists
/// to the <see cref="SkyOverlayView"/> for rendering.
/// </summary>
public sealed class SkyOverlayViewModel : IDisposable
{
    private readonly ISkyOrientationService _orientationService;
    private readonly ISkyProjectionService _projectionService;
    private readonly ICameraPreviewService _cameraPreviewService;
    private readonly ICatalogProvider _catalogProvider;
    private readonly ObserverOrientationService _observerService;
    private readonly ICompassAccuracyService _compassAccuracy;

    private SkyOverlayView? _overlayView;
    private ObserverContext? _observer;
    private bool _running;

    // The navigation target — set before StartAsync. Rendered as highlighted cross-hair.
    private double? _targetRaHours;
    private double? _targetDecDeg;
    private string _targetLabel = string.Empty;

    // The reference/anchor star — set before StartAsync. Also rendered as a highlighted cross-hair.
    private double? _referenceRaHours;
    private double? _referenceDecDeg;
    private string _referenceLabel = string.Empty;

    // Night mode can be toggled from the view.
    public bool IsNightMode { get; set; }

    /// <summary>
    /// Invoked on the main thread each orientation tick with a formatted diagnostics string.
    /// Assign before calling <see cref="StartAsync"/>.
    /// </summary>
    public Action<string>? DiagnosticsChanged { get; set; }

    /// <summary>
    /// Invoked on the main thread whenever the compass calibration requirement changes.
    /// True = calibration needed; False = calibration is sufficient.
    /// </summary>
    public Action<bool>? CalibrationNeededChanged { get; set; }

    /// <summary>
    /// Invoked on the main thread each orientation tick with the guide arrow angle
    /// (degrees clockwise from screen top) pointing toward the navigation target.
    /// <see langword="null"/> means the target is currently on-screen — hide the guide.
    /// </summary>
    public Action<float?>? TargetGuideChanged { get; set; }

    /// <summary>
    /// Set by <see cref="StartAsync"/> to describe how the observer location was resolved.
    /// Read from the page after awaiting StartAsync to update the status label.
    /// </summary>
    public string LocationStatus { get; private set; } = "No location";

    /// <summary>
    /// The most recent projected sky items from the last orientation tick.
    /// Updated on the main thread; read by the page to seed a pointing solve.
    /// </summary>
    public IReadOnlyList<ProjectedSkyItem>? LatestProjectedSnapshot { get; private set; }

    public SkyOverlayViewModel(
        ISkyOrientationService orientationService,
        ISkyProjectionService projectionService,
        ICameraPreviewService cameraPreviewService,
        ICatalogProvider catalogProvider,
        ObserverOrientationService observerService,
        ICompassAccuracyService compassAccuracy)
    {
        _orientationService = orientationService;
        _projectionService = projectionService;
        _cameraPreviewService = cameraPreviewService;
        _catalogProvider = catalogProvider;
        _observerService = observerService;
        _compassAccuracy = compassAccuracy;
    }

    /// <summary>Attaches the overlay graphics view that this view model will update.</summary>
    public void AttachOverlayView(SkyOverlayView overlayView)
    {
        _overlayView = overlayView;
    }

    /// <summary>
    /// Sets the current navigation target. The target is rendered as a highlighted red cross-hair
    /// regardless of whether it matches a catalog star.
    /// </summary>
    public void SetTarget(double raHours, double decDeg, string label)
    {
        _targetRaHours = raHours;
        _targetDecDeg = decDeg;
        _targetLabel = label;
    }

    /// <summary>
    /// Sets the reference/anchor star. It is rendered as a highlighted red cross-hair
    /// alongside the navigation target.
    /// </summary>
    public void SetReference(double raHours, double decDeg, string label)
    {
        _referenceRaHours = raHours;
        _referenceDecDeg = decDeg;
        _referenceLabel = label;
    }

    /// <summary>Starts sensors, refreshes observer location, and begins projection loop.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_running)
            return;

        // Location is always required for accurate Alt/Az projection — request it
        // unconditionally, regardless of the "use location orientation" preference.
        var observerCtx = await _observerService.GetArObserverContextAsync();
        if (observerCtx is not null)
        {
            _observer = new ObserverContext
            {
                LatitudeDegrees = observerCtx.LatitudeDegrees,
                LongitudeDegrees = observerCtx.LongitudeDegrees,
                UtcTime = observerCtx.ObservationTime.UtcDateTime,
            };
            LocationStatus = $"{observerCtx.LatitudeDegrees:F2}°, {observerCtx.LongitudeDegrees:F2}°";
        }
        else
        {
            // Fallback: approximate UK position. Still wrong for non-UK users but far
            // better than equator/Greenwich (0°N, 0°E) which was the previous default.
            _observer = null;
            LocationStatus = "No location (fallback)";
        }

        _orientationService.OrientationChanged += OnOrientationChanged;
        _compassAccuracy.CalibrationNeededChanged += OnCalibrationNeededChanged;
        _compassAccuracy.Start();
        await _orientationService.StartAsync(cancellationToken);
        await _cameraPreviewService.StartAsync(cancellationToken);

        // Report initial calibration state
        if (_compassAccuracy.IsCalibrationNeeded)
            MainThread.BeginInvokeOnMainThread(() => CalibrationNeededChanged?.Invoke(true));

        _running = true;
    }

    /// <summary>Stops sensors and the camera preview.</summary>
    public async Task StopAsync()
    {
        if (!_running)
            return;

        _orientationService.OrientationChanged -= OnOrientationChanged;
        _compassAccuracy.CalibrationNeededChanged -= OnCalibrationNeededChanged;
        _compassAccuracy.Stop();
        await _orientationService.StopAsync();
        await _cameraPreviewService.StopAsync();

        _running = false;
    }

    public void Dispose() => StopAsync().GetAwaiter().GetResult();

    // -------------------------------------------------------------------------

    private void OnCalibrationNeededChanged(object? sender, bool needed)
    {
        MainThread.BeginInvokeOnMainThread(() => CalibrationNeededChanged?.Invoke(needed));
    }

    private void OnOrientationChanged(object? sender, SkyOrientation orientation)
    {
        if (_overlayView is null)
            return;

        // Update observer time to now for accurate sidereal-time calculation.
        var observer = _observer is not null
            ? new ObserverContext
            {
                LatitudeDegrees = _observer.LatitudeDegrees,
                LongitudeDegrees = _observer.LongitudeDegrees,
                UtcTime = DateTime.UtcNow,
            }
            : FallbackObserver();

        var fov = _cameraPreviewService.HorizontalFovDegrees;

        var projected = BuildProjectedItems(orientation, observer, fov);

        // Guide arrow: compute angle toward target when it is off-screen.
        float? guideAngle = null;
        if (_targetRaHours.HasValue && _targetDecDeg.HasValue)
        {
            var onScreen = _projectionService.Project(
                _targetRaHours.Value * 15.0, _targetDecDeg.Value, orientation, observer, fov);
            if (!onScreen.HasValue)
                guideAngle = _projectionService.GetGuideAngleDegrees(
                    _targetRaHours.Value * 15.0, _targetDecDeg.Value, orientation, observer);
        }

        var diagnostics = $"Az: {orientation.AzimuthDegrees:F1}°  Alt: {orientation.AltitudeDegrees:F1}°  Roll: {orientation.RollDegrees:F1}°";

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_overlayView is null)
                return;
            _overlayView.SetNightMode(IsNightMode);
            _overlayView.Update(projected);
            _overlayView.Invalidate();
            LatestProjectedSnapshot = projected;
            DiagnosticsChanged?.Invoke(diagnostics);
            TargetGuideChanged?.Invoke(guideAngle);
        });
    }

    private IReadOnlyList<ProjectedSkyItem> BuildProjectedItems(
        SkyOrientation orientation,
        ObserverContext observer,
        double fov)
    {
        var items = new List<ProjectedSkyItem>();

        // Stars: show bright named stars (mag < 2) as labelled cross-hairs,
        // mag 2–6 as plain dots. DSOs are hidden to reduce clutter.
        // Skip any star that will be rendered as a highlighted target/reference marker.
        foreach (var star in _catalogProvider.GetStars())
        {
            if (star.VisualMagnitude > 6.0)
                continue;

            // Suppress catalog entry for stars that are already shown as highlighted markers.
            if (IsHighlightedCoord(star.RightAscensionHours, star.DeclinationDeg))
                continue;

            var screenPos = _projectionService.Project(
                star.RightAscensionHours * 15.0,
                star.DeclinationDeg,
                orientation,
                observer,
                fov);

            if (screenPos.HasValue)
            {
                bool isBrightMarker = star.VisualMagnitude < 2.0;
                items.Add(new ProjectedSkyItem
                {
                    Label = isBrightMarker ? star.DisplayName : string.Empty,
                    Magnitude = star.VisualMagnitude,
                    IsStar = !isBrightMarker,
                    IsHighlighted = false,
                    ScreenPosition = new System.Drawing.PointF(screenPos.Value.X, screenPos.Value.Y),
                });
            }
        }

        // Project the navigation target separately — always shown as a red highlighted cross-hair.
        if (_targetRaHours.HasValue && _targetDecDeg.HasValue)
        {
            var targetPos = _projectionService.Project(
                _targetRaHours.Value * 15.0,
                _targetDecDeg.Value,
                orientation,
                observer,
                fov);

            if (targetPos.HasValue)
            {
                items.Add(new ProjectedSkyItem
                {
                    Label = _targetLabel,
                    Magnitude = 0.0,
                    IsStar = false,
                    IsHighlighted = true,
                    ScreenPosition = new System.Drawing.PointF(targetPos.Value.X, targetPos.Value.Y),
                });
            }
        }

        // Project the reference/anchor star — also shown as a red highlighted cross-hair.
        if (_referenceRaHours.HasValue && _referenceDecDeg.HasValue)
        {
            var refPos = _projectionService.Project(
                _referenceRaHours.Value * 15.0,
                _referenceDecDeg.Value,
                orientation,
                observer,
                fov);

            if (refPos.HasValue)
            {
                items.Add(new ProjectedSkyItem
                {
                    Label = _referenceLabel,
                    Magnitude = 0.0,
                    IsStar = false,
                    IsHighlighted = true,
                    ScreenPosition = new System.Drawing.PointF(refPos.Value.X, refPos.Value.Y),
                });
            }
        }

        return items;
    }

    /// <summary>
    /// Returns a fallback observer when location permission is denied or unavailable.
    /// </summary>
    private static ObserverContext FallbackObserver() => new()
    {
        LatitudeDegrees = 51.0,
        LongitudeDegrees = -1.0,
        UtcTime = DateTime.UtcNow,
    };

    /// <summary>
    /// True if the given RA/Dec is close enough to the target or reference that the
    /// highlighted marker will already represent it — prevents duplicate catalog rendering.
    /// </summary>
    private bool IsHighlightedCoord(double raHours, double decDeg)
    {
        const double thresholdDeg = 0.2;

        if (_targetRaHours.HasValue && _targetDecDeg.HasValue)
        {
            var dRa = Math.Abs(raHours - _targetRaHours.Value) * 15.0;
            var dDec = Math.Abs(decDeg - _targetDecDeg.Value);
            if (dRa < thresholdDeg && dDec < thresholdDeg) return true;
        }

        if (_referenceRaHours.HasValue && _referenceDecDeg.HasValue)
        {
            var dRa = Math.Abs(raHours - _referenceRaHours.Value) * 15.0;
            var dDec = Math.Abs(decDeg - _referenceDecDeg.Value);
            if (dRa < thresholdDeg && dDec < thresholdDeg) return true;
        }

        return false;
    }
}
