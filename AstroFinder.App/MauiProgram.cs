using AstroFinder.App.Controls;
using AstroFinder.App.Services;
using AstroFinder.App.ViewModels;
using AstroFinder.App.Views;
using AstroApps.Equipment.Profiles;
using AstroApps.Maui.UIKit;
using AstroApps.Maui.UIKit.Settings;
using AstroApps.Maui.Theming;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
#if ANDROID
using AstroFinder.App.Platforms.Android.Ar;
#endif

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
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitCamera()
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler<ArCameraView, ArCameraViewHandler>();
#endif
            });

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
        builder.Services.AddSingleton<ManualGotoCalibrationService>();
        builder.Services.AddSingleton<AstroFinderSettingsModuleBootstrapper>();
        builder.Services.AddSingleton<AppCatalogProvider>();
        builder.Services.AddSingleton<ObserverOrientationService>();
        builder.Services.AddSingleton<FolderWatcherCaptureConfidenceService>();
#if ANDROID
        builder.Services.AddSingleton<IArFrameSource, AndroidCameraFrameSource>();
        // Android: ARCore provides camera pose via visual-inertial odometry.
        // Compass is read once at session start for absolute heading reference.
        builder.Services.AddSingleton<ArCorePoseProvider>();
        builder.Services.AddSingleton<IDeviceOrientationService>(sp => sp.GetRequiredService<ArCorePoseProvider>());
#else
        builder.Services.AddSingleton<IArFrameSource, NullArFrameSource>();
        // iOS/macOS: MAUI OrientationSensor wraps CMAttitudeReferenceFrame.XArbitraryZVertical
        // which is already gyro-based — no magnetometer noise on those platforms.
        builder.Services.AddSingleton<IDeviceOrientationService, DeviceOrientationService>();
#endif
        builder.Services.AddSingleton<ArCameraService>();

        // Pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<EquipmentKitSettingsPage>();

        // ViewModels
        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddTransient<SettingsPageViewModel>();

        return builder.Build();
    }
}
