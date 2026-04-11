using System.Reflection;
using AstroApps.Equipment.Profiles.Enums;
using AstroApps.Equipment.Profiles.Interfaces;
using AstroApps.Equipment.Profiles.Models;
using AstroApps.Equipment.Profiles.Repositories;
using AstroApps.Equipment.Profiles.Services;
using AstroFinder.App.Services;
using AstroFinder.App.ViewModels;

namespace AstroFinder.App.Tests.Views;

public class MainPageViewModelSearchTests
{
    [Fact]
    public void FilteredTargets_PrioritizesBestNameMatch_First()
    {
        var vm = CreateViewModel();
        SetPrivateField(vm, "_allTargets", new List<CatalogTarget>
        {
            new() { Id = "target-1", DisplayName = "Barnard's Loop near Orion", Category = ShootingTargetCategory.Nebula },
            new() { Id = "m042", DisplayName = "Orion Nebula", Category = ShootingTargetCategory.Nebula },
            new() { Id = "m031", DisplayName = "Andromeda Galaxy", Category = ShootingTargetCategory.Galaxy }
        });

        vm.TargetSearchText = "orion";

        Assert.Equal("Orion Nebula", vm.FilteredTargets.First().DisplayName);
    }

    [Fact]
    public void FilteredStars_PrioritizesPrefixMatch_First()
    {
        var vm = CreateViewModel();
        SetPrivateField(vm, "_allStars", new List<CatalogStar>
        {
            new() { Id = "star-1", DisplayName = "Alpha Orionis Reference", VisualMagnitude = 2.0 },
            new() { Id = "betelgeuse", DisplayName = "Betelgeuse", Aliases = new List<string> { "Alpha Orionis" }, VisualMagnitude = 0.4 },
            new() { Id = "rigel", DisplayName = "Rigel", VisualMagnitude = 0.1 }
        });

        vm.StarSearchText = "bet";

        Assert.Equal("Betelgeuse", vm.FilteredStars.First().DisplayName);
    }

    [Fact]
    public void SelectTarget_SuggestsReferenceStar_WithoutAutoSelectingIt()
    {
        var vm = CreateViewModel();
        SetPrivateField(vm, "_allStars", new List<CatalogStar>
        {
            new() { Id = "dubhe", DisplayName = "Dubhe", VisualMagnitude = 1.8, RightAscensionHours = 11.062, DeclinationDeg = 61.75 },
            new() { Id = "polaris", DisplayName = "Polaris", VisualMagnitude = 2.0, RightAscensionHours = 2.530, DeclinationDeg = 89.264 },
            new() { Id = "vega", DisplayName = "Vega", VisualMagnitude = 0.03, RightAscensionHours = 18.615, DeclinationDeg = 38.783 }
        });

        var m81 = new CatalogTarget
        {
            Id = "m81",
            DisplayName = "Bode's Galaxy",
            Category = ShootingTargetCategory.Galaxy,
            RightAscensionHours = 9.926,
            DeclinationDeg = 69.065
        };

        vm.SelectTarget(m81);

        Assert.False(vm.HasSelectedStar);
        Assert.Null(vm.SelectedStar);
        Assert.Equal("Dubhe", vm.StarSearchText);
        Assert.Equal("Dubhe", vm.FilteredStars.First().DisplayName);
    }

    private static MainPageViewModel CreateViewModel()
    {
        var starSerializer = new StarCatalogSerializer();
        var targetSerializer = new TargetCatalogSerializer();
        var asterismSerializer = new AsterismCatalogSerializer();

        var catalog = new AppCatalogProvider(
            new StarCatalogManager(new InMemoryStarCatalogRepository(starSerializer), new StarCatalogValidator()),
            new TargetCatalogManager(new InMemoryTargetCatalogRepository(targetSerializer), new TargetCatalogValidator()),
            new AsterismCatalogManager(new InMemoryAsterismCatalogRepository(asterismSerializer), new AsterismCatalogValidator()));

        return new MainPageViewModel(catalog, new ObserverOrientationService());
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }
}
