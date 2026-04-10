namespace AstroFinder.App.Views;

/// <summary>
/// A projected point on the star map canvas.
/// </summary>
public sealed record StarMapPoint(
    double RaHours,
    double DecDeg,
    double Magnitude,
    string? Label);

/// <summary>
/// All data needed to render a star hop map.
/// </summary>
public sealed class StarMapData
{
    public required string Title { get; init; }
    public required StarMapPoint Target { get; init; }
    public required IReadOnlyList<StarMapPoint> AsterismStars { get; init; }
    public required string AsterismName { get; init; }

    /// <summary>
    /// Pairs of indices into <see cref="AsterismStars"/> defining the asterism line pattern.
    /// </summary>
    public required IReadOnlyList<(int From, int To)> AsterismSegments { get; init; }

    /// <summary>
    /// Sequential hop waypoints: anchor star first, then each intermediate hop star.
    /// </summary>
    public required IReadOnlyList<StarMapPoint> HopSteps { get; init; }

    /// <summary>
    /// Nearby background stars for context.
    /// </summary>
    public required IReadOnlyList<StarMapPoint> BackgroundStars { get; init; }

    /// <summary>
    /// Whether the map is rotated into the observer's local sky orientation.
    /// </summary>
    public bool UseObserverOrientation { get; init; }

    public double? ObserverLatitudeDeg { get; init; }

    public double? ObserverLongitudeDeg { get; init; }

    public DateTimeOffset ObservationTime { get; init; } = DateTimeOffset.Now;

    public string OrientationSummary { get; init; } = "North-up chart";
}
