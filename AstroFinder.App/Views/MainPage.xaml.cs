using System.Windows.Input;
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
    private readonly IArFrameSource _arFrameSource;
    private readonly ManualGotoCalibrationService _manualGotoCalibrationService;

    public MainPage(
        MainPageViewModel viewModel,
        AstroFinderSettingsModuleBootstrapper settingsBootstrapper,
        ISharedSettingsPageService settingsPageService,
        IDeviceOrientationService deviceOrientationService,
        ObserverOrientationService observerOrientationService,
        IArFrameSource arFrameSource,
        ManualGotoCalibrationService manualGotoCalibrationService)
    {
        _vm = viewModel;
        _settingsBootstrapper = settingsBootstrapper;
        _settingsPageService = settingsPageService;
        _deviceOrientationService = deviceOrientationService;
        _observerOrientationService = observerOrientationService;
        _arFrameSource = arFrameSource;
        _manualGotoCalibrationService = manualGotoCalibrationService;

        // Must be assigned before InitializeComponent so the XAML binding resolves a non-null command.
        OpenSettingsCommand = new Command(async () => await OpenSettingsAsync());

        InitializeComponent();
        BindingContext = _vm;

        _vm.ShowStarMap += async data =>
        {
            await Navigation.PushModalAsync(
                new StarMapPage(data, _deviceOrientationService, _observerOrientationService, _arFrameSource));
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _settingsBootstrapper.EnsureRegistered();
        await _vm.LoadCatalogsAsync();
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
