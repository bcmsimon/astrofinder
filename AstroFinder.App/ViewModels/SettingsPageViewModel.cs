using System.ComponentModel;
using System.Runtime.CompilerServices;
using AstroFinder.App.Services;

namespace AstroFinder.App.ViewModels;

public sealed class SettingsPageViewModel : INotifyPropertyChanged
{
    private readonly ObserverOrientationService _observerOrientationService;
    private bool _useLocationOrientation;
    private bool _isBusy;
    private string _statusText = "Location access is off. Star hop maps use a north-up chart.";

    public SettingsPageViewModel(ObserverOrientationService observerOrientationService)
    {
        _observerOrientationService = observerOrientationService;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool UseLocationOrientation
    {
        get => _useLocationOrientation;
        private set
        {
            if (_useLocationOrientation == value) return;
            _useLocationOrientation = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value) return;
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public async Task InitializeAsync()
    {
        UseLocationOrientation = _observerOrientationService.IsLocationOrientationEnabled;
        var status = await _observerOrientationService.GetStatusAsync();
        StatusText = FormatStatus(status);
    }

    public async Task ApplyLocationToggleAsync(bool enabled)
    {
        IsBusy = true;

        var status = await _observerOrientationService.SetLocationOrientationEnabledAsync(enabled);
        UseLocationOrientation = _observerOrientationService.IsLocationOrientationEnabled;
        StatusText = FormatStatus(status);

        IsBusy = false;
    }

    private static string FormatStatus(LocationOrientationStatus status) => status switch
    {
        LocationOrientationStatus.Enabled => "Location enabled. Star hop maps will rotate to match your current sky.",
        LocationOrientationStatus.PermissionDenied => "Location permission was denied. Star hop maps will stay north-up until you enable access.",
        LocationOrientationStatus.LocationUnavailable => "Location is enabled, but the current position could not be read. The app will fall back to a north-up chart.",
        _ => "Location access is off. Star hop maps use a north-up chart."
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
