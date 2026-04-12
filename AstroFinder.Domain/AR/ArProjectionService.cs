using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Domain.AR;

/// <summary>
/// Converts equatorial sky coordinates into camera-frame screen positions given
/// the device's current orientation and camera field of view.
///
/// <para>
/// <b>Coordinate conventions:</b>
/// <list type="bullet">
///   <item>ENU (East-North-Up): local horizon frame. X=East, Y=North, Z=Up.</item>
///   <item>Camera frame: X=right, Y=forward (into scene), Z=up-on-screen.
///     This is a right-handed system where the camera looks along +Y.</item>
///   <item>Screen: origin at centre, +X right, +Y down (standard raster).</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Projection pipeline per sky object:</b>
/// <list type="number">
///   <item>RA/Dec → Alt/Az via <see cref="SkyProjection.EquatorialToHorizontal"/>.</item>
///   <item>Alt/Az → ENU unit vector:
///     <c>East = cos(alt)*sin(az)</c>, <c>North = cos(alt)*cos(az)</c>, <c>Up = sin(alt)</c>.</item>
///   <item>ENU → camera frame via <see cref="BuildCameraMatrix"/>.</item>
///   <item>Camera → screen pixels via pinhole projection using half-FOV tangent scale.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Camera matrix derivation</b> (heading h, pitch p, roll r — all in radians):
/// <c>R = R_roll(r) · R_pitch(p) · R_yaw(h)</c>.
/// Starting from ENU:
/// <list type="bullet">
///   <item>R_yaw: rotate around Z so that the camera looks at azimuth h.
///     Result: local X = East sin h - North cos h (right in horizontal plane at heading h),
///     local Y = East cos h + North sin h (forward horizontal), local Z = Up.</item>
///   <item>R_pitch: tilt camera up by p around the (new) X axis.
///     Result: local Y tilts to pitch p above horizontal.</item>
///   <item>R_roll: rotate around the forward (Y) axis by r.
///     Result: screen X and screen Z rotate, producing visible tilted overlay.</item>
/// </list>
/// </para>
///
/// All computation is deterministic and side-effect-free.
/// </summary>
public sealed class ArProjectionService
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    // Trace counter: log every 60th Project() call (~2 s at 30 fps) — filter with 'ARS.'.
    private static int _logCounter;

    /// <summary>
    /// Projects all sky objects in <paramref name="input"/> into screen-space coordinates
    /// for the given <paramref name="pose"/> and <paramref name="viewport"/>.
    /// </summary>
    public ArOverlayFrame Project(
        ArRouteInput input,
        DevicePose pose,
        CameraViewport viewport)
    {
        var camMatrix = pose.Quaternion is { } q
            ? BuildCameraMatrixFromQuaternion(q.qx, q.qy, q.qz, q.qw)
            : BuildCameraMatrix(
                pose.HeadingDegrees * DegToRad,
                pose.PitchDegrees   * DegToRad,
                pose.RollDegrees    * DegToRad);

        var halfFovH = viewport.HorizontalFovDegrees / 2.0 * DegToRad;
        var halfFovV = viewport.VerticalFovDegrees   / 2.0 * DegToRad;

        // Pixel-scale: a 1-radian offset maps to this many pixels from screen centre.
        var scaleH = (viewport.WidthPx  / 2.0) / Math.Tan(halfFovH);
        var scaleV = (viewport.HeightPx / 2.0) / Math.Tan(halfFovV);

        // Throttled trace — filter logcat with tag 'ARS.pose'.
        var traceThis = (System.Threading.Interlocked.Increment(ref _logCounter) % 60) == 0;
        if (traceThis)
        {
            var matPath = pose.Quaternion.HasValue ? "QUAT" : "EULER";
            System.Diagnostics.Debug.WriteLine(
                $"[ARS.pose#{_logCounter}] h={pose.HeadingDegrees:F1}° p={pose.PitchDegrees:F1}° r={pose.RollDegrees:F1}°  " +
                $"path={matPath}  " +
                $"fovH={viewport.HorizontalFovDegrees:F1}° vp={viewport.WidthPx:F0}x{viewport.HeightPx:F0}  " +
                $"camFwd(E={camMatrix[1, 0]:F3} N={camMatrix[1, 1]:F3} U={camMatrix[1, 2]:F3})");
        }

        ArProjectedPoint ProjectStar(ArStarPoint star)
        {
            var horizontal = SkyProjection.EquatorialToHorizontal(
                new EquatorialCoordinate(star.RaHours, star.DecDegrees),
                input.ObserverLatitudeDegrees,
                input.ObserverLongitudeDegrees,
                input.ObservationTime);

            // Objects below the horizon (with a small margin) are never visible.
            if (horizontal.AltitudeDegrees < -5.0)
                return new ArProjectedPoint(0, 0, false, star.Role, star.Label, star.Magnitude);

            var (screenX, screenY, visible) = ToScreen(
                horizontal.AzimuthDegrees, horizontal.AltitudeDegrees,
                camMatrix, scaleH, scaleV, viewport);

            // Log the target object specifically when tracing is enabled.
            if (traceThis && star.Role == ArOverlayRole.Target)
                System.Diagnostics.Debug.WriteLine(
                    $"[ARS.target] az={horizontal.AzimuthDegrees:F1}° alt={horizontal.AltitudeDegrees:F1}°  " +
                    $"screen=({screenX:F0},{screenY:F0}) visible={visible}");

            return new ArProjectedPoint(screenX, screenY, visible, star.Role, star.Label, star.Magnitude);
        }

        // Compute target bearing and angular distance in camera space so both the
        // bearing (pointer direction on screen) and angular distance are correctly
        // derived from the 3D geometry including roll.
        var targetHorizontal = SkyProjection.EquatorialToHorizontal(
            new EquatorialCoordinate(input.Target.RaHours, input.Target.DecDegrees),
            input.ObserverLatitudeDegrees,
            input.ObserverLongitudeDegrees,
            input.ObservationTime);

        var (bearingDeg, angularDistance) = ComputeTargetBearing(
            targetHorizontal.AzimuthDegrees, targetHorizontal.AltitudeDegrees, camMatrix);

        return new ArOverlayFrame(
            Target: ProjectStar(input.Target),
            AsterismStars: input.AsterismStars.Select(ProjectStar).ToList(),
            AsterismSegments: input.AsterismSegments,
            HopSteps: input.HopSteps.Select(ProjectStar).ToList(),
            BackgroundStars: input.BackgroundStars.Select(ProjectStar).ToList(),
            Viewport: viewport,
            TargetBearingDegrees: bearingDeg,
            TargetAngularDistanceDegrees: angularDistance);
    }

    // -------------------------------------------------------------------------
    // Camera matrix construction
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds the 3×3 rotation matrix that transforms ENU vectors into camera frame.
    ///
    /// Camera frame axes:
    ///   Row 0 (X) = screen right
    ///   Row 1 (Y) = camera forward (into scene)
    ///   Row 2 (Z) = screen up
    ///
    /// The matrix is stored row-major as [row][col] where col indexes ENU (0=E,1=N,2=U).
    /// To transform an ENU vector v: cam_component_i = Sum_j R[i][j] * v[j].
    /// </summary>
    internal static double[,] BuildCameraMatrix(double headingRad, double pitchRad, double rollRad)
    {
        double sh = Math.Sin(headingRad),  ch = Math.Cos(headingRad);
        double sp = Math.Sin(pitchRad),    cp = Math.Cos(pitchRad);
        double sr = Math.Sin(rollRad),     cr = Math.Cos(rollRad);

        // R_yaw (rotation around ENU Z=Up by heading):
        // Maps ENU to a frame where Y points toward heading h in the horizontal plane.
        //   [ sin(h)   -cos(h)   0 ]   (X = right at heading h)
        //   [ cos(h)    sin(h)   0 ]   (Y = forward at heading h)
        //   [  0         0      1 ]   (Z = up)
        // Wait — standard rotation by heading CW from North:
        //   East  component of new-Y = sin(h)
        //   North component of new-Y = cos(h)
        //   East  component of new-X = (right of heading) = new-Y rotated +90° in horizontal
        //                             = cos(h) East - (-sin(h)) North = ...
        // Let's be explicit:
        //   new-Y (forward) = (sin(h), cos(h), 0) in ENU
        //   new-X (right)   = (cos(h), -sin(h), 0) in ENU  [Y rotated CW by 90° in horiz]
        //   new-Z (up)      = (0, 0, 1)
        // R_yaw [new-axis][ENU]:
        //   row X: (ch,  -sh, 0)
        //   row Y: (sh,   ch, 0)
        //   row Z: (0,    0,  1)

        // R_pitch (rotate camera up by pitch around new-X axis):
        // new-Y tilts upward: new-Y' = cos(p)*new-Y + sin(p)*new-Z
        // new-Z tilts:        new-Z' = -sin(p)*new-Y + cos(p)*new-Z
        // new-X unchanged.
        // Combined R_pitch * R_yaw gives camera with heading h and pitch p:
        //   row X:  ( ch,   -sh,  0  )
        //   row Y:  ( sh*cp, ch*cp, sp )
        //   row Z:  (-sh*sp,-ch*sp, cp )

        // R_roll (rotate around forward Y by roll):
        // screen-X rotates: new-X' =  cos(r)*X + sin(r)*Z
        // screen-Z rotates: new-Z' = -sin(r)*X + cos(r)*Z
        // Y unchanged.
        // Full matrix R = R_roll * R_pitch * R_yaw:
        //   row X = cos(r)*(ch, -sh, 0)       + sin(r)*(-sh*sp, -ch*sp, cp)
        //   row Y = (sh*cp, ch*cp, sp)         [unchanged by roll]
        //   row Z = -sin(r)*(ch, -sh, 0)  + cos(r)*(-sh*sp, -ch*sp, cp)

        var m = new double[3, 3];

        // Row 0 (camera X = screen right):
        m[0, 0] = (cr * ch)  + (-sr * sh * sp);   // East
        m[0, 1] = (cr * -sh) + (-sr * -ch * sp);  // North   = -cr*sh + sr*ch*sp
        m[0, 2] = sr * cp;                          // Up

        // Row 1 (camera Y = forward):
        m[1, 0] = sh * cp;   // East
        m[1, 1] = ch * cp;   // North
        m[1, 2] = sp;         // Up

        // Row 2 (camera Z = screen up):
        m[2, 0] = (-sr * ch)  + (cr * -sh * sp);  // East    = -sr*ch - cr*sh*sp
        m[2, 1] = (-sr * -sh) + (cr * -ch * sp);  // North   = sr*sh - cr*ch*sp
        m[2, 2] = cr * cp;                          // Up

        return m;
    }

    /// <summary>
    /// Builds the 3×3 camera matrix directly from a smoothed sensor quaternion,
    /// bypassing Euler-angle extraction entirely.
    ///
    /// <para>
    /// The Android rotation-vector quaternion (GAME_ROTATION_VECTOR) encodes the
    /// passive rotation from ENU space into device/camera space. The standard
    /// quaternion-to-rotation-matrix formula therefore gives M directly where
    /// M[i][j] is the i-th camera-axis component along the j-th ENU axis:
    /// <list type="bullet">
    ///   <item>Row 0 (screen right) = device-X in ENU</item>
    ///   <item>Row 1 (forward / phone top) = device-Y in ENU</item>
    ///   <item>Row 2 (out of screen) = device-Z in ENU</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// Using the quaternion directly eliminates the Euler-angle singularity at
    /// pitch ≈ ±90° (gimbal lock when pointing the phone nearly straight up or down),
    /// which caused heading and roll to spin by tens of degrees from tiny wrist movements.
    /// The EMA smoothing applied in quaternion space by <c>AndroidOrientationService</c>
    /// also produces a smoother result than three independent angle EMAs.
    /// </para>
    /// </summary>
    internal static double[,] BuildCameraMatrixFromQuaternion(
        double qx, double qy, double qz, double qw)
    {
        var m = new double[3, 3];

        // Row 0 (camera X = screen right = device-X in ENU):
        m[0, 0] = 1.0 - 2.0 * ((qy * qy) + (qz * qz));  // East
        m[0, 1] = 2.0 * ((qx * qy) - (qw * qz));          // North
        m[0, 2] = 2.0 * ((qx * qz) + (qw * qy));          // Up

        // Row 1 (camera Y = forward = device-Y / phone top in ENU):
        m[1, 0] = 2.0 * ((qx * qy) + (qw * qz));          // East
        m[1, 1] = 1.0 - 2.0 * ((qx * qx) + (qz * qz));   // North
        m[1, 2] = 2.0 * ((qy * qz) - (qw * qx));          // Up

        // Row 2 (camera Z = out of screen = device-Z in ENU):
        m[2, 0] = 2.0 * ((qx * qz) - (qw * qy));          // East
        m[2, 1] = 2.0 * ((qy * qz) + (qw * qx));          // North
        m[2, 2] = 1.0 - 2.0 * ((qx * qx) + (qy * qy));   // Up

        return m;
    }

    // -------------------------------------------------------------------------
    // Internal geometry helpers
    // -------------------------------------------------------------------------

    private static (double screenX, double screenY, bool visible) ToScreen(
        double azimuthDeg,
        double altitudeDeg,
        double[,] camMatrix,
        double scaleH,
        double scaleV,
        CameraViewport viewport)
    {
        // Convert Alt/Az to ENU unit vector.
        var altRad = altitudeDeg * DegToRad;
        var azRad  = azimuthDeg  * DegToRad;

        var east  = Math.Cos(altRad) * Math.Sin(azRad);
        var north = Math.Cos(altRad) * Math.Cos(azRad);
        var up    = Math.Sin(altRad);

        // Transform into camera frame.
        var cx = (camMatrix[0, 0] * east) + (camMatrix[0, 1] * north) + (camMatrix[0, 2] * up);
        var cy = (camMatrix[1, 0] * east) + (camMatrix[1, 1] * north) + (camMatrix[1, 2] * up);
        var cz = (camMatrix[2, 0] * east) + (camMatrix[2, 1] * north) + (camMatrix[2, 2] * up);

        // Objects behind the camera (cy ≤ 0) are not visible.
        if (cy <= 0.0)
            return (0, 0, false);

        // Pinhole projection: divide by forward component, scale by focal-length equivalent.
        // screenX: cx positive = camera-right; negate because device-X in the Android sensor
        //          frame points physically LEFT when the phone is held face-up toward the sky
        //          (camera mirror flip relative to the display coordinate convention).
        // screenY: positive = down (screen raster), derived from -cz (cz up → screen up → negative screenY).
        var screenX = (viewport.WidthPx  / 2.0) - (cx / cy) * scaleH;
        var screenY = (viewport.HeightPx / 2.0) - (cz / cy) * scaleV;

        // 120% allows route lines to exit gracefully beyond the viewport edge.
        var halfW = viewport.WidthPx  / 2.0 * 1.2;
        var halfH = viewport.HeightPx / 2.0 * 1.2;
        var visible = Math.Abs(screenX - viewport.WidthPx  / 2.0) <= halfW
                   && Math.Abs(screenY - viewport.HeightPx / 2.0) <= halfH;

        return (screenX, screenY, visible);
    }

    /// <summary>
    /// Computes the bearing (direction on screen to move toward target, clockwise from screen up)
    /// and angular distance from the current device pointing direction to the target.
    ///
    /// Bearing is derived from the target's camera-frame coordinates (cx, cz):
    ///   0° = above screen centre (target is above), 90° = right, 180° = below, 270° = left.
    /// This is correct even when the target is behind the camera.
    /// </summary>
    private static (double bearingDeg, double angularDistanceDeg) ComputeTargetBearing(
        double azimuthDeg,
        double altitudeDeg,
        double[,] camMatrix)
    {
        var altRad = altitudeDeg * DegToRad;
        var azRad  = azimuthDeg  * DegToRad;

        var east  = Math.Cos(altRad) * Math.Sin(azRad);
        var north = Math.Cos(altRad) * Math.Cos(azRad);
        var up    = Math.Sin(altRad);

        var cx = (camMatrix[0, 0] * east) + (camMatrix[0, 1] * north) + (camMatrix[0, 2] * up);
        var cy = (camMatrix[1, 0] * east) + (camMatrix[1, 1] * north) + (camMatrix[1, 2] * up);
        var cz = (camMatrix[2, 0] * east) + (camMatrix[2, 1] * north) + (camMatrix[2, 2] * up);

        // Bearing: angle from screen-up (cz positive) clockwise to the target direction in
        // the cx/cz plane.  atan2(cx, cz) gives 0°=up, 90°=right.
        // Negate cx to match the horizontal mirror applied in ToScreen.
        var bearingDeg = (Math.Atan2(-cx, cz) * RadToDeg + 360.0) % 360.0;

        // Angular distance from the forward axis: angle between the target vector and the
        // camera Y-axis (forward direction).
        // angDist = acos(cy) since the ENU vectors are unit vectors.
        var angularDistanceDeg = Math.Acos(Math.Clamp(cy, -1.0, 1.0)) * RadToDeg;

        return (bearingDeg, angularDistanceDeg);
    }
}
