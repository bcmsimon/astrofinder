using AstroApps.Equipment.Profiles.Interfaces;

namespace AstroFinder.App.Services;

public sealed class MountSelectionService
{
    private const string MountsSectionId = "mounts";

    private readonly IEquipmentKitCollectionManager _collectionManager;
    private readonly IEquipmentKitWizardSchemaProvider _schemaProvider;
    private readonly ManualGotoCalibrationService _calibrationService;

    public MountSelectionService(
        IEquipmentKitCollectionManager collectionManager,
        IEquipmentKitWizardSchemaProvider schemaProvider,
        ManualGotoCalibrationService calibrationService)
    {
        _collectionManager = collectionManager;
        _schemaProvider = schemaProvider;
        _calibrationService = calibrationService;
    }

    public async Task<string?> GetSelectedMountNameAsync(CancellationToken ct = default)
    {
        var collection = await _collectionManager.GetCollectionAsync(ct).ConfigureAwait(false);
        var selectedMount = NormalizeMountName(collection.SelectedKit.Mount);

        if (!string.IsNullOrWhiteSpace(selectedMount))
        {
            _calibrationService.SetSelectedMountName(selectedMount);
            return selectedMount;
        }

        var legacySelectedMount = NormalizeMountName(_calibrationService.GetSelectedMountName());
        if (!string.IsNullOrWhiteSpace(legacySelectedMount))
        {
            await SaveSelectedMountAsync(legacySelectedMount, ct).ConfigureAwait(false);
            return legacySelectedMount;
        }

        return null;
    }

    public async Task<IReadOnlyList<string>> GetAvailableMountNamesAsync(CancellationToken ct = default)
    {
        var collection = await _collectionManager.GetCollectionAsync(ct).ConfigureAwait(false);
        var schema = _schemaProvider.GetSchema();
        var seedMounts = schema.InventorySections
            .FirstOrDefault(section => string.Equals(section.SectionId, MountsSectionId, StringComparison.OrdinalIgnoreCase))?
            .SeedOptions ?? [];

        return collection.Mounts
            .Concat(seedMounts)
            .Append(collection.SelectedKit.Mount)
            .Append(_calibrationService.GetSelectedMountName())
            .Select(NormalizeMountName)
            .Where(mount => !string.IsNullOrWhiteSpace(mount))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(mount => mount, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task SaveSelectedMountAsync(string mountName, CancellationToken ct = default)
    {
        var normalizedMount = NormalizeMountName(mountName);
        if (string.IsNullOrWhiteSpace(normalizedMount))
        {
            throw new ArgumentException("Mount name is required.", nameof(mountName));
        }

        var collection = await _collectionManager.GetCollectionAsync(ct).ConfigureAwait(false);
        collection.SelectedKit.Mount = normalizedMount;

        if (!collection.Mounts.Any(existing => string.Equals(existing?.Trim(), normalizedMount, StringComparison.OrdinalIgnoreCase)))
        {
            collection.Mounts.Add(normalizedMount);
        }

        await _collectionManager.SaveCollectionAsync(collection, ct).ConfigureAwait(false);
        _calibrationService.SetSelectedMountName(normalizedMount);
    }

    private static string NormalizeMountName(string? mountName) =>
        string.IsNullOrWhiteSpace(mountName)
            ? string.Empty
            : mountName.Trim();
}