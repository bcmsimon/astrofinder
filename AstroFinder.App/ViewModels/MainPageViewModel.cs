using System.Windows.Input;

namespace AstroFinder.App.ViewModels;

public sealed class MainPageViewModel
{
    public MainPageViewModel()
    {
        NavigateToSettingsCommand = new Command(async () =>
        {
            await Shell.Current.GoToAsync("SettingsPage");
        });
    }

    public ICommand NavigateToSettingsCommand { get; }
}
