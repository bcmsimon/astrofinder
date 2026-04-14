using AstroApps.Equipment.Profiles.Interfaces;
using AstroApps.Equipment.Profiles.Models;
using AstroApps.Equipment.Profiles.Services;
using AstroFinder.App.Services;
using System.Windows.Input;

namespace AstroFinder.App.Views;

public partial class EquipmentKitSettingsPage : ContentPage
{
    private readonly IEquipmentKitCollectionManager _collectionManager;
    private readonly IEquipmentKitWizardSchemaProvider _schemaProvider;
    private readonly ManualGotoCalibrationService _calibrationService;

    private bool _isLoaded;
    private EquipmentKitCollection? _currentCollection;

    public EquipmentKitSettingsPage(
        IEquipmentKitCollectionManager collectionManager,
        IEquipmentKitWizardSchemaProvider schemaProvider,
        ManualGotoCalibrationService calibrationService)
    {
        _collectionManager = collectionManager;
        _schemaProvider = schemaProvider;
        _calibrationService = calibrationService;

        SaveCommand = new Command(async () => await SaveAsync());
        CalibrateCommand = new Command(async () => await CalibrateAsync());

        InitializeComponent();
    }

    public ICommand SaveCommand { get; }

    public ICommand CalibrateCommand { get; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isLoaded)
        {
            return;
        }

        WizardView.Schema = _schemaProvider.GetSchema();
        _currentCollection = await _collectionManager.GetCollectionAsync();
        WizardView.Draft = EquipmentKitDraftMapper.FromCollection(_currentCollection);
        _calibrationService.SetSelectedMountName(_currentCollection.SelectedKit.Mount);
        RefreshStatus(_currentCollection.SelectedKit.Mount);
        _isLoaded = true;
    }

    private async Task SaveAsync()
    {
        var previousMount = _currentCollection?.SelectedKit.Mount;

        var draft = WizardView.CreateDraft();
        var updated = EquipmentKitDraftMapper.ToCollection(draft);

        await _collectionManager.SaveCollectionAsync(updated);
        _calibrationService.SetSelectedMountName(updated.SelectedKit.Mount);

        var mountChanged = !string.Equals(
            NormalizeMountName(previousMount),
            NormalizeMountName(updated.SelectedKit.Mount),
            StringComparison.Ordinal);

        if (mountChanged)
        {
            _calibrationService.InvalidateCalibration(updated.SelectedKit.Mount);
            await PromptCalibrationAsync(updated.SelectedKit.Mount);
        }

        _currentCollection = updated;
        RefreshStatus(updated.SelectedKit.Mount);
    }

    private async Task CalibrateAsync()
    {
        var mount = WizardView.CreateDraft().Mount;
        await PromptCalibrationAsync(mount);
        RefreshStatus(mount);
    }

    private async Task PromptCalibrationAsync(string? mountName)
    {
        if (string.IsNullOrWhiteSpace(mountName))
        {
            await DisplayAlert("Mount needed", "Choose a mount in Equipment Kit before calibrating directions.", "OK");
            return;
        }

        var calibrateNow = await DisplayAlert(
            "Mount calibration",
            $"Run Manual Goto direction calibration for {mountName}?",
            "Calibrate",
            "Later");

        if (!calibrateNow)
        {
            return;
        }

        var raChoice = await DisplayActionSheet(
            "For a positive RA/HA move on this mount, which direction should the app instruct?",
            "Cancel",
            null,
            "Clockwise",
            "Anti-clockwise");

        if (raChoice is not "Clockwise" and not "Anti-clockwise")
        {
            return;
        }

        var decChoice = await DisplayActionSheet(
            "For a positive Dec move on this mount, which direction should the app instruct?",
            "Cancel",
            null,
            "Clockwise",
            "Anti-clockwise");

        if (decChoice is not "Clockwise" and not "Anti-clockwise")
        {
            return;
        }

        _calibrationService.SaveCalibration(
            mountName,
            new ManualGotoDirectionCalibration(
                RaPositiveClockwise: raChoice == "Clockwise",
                DecPositiveClockwise: decChoice == "Clockwise"));

        _calibrationService.SetSelectedMountName(mountName);

        await DisplayAlert("Calibration saved", "Manual Goto will now use this mount-specific direction mapping.", "OK");
    }

    private void RefreshStatus(string? mountName)
    {
        var mountLabel = string.IsNullOrWhiteSpace(mountName) ? "No mount selected" : mountName.Trim();
        var isCalibrated = _calibrationService.TryGetCalibration(mountName, out _);
        StatusLabel.Text = isCalibrated
            ? $"Active mount: {mountLabel}. Direction calibration is ready."
            : $"Active mount: {mountLabel}. Direction calibration is not set yet.";
    }

    private static string NormalizeMountName(string? mountName) =>
        string.IsNullOrWhiteSpace(mountName)
            ? string.Empty
            : mountName.Trim().ToLowerInvariant();
}
