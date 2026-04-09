using AstroFinder.App.Services;
using AstroFinder.App.ViewModels;
using AstroFinder.App.Views;
using AstroApps.Equipment.Profiles;
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

        // Equipment profiles & catalogs
        builder.Services.AddAstroAppsEquipmentProfiles(
            starCatalogFileSystemPath: Path.Combine(FileSystem.AppDataDirectory, "star-catalogs"),
            asterismCatalogFileSystemPath: Path.Combine(FileSystem.AppDataDirectory, "asterism-catalogs"),
            targetCatalogFileSystemPath: Path.Combine(FileSystem.AppDataDirectory, "target-catalogs"));

        // App services
        builder.Services.AddSingleton<IUserSettingsStore, PreferencesUserSettingsStore>();
        builder.Services.AddAstroAppsThemeSettings<AstroFinderThemeSettingsAdapter>();
        builder.Services.AddSingleton<AstroFinderSettingsModuleBootstrapper>();
        builder.Services.AddSingleton<AppCatalogProvider>();

        // Pages
        builder.Services.AddTransient<MainPage>();

        // ViewModels
        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddTransient<SettingsPageViewModel>();

        return builder.Build();
    }
}
