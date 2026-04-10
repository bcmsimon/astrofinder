using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
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
    private readonly AnchorSelector _anchorSelector = new();
    private readonly HopGenerator _hopGenerator = new();

    private IReadOnlyList<CatalogTarget> _allTargets = [];
    private IReadOnlyList<CatalogStar> _allStars = [];

    private string _targetSearchText = string.Empty;
    private string _starSearchText = string.Empty;
    private CatalogTarget? _selectedTarget;
    private CatalogStar? _selectedStar;
    private string _resultText = string.Empty;
    private bool _hasResult;
    private bool _isLoaded;

    public MainPageViewModel(AppCatalogProvider catalog, ObserverOrientationService observerOrientationService)
    {
        _catalog = catalog;
        _observerOrientationService = observerOrientationService;

        BuildMapCommand = new Command(async () => await BuildStarHopMapAsync(), () => _selectedTarget is not null);
        ShowDeltasCommand = new Command(ShowDeltas, () => _selectedTarget is not null && _selectedStar is not null);
        ClearTargetCommand = new Command(ClearTarget);
        ClearStarCommand = new Command(ClearStar);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when a star map has been computed and should be displayed.
    /// </summary>
    public event Action<StarMapData>? ShowStarMap;

    public string TargetSearchText
    {
        get => _targetSearchText;
        set
        {
            if (_targetSearchText == value) return;
            _targetSearchText = value;
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
            ((Command)ShowDeltasCommand).ChangeCanExecute();
        }
    }

    public string SelectedTargetText => _selectedTarget?.DisplayName ?? string.Empty;
    public string SelectedStarText => _selectedStar?.DisplayName ?? string.Empty;
    public bool HasSelectedTarget => _selectedTarget is not null;
    public bool HasSelectedStar => _selectedStar is not null;

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

    public bool IsTargetListVisible => !HasSelectedTarget && TargetSearchText.Length > 0 && FilteredTargets.Count > 0;
    public bool IsStarListVisible => !HasSelectedStar && StarSearchText.Length > 0 && FilteredStars.Count > 0;

    public IReadOnlyList<CatalogTarget> FilteredTargets
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_targetSearchText))
                return [];

            var q = _targetSearchText.Trim();
            var queries = GetSearchVariants(q);
            return _allTargets
                .Where(t => queries.Any(v => t.DisplayName.Contains(v, StringComparison.OrdinalIgnoreCase)
                         || t.Id.Contains(v, StringComparison.OrdinalIgnoreCase)
                         || t.Aliases.Any(a => a.Contains(v, StringComparison.OrdinalIgnoreCase))))
                .Take(20)
                .ToList();
        }
    }

    public IReadOnlyList<CatalogStar> FilteredStars
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_starSearchText))
                return [];

            var q = _starSearchText.Trim();
            return _allStars
                .Where(s => s.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                         || s.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                         || s.Aliases.Any(a => a.Contains(q, StringComparison.OrdinalIgnoreCase)))
                .Take(20)
                .ToList();
        }
    }

    public ICommand BuildMapCommand { get; }
    public ICommand ShowDeltasCommand { get; }
    public ICommand ClearTargetCommand { get; }
    public ICommand ClearStarCommand { get; }

    public async Task LoadCatalogsAsync()
    {
        if (_isLoaded) return;

        try
        {
            await _catalog.LoadAsync();
            _allTargets = _catalog.GetTargets();
            _allStars = _catalog.GetStars();
            _isLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AstroFinder] Catalog load failed: {ex}");
            ResultText = $"Failed to load catalogs: {ex.Message}";
            HasResult = true;
        }
    }

    public void SelectTarget(CatalogTarget target)
    {
        SelectedTarget = target;
        TargetSearchText = string.Empty;
        OnPropertyChanged(nameof(IsTargetListVisible));
    }

    public void SelectStar(CatalogStar star)
    {
        SelectedStar = star;
        StarSearchText = string.Empty;
        OnPropertyChanged(nameof(IsStarListVisible));
    }

    private void ClearTarget()
    {
        SelectedTarget = null;
        TargetSearchText = string.Empty;
        HasResult = false;
        ResultText = string.Empty;
    }

    private void ClearStar()
    {
        SelectedStar = null;
        StarSearchText = string.Empty;
    }

    private async Task BuildStarHopMapAsync()
    {
        if (_selectedTarget is null) return;

        var targetCoord = new EquatorialCoordinate(_selectedTarget.RightAscensionHours, _selectedTarget.DeclinationDeg);

        var anchor = _anchorSelector.FindBestAnchor(
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

        var route = _hopGenerator.GenerateRoute(anchor.AnchorStar, _selectedTarget, _catalog.GetStars());
        var useObserverOrientationRequested = _observerOrientationService.IsLocationOrientationEnabled;
        var observerContext = await _observerOrientationService.TryGetObserverContextAsync();

        var mapData = BuildStarMapData(anchor, route, targetCoord, observerContext, useObserverOrientationRequested);
        if (mapData is not null)
        {
            ShowStarMap?.Invoke(mapData);
            return;
        }

        var lines = new List<string>
        {
            $"Star Hop Map: {_selectedTarget.DisplayName}",
            $"Anchor: {anchor.Asterism.DisplayName} → {anchor.AnchorStar.DisplayName}",
            $"Distance to target: {anchor.AngularDistanceDegrees:F1}°",
            ""
        };

        if (route.Steps.Count == 0)
        {
            lines.Add("Direct hop — target is near the anchor star.");
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
            new(anchor.AnchorStar.RightAscensionHours,
                anchor.AnchorStar.DeclinationDeg,
                anchor.AnchorStar.VisualMagnitude,
                anchor.AnchorStar.DisplayName)
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
        foreach (var step in route.Steps)
        {
            if (step.ToStar.HipparcosId.HasValue)
                drawnHipIds.Add(step.ToStar.HipparcosId.Value);
            if (step.FromStar.HipparcosId.HasValue)
                drawnHipIds.Add(step.FromStar.HipparcosId.Value);
        }

        var backgroundStars = _catalog.GetStars()
            .Where(s => !s.HipparcosId.HasValue || !drawnHipIds.Contains(s.HipparcosId.Value))
            .Select(s =>
            {
                var coord = new EquatorialCoordinate(s.RightAscensionHours, s.DeclinationDeg);
                var distanceFromTarget = SphericalGeometry.AngularSeparationDegrees(targetCoord, coord);
                return new { Star = s, DistanceFromTarget = distanceFromTarget };
            })
            .Where(x =>
                (x.Star.VisualMagnitude <= 6.2 && x.DistanceFromTarget <= contextFieldRadius) ||
                (x.Star.VisualMagnitude <= 3.2 && x.DistanceFromTarget <= extendedBrightFieldRadius))
            .OrderBy(x => x.Star.VisualMagnitude)
            .ThenBy(x => x.DistanceFromTarget)
            .Take(140)
            .Select(x => new StarMapPoint(x.Star.RightAscensionHours, x.Star.DeclinationDeg, x.Star.VisualMagnitude, null))
            .ToList();

        var orientationSummary = observerContext is not null
            ? $"Sky-oriented • {FormatLatitude(observerContext.LatitudeDegrees)}, {FormatLongitude(observerContext.LongitudeDegrees)} • {observerContext.ObservationTime.LocalDateTime:HH:mm}"
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
            AsterismStars = asterismStars,
            AsterismSegments = segments,
            HopSteps = hopSteps,
            BackgroundStars = backgroundStars,
            UseObserverOrientation = observerContext is not null,
            ObserverLatitudeDeg = observerContext?.LatitudeDegrees,
            ObserverLongitudeDeg = observerContext?.LongitudeDegrees,
            ObservationTime = observerContext?.ObservationTime ?? DateTimeOffset.Now,
            OrientationSummary = orientationSummary
        };
    }

    private void ShowDeltas()
    {
        if (_selectedTarget is null || _selectedStar is null) return;

        var refCoord = new EquatorialCoordinate(_selectedStar.RightAscensionHours, _selectedStar.DeclinationDeg);
        var targetCoord = new EquatorialCoordinate(_selectedTarget.RightAscensionHours, _selectedTarget.DeclinationDeg);

        var rel = SphericalGeometry.ComputeRelativePosition(refCoord, targetCoord);

        var deltaRaMin = rel.DeltaRaDegrees * 60.0;
        var deltaDecMin = rel.DeltaDecDegrees * 60.0;

        ResultText = $"Deltas: {_selectedStar.DisplayName} → {_selectedTarget.DisplayName}\n\n" +
                     $"ΔRA:  {FormatDelta(rel.DeltaRaDegrees)} ({deltaRaMin:+0.0;-0.0} arcmin)\n" +
                     $"ΔDec: {FormatDelta(rel.DeltaDecDegrees)} ({deltaDecMin:+0.0;-0.0} arcmin)\n\n" +
                     $"Separation: {rel.AngularSeparationDegrees:F2}°\n" +
                     $"Position angle: {rel.PositionAngleDegrees:F1}° (N through E)";
        HasResult = true;
    }

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
        degrees >= 0 ? $"+{degrees:F4}°" : $"{degrees:F4}°";

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

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
