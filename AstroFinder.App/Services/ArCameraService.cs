using Microsoft.Maui.ApplicationModel;

namespace AstroFinder.App.Services;

public enum CameraArStatus
{
    Disabled,
    Enabled,
    PermissionDenied,
}

/// <summary>
/// Manages the user's opt-in to the AR camera feature and the associated
/// camera permission, following the same pattern as <see cref="ObserverOrientationService"/>.
/// </summary>
public sealed class ArCameraService
{
    private const string UseArCameraKey = "astrofinder.use-ar-camera";

    public bool IsArCameraEnabled => Preferences.Default.Get(UseArCameraKey, false);

    public async Task<CameraArStatus> SetArCameraEnabledAsync(bool enabled)
    {
        if (!enabled)
        {
            Preferences.Default.Set(UseArCameraKey, false);
            return CameraArStatus.Disabled;
        }

        var status = await MainThread.InvokeOnMainThreadAsync(
            () => Permissions.CheckStatusAsync<Permissions.Camera>());
        if (status != PermissionStatus.Granted)
        {
            status = await MainThread.InvokeOnMainThreadAsync(
                () => Permissions.RequestAsync<Permissions.Camera>());
        }

        if (status == PermissionStatus.Granted)
        {
            Preferences.Default.Set(UseArCameraKey, true);
            return CameraArStatus.Enabled;
        }

        Preferences.Default.Set(UseArCameraKey, false);
        return CameraArStatus.PermissionDenied;
    }

    public async Task<CameraArStatus> GetStatusAsync()
    {
        if (!IsArCameraEnabled)
            return CameraArStatus.Disabled;

        var permission = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
            return CameraArStatus.PermissionDenied;

        return CameraArStatus.Enabled;
    }
}
