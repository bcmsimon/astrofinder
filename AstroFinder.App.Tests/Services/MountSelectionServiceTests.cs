using AstroApps.Equipment.Profiles.Interfaces;
using AstroApps.Equipment.Profiles.Repositories;
using AstroApps.Equipment.Profiles.Services;
using AstroFinder.App.Services;

namespace AstroFinder.App.Tests.Services;

public class MountSelectionServiceTests
{
    [Fact]
    public async Task GetAvailableMountNamesAsync_IncludesSchemaSeedMounts_WhenCollectionIsEmpty()
    {
        var service = CreateService();

        var mounts = await service.GetAvailableMountNamesAsync();

        Assert.Contains("Sky-Watcher Star Adventurer 2i", mounts);
        Assert.Contains("Sky-Watcher HEQ5", mounts);
    }

    [Fact]
    public async Task SaveSelectedMountAsync_PersistsMountToCollection_AndCalibrationStore()
    {
        var repository = new InMemoryEquipmentKitCollectionRepository();
        var manager = new EquipmentKitCollectionManager(repository);
        var calibrationService = new ManualGotoCalibrationService();
        var service = new MountSelectionService(manager, new EquipmentKitWizardSchemaProvider(), calibrationService);

        await service.SaveSelectedMountAsync("Sky-Watcher Star Adventurer 2i");

        var collection = await manager.GetCollectionAsync();
        Assert.Equal("Sky-Watcher Star Adventurer 2i", collection.SelectedKit.Mount);
        Assert.Contains("Sky-Watcher Star Adventurer 2i", collection.Mounts);
        Assert.Equal("Sky-Watcher Star Adventurer 2i", calibrationService.GetSelectedMountName());
    }

    private static MountSelectionService CreateService()
    {
        IEquipmentKitCollectionRepository repository = new InMemoryEquipmentKitCollectionRepository();
        var manager = new EquipmentKitCollectionManager(repository);
        return new MountSelectionService(manager, new EquipmentKitWizardSchemaProvider(), new ManualGotoCalibrationService());
    }
}