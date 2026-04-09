namespace AstroFinder.App.Services;

public interface IUserSettingsStore
{
    string? GetString(string key);
    void SetString(string key, string value);
    void Remove(string key);
}
