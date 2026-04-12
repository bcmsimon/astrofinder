namespace AstroFinder.Domain.AR;

/// <summary>
/// All of the sky objects and observer context needed to produce one AR overlay frame.
/// </summary>
public sealed record ArRouteInput(
    double ObserverLatitudeDegrees,
    double ObserverLongitudeDegrees,
    DateTimeOffset ObservationTime,
    ArStarPoint Target,
    IReadOnlyList<ArStarPoint> AsterismStars,
    IReadOnlyList<(int From, int To)> AsterismSegments,
    /// <summary>
    /// Anchor star first (index 0), then each intermediate hop star in order.
    /// </summary>
    IReadOnlyList<ArStarPoint> HopSteps,
    IReadOnlyList<ArStarPoint> BackgroundStars);
