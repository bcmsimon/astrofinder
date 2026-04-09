using AstroApps.Equipment.Profiles.Models;

namespace AstroFinder.Engine.Catalog;

/// <summary>
/// Read-only access to the star, target, and asterism catalogs.
/// </summary>
public interface ICatalogProvider
{
    IReadOnlyList<CatalogStar> GetStars();
    IReadOnlyList<CatalogTarget> GetTargets();
    IReadOnlyList<CatalogAsterism> GetAsterisms();
    CatalogStar? FindStarById(string id);
    CatalogStar? FindStarByHipparcosId(int hipparcosId);
    CatalogTarget? FindTargetById(string id);
}
