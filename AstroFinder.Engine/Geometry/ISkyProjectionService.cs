using System.Drawing;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Geometry;

/// <summary>
/// Projects equatorial catalog coordinates onto a 2-D screen given the current
/// sensor-fusion pointing vector and field of view.
/// Pure math — no platform split, fully unit-testable.
/// </summary>
public interface ISkyProjectionService
{
    /// <summary>
    /// Projects a catalog object at the given equatorial coordinates onto the screen.
    /// Returns <see langword="null"/> when the object is outside the current field of view.
    /// </summary>
    /// <param name="raDegrees">Right ascension in degrees (0–360).</param>
    /// <param name="decDegrees">Declination in degrees (-90 to +90).</param>
    /// <param name="orientation">Current pointing vector from <see cref="ISkyOrientationService"/>.</param>
    /// <param name="observer">Observer location and current UTC time.</param>
    /// <param name="fovDegrees">Horizontal field of view in degrees from <c>ICameraPreviewService</c>.</param>
    /// <returns>
    /// Normalised screen position (0,0 = top-left, 1,1 = bottom-right), or
    /// <see langword="null"/> if the object is off-screen.
    /// </returns>
    PointF? Project(
        double raDegrees,
        double decDegrees,
        SkyOrientation orientation,
        ObserverContext observer,
        double fovDegrees);

    /// <summary>
    /// Returns the screen-space guide angle in degrees (clockwise from screen top)
    /// pointing toward the given equatorial coordinates, regardless of whether the
    /// object is inside or outside the field of view.  Use this to drive a guide
    /// arrow when <see cref="Project"/> returns <see langword="null"/>.
    /// Returns <see langword="null"/> only when the sky position cannot be resolved.
    /// </summary>
    float? GetGuideAngleDegrees(
        double raDegrees,
        double decDegrees,
        SkyOrientation orientation,
        ObserverContext observer);
}
