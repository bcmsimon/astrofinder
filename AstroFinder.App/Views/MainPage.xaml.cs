using System.Windows.Input;
using AstroApps.Equipment.Profiles.Models;
using AstroApps.Maui.UIKit.Settings;
using AstroFinder.App.Services;
using AstroFinder.App.ViewModels;

namespace AstroFinder.App.Views;

public partial class MainPage : ContentPage
{
    private readonly MainPageViewModel _vm;
    private readonly AstroFinderSettingsModuleBootstrapper _settingsBootstrapper;
    private readonly ISharedSettingsPageService _settingsPageService;

    public MainPage(
        MainPageViewModel viewModel,
        AstroFinderSettingsModuleBootstrapper settingsBootstrapper,
        ISharedSettingsPageService settingsPageService)
    {
        InitializeComponent();
        _vm = viewModel;
        _settingsBootstrapper = settingsBootstrapper;
        _settingsPageService = settingsPageService;
        BindingContext = _vm;

        _vm.ShowStarMap += async data =>
        {
            await Navigation.PushModalAsync(new StarMapPage(data));
        };

        OpenSettingsCommand = new Command(async () =>
        {
            var settingsPage = _settingsPageService.CreateSettingsPage("Settings");
            await Navigation.PushAsync(settingsPage);
        });
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
    }

    private void OnStarSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _vm.StarSearchText = e.NewTextValue ?? string.Empty;
    }

    private void OnTargetSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is CatalogTarget target)
        {
            _vm.SelectTarget(target);
        }
    }

    private void OnStarSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is CatalogStar star)
        {
            _vm.SelectStar(star);
        }
    }
}
