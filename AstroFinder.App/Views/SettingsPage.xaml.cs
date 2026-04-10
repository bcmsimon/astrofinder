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
}
