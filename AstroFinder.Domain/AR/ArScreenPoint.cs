namespace AstroFinder.Domain.AR;

/// <summary>
/// A projected screen position in pixels (origin top-left, +X right, +Y down).
/// </summary>
public readonly record struct ArScreenPoint(float XPx, float YPx);
