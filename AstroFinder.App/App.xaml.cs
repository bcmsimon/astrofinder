using AstroApps.Maui.Theming.Interfaces;

namespace AstroFinder.App;

public partial class App : Application
{
    private readonly IThemeManager _themeManager;

    public App(IThemeManager themeManager)
    {
        _themeManager = themeManager;

        InitializeComponent();

        _ = RestoreThemeAsync();
    }

    private async Task RestoreThemeAsync()
    {
        try
        {
            var activeTheme = await _themeManager.GetActiveThemeAsync();
            if (activeTheme != null)
            {
                await _themeManager.ApplyThemeAsync(activeTheme);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore theme: {ex.Message}");
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
#if WINDOWS
        window.HandlerChanged += OnWindowHandlerChanged;
#endif
        return window;
    }

#if WINDOWS
    private const int DefaultWindowWidth = 430;
    private const int DefaultWindowHeight = 932;

    private static void OnWindowHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.HandlerChanged -= OnWindowHandlerChanged;

        if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
        {
            nativeWindow.Activated += OnNativeWindowFirstActivated;
        }
    }

    private static void OnNativeWindowFirstActivated(object? sender, Microsoft.UI.Xaml.WindowActivatedEventArgs e)
    {
        if (sender is not Microsoft.UI.Xaml.Window nativeWindow)
        {
            return;
        }

        nativeWindow.Activated -= OnNativeWindowFirstActivated;

        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(DefaultWindowWidth, DefaultWindowHeight));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to resize window: {ex.Message}");
        }
    }
#endif
}
