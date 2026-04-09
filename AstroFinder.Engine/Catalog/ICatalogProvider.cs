namespace AstroFinder.Engine.Catalog;

/// <summary>
/// Read-only access to the star, target, and asterism catalogs.
/// </summary>
public interface ICatalogProvider
{
    IReadOnlyList<StarEntry> GetStars();
    IReadOnlyList<TargetEntry> GetTargets();
    IReadOnlyList<AsterismEntry> GetAsterisms();
    StarEntry? FindStarById(string id);
    TargetEntry? FindTargetById(string id);
}
