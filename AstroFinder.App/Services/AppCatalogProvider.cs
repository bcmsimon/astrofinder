using AstroApps.Equipment.Profiles.Interfaces;
using AstroApps.Equipment.Profiles.Models;
using AstroFinder.Engine.Catalog;

namespace AstroFinder.App.Services;

public sealed class AppCatalogProvider : ICatalogProvider
{
    private IReadOnlyList<CatalogStar> _stars = [];
    private IReadOnlyList<CatalogTarget> _targets = [];
    private IReadOnlyList<CatalogAsterism> _asterisms = [];
    private Dictionary<string, CatalogStar> _starById = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<int, CatalogStar> _starByHip = [];
    private Dictionary<string, CatalogTarget> _targetById = new(StringComparer.OrdinalIgnoreCase);

    private readonly IStarCatalogManager _starManager;
    private readonly ITargetCatalogManager _targetManager;
    private readonly IAsterismCatalogManager _asterismManager;

    public AppCatalogProvider(
        IStarCatalogManager starManager,
        ITargetCatalogManager targetManager,
        IAsterismCatalogManager asterismManager)
    {
        _starManager = starManager;
        _targetManager = targetManager;
        _asterismManager = asterismManager;
    }

    public async Task LoadAsync()
    {
        var starCatalog = await _starManager.GetCatalogAsync();
        var targetCatalog = await _targetManager.GetCatalogAsync();
        var asterismCatalog = await _asterismManager.GetCatalogAsync();

        _stars = starCatalog.Stars.ToList();
        _targets = targetCatalog.Targets.ToList();
        _asterisms = asterismCatalog.Asterisms.ToList();

        _starById = _stars.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
        _starByHip = _stars
            .Where(s => s.HipparcosId.HasValue)
            .ToDictionary(s => s.HipparcosId!.Value);
        _targetById = _targets.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<CatalogStar> GetStars() => _stars;
    public IReadOnlyList<CatalogTarget> GetTargets() => _targets;
    public IReadOnlyList<CatalogAsterism> GetAsterisms() => _asterisms;
    public CatalogStar? FindStarById(string id) => _starById.GetValueOrDefault(id);
    public CatalogStar? FindStarByHipparcosId(int hipparcosId) => _starByHip.GetValueOrDefault(hipparcosId);
    public CatalogTarget? FindTargetById(string id) => _targetById.GetValueOrDefault(id);
}
