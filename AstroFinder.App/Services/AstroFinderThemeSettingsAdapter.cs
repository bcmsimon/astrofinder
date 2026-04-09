using AstroApps.Maui.Theming.Interfaces;
using AstroApps.Maui.UIKit.Settings;

namespace AstroFinder.App.Services;

public sealed class AstroFinderThemeSettingsAdapter : IThemeSettingsAdapter
{
    private readonly IThemeManager _themeManager;

    public AstroFinderThemeSettingsAdapter(IThemeManager themeManager)
    {
        _themeManager = themeManager;
    }

    public async Task<IReadOnlyList<ThemeSettingsOption>> GetAvailableThemesAsync(CancellationToken cancellationToken = default)
    {
        var themes = await _themeManager.GetThemesAsync(cancellationToken);

        return themes
            .OrderBy(theme => theme.IsBuiltIn ? 0 : 1)
            .ThenBy(theme => theme.Name, StringComparer.Ordinal)
            .Select(theme => new ThemeSettingsOption(theme.Id, theme.Name, theme.Description))
            .ToArray();
    }

    public async Task<string?> GetActiveThemeIdAsync(CancellationToken cancellationToken = default)
    {
        var activeTheme = await _themeManager.GetActiveThemeAsync(cancellationToken);
        return activeTheme?.Id;
    }

    public async Task<bool> ApplyAndPersistThemeAsync(string themeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(themeId))
        {
            return false;
        }

        try
        {
            await _themeManager.SetActiveThemeAsync(themeId, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
