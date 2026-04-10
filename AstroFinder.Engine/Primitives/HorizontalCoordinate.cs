namespace AstroFinder.Engine.Primitives;

/// <summary>
/// A horizontal sky coordinate for a specific observer and time.
/// Altitude is degrees above the horizon. Azimuth is degrees clockwise from north.
/// </summary>
public readonly record struct HorizontalCoordinate(double AltitudeDegrees, double AzimuthDegrees);
