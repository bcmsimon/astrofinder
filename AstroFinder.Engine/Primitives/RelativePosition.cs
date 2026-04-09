namespace AstroFinder.Engine.Primitives;

/// <summary>
/// The positional relationship between a reference star and a target.
/// </summary>
public readonly record struct RelativePosition(
    double DeltaRaDegrees,
    double DeltaDecDegrees,
    double AngularSeparationDegrees,
    double PositionAngleDegrees);
