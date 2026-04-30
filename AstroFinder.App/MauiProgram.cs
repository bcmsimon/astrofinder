using AstroFinder.App.Controls;
using AstroFinder.App.Services;
using AstroFinder.App.ViewModels;
using AstroFinder.App.Views;
using AstroApps.Equipment.Profiles;
using AstroApps.Maui.UIKit;
using AstroApps.Maui.UIKit.Settings;
using AstroApps.Maui.Theming;
using AstroFinder.Engine.Catalog;
using AstroFinder.Engine.Geometry;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
#if ANDROID
using AstroFinder.App.Platforms.Android.Ar;
using AstroFinder.App.Platforms.Android.Services;
using AstroFinder.App.Platforms.Android;
#elif IOS
using AstroFinder.App.Platforms.iOS.Ar;
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
                handlers.AddHandler<PinchCaptureView, PinchCaptureViewHandler>();
#elif IOS
                                handlers.AddHandler<ArCameraView, ArCameraViewHandler>();
#endif
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Equipment profiles & catalogs
        builder.Services.AddAstroAppsEquipmentProfiles(
            equipmentKitFileSystemPath: Path.Combine(FileSystem.AppDataDirectory, "equipment-kit"),
            starCatalogFileSystemPath: Path.Combine(FileSystem.AppDataDirectory, "star-catalogs"),
            asterismCatalogFileSystemPath: Path.Combine(FileSystem.AppDataDirectory, "asterism-catalogs"),
            targetCatalogFileSystemPath: Path.Combine(FileSystem.AppDataDirectory, "target-catalogs"));

        // App services
        builder.Services.AddSingleton<IUserSettingsStore, PreferencesUserSettingsStore>();
        builder.Services.AddAstroAppsThemeSettings<AstroFinderThemeSettingsAdapter>();
        builder.Services.AddSingleton<ManualGotoCalibrationService>();
        builder.Services.AddSingleton<MountSelectionService>();
        builder.Services.AddSingleton<LabelScaleService>();
        builder.Services.AddSingleton<AstroFinderSettingsModuleBootstrapper>();
        builder.Services.AddSingleton<AppCatalogProvider>();
        builder.Services.AddSingleton<ObserverOrientationService>();
        builder.Services.AddSingleton<FolderWatcherCaptureConfidenceService>();
        builder.Services.AddSingleton<ArDebugFixtureReplayService>();
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

        // Compass calibration accuracy monitor
#if ANDROID
        builder.Services.AddSingleton<ICompassAccuracyService, CompassAccuracyService>();
#else
        builder.Services.AddSingleton<ICompassAccuracyService, StubCompassAccuracyService>();
#endif

        // Sky overlay (sensor-fusion AR)
        builder.Services.AddSingleton<ISkyOrientationService, AstroFinder.App.Services.SkyOrientationService>();
        builder.Services.AddSingleton<ISkyProjectionService, SkyProjectionService>();
        builder.Services.AddSingleton<CommunityToolkitCameraPreviewService>();
        builder.Services.AddSingleton<ICameraPreviewService>(sp =>
        {
#if WINDOWS
            return sp.GetRequiredService<NullCameraPreviewService>();
#else
            return sp.GetRequiredService<CommunityToolkitCameraPreviewService>();
#endif
        });
        builder.Services.AddSingleton<NullCameraPreviewService>();
        builder.Services.AddSingleton<ICatalogProvider>(sp => sp.GetRequiredService<AppCatalogProvider>());

        // Pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<EquipmentKitSettingsPage>();
        builder.Services.AddTransient<SkyOverlayPage>();

        // ViewModels
        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddTransient<SettingsPageViewModel>();
        builder.Services.AddTransient<SkyOverlayViewModel>();

        return builder.Build();
    }
}
