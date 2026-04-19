namespace AstroFinder.Engine.Primitives;

/// <summary>
/// Immutable double-precision 2D point used for chart-space math.
/// </summary>
public readonly record struct PointD(double X, double Y);