using System.Windows.Input;
using AstroApps.Equipment.Profiles.Interfaces;
using AstroApps.Equipment.Profiles.Models;
using AstroApps.Maui.UIKit.Settings;
using AstroFinder.App.Services;
using AstroFinder.App.ViewModels;
using CommunityToolkit.Maui.Core.Platform;

namespace AstroFinder.App.Views;

public partial class MainPage : ContentPage
{
    private readonly MainPageViewModel _vm;
    private readonly AstroFinderSettingsModuleBootstrapper _settingsBootstrapper;
    private readonly ISharedSettingsPageService _settingsPageService;
    private readonly IDeviceOrientationService _deviceOrientationService;
    private readonly ObserverOrientationService _observerOrientationService;
    private readonly ManualGotoCalibrationService _manualGotoCalibrationService;
    private readonly MountSelectionService _mountSelectionService;
    private readonly ArDebugFixtureReplayService _arDebugFixtureReplayService;
    private readonly IEquipmentKitCollectionManager _kitCollectionManager;
    private readonly EquipmentKitSettingsPage _equipmentKitSettingsPage;
    private bool _startupInitializationStarted;

    public MainPage(
        MainPageViewModel viewModel,
        AstroFinderSettingsModuleBootstrapper settingsBootstrapper,
        ISharedSettingsPageService settingsPageService,
        IDeviceOrientationService deviceOrientationService,
        ObserverOrientationService observerOrientationService,
        ManualGotoCalibrationService manualGotoCalibrationService,
        MountSelectionService mountSelectionService,
        ArDebugFixtureReplayService arDebugFixtureReplayService,
        IEquipmentKitCollectionManager kitCollectionManager,
        EquipmentKitSettingsPage equipmentKitSettingsPage)
    {
        _vm = viewModel;
        _settingsBootstrapper = settingsBootstrapper;
        _settingsPageService = settingsPageService;
        _deviceOrientationService = deviceOrientationService;
        _observerOrientationService = observerOrientationService;
        _manualGotoCalibrationService = manualGotoCalibrationService;
        _mountSelectionService = mountSelectionService;
        _arDebugFixtureReplayService = arDebugFixtureReplayService;
        _kitCollectionManager = kitCollectionManager;
        _equipmentKitSettingsPage = equipmentKitSettingsPage;

        // Must be assigned before InitializeComponent so the XAML binding resolves a non-null command.
        OpenSettingsCommand = new Command(async () => await OpenSettingsAsync());

        InitializeComponent();
        BindingContext = _vm;

        _vm.ShowStarMap += async data =>
        {
            await Navigation.PushModalAsync(
                new StarMapPage(data, _deviceOrientationService, _observerOrientationService, _arDebugFixtureReplayService));
        };

        _vm.ShowDeltasFlyout += async data =>
        {
            await Navigation.PushModalAsync(new RaDecDeltaPage(data, _manualGotoCalibrationService));
        };
    }

    private async Task OpenSettingsAsync()
    {
        if (Shell.Current is null)
        {
            return;
        }

        try
        {
            _settingsBootstrapper.EnsureRegistered();
            var settingsPage = _settingsPageService.CreateSettingsPage("Settings");
            await Shell.Current.Navigation.PushAsync(settingsPage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AstroFinder] Failed to open settings: {ex}");
            await DisplayAlert("Settings", "Could not open the settings page.", "OK");
        }
    }

    public ICommand OpenSettingsCommand { get; }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_startupInitializationStarted)
        {
            return;
        }

        _startupInitializationStarted = true;
        _ = InitializeStartupAsync();
    }

    private async Task InitializeStartupAsync()
    {
        try
        {
            _settingsBootstrapper.EnsureRegistered();
            await _vm.LoadCatalogsAsync();
            await EnsureKitConfiguredAsync();
            await EnsureMountSelectionAsync();
            _vm.RefreshMountSelection();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AstroFinder] Startup initialization failed: {ex}");
        }
    }

    private async Task EnsureKitConfiguredAsync()
    {
        var collection = await _kitCollectionManager.GetCollectionAsync();
        if (HasKitConfigured(collection))
        {
            return;
        }

        await Navigation.PushModalAsync(_equipmentKitSettingsPage);
    }

    private static bool HasKitConfigured(EquipmentKitCollection collection)
    {
        return !string.IsNullOrWhiteSpace(collection.SelectedKit.Camera)
            || !string.IsNullOrWhiteSpace(collection.SelectedKit.Lens)
            || !string.IsNullOrWhiteSpace(collection.SelectedKit.Mount)
            || collection.Cameras.Count > 0
            || collection.Lenses.Count > 0
            || collection.Mounts.Count > 0;
    }

    private async Task EnsureMountSelectionAsync()
    {
        var selectedMount = await _mountSelectionService.GetSelectedMountNameAsync();
        if (!string.IsNullOrWhiteSpace(selectedMount))
        {
            return;
        }

        var options = await _mountSelectionService.GetAvailableMountNamesAsync();
        if (options.Count == 0)
        {
            await DisplayAlert(
                "Mount required",
                "No mount profiles are available yet. Open Settings and choose a mount before using RA/Dec guidance.",
                "OK");
            return;
        }

        var choice = await DisplayActionSheet(
            "Select mount",
            "Settings",
            null,
            options.ToArray());

        if (string.Equals(choice, "Settings", StringComparison.Ordinal))
        {
            await OpenSettingsAsync();
            return;
        }

        if (!string.IsNullOrWhiteSpace(choice))
        {
            await _mountSelectionService.SaveSelectedMountAsync(choice);
        }
    }

    private void OnTargetSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _vm.TargetSearchText = e.NewTextValue ?? string.Empty;
        _vm.BeginTargetSearch();
        ScrollTargetResultsToTop();
    }

    private void OnStarSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _vm.StarSearchText = e.NewTextValue ?? string.Empty;
        _vm.BeginStarSearch();
        ScrollStarResultsToTop();
    }

    private void OnTargetSearchFocused(object? sender, FocusEventArgs e)
    {
        _vm.BeginTargetSearch();
        ScrollTargetResultsToTop();
    }

    private void OnTargetSearchUnfocused(object? sender, FocusEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.TargetSearchText))
        {
            _vm.EndTargetSearch();
        }
    }

    private void OnStarSearchFocused(object? sender, FocusEventArgs e)
    {
        _vm.BeginStarSearch();
        ScrollStarResultsToTop();
    }

    private void OnStarSearchUnfocused(object? sender, FocusEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.StarSearchText))
        {
            _vm.EndStarSearch();
        }
    }

    private void OnTargetSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is CatalogTarget target)
        {
            TargetSearchBar.Unfocus();
            _vm.SelectTarget(target);
            _ = DismissSearchKeyboardsAsync();
        }
    }

    private void OnStarSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is CatalogStar star)
        {
            StarSearchBar.Unfocus();
            _vm.SelectStar(star);
            _ = DismissSearchKeyboardsAsync();
        }
    }

    private void OnTargetResultTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is CatalogTarget target)
        {
            TargetSearchBar.Unfocus();
            _vm.SelectTarget(target);
            _ = DismissSearchKeyboardsAsync();
        }
    }

    private void OnStarResultTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is CatalogStar star)
        {
            StarSearchBar.Unfocus();
            _vm.SelectStar(star);
            _ = DismissSearchKeyboardsAsync();
        }
    }

    private void OnNearbyTargetTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is NearbyTargetSuggestion suggestion)
        {
            _vm.SelectTarget(suggestion.Target);
        }
    }

    private void ScrollTargetResultsToTop()
    {
        Dispatcher.Dispatch(() =>
        {
            var first = _vm.FilteredTargets.FirstOrDefault();
            if (first is not null)
            {
                TargetResultsList.ScrollTo(first, position: ScrollToPosition.Start, animate: false);
            }
        });
    }

    private void ScrollStarResultsToTop()
    {
        Dispatcher.Dispatch(() =>
        {
            var first = _vm.FilteredStars.FirstOrDefault();
            if (first is not null)
            {
                StarResultsList.ScrollTo(first, position: ScrollToPosition.Start, animate: false);
            }
        });
    }

    private async Task DismissSearchKeyboardsAsync()
    {
        try
        {
            TargetSearchBar.Unfocus();
            StarSearchBar.Unfocus();
#if ANDROID || IOS || WINDOWS
            await TargetSearchBar.HideKeyboardAsync(CancellationToken.None);
            await StarSearchBar.HideKeyboardAsync(CancellationToken.None);
#endif

            // Android can keep keyboard visible briefly during view transitions.
            await Task.Delay(80);

            TargetSearchBar.Unfocus();
            StarSearchBar.Unfocus();
#if ANDROID || IOS || WINDOWS
            await TargetSearchBar.HideKeyboardAsync(CancellationToken.None);
            await StarSearchBar.HideKeyboardAsync(CancellationToken.None);
#endif
        }
        catch
        {
            // Do not block selection flow if keyboard APIs are unavailable on a platform.
        }
    }
}
