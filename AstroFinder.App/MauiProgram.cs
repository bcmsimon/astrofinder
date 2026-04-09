using AstroFinder.App.Services;
using AstroFinder.App.ViewModels;
using AstroApps.Maui.UIKit;
using AstroApps.Maui.UIKit.Settings;
using AstroApps.Maui.Theming;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace AstroFinder.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseAstroAppsDesignSystem()
            .UseAstroAppsMauiTheming()
            .UseMauiCommunityToolkit();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Services
        builder.Services.AddSingleton<IUserSettingsStore, PreferencesUserSettingsStore>();
        builder.Services.AddAstroAppsThemeSettings<AstroFinderThemeSettingsAdapter>();
        builder.Services.AddSingleton<AstroFinderSettingsModuleBootstrapper>();

        // ViewModels
        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddTransient<SettingsPageViewModel>();

        return builder.Build();
    }
}
