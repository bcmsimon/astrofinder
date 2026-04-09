using AstroApps.Equipment.Profiles.Models;
using AstroFinder.Domain;
using AstroFinder.Engine.Anchors;
using AstroFinder.Engine.Catalog;
using AstroFinder.Engine.Hops;

namespace AstroFinder.Domain.Tests;

public class FinderOrchestratorTests
{
    private sealed class TestCatalogProvider : ICatalogProvider
    {
        private readonly List<CatalogStar> _stars = new();
        private readonly List<CatalogTarget> _targets = new();
        private readonly List<CatalogAsterism> _asterisms = new();

        public void AddStar(CatalogStar star) => _stars.Add(star);
        public void AddTarget(CatalogTarget target) => _targets.Add(target);
        public void AddAsterism(CatalogAsterism asterism) => _asterisms.Add(asterism);

        public IReadOnlyList<CatalogStar> GetStars() => _stars;
        public IReadOnlyList<CatalogTarget> GetTargets() => _targets;
        public IReadOnlyList<CatalogAsterism> GetAsterisms() => _asterisms;
        public CatalogStar? FindStarById(string id) => _stars.FirstOrDefault(s => s.Id == id);
        public CatalogStar? FindStarByHipparcosId(int hipparcosId) => _stars.FirstOrDefault(s => s.HipparcosId == hipparcosId);
        public CatalogTarget? FindTargetById(string id) => _targets.FirstOrDefault(t => t.Id == id);
    }

    [Fact]
    public void CreateSession_KnownTarget_ReturnsSession()
    {
        var catalog = new TestCatalogProvider();
        catalog.AddTarget(new CatalogTarget
        {
            Id = "M31", DisplayName = "M31",
            Aliases = new List<string> { "Andromeda Galaxy" },
            RightAscensionHours = 0.712, DeclinationDeg = 41.269,
            Category = AstroApps.Equipment.Profiles.Enums.ShootingTargetCategory.Galaxy,
        });
        catalog.AddStar(new CatalogStar
        {
            Id = "alpheratz", DisplayName = "Alpheratz", HipparcosId = 677,
            RightAscensionHours = 0.1398, DeclinationDeg = 29.091, VisualMagnitude = 2.06,
        });
        catalog.AddAsterism(new CatalogAsterism
        {
            Id = "great-square",
            DisplayName = "Great Square of Pegasus",
            StarIds = new List<int> { 677 },
            Segments = new List<int[]>(),
            FamiliarityScore = 8,
        });

        var orchestrator = new FinderOrchestrator(
            catalog,
            new AnchorSelector(),
            new HopGenerator());

        var session = orchestrator.CreateSession("M31");

        Assert.NotNull(session);
        Assert.Equal("M31", session.Target.Id);
        Assert.NotNull(session.Anchor);
    }

    [Fact]
    public void CreateSession_UnknownTarget_Throws()
    {
        var catalog = new TestCatalogProvider();
        var orchestrator = new FinderOrchestrator(
            catalog,
            new AnchorSelector(),
            new HopGenerator());

        Assert.Throws<ArgumentException>(() => orchestrator.CreateSession("NONEXISTENT"));
    }
}
