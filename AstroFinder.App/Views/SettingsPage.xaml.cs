using AstroFinder.App.ViewModels;

namespace AstroFinder.App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsPageViewModel _viewModel;
    private bool _isInitializing;

    public SettingsPage(SettingsPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isInitializing = true;
        await _viewModel.InitializeAsync();
        _isInitializing = false;
    }

    private async void OnLocationToggleChanged(object? sender, ToggledEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        await _viewModel.ApplyLocationToggleAsync(e.Value);
    }

    private async void OnArCameraToggleChanged(object? sender, ToggledEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        await _viewModel.ApplyArCameraToggleAsync(e.Value);
    }

    private void OnParallacticDirectionToggleChanged(object? sender, ToggledEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        _viewModel.ApplyInvertParallacticAngleToggle(e.Value);
    }

    private void OnArDebugHudToggleChanged(object? sender, ToggledEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        _viewModel.ApplyArDebugHudToggle(e.Value);
    }

    private async void OnSelectMountClicked(object? sender, EventArgs e)
    {
        var options = await _viewModel.GetAvailableMountNamesAsync();
        if (options.Count == 0)
        {
            await DisplayAlert("Mounts", "No mount profiles are available to select.", "OK");
            return;
        }

        var choice = await DisplayActionSheet(
            "Select mount",
            "Cancel",
            null,
            options.ToArray());

        if (string.IsNullOrWhiteSpace(choice) || string.Equals(choice, "Cancel", StringComparison.Ordinal))
        {
            return;
        }

        await _viewModel.ApplySelectedMountAsync(choice);
    }
}
