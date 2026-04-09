using AstroFinder.Domain;
using AstroFinder.Engine.Anchors;
using AstroFinder.Engine.Catalog;
using AstroFinder.Engine.Hops;

namespace AstroFinder.Domain.Tests;

public class FinderOrchestratorTests
{
    private sealed class TestCatalogProvider : ICatalogProvider
    {
        private readonly List<StarEntry> _stars = new();
        private readonly List<TargetEntry> _targets = new();
        private readonly List<AsterismEntry> _asterisms = new();

        public void AddStar(StarEntry star) => _stars.Add(star);
        public void AddTarget(TargetEntry target) => _targets.Add(target);
        public void AddAsterism(AsterismEntry asterism) => _asterisms.Add(asterism);

        public IReadOnlyList<StarEntry> GetStars() => _stars;
        public IReadOnlyList<TargetEntry> GetTargets() => _targets;
        public IReadOnlyList<AsterismEntry> GetAsterisms() => _asterisms;
        public StarEntry? FindStarById(string id) => _stars.FirstOrDefault(s => s.Id == id);
        public TargetEntry? FindTargetById(string id) => _targets.FirstOrDefault(t => t.Id == id);
    }

    [Fact]
    public void CreateSession_KnownTarget_ReturnsSession()
    {
        var catalog = new TestCatalogProvider();
        catalog.AddTarget(new TargetEntry
        {
            Id = "M31", Name = "M31", CommonName = "Andromeda Galaxy",
            RaHours = 0.712, DecDegrees = 41.269, ObjectType = "Galaxy",
        });
        catalog.AddStar(new StarEntry
        {
            Id = "HIP677", Name = "Alpheratz", RaHours = 0.1398, DecDegrees = 29.091, Magnitude = 2.06,
        });
        catalog.AddAsterism(new AsterismEntry
        {
            Id = "great-square",
            Name = "Great Square of Pegasus",
            StarIds = new[] { "HIP677" },
            FamiliarityScore = 0.8,
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
