namespace AstroFinder.Engine.Geometry;

/// <summary>
/// Sensor-fused pointing vector produced by <see cref="ISkyOrientationService"/>.
/// All angles are in degrees.
/// </summary>
public sealed class SkyOrientation
{
    /// <summary>Magnetic azimuth in degrees (0 = North, 90 = East, clockwise).</summary>
    public double AzimuthDegrees { get; init; }

    /// <summary>Altitude above the horizon in degrees, range [-90, +90].</summary>
    public double AltitudeDegrees { get; init; }

    /// <summary>Camera roll in degrees — rotation around the camera's line of sight.</summary>
    public double RollDegrees { get; init; }
}
