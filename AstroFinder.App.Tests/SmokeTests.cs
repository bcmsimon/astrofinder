using AstroApps.Equipment.Profiles.Interfaces;
using AstroApps.Equipment.Profiles.Repositories;
using AstroApps.Equipment.Profiles.Services;
using AstroFinder.App.Services;
using AstroFinder.App.ViewModels;

namespace AstroFinder.App.Tests;

public class SmokeTests
{
    [Fact]
    public void ViewModel_HasExpectedCommands()
    {
        var starSerializer = new StarCatalogSerializer();
        var targetSerializer = new TargetCatalogSerializer();
        var asterismSerializer = new AsterismCatalogSerializer();

        var catalog = new AppCatalogProvider(
            new StarCatalogManager(new InMemoryStarCatalogRepository(starSerializer), new StarCatalogValidator()),
            new TargetCatalogManager(new InMemoryTargetCatalogRepository(targetSerializer), new TargetCatalogValidator()),
            new AsterismCatalogManager(new InMemoryAsterismCatalogRepository(asterismSerializer), new AsterismCatalogValidator()));

        var vm = new MainPageViewModel(catalog);
        Assert.NotNull(vm.BuildMapCommand);
        Assert.NotNull(vm.ShowDeltasCommand);
        Assert.NotNull(vm.ClearTargetCommand);
        Assert.NotNull(vm.ClearStarCommand);
    }
}
