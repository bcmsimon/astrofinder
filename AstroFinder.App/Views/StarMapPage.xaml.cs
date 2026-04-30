using AstroFinder.App.Services;

namespace AstroFinder.App.Views;

public partial class StarMapPage : ContentPage
{
    private readonly StarMapData _data;
    private readonly string _mapHtml;
    private readonly IDeviceOrientationService _deviceOrientationService;
    private readonly ObserverOrientationService _observerOrientationService;
    private readonly ArDebugFixtureReplayService _arDebugFixtureReplayService;
    private double _lastViewportSide;

    public StarMapPage(
        StarMapData data,
        IDeviceOrientationService deviceOrientationService,
        ObserverOrientationService observerOrientationService,
        ArDebugFixtureReplayService arDebugFixtureReplayService)
    {
        _data = data;
        _deviceOrientationService = deviceOrientationService;
        _observerOrientationService = observerOrientationService;
        _arDebugFixtureReplayService = arDebugFixtureReplayService;

        InitializeComponent();

        _mapHtml = StarMapHtmlBuilder.BuildHtml(data);
        ConfigureNativeZoom();
        LoadMap();

        var hopCount = data.HopSteps.Count > 1 ? data.HopSteps.Count - 1 : 0;
        var referenceText = string.IsNullOrWhiteSpace(data.ReferenceLabel)
            ? $"Anchor: {data.AsterismName}"
            : $"Reference: {data.ReferenceLabel}  •  Pattern: {data.AsterismName}";

        SummaryLabel.Text = $"Target: {data.Target.Label}  •  {referenceText}" +
                            (hopCount > 0 ? $"  •  {hopCount} hop{(hopCount != 1 ? "s" : "")}" : "  •  Direct") +
                            $"\n{data.OrientationSummary}";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Do NOT reload the map on every appearance — the HTML is already set in
        // the constructor and is static. Reloading causes a visible flicker each
        // time the user returns from the AR modal.
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdateMapViewportSize(width, height);
    }

    private void UpdateMapViewportSize(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        // Keep the map viewport square so the SVG fills it cleanly with no dead area.
        // Clamp by available vertical space so header and summary remain visible.
        var maxWidthSide = Math.Max(220.0, width - 32.0);
        var maxHeightSide = Math.Max(220.0, height - 260.0);
        var side = Math.Max(220.0, Math.Min(maxWidthSide, maxHeightSide));

        if (Math.Abs(side - _lastViewportSide) < 0.5)
        {
            return;
        }

        _lastViewportSide = side;
        MapViewport.HeightRequest = side;
        MapWebView.HeightRequest = Math.Max(120.0, side - 16.0);
    }

    private void LoadMap()
    {
        MapWebView.Source = new HtmlWebViewSource { Html = _mapHtml };
    }

    private void OnResetClicked(object? sender, EventArgs e)
    {
        LoadMap();
    }

    private void ConfigureNativeZoom()
    {
#if ANDROID
        MapWebView.HandlerChanged += (_, _) =>
        {
            if (MapWebView.Handler?.PlatformView is global::Android.Webkit.WebView nativeWebView)
            {
                var settings = nativeWebView.Settings;
                settings.SetSupportZoom(true);
                settings.BuiltInZoomControls = true;
                settings.DisplayZoomControls = false;
                settings.LoadWithOverviewMode = true;
                settings.UseWideViewPort = true;
            }
        };
#endif
    }

    private async void OnArViewClicked(object? sender, EventArgs e)
    {
        // Request camera permission at the point of use if it has not been granted.
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
        }

        // Navigate regardless — the overlay page gracefully handles unavailable sensors.
        _ = status; // used above; explicit discard avoids warning
        var page = IPlatformApplication.Current!.Services.GetRequiredService<SkyOverlayPage>();
        page.SetTarget(_data.Target.RaHours, _data.Target.DecDeg, _data.Target.Label ?? string.Empty);
        if (_data.HopSteps.Count > 0)
        {
            var refStar = _data.HopSteps[0];
            page.SetReference(refStar.RaHours, refStar.DecDeg, refStar.Label ?? string.Empty);
        }
        await Navigation.PushModalAsync(page);
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
