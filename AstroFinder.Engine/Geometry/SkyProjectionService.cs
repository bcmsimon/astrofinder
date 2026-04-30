using System.Drawing;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Engine.Geometry;

/// <summary>
/// Gnomonic (tangent-plane) projection from equatorial RA/Dec coordinates onto
/// a normalised 2-D screen given the observer's sensor-fused pointing vector.
///
/// Algorithm:
///  1. Compute LST from observer UTC + longitude.
///  2. Convert RA/Dec → Alt/Az using standard equatorial-to-horizontal conversion.
///  3. Compute angular offsets Δaz, Δalt from the pointing vector to the object.
///  4. Apply gnomonic projection: x = tan(Δaz),  y = tan(Δalt) / cos(Δaz).
///  5. Normalise by tan(fov/2) → screen range [-1, 1].
///  6. Map to [0, 1] screen space (Y inverted: screen top = sky up).
///  7. Return null when the result is outside the field of view.
/// </summary>
public sealed class SkyProjectionService : ISkyProjectionService
{
    private const double Deg2Rad = Math.PI / 180.0;

    /// <inheritdoc/>
    public PointF? Project(
        double raDegrees,
        double decDegrees,
        SkyOrientation orientation,
        ObserverContext observer,
        double fovDegrees)
    {
        // Step 1-3: RA/Dec → Alt/Az for this observer and time.
        var coordEq = EquatorialCoordinate.FromDegrees(raDegrees, decDegrees);
        var observerLocation = new ObserverLocation(observer.LatitudeDegrees, observer.LongitudeDegrees);
        var objHoriz = SkyOrientationService.GetHorizontalCoordinate(observerLocation, coordEq, observer.UtcTime);

        // Step 4: angular offsets in degrees from pointing vector to object.
        var deltaAzDeg = NormalizeSignedDegrees(objHoriz.AzimuthDegrees - orientation.AzimuthDegrees);
        var deltaAltDeg = objHoriz.AltitudeDegrees - orientation.AltitudeDegrees;

        // Guard: object is more than 90° away — behind the observer.
        if (Math.Abs(deltaAzDeg) >= 90.0 || Math.Abs(deltaAltDeg) >= 90.0)
            return null;

        // Step 5: gnomonic projection (radians).
        var deltaAzRad = deltaAzDeg * Deg2Rad;
        var deltaAltRad = deltaAltDeg * Deg2Rad;
        var cosDeltaAz = Math.Cos(deltaAzRad);

        if (cosDeltaAz <= 0.0)
            return null;

        var xGnomonic = Math.Tan(deltaAzRad);
        var yGnomonic = Math.Tan(deltaAltRad) / cosDeltaAz;

        // Step 6: normalise by tan(fov/2) → [-1, 1].
        var halfFovRad = (fovDegrees / 2.0) * Deg2Rad;
        var scale = Math.Tan(halfFovRad);

        if (scale <= 0.0)
            return null;

        var xNorm = xGnomonic / scale;
        var yNorm = yGnomonic / scale;

        // Step 7: return null when outside field of view.
        // Horizontal bound: |x| > 1.  Vertical: use a generous 1.5x to allow for
        // portrait/landscape aspect ratios (the GraphicsView clips any excess).
        if (xNorm < -1.0 || xNorm > 1.0)
            return null;
        if (yNorm < -1.5 || yNorm > 1.5)
            return null;

        // Map [-1, 1] → [0, 1].  Invert Y: positive altitude = upper screen.
        var screenX = (float)((xNorm + 1.0) / 2.0);
        var screenY = (float)((1.0 - yNorm) / 2.0);

        return new PointF(screenX, screenY);
    }

    /// <inheritdoc/>
    public float? GetGuideAngleDegrees(
        double raDegrees,
        double decDegrees,
        SkyOrientation orientation,
        ObserverContext observer)
    {
        var coordEq = EquatorialCoordinate.FromDegrees(raDegrees, decDegrees);
        var observerLocation = new ObserverLocation(observer.LatitudeDegrees, observer.LongitudeDegrees);
        var objHoriz = SkyOrientationService.GetHorizontalCoordinate(observerLocation, coordEq, observer.UtcTime);

        var deltaAzDeg = NormalizeSignedDegrees(objHoriz.AzimuthDegrees - orientation.AzimuthDegrees);
        var deltaAltDeg = objHoriz.AltitudeDegrees - orientation.AltitudeDegrees;

        // Clockwise-from-top screen angle:
        //   East  (positive deltaAz)  = right  = +90°
        //   Up    (positive deltaAlt) = top    =   0°
        var angleRad = Math.Atan2(deltaAzDeg, deltaAltDeg);
        var angleDeg = angleRad * (180.0 / Math.PI);
        return (float)((angleDeg + 360.0) % 360.0);
    }

    private static double NormalizeSignedDegrees(double degrees)
    {
        degrees %= 360.0;
        if (degrees > 180.0) degrees -= 360.0;
        else if (degrees < -180.0) degrees += 360.0;
        return degrees;
    }
}
