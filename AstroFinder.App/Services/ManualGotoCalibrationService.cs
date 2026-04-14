namespace AstroFinder.App.Services;

public sealed record ManualGotoDirectionCalibration(bool RaPositiveClockwise, bool DecPositiveClockwise);

public sealed class ManualGotoCalibrationService
{
    private const string LegacyCalibrationValidKey = "manual_goto.calibration.valid";
    private const string LegacyRaPositiveClockwiseKey = "manual_goto.calibration.ra_positive_clockwise";
    private const string LegacyDecPositiveClockwiseKey = "manual_goto.calibration.dec_positive_clockwise";

    private const string SelectedMountKey = "manual_goto.selected_mount";
    private const string CalibrationPrefix = "manual_goto.calibration.mount";
    private readonly Dictionary<string, object> _fallbackStore = new(StringComparer.Ordinal);

    public string? GetSelectedMountName()
    {
        var mount = GetString(SelectedMountKey, string.Empty);
        return string.IsNullOrWhiteSpace(mount) ? null : mount.Trim();
    }

    public void SetSelectedMountName(string? mountName)
    {
        if (string.IsNullOrWhiteSpace(mountName))
        {
            RemoveValue(SelectedMountKey);
            return;
        }

        SetString(SelectedMountKey, mountName.Trim());
    }

    public bool TryGetCalibration(string? mountName, out ManualGotoDirectionCalibration calibration)
    {
        var keySuffix = BuildMountKeySuffix(mountName);
        var validKey = BuildScopedKey(keySuffix, "valid");
        var raKey = BuildScopedKey(keySuffix, "ra_positive_clockwise");
        var decKey = BuildScopedKey(keySuffix, "dec_positive_clockwise");

        if (GetBoolean(validKey, false))
        {
            calibration = new ManualGotoDirectionCalibration(
                GetBoolean(raKey, false),
                GetBoolean(decKey, false));
            return true;
        }

        // Backward compatibility: migrate old global calibration into the selected mount scope.
        if (GetBoolean(LegacyCalibrationValidKey, false))
        {
            calibration = new ManualGotoDirectionCalibration(
                GetBoolean(LegacyRaPositiveClockwiseKey, false),
                GetBoolean(LegacyDecPositiveClockwiseKey, false));
            SaveCalibration(mountName, calibration);
            return true;
        }

        calibration = new ManualGotoDirectionCalibration(RaPositiveClockwise: false, DecPositiveClockwise: false);
        return false;
    }

    public void SaveCalibration(string? mountName, ManualGotoDirectionCalibration calibration)
    {
        var keySuffix = BuildMountKeySuffix(mountName);
        SetBoolean(BuildScopedKey(keySuffix, "valid"), true);
        SetBoolean(BuildScopedKey(keySuffix, "ra_positive_clockwise"), calibration.RaPositiveClockwise);
        SetBoolean(BuildScopedKey(keySuffix, "dec_positive_clockwise"), calibration.DecPositiveClockwise);
    }

    public void InvalidateCalibration(string? mountName)
    {
        var keySuffix = BuildMountKeySuffix(mountName);
        RemoveValue(BuildScopedKey(keySuffix, "valid"));
        RemoveValue(BuildScopedKey(keySuffix, "ra_positive_clockwise"));
        RemoveValue(BuildScopedKey(keySuffix, "dec_positive_clockwise"));
    }

    private bool GetBoolean(string key, bool defaultValue)
    {
        try
        {
            return Preferences.Default.Get(key, defaultValue);
        }
        catch
        {
            return _fallbackStore.TryGetValue(key, out var value) && value is bool boolValue
                ? boolValue
                : defaultValue;
        }
    }

    private string GetString(string key, string defaultValue)
    {
        try
        {
            return Preferences.Default.Get(key, defaultValue);
        }
        catch
        {
            return _fallbackStore.TryGetValue(key, out var value) && value is string stringValue
                ? stringValue
                : defaultValue;
        }
    }

    private void SetBoolean(string key, bool value)
    {
        try
        {
            Preferences.Default.Set(key, value);
        }
        catch
        {
            _fallbackStore[key] = value;
        }
    }

    private void SetString(string key, string value)
    {
        try
        {
            Preferences.Default.Set(key, value);
        }
        catch
        {
            _fallbackStore[key] = value;
        }
    }

    private void RemoveValue(string key)
    {
        try
        {
            Preferences.Default.Remove(key);
        }
        catch
        {
            _fallbackStore.Remove(key);
        }
    }

    private static string BuildScopedKey(string mountKeySuffix, string token) =>
        $"{CalibrationPrefix}.{mountKeySuffix}.{token}";

    private static string BuildMountKeySuffix(string? mountName)
    {
        if (string.IsNullOrWhiteSpace(mountName))
        {
            return "unknown";
        }

        var normalized = mountName.Trim().ToLowerInvariant();
        var chars = normalized
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();

        return new string(chars);
    }
}
