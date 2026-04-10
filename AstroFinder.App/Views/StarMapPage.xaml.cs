namespace AstroFinder.App.Views;

public partial class StarMapPage : ContentPage
{
    private readonly string _mapHtml;

    public StarMapPage(StarMapData data)
    {
        InitializeComponent();

        _mapHtml = StarMapHtmlBuilder.BuildHtml(data);
        ConfigureNativeZoom();
        LoadMap();

        var hopCount = data.HopSteps.Count > 1 ? data.HopSteps.Count - 1 : 0;
        SummaryLabel.Text = $"Target: {data.Target.Label}  •  Anchor: {data.AsterismName}" +
                            (hopCount > 0 ? $"  •  {hopCount} hop{(hopCount != 1 ? "s" : "")}" : "  •  Direct") +
                            $"\n{data.OrientationSummary}  •  Native pinch zoom";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadMap();
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

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
