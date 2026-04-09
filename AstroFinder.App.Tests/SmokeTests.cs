namespace AstroFinder.App.Tests;

public class SmokeTests
{
    [Fact]
    public void AppModule_Resolves_MainPageViewModel()
    {
        // Smoke test to verify the basic type exists and can be instantiated
        var vm = new ViewModels.MainPageViewModel();
        Assert.NotNull(vm);
        Assert.NotNull(vm.NavigateToSettingsCommand);
    }
}
