using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AstroApps.Equipment.Profiles.Enums;
using AstroApps.Equipment.Profiles.Models;
using AstroFinder.App.Services;
using AstroFinder.Engine.Anchors;
using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Hops;
using AstroFinder.Engine.Primitives;
using AstroFinder.App.Views;


namespace AstroFinder.App.ViewModels;

public sealed class MainPageViewModel : INotifyPropertyChanged
{
    private readonly AppCatalogProvider _catalog;
    private readonly ObserverOrientationService _observerOrientationService;
    private readonly ManualGotoCalibrationService _manualGotoCalibrationService;
    private readonly LabelScaleService _labelScaleService;
    private readonly AnchorSelector _anchorSelector = new();
    private readonly HopGenerator _hopGenerator = new();

    private IReadOnlyList<CatalogTarget> _allTargets = [];
    private IReadOnlyList<CatalogStar> _allStars = [];

    // Pre-built caches — populated in LoadCatalogsAsync, never recomputed on binding ticks.
    private IReadOnlyList<CatalogStar> _defaultStarList = [];
    private IReadOnlyList<CatalogStar> _searchableStars = []; // named stars only — "HIP xxxxxx" entries excluded
    private IReadOnlyList<CatalogTarget> _defaultTargetList = [];
    private StarSpatialIndex? _starSpatialIndex;

    // Cached results — re-evaluated only when search text changes.
    private IReadOnlyList<CatalogStar> _cachedFilteredStars = [];
    private IReadOnlyList<CatalogTarget> _cachedFilteredTargets = [];
    private string _lastStarQuery = string.Empty;
    private string _lastTargetQuery = string.Empty;

    private string _targetSearchText = string.Empty;
    private string _starSearchText = string.Empty;
    private CatalogTarget? _selectedTarget;
    private CatalogStar? _selectedStar;
    private string _resultText = string.Empty;
    private bool _hasResult;
    private bool _isLoaded;
    private bool _isTargetSearchActive;
    private bool _isStarSearchActive;
    private string _lastSuggestedStarText = string.Empty;
    private IReadOnlyList<NearbyTargetSuggestion> _nearbyTargetSuggestions = [];
    private const double NearbyTargetMaxSeparationDeg = 24.0;

    public MainPageViewModel(
        AppCatalogProvider catalog,
        ObserverOrientationService observerOrientationService,
        ManualGotoCalibrationService manualGotoCalibrationService,
        LabelScaleService labelScaleService)
    {
        _catalog = catalog;
        _observerOrientationService = observerOrientationService;
        _manualGotoCalibrationService = manualGotoCalibrationService;
        _labelScaleService = labelScaleService;

        BuildMapCommand = new Command(async () => await BuildStarHopMapAsync(), () => _selectedTarget is not null);
        ShowDeltasCommand = new Command(ShowDeltas, () => _selectedTarget is not null && _selectedStar is not null && HasSelectedMount());
        ClearTargetCommand = new Command(ClearTarget);
        ClearStarCommand = new Command(ClearStar);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when a star map has been computed and should be displayed.
    /// </summary>
    public event Action<StarMapData>? ShowStarMap;

    /// <summary>
    /// Raised when RA/Dec deltas are computed and should be shown in a flyout.
    /// </summary>
    public event Action<RaDecDeltaFlyoutData>? ShowDeltasFlyout;

    public string TargetSearchText
    {
        get => _targetSearchText;
        set
        {
            if (_targetSearchText == value) return;
            _targetSearchText = value;
            RefreshFilteredTargets();
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilteredTargets));
            OnPropertyChanged(nameof(IsTargetListVisible));
        }
    }

    public string StarSearchText
    {
        get => _starSearchText;
        set
        {
            if (_starSearchText == value) return;
            _starSearchText = value;
            RefreshFilteredStars();
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilteredStars));
            OnPropertyChanged(nameof(IsStarListVisible));
        }
    }

    public CatalogTarget? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (_selectedTarget == value) return;
            _selectedTarget = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedTargetText));
            OnPropertyChanged(nameof(HasSelectedTarget));
            RefreshNearbyTargetSuggestions();
            ((Command)BuildMapCommand).ChangeCanExecute();
            ((Command)ShowDeltasCommand).ChangeCanExecute();
        }
    }

    public CatalogStar? SelectedStar
    {
        get => _selectedStar;
        set
        {
            if (_selectedStar == value) return;
            _selectedStar = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedStarText));
            OnPropertyChanged(nameof(HasSelectedStar));
            RefreshNearbyTargetSuggestions();
            ((Command)ShowDeltasCommand).ChangeCanExecute();
        }
    }

    public string SelectedTargetText => _selectedTarget?.DisplayName ?? string.Empty;
    public string SelectedStarText => _selectedStar?.DisplayName ?? string.Empty;
    public bool HasSelectedTarget => _selectedTarget is not null;
    public bool HasSelectedStar => _selectedStar is not null;
    public IReadOnlyList<NearbyTargetSuggestion> NearbyTargetSuggestions => _nearbyTargetSuggestions;
    public bool HasNearbyTargetSuggestions => _nearbyTargetSuggestions.Count > 0;
    public string NearbyTargetHeaderText => _selectedStar is null
        ? "Nearby targets"
        : $"Nearby targets from {SelectedStarText}";

    public string ResultText
    {
        get => _resultText;
        private set { _resultText = value; OnPropertyChanged(); }
    }

    public bool HasResult
    {
        get => _hasResult;
        private set { _hasResult = value; OnPropertyChanged(); }
    }

    public bool IsTargetListVisible => !HasSelectedTarget && (_isTargetSearchActive || TargetSearchText.Length > 0) && FilteredTargets.Count > 0;
    public bool IsStarListVisible => !HasSelectedStar && (_isStarSearchActive || StarSearchText.Length > 0) && FilteredStars.Count > 0;

    public IReadOnlyList<CatalogTarget> FilteredTargets => _cachedFilteredTargets;

    public IReadOnlyList<CatalogStar> FilteredStars => _cachedFilteredStars;

    private void RefreshFilteredTargets()
    {
        var q = _targetSearchText.Trim();
        if (q == _lastTargetQuery) return;
        _lastTargetQuery = q;

        if (string.IsNullOrWhiteSpace(q))
        {
            _cachedFilteredTargets = _defaultTargetList;
        }
        else
        {
            var queries = GetSearchVariants(q);
            _cachedFilteredTargets = _allTargets
                .Select(target => new { Target = target, Rank = GetTargetMatchRank(target, queries) })
                .Where(x => x.Rank < int.MaxValue)
                .OrderBy(x => x.Rank)
                .ThenBy(x => x.Target.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(25)
                .Select(x => x.Target)
                .ToList();
        }
    }

    private void RefreshFilteredStars()
    {
        var q = _starSearchText.Trim();
        if (q == _lastStarQuery) return;
        _lastStarQuery = q;

        var searchableStars = _searchableStars.Count > 0 ? _searchableStars : _allStars;
        var defaultStarList = _defaultStarList.Count > 0
            ? _defaultStarList
            : _allStars
                .OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(25)
                .ToList();

        if (string.IsNullOrWhiteSpace(q))
        {
            _cachedFilteredStars = defaultStarList;
        }
        else
        {
            // Search only named stars — "HIP xxxxxx" entries have no value in a pick list.
            _cachedFilteredStars = searchableStars
                .Select(star => new { Star = star, Rank = GetStarMatchRank(star, q) })
                .Where(x => x.Rank < int.MaxValue)
                .OrderBy(x => x.Rank)
                .ThenBy(x => x.Star.VisualMagnitude)
                .ThenBy(x => x.Star.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(50)
                .Select(x => x.Star)
                .ToList();
        }
    }

    public ICommand BuildMapCommand { get; }
    public ICommand ShowDeltasCommand { get; }
    public ICommand ClearTargetCommand { get; }
    public ICommand ClearStarCommand { get; }

    public void RefreshMountSelection()
    {
        ((Command)ShowDeltasCommand).ChangeCanExecute();
    }

    public async Task LoadCatalogsAsync()
    {
        if (_isLoaded) return;

        try
        {
            await _catalog.LoadAsync();
            _allTargets = _catalog.GetTargets();
            _allStars = _catalog.GetStars();

            // Pre-build caches so FilteredTargets/FilteredStars are never computed on binding ticks.
            _defaultTargetList = _allTargets
                .OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(25)
                .ToList();

            _defaultStarList = _allStars
                .OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(25)
                .ToList();

            // Searchable set: exclude pure HIP-number entries (not useful in a pick list).
            _searchableStars = _allStars
                .Where(s => !s.DisplayName.StartsWith("HIP ", StringComparison.OrdinalIgnoreCase))
                .ToList();

            _starSpatialIndex = new StarSpatialIndex(_allStars);

            _cachedFilteredTargets = _defaultTargetList;
            _cachedFilteredStars = _defaultStarList;
            _lastTargetQuery = string.Empty;
            _lastStarQuery = string.Empty;

            _isLoaded = true;

            if (_selectedTarget is not null && _selectedStar is null)
            {
                ApplySuggestedStarSearchText(_selectedTarget);
            }

            RefreshNearbyTargetSuggestions();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AstroFinder] Catalog load failed: {ex}");
            ResultText = $"Failed to load catalogs: {ex.Message}";
            HasResult = true;
        }
    }

    // Only used as a fallback suggestion when no nearby bright star is found.
    private static readonly string[] DefaultStarCandidates =
    [
        "Polaris", "Vega", "Arcturus", "Capella", "Sirius", "Rigel", "Procyon", "Betelgeuse"
    ];

    private void ApplySuggestedStarSearchText(CatalogTarget target)
    {
        var suggestedStar = FindSuggestedStar(target);
        if (suggestedStar is null)
        {
            _lastSuggestedStarText = string.Empty;
            return;
        }

        var shouldOverwriteSearchText = string.IsNullOrWhiteSpace(StarSearchText)
            || string.Equals(StarSearchText, _lastSuggestedStarText, StringComparison.OrdinalIgnoreCase);

        _lastSuggestedStarText = suggestedStar.DisplayName;

        if (shouldOverwriteSearchText)
        {
            StarSearchText = suggestedStar.DisplayName;
            BeginStarSearch();
        }
    }

    private CatalogStar? FindSuggestedStar(CatalogTarget target)
    {
        const double MaxSeparationDeg = 25.0;
        const double MaxMagnitude = 3.0;

        var targetCoord = new EquatorialCoordinate(target.RightAscensionHours, target.DeclinationDeg);

        var candidateStars = (_starSpatialIndex?.Query(target.RightAscensionHours, target.DeclinationDeg, MaxSeparationDeg) ?? _allStars)
            .Where(s => s.VisualMagnitude <= MaxMagnitude)
            .ToList();

        var nearbyBrightStar = candidateStars
            .Select(s => new
            {
                Star = s,
                Sep = SphericalGeometry.AngularSeparationDegrees(
                    new EquatorialCoordinate(s.RightAscensionHours, s.DeclinationDeg),
                    targetCoord)
            })
            .Where(x => x.Sep <= MaxSeparationDeg)
            .OrderBy(x => x.Sep)
            .ThenBy(x => x.Star.VisualMagnitude)
            .Select(x => x.Star)
            .FirstOrDefault();

        if (nearbyBrightStar is not null)
        {
            return nearbyBrightStar;
        }

        foreach (var candidate in DefaultStarCandidates)
        {
            var match = _allStars.FirstOrDefault(s =>
                string.Equals(s.DisplayName, candidate, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private void RefreshNearbyTargetSuggestions()
    {
        if (_selectedStar is null || _allTargets.Count == 0 || _allStars.Count == 0)
        {
            _nearbyTargetSuggestions = [];
            OnPropertyChanged(nameof(NearbyTargetSuggestions));
            OnPropertyChanged(nameof(HasNearbyTargetSuggestions));
            OnPropertyChanged(nameof(NearbyTargetHeaderText));
            return;
        }

        var referenceCoord = new EquatorialCoordinate(_selectedStar.RightAscensionHours, _selectedStar.DeclinationDeg);

        _nearbyTargetSuggestions = _allTargets
            .Where(target => _selectedTarget is null || !string.Equals(target.Id, _selectedTarget.Id, StringComparison.OrdinalIgnoreCase))
            .Select(target =>
            {
                var targetCoord = new EquatorialCoordinate(target.RightAscensionHours, target.DeclinationDeg);
                var relative = SphericalGeometry.ComputeRelativePosition(referenceCoord, targetCoord);
                return new { Target = target, Relative = relative };
            })
            .Where(x => x.Relative.AngularSeparationDegrees <= NearbyTargetMaxSeparationDeg)
            .Select(x =>
            {
                var routeCandidates = _starSpatialIndex?.Query(x.Target.RightAscensionHours, x.Target.DeclinationDeg, 30.0)
                    ?? _searchableStars;
                var route = _hopGenerator.GenerateRoute(_selectedStar, x.Target, routeCandidates);
                var hopCount = route.Steps.Count;
                var easeScore = ComputeNearbyTargetEaseScore(x.Relative.AngularSeparationDegrees, hopCount);
                var direction = FormatSkyDirection(x.Relative.PositionAngleDegrees);
                var easeLabel = FormatEaseLabel(x.Relative.AngularSeparationDegrees, hopCount);
                var hopLabel = FormatHopLabel(x.Relative.AngularSeparationDegrees, hopCount);

                return new NearbyTargetSuggestion(
                    x.Target,
                    x.Relative.AngularSeparationDegrees,
                    x.Relative.PositionAngleDegrees,
                    direction,
                    hopCount,
                    easeScore,
                    easeLabel,
                    $"{x.Relative.AngularSeparationDegrees:F1}° {direction} • {hopLabel}");
            })
            .Where(x => x.SeparationDegrees <= 18.0 || (x.SeparationDegrees <= NearbyTargetMaxSeparationDeg && x.HopCount <= 1))
            .OrderBy(x => x.EaseScore)
            .ThenBy(x => x.SeparationDegrees)
            .ThenBy(x => x.DisplayName)
            .Take(10)
            .ToList();

        OnPropertyChanged(nameof(NearbyTargetSuggestions));
        OnPropertyChanged(nameof(HasNearbyTargetSuggestions));
        OnPropertyChanged(nameof(NearbyTargetHeaderText));
    }

    private static double ComputeNearbyTargetEaseScore(double separationDegrees, int hopCount) =>
        separationDegrees + (hopCount * 2.0);

    private static string FormatEaseLabel(double separationDegrees, int hopCount)
    {
        var score = ComputeNearbyTargetEaseScore(separationDegrees, hopCount);
        if (score <= 5.0) return "Very easy";
        if (score <= 9.0) return "Easy";
        if (score <= 14.0) return "Good";
        return "Longer";
    }

    private static string FormatHopLabel(double separationDegrees, int hopCount)
    {
        if (separationDegrees <= 4.0)
        {
            return "very short hop";
        }

        return hopCount switch
        {
            0 => separationDegrees <= 9.0 ? "direct hop" : "wide direct hop",
            1 => "1 bright-star hop",
            _ => $"{hopCount} hops"
        };
    }

    private static string FormatSkyDirection(double positionAngleDegrees)
    {
        string[] labels = ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"];
        var index = (int)Math.Round((((positionAngleDegrees % 360.0) + 360.0) % 360.0) / 22.5, MidpointRounding.AwayFromZero) % labels.Length;
        return labels[index];
    }

    public void BeginTargetSearch()
    {
        if (_isTargetSearchActive)
        {
            return;
        }

        _isTargetSearchActive = true;
        OnPropertyChanged(nameof(IsTargetListVisible));
    }

    public void BeginStarSearch()
    {
        if (_isStarSearchActive)
        {
            return;
        }

        _isStarSearchActive = true;
        OnPropertyChanged(nameof(IsStarListVisible));
    }

    public void EndTargetSearch()
    {
        _isTargetSearchActive = false;
        OnPropertyChanged(nameof(IsTargetListVisible));
    }

    public void EndStarSearch()
    {
        _isStarSearchActive = false;
        OnPropertyChanged(nameof(IsStarListVisible));
    }

    public void SelectTarget(CatalogTarget target)
    {
        SelectedTarget = target;
        TargetSearchText = string.Empty;
        EndTargetSearch();

        if (_selectedStar is null)
        {
            ApplySuggestedStarSearchText(target);
        }
    }

    public void SelectStar(CatalogStar star)
    {
        SelectedStar = star;
        StarSearchText = string.Empty;
        _lastSuggestedStarText = string.Empty;
        EndStarSearch();
    }

    private void ClearTarget()
    {
        SelectedTarget = null;
        TargetSearchText = string.Empty;
        EndTargetSearch();

        if (_selectedStar is null)
        {
            StarSearchText = string.Empty;
            _lastSuggestedStarText = string.Empty;
            EndStarSearch();
        }

        HasResult = false;
        ResultText = string.Empty;
    }

    private void ClearStar()
    {
        SelectedStar = null;

        if (_selectedTarget is not null)
        {
            ApplySuggestedStarSearchText(_selectedTarget);
        }
        else
        {
            StarSearchText = string.Empty;
            _lastSuggestedStarText = string.Empty;
            EndStarSearch();
        }
    }

    private async Task BuildStarHopMapAsync()
    {
        if (_selectedTarget is null) return;

        var targetCoord = new EquatorialCoordinate(_selectedTarget.RightAscensionHours, _selectedTarget.DeclinationDeg);

        // When the user explicitly chose a reference star, prefer an asterism that
        // contains that star so the map pattern matches their chosen starting point.
        // If the selected star belongs to no asterism, use it as a standalone anchor
        // (empty asterism — no pattern lines drawn). Only fall back to auto-selection
        // when no reference star has been chosen.
        AnchorResult? anchor;
        CatalogStar referenceStar;

        if (_selectedStar is not null)
        {
            referenceStar = _selectedStar;
            anchor = _selectedStar.HipparcosId.HasValue
                ? _anchorSelector.FindAnchorContainingStar(
                    _selectedStar.HipparcosId.Value,
                    _selectedStar,
                    targetCoord,
                    _catalog.GetAsterisms())
                : null;

            // If the chosen star belongs to no asterism, synthesise a bare anchor so
            // the rest of the pipeline can continue without an asterism pattern.
            anchor ??= new AnchorResult
            {
                Asterism = new AstroApps.Equipment.Profiles.Models.CatalogAsterism
                {
                    Id = "user-selected",
                    DisplayName = referenceStar.DisplayName,
                },
                AnchorStar = referenceStar,
                AngularDistanceDegrees = AstroFinder.Engine.Geometry.SphericalGeometry.AngularSeparationDegrees(
                    new EquatorialCoordinate(referenceStar.RightAscensionHours, referenceStar.DeclinationDeg),
                    targetCoord),
                Score = 0.0,
            };
        }
        else
        {
            // Prefer the nearest bright star (same logic as the suggestion text) as reference
            // before falling back to asterism auto-selection.  This avoids drawing a distant
            // familiar asterism (e.g. Big Dipper for M3) when a closer bright star such as
            // Arcturus provides a shorter, cleaner route.  A bare anchor (no StarIds/Segments)
            // is synthesised so no asterism pattern is drawn on the map.
            var autoStar = FindSuggestedStar(_selectedTarget!);
            if (autoStar is not null)
            {
                referenceStar = autoStar;
                anchor = new AnchorResult
                {
                    Asterism = new AstroApps.Equipment.Profiles.Models.CatalogAsterism
                    {
                        Id = "auto-suggested",
                        DisplayName = autoStar.DisplayName,
                    },
                    AnchorStar = autoStar,
                    AngularDistanceDegrees = AstroFinder.Engine.Geometry.SphericalGeometry.AngularSeparationDegrees(
                        new EquatorialCoordinate(autoStar.RightAscensionHours, autoStar.DeclinationDeg),
                        targetCoord),
                    Score = 0.0,
                };
            }
            else
            {
                anchor = _anchorSelector.FindBestAnchor(
                    targetCoord,
                    _catalog.GetAsterisms(),
                    _catalog.FindStarByHipparcosId);

                if (anchor is null)
                {
                    ResultText = $"No nearby asterism found for {_selectedTarget.DisplayName}.\n\n" +
                                 $"Target position: RA {FormatRa(_selectedTarget.RightAscensionHours)}, Dec {FormatDec(_selectedTarget.DeclinationDeg)}";
                    HasResult = true;
                    return;
                }

                referenceStar = anchor.AnchorStar;
            }
        }
        var useObserverOrientationRequested = _observerOrientationService.IsLocationOrientationEnabled;
        var observerContext = await _observerOrientationService.TryGetObserverContextAsync();

        // Move the heavy CPU work (trig over 8870 stars) off the main thread to prevent ANR.
        // Hop waypoints use named stars only — HIP-numbered entries are not identifiable by users.
        var namedStars = _searchableStars;
        var allStars = _allStars;
        var (route, mapData) = await Task.Run(() =>
        {
            var r = _hopGenerator.GenerateRoute(referenceStar, _selectedTarget!, namedStars);
            var md = BuildStarMapData(anchor, referenceStar, r, targetCoord, observerContext, useObserverOrientationRequested);
            return (r, md);
        });

        if (mapData is not null)
        {
            ShowStarMap?.Invoke(mapData);
            return;
        }

        var referenceCoord = new EquatorialCoordinate(referenceStar.RightAscensionHours, referenceStar.DeclinationDeg);
        var relativeToReference = SphericalGeometry.ComputeRelativePosition(referenceCoord, targetCoord);

        var lines = new List<string>
        {
            $"Star Hop Map: {_selectedTarget.DisplayName}",
            $"Reference star: {referenceStar.DisplayName}",
            $"Target from reference: {relativeToReference.AngularSeparationDegrees:F1}° {FormatSkyDirection(relativeToReference.PositionAngleDegrees)}",
            $"Pattern context: {anchor.Asterism.DisplayName} → {anchor.AnchorStar.DisplayName}",
            ""
        };

        if (route.Steps.Count == 0)
        {
            lines.Add($"Direct hop — target is near {referenceStar.DisplayName}.");
        }
        else
        {
            for (int i = 0; i < route.Steps.Count; i++)
            {
                lines.Add($"  {i + 1}. {route.Steps[i].Instruction}");
            }
            lines.Add($"\nTotal route: {route.TotalAngularDistanceDegrees:F1}°");
        }

        ResultText = string.Join("\n", lines);
        HasResult = true;
    }

    private StarMapData? BuildStarMapData(
        AnchorResult anchor,
        CatalogStar referenceStar,
        HopRoute route,
        EquatorialCoordinate targetCoord,
        ObserverOrientationContext? observerContext,
        bool useObserverOrientationRequested)
    {
        if (_selectedTarget is null) return null;

        // Resolve asterism stars by HIP ID
        var asterismStars = new List<StarMapPoint>();
        var hipToIndex = new Dictionary<int, int>();

        foreach (var hipId in anchor.Asterism.StarIds)
        {
            var star = _catalog.FindStarByHipparcosId(hipId);
            if (star is null) continue;

            hipToIndex[hipId] = asterismStars.Count;
            asterismStars.Add(new StarMapPoint(
                star.RightAscensionHours,
                star.DeclinationDeg,
                star.VisualMagnitude,
                star.DisplayName));
        }

        // Build segment index pairs
        var segments = new List<(int From, int To)>();
        foreach (var seg in anchor.Asterism.Segments)
        {
            if (seg.Length >= 2
                && hipToIndex.TryGetValue(seg[0], out var fromIdx)
                && hipToIndex.TryGetValue(seg[1], out var toIdx))
            {
                segments.Add((fromIdx, toIdx));
            }
        }

        // Build hop steps list: anchor star first, then each hop ToStar
        var hopSteps = new List<StarMapPoint>
        {
            new(referenceStar.RightAscensionHours,
                referenceStar.DeclinationDeg,
                referenceStar.VisualMagnitude,
                referenceStar.DisplayName)
        };

        foreach (var step in route.Steps)
        {
            hopSteps.Add(new StarMapPoint(
                step.ToStar.RightAscensionHours,
                step.ToStar.DeclinationDeg,
                step.ToStar.VisualMagnitude,
                step.ToStar.DisplayName));
        }

        // Collect a richer set of nearby context stars so the plotted field better resembles the real sky.
        var keyCoordinates = asterismStars
            .Concat(hopSteps)
            .Append(new StarMapPoint(targetCoord.RaHours, targetCoord.DecDegrees, _selectedTarget.VisualMagnitude ?? 10.0, null))
            .Select(point => new EquatorialCoordinate(point.RaHours, point.DecDeg))
            .ToList();

        var maxKeySeparation = keyCoordinates
            .Select(coord => SphericalGeometry.AngularSeparationDegrees(targetCoord, coord))
            .DefaultIfEmpty(0.0)
            .Max();

        var contextFieldRadius = Math.Clamp(maxKeySeparation + 6.0, 10.0, 28.0);
        var extendedBrightFieldRadius = Math.Clamp(contextFieldRadius * 1.25, 12.0, 36.0);

        // Set of HIP IDs already drawn as asterism/hop stars.
        var drawnHipIds = new HashSet<int>(anchor.Asterism.StarIds);
        if (referenceStar.HipparcosId.HasValue)
            drawnHipIds.Add(referenceStar.HipparcosId.Value);

        foreach (var step in route.Steps)
        {
            if (step.ToStar.HipparcosId.HasValue)
                drawnHipIds.Add(step.ToStar.HipparcosId.Value);
            if (step.FromStar.HipparcosId.HasValue)
                drawnHipIds.Add(step.FromStar.HipparcosId.Value);
        }

        // Two-step background star selection so we can track which HIP IDs are already plotted.
        var selectedBackgroundEntries = _catalog.GetStars()
            .Where(s => !s.HipparcosId.HasValue || !drawnHipIds.Contains(s.HipparcosId.Value))
            .Select(s =>
            {
                var coord = new EquatorialCoordinate(s.RightAscensionHours, s.DeclinationDeg);
                var distanceFromTarget = SphericalGeometry.AngularSeparationDegrees(targetCoord, coord);
                return (Star: s, DistanceFromTarget: distanceFromTarget);
            })
            .Where(x =>
                (x.Star.VisualMagnitude <= 6.2 && x.DistanceFromTarget <= contextFieldRadius) ||
                (x.Star.VisualMagnitude <= 3.2 && x.DistanceFromTarget <= extendedBrightFieldRadius))
            .OrderBy(x => x.Star.VisualMagnitude)
            .ThenBy(x => x.DistanceFromTarget)
            .Take(140)
            .ToList();

        var backgroundStars = selectedBackgroundEntries
            .Select(x => new StarMapPoint(x.Star.RightAscensionHours, x.Star.DeclinationDeg, x.Star.VisualMagnitude, null))
            .ToList();

        // Additional field stars that fill the rendered map viewport rectangle.
        // The map is centered on the average of all key coords and the viewport covers a
        // rectangular region that extends ~√2 × maxSep from centre (rectangle corners) plus
        // 18 % padding.  Use 1.7× to reliably cover those corners without an expensive exact
        // projection computation here.
        var mapCenterRaHours = keyCoordinates.Average(c => c.RaHours);
        var mapCenterDecDeg = keyCoordinates.Average(c => c.DecDegrees);
        var mapCenter = new EquatorialCoordinate(mapCenterRaHours, mapCenterDecDeg);

        var maxSepFromMapCenter = keyCoordinates
            .Select(coord => SphericalGeometry.AngularSeparationDegrees(mapCenter, coord))
            .DefaultIfEmpty(0.0)
            .Max();

        var mapFillRadius = Math.Max(maxSepFromMapCenter * 1.7, 8.0);

        var nearbyTargets = _catalog.GetTargets()
            .Where(t => !string.Equals(t.Id, _selectedTarget.Id, StringComparison.OrdinalIgnoreCase))
            .Select(t =>
            {
                var coord = new EquatorialCoordinate(t.RightAscensionHours, t.DeclinationDeg);
                return (Target: t, Dist: SphericalGeometry.AngularSeparationDegrees(mapCenter, coord));
            })
            .Where(x => x.Dist <= mapFillRadius)
            .Select(x => new StarMapNearbyTarget(
                x.Target.RightAscensionHours,
                x.Target.DeclinationDeg,
                x.Target.VisualMagnitude ?? 10.0,
                x.Target.DisplayName,
                x.Target.Category,
                x.Target.PositionAngleDeg))
            .ToList();

        var allPlottedIds = new HashSet<int>(drawnHipIds);
        foreach (var entry in selectedBackgroundEntries)
        {
            if (entry.Star.HipparcosId.HasValue)
                allPlottedIds.Add(entry.Star.HipparcosId.Value);
        }

        var mapFillStars = _catalog.GetStars()
            .Where(s => !s.HipparcosId.HasValue || !allPlottedIds.Contains(s.HipparcosId.Value))
            .Select(s =>
            {
                var coord = new EquatorialCoordinate(s.RightAscensionHours, s.DeclinationDeg);
                var distFromCenter = SphericalGeometry.AngularSeparationDegrees(mapCenter, coord);
                return (Star: s, DistFromCenter: distFromCenter);
            })
            .Where(x => x.Star.VisualMagnitude <= 7.5 && x.DistFromCenter <= mapFillRadius)
            .OrderBy(x => x.Star.VisualMagnitude)
            .ThenBy(x => x.DistFromCenter)
            .Take(300)
            .Select(x => new StarMapPoint(x.Star.RightAscensionHours, x.Star.DeclinationDeg, x.Star.VisualMagnitude, null))
            .ToList();

        var invertParallacticAngleForDisplay = _observerOrientationService.InvertParallacticAngleForDisplay;
        SkyOrientationResult? orientation = null;
        if (observerContext is not null)
        {
            orientation = AstroFinder.Engine.Geometry.SkyOrientationService.GetChartOrientation(
                new ObserverLocation(observerContext.LatitudeDegrees, observerContext.LongitudeDegrees),
                observerContext.ObservationTime.UtcDateTime,
                targetCoord,
                invertParallacticAngleForDisplay);
        }

        var orientationSummary = orientation is not null
            ? BuildOrientationSummary(observerContext!, orientation.Value, invertParallacticAngleForDisplay)
            : useObserverOrientationRequested
                ? "North-up chart • location unavailable"
                : "North-up chart";

        return new StarMapData
        {
            Title = $"Star Hop: {_selectedTarget.DisplayName}",
            Target = new StarMapPoint(
                _selectedTarget.RightAscensionHours,
                _selectedTarget.DeclinationDeg,
                _selectedTarget.VisualMagnitude ?? 10.0,
                _selectedTarget.DisplayName),
            AsterismName = anchor.Asterism.DisplayName,
            ReferenceLabel = referenceStar.DisplayName,
            AsterismStars = asterismStars,
            AsterismSegments = segments,
            HopSteps = hopSteps,
            BackgroundStars = backgroundStars,
            MapFillStars = mapFillStars,
            UseObserverOrientation = observerContext is not null,
            ObserverLatitudeDeg = observerContext?.LatitudeDegrees,
            ObserverLongitudeDeg = observerContext?.LongitudeDegrees,
            ObservationTime = observerContext?.ObservationTime ?? DateTimeOffset.Now,
            DisplayRotationDegrees = orientation?.DisplayRotationDegrees ?? 0.0,
            InvertParallacticAngleForDisplay = invertParallacticAngleForDisplay,
            IsTargetAboveHorizon = orientation?.IsAboveHorizon ?? true,
            IsNearZenithSensitive = orientation?.IsNearZenithSensitive ?? false,
            OrientationSummary = orientationSummary,
            TargetCategory = _selectedTarget.Category,
            TargetPositionAngleDeg = _selectedTarget.PositionAngleDeg,
            NearbyTargets = nearbyTargets,
            LabelScale = _labelScaleService.LabelScale
        };
    }

    private static string BuildOrientationSummary(
        ObserverOrientationContext observerContext,
        SkyOrientationResult orientation,
        bool invertParallacticAngleForDisplay)
    {
        var horizonSummary = orientation.IsAboveHorizon
            ? $"alt {orientation.AltitudeDegrees:F1}°"
            : $"below horizon ({orientation.AltitudeDegrees:F1}°)";

        var zenithSummary = orientation.IsNearZenithSensitive
            ? " • near zenith: rotation can change quickly"
            : string.Empty;

        var signSummary = invertParallacticAngleForDisplay ? " • -q" : " • q";

        return $"Sky-oriented • rot {orientation.DisplayRotationDegrees:F1}°{signSummary} • q {orientation.ParallacticAngleDegrees:F1}° • {horizonSummary} • {FormatLatitude(observerContext.LatitudeDegrees)}, {FormatLongitude(observerContext.LongitudeDegrees)} • {observerContext.ObservationTime.LocalDateTime:HH:mm}{zenithSummary}";
    }

    private void ShowDeltas()
    {
        if (_selectedTarget is null || _selectedStar is null) return;

        var refCoord = new EquatorialCoordinate(_selectedStar.RightAscensionHours, _selectedStar.DeclinationDeg);
        var targetCoord = new EquatorialCoordinate(_selectedTarget.RightAscensionHours, _selectedTarget.DeclinationDeg);

        var rel = SphericalGeometry.ComputeRelativePosition(refCoord, targetCoord);

        var deltaHaHours = -rel.DeltaRaDegrees / 15.0;

        var deltaText = $"Deltas: {_selectedStar.DisplayName} → {_selectedTarget.DisplayName}\n\n" +
                        $"ΔRA:  {FormatDelta(rel.DeltaRaDegrees)}\n" +
                        $"ΔDec: {FormatDelta(rel.DeltaDecDegrees)}\n" +
                        $"ΔHA (RA move): {FormatSignedHours(deltaHaHours)}\n" +
                        "HA is the RA-side move in mount time units (1.0h ≈ 15°).\n\n" +
                        $"Separation: {rel.AngularSeparationDegrees:F2}°\n" +
                        $"Position angle: {rel.PositionAngleDegrees:F1}° (N through E)";

        ResultText = deltaText;
        HasResult = true;

        ShowDeltasFlyout?.Invoke(new RaDecDeltaFlyoutData(
            Title: "RA/Dec Deltas",
            Details: deltaText,
            ReferenceStarName: _selectedStar.DisplayName,
            TargetName: _selectedTarget.DisplayName,
            DeltaRaDegrees: rel.DeltaRaDegrees,
            DeltaDecDegrees: rel.DeltaDecDegrees,
            SelectedMountDisplayName: _manualGotoCalibrationService.GetSelectedMountName(),
            IsManualGotoEnabled: IsManualGotoTemporarilyEnabled()));
    }

    private static bool IsManualGotoTemporarilyEnabled()
    {
        // Phase 1 temporary behavior: always enabled until multiple built-in mount
        // profiles are available and mount-specific gating can be enforced.
        return true;
    }

    private bool HasSelectedMount() =>
        !string.IsNullOrWhiteSpace(_manualGotoCalibrationService.GetSelectedMountName());

    private static string FormatRa(double hours)
    {
        int h = (int)hours;
        double remainder = (hours - h) * 60.0;
        int m = (int)remainder;
        double s = (remainder - m) * 60.0;
        return $"{h}h {m}m {s:F1}s";
    }

    private static string FormatDec(double degrees)
    {
        string sign = degrees >= 0 ? "+" : "-";
        double abs = Math.Abs(degrees);
        int d = (int)abs;
        double remainder = (abs - d) * 60.0;
        int m = (int)remainder;
        double s = (remainder - m) * 60.0;
        return $"{sign}{d}° {m}' {s:F1}\"";
    }

    private static string FormatDelta(double degrees) =>
        degrees >= 0 ? $"+{degrees:F1}°" : $"{degrees:F1}°";

    private static string FormatSignedHours(double hours) =>
        hours >= 0 ? $"+{hours:F1}h" : $"{hours:F1}h";

    private static string FormatLatitude(double latitudeDegrees) =>
        $"{Math.Abs(latitudeDegrees):F1}°{(latitudeDegrees >= 0 ? "N" : "S")}";

    private static string FormatLongitude(double longitudeDegrees) =>
        $"{Math.Abs(longitudeDegrees):F1}°{(longitudeDegrees >= 0 ? "E" : "W")}";

    /// <summary>
    /// Returns search variants for a query, including Messier number normalization.
    /// "M81" yields ["M81", "M081"] so both padded and unpadded forms match.
    /// </summary>
    private static string[] GetSearchVariants(string query)
    {
        if (query.Length >= 2
            && query[0] is 'M' or 'm'
            && int.TryParse(query.AsSpan(1), out var num)
            && num > 0 && num <= 110)
        {
            var padded = $"M{num:D3}";
            var unpadded = $"M{num}";
            return padded.Equals(unpadded, StringComparison.Ordinal)
                ? [query]
                : [query, padded, unpadded];
        }

        return [query];
    }

    private static int GetTargetMatchRank(CatalogTarget target, IReadOnlyList<string> queries)
    {
        var best = int.MaxValue;

        foreach (var query in queries)
        {
            best = Math.Min(best, GetTextMatchRank(target.DisplayName, query, preferStartsWith: true));
            best = Math.Min(best, GetTextMatchRank(target.Id, query));

            foreach (var alias in target.Aliases)
            {
                best = Math.Min(best, GetTextMatchRank(alias, query));
            }
        }

        return best;
    }

    private static int GetStarMatchRank(CatalogStar star, string query)
    {
        // Exact / prefix / contains pass (existing tiers 0-4)
        var best = GetTextMatchRank(star.DisplayName, query, preferStartsWith: true);
        best = Math.Min(best, GetTextMatchRank(star.Id, query));
        best = Math.Min(best, GetTextMatchRank(star.BayerDesignation, query));

        // "HIP 12345" explicit search
        if (star.HipparcosId.HasValue)
            best = Math.Min(best, GetTextMatchRank($"HIP {star.HipparcosId}", query));

        foreach (var alias in star.Aliases)
            best = Math.Min(best, GetTextMatchRank(alias, query));

        // Fuzzy tier (rank 5) — token-based partial overlap.
        // Catches "24 UMa" → "24 Ursae Majoris", "alp UMa" → "Alpha Ursae Majoris",
        // and typos like "Betelgese".
        if (best == int.MaxValue)
        {
            var fuzzyRank = GetStarFuzzyRank(star, query);
            best = Math.Min(best, fuzzyRank);
        }

        return best;
    }

    /// <summary>
    /// Fuzzy rank: returns 5 (weak match) or int.MaxValue (no match).
    /// Checks all tokens in the query appear as substrings within display name / aliases,
    /// plus a bigram overlap fallback for single-token queries (handles minor typos).
    /// </summary>
    private static int GetStarFuzzyRank(CatalogStar star, string query)
    {
        var qTokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (qTokens.Length == 0)
            return int.MaxValue;

        // Candidates: displayName + all aliases
        var candidates = new List<string>(star.Aliases.Count + 2)
        {
            star.DisplayName,
            star.Id,
        };
        if (!string.IsNullOrWhiteSpace(star.BayerDesignation))
            candidates.Add(star.BayerDesignation);
        if (star.HipparcosId.HasValue)
            candidates.Add($"HIP {star.HipparcosId}");
        candidates.AddRange(star.Aliases);

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            // All query tokens must appear somewhere in this candidate
            bool allMatch = qTokens.All(t =>
                candidate.Contains(t, StringComparison.OrdinalIgnoreCase));
            if (allMatch)
                return 5;

            // Single-token query: bigram overlap ≥ 50% catches minor typos
            if (qTokens.Length == 1 && BigramOverlap(qTokens[0], candidate) >= 0.5)
                return 6;
        }

        return int.MaxValue;
    }

    private static double BigramOverlap(string a, string b)
    {
        static HashSet<string> Bigrams(string s)
        {
            s = s.ToLowerInvariant();
            var set = new HashSet<string>();
            for (var i = 0; i < s.Length - 1; i++)
                set.Add(s.Substring(i, 2));
            return set;
        }

        var ba = Bigrams(a);
        var bb = Bigrams(b);
        if (ba.Count == 0 || bb.Count == 0)
            return 0;
        var shared = ba.Count(x => bb.Contains(x));
        return (2.0 * shared) / (ba.Count + bb.Count);
    }

    private static int GetTextMatchRank(string? text, string query, bool preferStartsWith = false)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(query))
        {
            return int.MaxValue;
        }

        if (string.Equals(text, query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (preferStartsWith && text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (preferStartsWith && text.Contains($" {query}", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (text.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        return int.MaxValue;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed record NearbyTargetSuggestion(
    CatalogTarget Target,
    double SeparationDegrees,
    double PositionAngleDegrees,
    string DirectionText,
    int HopCount,
    double EaseScore,
    string EaseLabel,
    string SummaryText)
{
    public string DisplayName => Target.DisplayName;
    public string CategoryText => Target.Category.ToString();
}
