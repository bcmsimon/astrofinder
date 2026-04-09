namespace AstroFinder.App.Services;

public sealed class PreferencesUserSettingsStore : IUserSettingsStore
{
    public string? GetString(string key) =>
        Preferences.Default.ContainsKey(key) ? Preferences.Default.Get<string>(key, string.Empty) : null;

    public void SetString(string key, string value) =>
        Preferences.Default.Set(key, value);

    public void Remove(string key) =>
        Preferences.Default.Remove(key);
}
