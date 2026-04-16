using System.ComponentModel;
using System.Runtime.CompilerServices;
using AstroFinder.App.Services;

namespace AstroFinder.App.ViewModels;

public sealed class SettingsPageViewModel : INotifyPropertyChanged
{
    private readonly ObserverOrientationService _observerOrientationService;
    private readonly ArCameraService _arCameraService;
    private bool _useLocationOrientation;
    private bool _useArCamera;
    private bool _showArDebugHud;
    private bool _isBusy;
    private string _statusText = "Location access is off. Star hop maps use a north-up chart.";
    private string _arStatusText = "AR sky view is off. Enable to use the live camera viewfinder.";

    public const string ShowArDebugHudKey = "astrofinder.show-ar-debug-hud";

    public SettingsPageViewModel(
        ObserverOrientationService observerOrientationService,
        ArCameraService arCameraService)
    {
        _observerOrientationService = observerOrientationService;
        _arCameraService = arCameraService;
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

    public bool UseArCamera
    {
        get => _useArCamera;
        private set
        {
            if (_useArCamera == value) return;
            _useArCamera = value;
            OnPropertyChanged();
        }
    }

    public string ArStatusText
    {
        get => _arStatusText;
        private set
        {
            if (_arStatusText == value) return;
            _arStatusText = value;
            OnPropertyChanged();
        }
    }

    public bool ShowArDebugHud
    {
        get => _showArDebugHud;
        private set
        {
            if (_showArDebugHud == value) return;
            _showArDebugHud = value;
            OnPropertyChanged();
        }
    }

    public async Task InitializeAsync()
    {
        UseLocationOrientation = _observerOrientationService.IsLocationOrientationEnabled;
        var status = await _observerOrientationService.GetStatusAsync();
        StatusText = FormatStatus(status);

        UseArCamera = _arCameraService.IsArCameraEnabled;
        var arStatus = await _arCameraService.GetStatusAsync();
        ArStatusText = FormatArStatus(arStatus);

        ShowArDebugHud = Preferences.Default.Get(ShowArDebugHudKey, false);
    }

    public async Task ApplyLocationToggleAsync(bool enabled)
    {
        IsBusy = true;

        var status = await _observerOrientationService.SetLocationOrientationEnabledAsync(enabled);
        UseLocationOrientation = _observerOrientationService.IsLocationOrientationEnabled;
        StatusText = FormatStatus(status);

        IsBusy = false;
    }

    public async Task ApplyArCameraToggleAsync(bool enabled)
    {
        IsBusy = true;

        var status = await _arCameraService.SetArCameraEnabledAsync(enabled);
        UseArCamera = _arCameraService.IsArCameraEnabled;
        ArStatusText = FormatArStatus(status);

        IsBusy = false;
    }

    public void ApplyArDebugHudToggle(bool enabled)
    {
        Preferences.Default.Set(ShowArDebugHudKey, enabled);
        ShowArDebugHud = enabled;
    }

    private static string FormatStatus(LocationOrientationStatus status) => status switch
    {
        LocationOrientationStatus.Enabled => "Location enabled. Star hop maps will rotate to match your current sky.",
        LocationOrientationStatus.PermissionDenied => "Location permission was denied. Star hop maps will stay north-up until you enable access.",
        LocationOrientationStatus.LocationUnavailable => "Location is enabled, but the current position could not be read. The app will fall back to a north-up chart.",
        _ => "Location access is off. Star hop maps use a north-up chart."
    };

    private static string FormatArStatus(CameraArStatus status) => status switch
    {
        CameraArStatus.Enabled => "Camera access enabled. AR star-hop view is available from the star map.",
        CameraArStatus.PermissionDenied => "Camera permission was denied. Enable camera access in your device settings to use the AR view.",
        _ => "AR sky view is off. Enable to use the live camera viewfinder."
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
