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

    [Fact]
    public void SelectStar_ShowsNearbyEasyTargets_AndFiltersFarOnesOut()
    {
        var vm = CreateViewModel();

        var dubhe = new CatalogStar
        {
            Id = "dubhe",
            DisplayName = "Dubhe",
            VisualMagnitude = 1.8,
            RightAscensionHours = 11.062,
            DeclinationDeg = 61.75
        };

        SetPrivateField(vm, "_allStars", new List<CatalogStar>
        {
            dubhe,
            new() { Id = "merak", DisplayName = "Merak", VisualMagnitude = 2.3, RightAscensionHours = 11.031, DeclinationDeg = 56.38 },
            new() { Id = "phecda", DisplayName = "Phecda", VisualMagnitude = 2.4, RightAscensionHours = 11.897, DeclinationDeg = 53.69 }
        });

        var m81 = new CatalogTarget
        {
            Id = "m81",
            DisplayName = "Bode's Galaxy",
            Category = ShootingTargetCategory.Galaxy,
            RightAscensionHours = 9.926,
            DeclinationDeg = 69.065
        };

        var m82 = new CatalogTarget
        {
            Id = "m82",
            DisplayName = "Cigar Galaxy",
            Category = ShootingTargetCategory.Galaxy,
            RightAscensionHours = 9.928,
            DeclinationDeg = 69.679
        };

        var m13 = new CatalogTarget
        {
            Id = "m13",
            DisplayName = "Hercules Globular",
            Category = ShootingTargetCategory.Cluster,
            RightAscensionHours = 16.694,
            DeclinationDeg = 36.467
        };

        SetPrivateField(vm, "_allTargets", new List<CatalogTarget> { m81, m82, m13 });

        vm.SelectStar(dubhe);

        Assert.True(vm.HasNearbyTargetSuggestions);
        Assert.Contains(vm.NearbyTargetSuggestions, x => x.DisplayName == "Bode's Galaxy");
        Assert.Contains(vm.NearbyTargetSuggestions, x => x.DisplayName == "Cigar Galaxy");
        Assert.DoesNotContain(vm.NearbyTargetSuggestions, x => x.DisplayName == "Hercules Globular");
        Assert.All(vm.NearbyTargetSuggestions, x => Assert.True(x.SeparationDegrees <= 24.0));
    }

    [Fact]
    public void SelectTarget_RemovesCurrentTargetFromNearbySuggestions()
    {
        var vm = CreateViewModel();

        var dubhe = new CatalogStar
        {
            Id = "dubhe",
            DisplayName = "Dubhe",
            VisualMagnitude = 1.8,
            RightAscensionHours = 11.062,
            DeclinationDeg = 61.75
        };

        var m81 = new CatalogTarget
        {
            Id = "m81",
            DisplayName = "Bode's Galaxy",
            Category = ShootingTargetCategory.Galaxy,
            RightAscensionHours = 9.926,
            DeclinationDeg = 69.065
        };

        SetPrivateField(vm, "_allStars", new List<CatalogStar> { dubhe });
        SetPrivateField(vm, "_allTargets", new List<CatalogTarget>
        {
            m81,
            new() { Id = "m82", DisplayName = "Cigar Galaxy", Category = ShootingTargetCategory.Galaxy, RightAscensionHours = 9.928, DeclinationDeg = 69.679 }
        });

        vm.SelectStar(dubhe);
        vm.SelectTarget(m81);

        Assert.DoesNotContain(vm.NearbyTargetSuggestions, x => x.Target.Id == "m81");
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

        return new MainPageViewModel(catalog, new ObserverOrientationService(), new ManualGotoCalibrationService());
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);

        if (fieldName == "_allStars" && value is IReadOnlyList<CatalogStar> stars)
        {
            var searchableField = instance.GetType().GetField("_searchableStars", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(searchableField);
            searchableField!.SetValue(instance, stars);

            var defaultField = instance.GetType().GetField("_defaultStarList", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(defaultField);
            defaultField!.SetValue(instance, stars.Take(25).ToList());

            var cachedField = instance.GetType().GetField("_cachedFilteredStars", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(cachedField);
            cachedField!.SetValue(instance, stars.Take(25).ToList());
        }
    }
}
