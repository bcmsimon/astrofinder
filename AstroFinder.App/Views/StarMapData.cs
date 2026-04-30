using AstroApps.Equipment.Profiles.Enums;

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
/// A nearby catalog target visible within the current map viewport.
/// </summary>
public sealed record StarMapNearbyTarget(
    double RaHours,
    double DecDeg,
    double Magnitude,
    string? Label,
    ShootingTargetCategory Category,
    double? PositionAngleDeg);

/// <summary>
/// All data needed to render a star hop map.
/// </summary>
public sealed class StarMapData
{
    public required string Title { get; init; }
    public required StarMapPoint Target { get; init; }
    public required IReadOnlyList<StarMapPoint> AsterismStars { get; init; }
    public required string AsterismName { get; init; }
    public string ReferenceLabel { get; init; } = string.Empty;

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
    /// Additional field stars covering the full rendered map viewport rectangle (including
    /// corners not reached by the circular background-star query). Shown in the 2D star map
    /// only — not used in the AR overlay.
    /// </summary>
    public required IReadOnlyList<StarMapPoint> MapFillStars { get; init; }

    /// <summary>
    /// Whether the map is rotated into the observer's local sky orientation.
    /// </summary>
    public bool UseObserverOrientation { get; init; }

    public double? ObserverLatitudeDeg { get; init; }

    public double? ObserverLongitudeDeg { get; init; }

    public DateTimeOffset ObservationTime { get; init; } = DateTimeOffset.Now;

    /// <summary>
    /// When observer orientation is enabled, the map is rotated by this display angle in degrees.
    /// This is typically either q or -q depending on screen-coordinate conventions.
    /// </summary>
    public double DisplayRotationDegrees { get; init; }

    /// <summary>
    /// Whether the display rotation should use -q instead of q.
    /// </summary>
    public bool InvertParallacticAngleForDisplay { get; init; } = true;

    /// <summary>
    /// Whether the target is above the local horizon for the supplied observer and time.
    /// </summary>
    public bool IsTargetAboveHorizon { get; init; } = true;

    /// <summary>
    /// Near the zenith the parallactic-angle interpretation becomes numerically sensitive.
    /// </summary>
    public bool IsNearZenithSensitive { get; init; }

    public string OrientationSummary { get; init; } = "North-up chart";

    /// <summary>
    /// The object-type category of the primary target (e.g. Galaxy, Nebula).
    /// </summary>
    public ShootingTargetCategory TargetCategory { get; init; }

    /// <summary>
    /// Position angle of the primary target's major axis, degrees East of North (0–180).
    /// Only meaningful when <see cref="TargetCategory"/> is <see cref="ShootingTargetCategory.Galaxy"/>.
    /// </summary>
    public double? TargetPositionAngleDeg { get; init; }

    /// <summary>
    /// Other catalog targets that fall within the current map viewport, excluding the primary target.
    /// </summary>
    public required IReadOnlyList<StarMapNearbyTarget> NearbyTargets { get; init; }

    /// <summary>
    /// Scale multiplier applied to all star-map label font sizes. 1.0 is default.
    /// </summary>
    public float LabelScale { get; init; } = 1f;
}
