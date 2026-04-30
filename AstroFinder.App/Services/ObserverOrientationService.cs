using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace AstroFinder.App.Services;

public enum LocationOrientationStatus
{
    Disabled,
    Enabled,
    PermissionDenied,
    LocationUnavailable
}

public sealed record ObserverOrientationContext(
    double LatitudeDegrees,
    double LongitudeDegrees,
    DateTimeOffset ObservationTime);

public sealed class ObserverOrientationService
{
    private const string UseLocationKey = "astrofinder.use-location-orientation";
    private const string InvertParallacticAngleKey = "astrofinder.invert-parallactic-angle-for-display";

    public bool IsLocationOrientationEnabled => Preferences.Default.Get(UseLocationKey, false);

    public bool InvertParallacticAngleForDisplay => Preferences.Default.Get(InvertParallacticAngleKey, true);

    public async Task<LocationOrientationStatus> SetLocationOrientationEnabledAsync(bool enabled)
    {
        if (!enabled)
        {
            Preferences.Default.Set(UseLocationKey, false);
            return LocationOrientationStatus.Disabled;
        }

        var status = await MainThread.InvokeOnMainThreadAsync(() => Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>());
        if (status != PermissionStatus.Granted)
        {
            status = await MainThread.InvokeOnMainThreadAsync(() => Permissions.RequestAsync<Permissions.LocationWhenInUse>());
        }

        if (status == PermissionStatus.Granted)
        {
            Preferences.Default.Set(UseLocationKey, true);
            return LocationOrientationStatus.Enabled;
        }

        Preferences.Default.Set(UseLocationKey, false);
        return LocationOrientationStatus.PermissionDenied;
    }

    public void SetInvertParallacticAngleForDisplay(bool enabled)
    {
        Preferences.Default.Set(InvertParallacticAngleKey, enabled);
    }

    public async Task<LocationOrientationStatus> GetStatusAsync()
    {
        if (!IsLocationOrientationEnabled)
        {
            return LocationOrientationStatus.Disabled;
        }

        var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (permission != PermissionStatus.Granted)
        {
            return LocationOrientationStatus.PermissionDenied;
        }

        var observer = await TryGetObserverContextAsync();
        return observer is null ? LocationOrientationStatus.LocationUnavailable : LocationOrientationStatus.Enabled;
    }

    public async Task<ObserverOrientationContext?> TryGetObserverContextAsync()
    {
        if (!IsLocationOrientationEnabled)
        {
            return null;
        }

        var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (permission != PermissionStatus.Granted)
        {
            return null;
        }

        try
        {
            var location = await Geolocation.Default.GetLastKnownLocationAsync();
            location ??= await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8)));
            if (location is null)
            {
                return null;
            }

            return new ObserverOrientationContext(location.Latitude, location.Longitude, DateTimeOffset.Now);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Always attempts to obtain the device location for the AR overlay, regardless of the
    /// "use location orientation" preference. Location is required for correct Alt/Az projection.
    /// Requests permission if not already granted. Returns null only if permission is denied
    /// or the device cannot provide a fix.
    /// </summary>
    public async Task<ObserverOrientationContext?> GetArObserverContextAsync()
    {
        var status = await MainThread.InvokeOnMainThreadAsync(
            () => Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>());

        if (status != PermissionStatus.Granted)
        {
            status = await MainThread.InvokeOnMainThreadAsync(
                () => Permissions.RequestAsync<Permissions.LocationWhenInUse>());
        }

        if (status != PermissionStatus.Granted)
            return null;

        try
        {
            var location = await Geolocation.Default.GetLastKnownLocationAsync();
            location ??= await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));

            if (location is null)
                return null;

            return new ObserverOrientationContext(location.Latitude, location.Longitude, DateTimeOffset.Now);
        }
        catch
        {
            return null;
        }
    }
}
