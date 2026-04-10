using AstroFinder.App.Views;
using AstroApps.Maui.UIKit.Pages.Settings;
using AstroApps.Maui.UIKit.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;

namespace AstroFinder.App.Services;

public sealed class AstroFinderSettingsModuleBootstrapper
{
    private readonly ISettingsModuleRegistry _registry;
    private readonly IServiceProvider _services;
    private readonly ISharedSettingsPageService _sharedSettingsPageService;
    private bool _isInitialized;

    public AstroFinderSettingsModuleBootstrapper(
        ISettingsModuleRegistry registry,
        IServiceProvider services,
        ISharedSettingsPageService sharedSettingsPageService)
    {
        _registry = registry;
        _services = services;
        _sharedSettingsPageService = sharedSettingsPageService;
    }

    public void EnsureRegistered()
    {
        if (_isInitialized)
        {
            return;
        }

        _registry.AddModule(new SettingsModuleDefinition(
            id: "astrofinder-theme",
            title: "Theme",
            description: "Shared theme selector with app-local persistence",
            iconGlyph: "",
            section: SettingsModuleSection.General,
            createPage: () => _services.GetRequiredService<ThemeSettingsPage>(),
            order: 10));

        _registry.AddModule(new SettingsModuleDefinition(
            id: "astrofinder-orientation",
            title: "Sky orientation",
            description: "Use your location and current time to orient star hop maps to match the sky.",
            iconGlyph: "",
            section: SettingsModuleSection.General,
            createPage: () => _services.GetRequiredService<SettingsPage>(),
            order: 20));

        _registry.AddModule(new SettingsModuleDefinition(
            id: "astrofinder-about",
            title: "About",
            description: "App identity and version information",
            iconGlyph: "",
            section: SettingsModuleSection.About,
            createPage: () => _sharedSettingsPageService.CreateAboutPage(new AboutPageOptions
            {
                AppName = AppInfo.Current.Name,
                Version = AppInfo.Current.VersionString,
                Description = "Astrophotography sky navigation and framing assistant.",
                Company = "AstroApps",
                Website = "https://github.com/bcmsimon/astrofinder"
            }),
            order: 10));

        _isInitialized = true;
    }
}
