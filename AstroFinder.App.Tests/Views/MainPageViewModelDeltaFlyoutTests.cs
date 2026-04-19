using AstroApps.Equipment.Profiles.Interfaces;
using AstroApps.Equipment.Profiles.Models;
using AstroApps.Equipment.Profiles.Repositories;
using AstroApps.Equipment.Profiles.Services;
using AstroFinder.App.Services;
using AstroFinder.App.ViewModels;
using AstroFinder.App.Views;
using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.App.Tests.Views;

public class MainPageViewModelDeltaFlyoutTests
{
    [Fact]
    public void ShowDeltas_RaisesFlyoutData_WithComputedDeltas_AndManualGotoTemporarilyEnabled()
    {
        var calibrationService = new ManualGotoCalibrationService();
        calibrationService.SetSelectedMountName("Sky-Watcher Star Adventurer 2i");

        var vm = CreateViewModel(calibrationService);
        vm.RefreshMountSelection();

        var referenceStar = new CatalogStar
        {
            Id = "dubhe",
            DisplayName = "Dubhe",
            VisualMagnitude = 1.8,
            RightAscensionHours = 11.062,
            DeclinationDeg = 61.75
        };

        var target = new CatalogTarget
        {
            Id = "m81",
            DisplayName = "Bode's Galaxy",
            RightAscensionHours = 9.926,
            DeclinationDeg = 69.065
        };

        vm.SelectStar(referenceStar);
        vm.SelectTarget(target);

        RaDecDeltaFlyoutData? captured = null;
        vm.ShowDeltasFlyout += data => captured = data;

        vm.ShowDeltasCommand.Execute(null);

        Assert.NotNull(captured);
        Assert.Equal("Dubhe", captured!.ReferenceStarName);
        Assert.Equal("Bode's Galaxy", captured.TargetName);
        Assert.True(captured.IsManualGotoEnabled);

        var expected = SphericalGeometry.ComputeRelativePosition(
            new EquatorialCoordinate(referenceStar.RightAscensionHours, referenceStar.DeclinationDeg),
            new EquatorialCoordinate(target.RightAscensionHours, target.DeclinationDeg));

        Assert.Equal(expected.DeltaRaDegrees, captured.DeltaRaDegrees, 8);
        Assert.Equal(expected.DeltaDecDegrees, captured.DeltaDecDegrees, 8);
    }

    [Fact]
    public void ShowDeltasCommand_IsDisabled_WhenNoMountIsSelected()
    {
        var vm = CreateViewModel();

        vm.SelectStar(new CatalogStar
        {
            Id = "dubhe",
            DisplayName = "Dubhe",
            VisualMagnitude = 1.8,
            RightAscensionHours = 11.062,
            DeclinationDeg = 61.75
        });

        vm.SelectTarget(new CatalogTarget
        {
            Id = "m81",
            DisplayName = "Bode's Galaxy",
            RightAscensionHours = 9.926,
            DeclinationDeg = 69.065
        });

        Assert.False(vm.ShowDeltasCommand.CanExecute(null));
    }

    private static MainPageViewModel CreateViewModel(ManualGotoCalibrationService? calibrationService = null)
    {
        var starSerializer = new StarCatalogSerializer();
        var targetSerializer = new TargetCatalogSerializer();
        var asterismSerializer = new AsterismCatalogSerializer();

        var catalog = new AppCatalogProvider(
            new StarCatalogManager(new InMemoryStarCatalogRepository(starSerializer), new StarCatalogValidator()),
            new TargetCatalogManager(new InMemoryTargetCatalogRepository(targetSerializer), new TargetCatalogValidator()),
            new AsterismCatalogManager(new InMemoryAsterismCatalogRepository(asterismSerializer), new AsterismCatalogValidator()));

        return new MainPageViewModel(catalog, new ObserverOrientationService(), calibrationService ?? new ManualGotoCalibrationService());
    }
}
