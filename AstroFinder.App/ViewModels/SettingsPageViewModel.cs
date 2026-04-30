using System.ComponentModel;
using System.Runtime.CompilerServices;
using AstroFinder.App.Services;

namespace AstroFinder.App.ViewModels;

public sealed class SettingsPageViewModel : INotifyPropertyChanged
{
    private readonly ObserverOrientationService _observerOrientationService;
    private readonly ArCameraService _arCameraService;
    private readonly ArDebugFixtureReplayService _arDebugFixtureReplayService;
    private readonly MountSelectionService _mountSelectionService;
    private readonly LabelScaleService _labelScaleService;
    private bool _useLocationOrientation;
    private bool _invertParallacticAngleForDisplay;
    private bool _useArCamera;
    private bool _showArDebugHud;
    private bool _useArDebugFixtureReplay;
    private bool _isBusy;
    private float _labelScale;
    private string _selectedMountText = "No mount selected";
    private string _statusText = "Location access is off. Star hop maps use a north-up chart.";
    private string _arStatusText = "AR sky view is off. Enable to use the live camera viewfinder.";
    private string _arDebugFixtureStatusText = "Debug fixture replay is off.";

    public const string ShowArDebugHudKey = "astrofinder.show-ar-debug-hud";

    public SettingsPageViewModel(
        ObserverOrientationService observerOrientationService,
        ArCameraService arCameraService,
        ArDebugFixtureReplayService arDebugFixtureReplayService,
        MountSelectionService mountSelectionService,
        LabelScaleService labelScaleService)
    {
        _observerOrientationService = observerOrientationService;
        _arCameraService = arCameraService;
        _arDebugFixtureReplayService = arDebugFixtureReplayService;
        _mountSelectionService = mountSelectionService;
        _labelScaleService = labelScaleService;
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

    public float LabelScale
    {
        get => _labelScale;
        private set
        {
            if (Math.Abs(_labelScale - value) < 0.001f) return;
            _labelScale = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LabelScaleSampleFontSize));
            OnPropertyChanged(nameof(LabelScalePercent));
        }
    }

    public float LabelScaleSampleFontSize => Math.Clamp(_labelScale * 14f, 8f, 22f);

    public string LabelScalePercent => $"{(int)Math.Round(_labelScale * 100)}%";

    public bool InvertParallacticAngleForDisplay
    {
        get => _invertParallacticAngleForDisplay;
        private set
        {
            if (_invertParallacticAngleForDisplay == value) return;
            _invertParallacticAngleForDisplay = value;
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

    public string SelectedMountText
    {
        get => _selectedMountText;
        private set
        {
            if (_selectedMountText == value) return;
            _selectedMountText = value;
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

    public bool IsArDebugFixtureReplayAvailable => _arDebugFixtureReplayService.IsAvailable;

    public bool UseArDebugFixtureReplay
    {
        get => _useArDebugFixtureReplay;
        private set
        {
            if (_useArDebugFixtureReplay == value) return;
            _useArDebugFixtureReplay = value;
            OnPropertyChanged();
        }
    }

    public string ArDebugFixtureStatusText
    {
        get => _arDebugFixtureStatusText;
        private set
        {
            if (_arDebugFixtureStatusText == value) return;
            _arDebugFixtureStatusText = value;
            OnPropertyChanged();
        }
    }

    public async Task InitializeAsync()
    {
        UseLocationOrientation = _observerOrientationService.IsLocationOrientationEnabled;
        InvertParallacticAngleForDisplay = _observerOrientationService.InvertParallacticAngleForDisplay;
        var status = await _observerOrientationService.GetStatusAsync();
        StatusText = FormatStatus(status);

        UseArCamera = _arCameraService.IsArCameraEnabled;
        var arStatus = await _arCameraService.GetStatusAsync();
        ArStatusText = FormatArStatus(arStatus);

        var selectedMount = await _mountSelectionService.GetSelectedMountNameAsync();
        SelectedMountText = string.IsNullOrWhiteSpace(selectedMount)
            ? "No mount selected"
            : selectedMount;

        ShowArDebugHud = Preferences.Default.Get(ShowArDebugHudKey, false);
        UseArDebugFixtureReplay = _arDebugFixtureReplayService.IsEnabled;
        ArDebugFixtureStatusText = FormatArDebugFixtureStatus();
        LabelScale = _labelScaleService.LabelScale;
    }

    public Task<IReadOnlyList<string>> GetAvailableMountNamesAsync()
    {
        return _mountSelectionService.GetAvailableMountNamesAsync();
    }

    public async Task ApplySelectedMountAsync(string mountName)
    {
        await _mountSelectionService.SaveSelectedMountAsync(mountName);
        SelectedMountText = mountName.Trim();
    }

    public async Task ApplyLocationToggleAsync(bool enabled)
    {
        IsBusy = true;

        var status = await _observerOrientationService.SetLocationOrientationEnabledAsync(enabled);
        UseLocationOrientation = _observerOrientationService.IsLocationOrientationEnabled;
        StatusText = FormatStatus(status);

        IsBusy = false;
    }

    public void ApplyInvertParallacticAngleToggle(bool enabled)
    {
        _observerOrientationService.SetInvertParallacticAngleForDisplay(enabled);
        InvertParallacticAngleForDisplay = _observerOrientationService.InvertParallacticAngleForDisplay;
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

    public void ApplyArDebugFixtureReplayToggle(bool enabled)
    {
        _arDebugFixtureReplayService.SetEnabled(enabled);
        UseArDebugFixtureReplay = _arDebugFixtureReplayService.IsEnabled;
        ArDebugFixtureStatusText = FormatArDebugFixtureStatus();
    }

    public void ApplyLabelScale(float scale)
    {
        _labelScaleService.LabelScale = scale;
        LabelScale = _labelScaleService.LabelScale;
    }

    public IReadOnlyList<ArDebugFixtureOption> GetAvailableArDebugFixtures()
    {
        return _arDebugFixtureReplayService.GetAvailableFixtures();
    }

    public void ApplySelectedArDebugFixture(string fixtureId)
    {
        _arDebugFixtureReplayService.SetSelectedFixture(fixtureId);
        ArDebugFixtureStatusText = FormatArDebugFixtureStatus();
    }

    private static string FormatStatus(LocationOrientationStatus status) => status switch
    {
        LocationOrientationStatus.Enabled => "Location enabled. Star hop maps will rotate to match your current sky using the configured display direction.",
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

    private string FormatArDebugFixtureStatus()
    {
        if (!_arDebugFixtureReplayService.IsAvailable)
        {
            return "Debug fixture replay is unavailable in this build.";
        }

        if (!_arDebugFixtureReplayService.IsEnabled)
        {
            return "Debug fixture replay is off.";
        }

        var options = _arDebugFixtureReplayService.GetAvailableFixtures();
        var selected = options.FirstOrDefault(option => option.Id == _arDebugFixtureReplayService.SelectedFixtureId)
            ?? options.First();
        return $"Debug fixture replay is on. Active fixture: {selected.DisplayName}. {selected.Description}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
