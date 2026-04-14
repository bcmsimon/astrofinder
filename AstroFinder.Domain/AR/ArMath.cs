namespace AstroFinder.Domain.AR;

/// <summary>
/// Pure, deterministic AR math ported from the reference AstronomyMath implementation.
///
/// <para><b>Coordinate conventions:</b></para>
/// <list type="bullet">
///   <item>World frame (ENU): +X east, +Y north, +Z up.</item>
///   <item>Camera frame: +X screen-right, +Y screen-up, +Z forward through back camera.</item>
///   <item>Screen frame: origin top-left, +X right, +Y down.</item>
/// </list>
/// </summary>
public static class ArMath
{
    internal const double DegToRad = Math.PI / 180.0;
    internal const double RadToDeg = 180.0 / Math.PI;
    private const double HoursToRad = Math.PI / 12.0;

    /// <summary>
    /// Converts altitude/azimuth to an ENU unit vector.
    /// Azimuth is measured clockwise from north.
    /// </summary>
    public static Vec3 AltAzToEnu(double altitudeDeg, double azimuthDeg)
    {
        var alt = altitudeDeg * DegToRad;
        var az = azimuthDeg * DegToRad;
        return new Vec3(
            Math.Cos(alt) * Math.Sin(az),
            Math.Cos(alt) * Math.Cos(az),
            Math.Sin(alt)
        ).Normalized();
    }

    /// <summary>
    /// Returns true if the ENU direction is in front of the camera (cam.Z > 0).
    /// </summary>
    public static bool IsInFrontOfCamera(PoseMatrix pose, Vec3 enuDirection)
    {
        var camera = pose.Multiply(enuDirection.Normalized());
        return camera.Z > 0.0;
    }

    /// <summary>
    /// Projects an ENU direction onto the screen using NDC-based pinhole projection.
    /// Returns null if the object is behind the camera.
    /// </summary>
    public static ArScreenPoint? ProjectToScreen(
        PoseMatrix pose,
        Vec3 enuDirection,
        CameraIntrinsics intrinsics)
    {
        var camera = pose.Multiply(enuDirection.Normalized());
        if (camera.Z <= 0.0) return null;

        var halfHFov = intrinsics.HorizontalFovDeg * DegToRad / 2.0;
        var halfVFov = intrinsics.VerticalFovDeg * DegToRad / 2.0;
        var tanHalfH = Math.Tan(halfHFov);
        var tanHalfV = Math.Tan(halfVFov);

        var xNdc = camera.X / (camera.Z * tanHalfH);
        var yNdc = camera.Y / (camera.Z * tanHalfV);

        var xPx = (float)((xNdc + 1.0) * 0.5 * intrinsics.WidthPx);
        var yPx = (float)((1.0 - (yNdc + 1.0) * 0.5) * intrinsics.HeightPx);
        return new ArScreenPoint(xPx, yPx);
    }

    /// <summary>
    /// Returns true if the screen point is within the viewport bounds.
    /// </summary>
    public static bool IsWithinViewport(ArScreenPoint point, CameraIntrinsics intrinsics)
    {
        return point.XPx >= 0f && point.XPx <= intrinsics.WidthPx
            && point.YPx >= 0f && point.YPx <= intrinsics.HeightPx;
    }

    /// <summary>
    /// Returns the angular separation in degrees between two direction vectors.
    /// </summary>
    public static double AngularSeparationDeg(Vec3 a, Vec3 b)
    {
        var dot = Math.Clamp(a.Normalized().Dot(b.Normalized()), -1.0, 1.0);
        return Math.Acos(dot) * RadToDeg;
    }

    /// <summary>
    /// Computes arrow guidance for an off-screen target.
    /// Returns null when the target is within the on-target threshold.
    /// </summary>
    public static ArrowGuidance? OffscreenArrowGuidance(
        PoseMatrix pose,
        Vec3 targetEnu,
        double thresholdDeg = 1.5)
    {
        var targetCam = pose.Multiply(targetEnu.Normalized());
        var centerCam = new Vec3(0.0, 0.0, 1.0);
        var errorDeg = AngularSeparationDeg(centerCam, targetCam);
        if (errorDeg <= thresholdDeg) return null;

        var horizontalHint = Math.Atan2(targetCam.X, targetCam.Z) * RadToDeg;
        var verticalHint = Math.Atan2(targetCam.Y, targetCam.Z) * RadToDeg;
        var angle = (float)Math.Atan2(verticalHint, horizontalHint);

        return new ArrowGuidance(angle, errorDeg, horizontalHint, verticalHint);
    }

    /// <summary>
    /// Converts RA/Dec to Alt/Az for a given observer location and time.
    /// </summary>
    public static (double AltitudeDeg, double AzimuthDeg) RaDecToAltAz(
        double raHours,
        double decDeg,
        double latitudeDeg,
        double longitudeDeg,
        long utcMillis)
    {
        var lat = latitudeDeg * DegToRad;
        var dec = decDeg * DegToRad;
        var lst = LocalSiderealTimeRad(longitudeDeg, utcMillis);
        var hourAngle = NormalizeRadians(lst - raHours * HoursToRad);

        var sinAlt = Math.Sin(lat) * Math.Sin(dec) + Math.Cos(lat) * Math.Cos(dec) * Math.Cos(hourAngle);
        var alt = Math.Asin(Math.Clamp(sinAlt, -1.0, 1.0));

        var y = -Math.Cos(dec) * Math.Sin(hourAngle);
        var x = Math.Sin(dec) * Math.Cos(lat) - Math.Cos(dec) * Math.Sin(lat) * Math.Cos(hourAngle);
        var az = NormalizeRadians(Math.Atan2(y, x));

        return (alt * RadToDeg, az * RadToDeg);
    }

    /// <summary>
    /// Julian Date from UTC milliseconds since Unix epoch.
    /// </summary>
    public static double JulianDate(long utcMillis) =>
        utcMillis / 86_400_000.0 + 2440587.5;

    /// <summary>
    /// Local sidereal time in radians.
    /// </summary>
    public static double LocalSiderealTimeRad(double longitudeDeg, long utcMillis)
    {
        var jd = JulianDate(utcMillis);
        var t = (jd - 2451545.0) / 36525.0;
        var gmstDeg = 280.46061837
            + 360.98564736629 * (jd - 2451545.0)
            + 0.000387933 * t * t
            - t * t * t / 38710000.0;
        var lstDeg = gmstDeg + longitudeDeg;
        return NormalizeRadians(lstDeg * DegToRad);
    }

    /// <summary>
    /// Normalizes an angle in radians to [0, 2π).
    /// </summary>
    public static double NormalizeRadians(double angle)
    {
        var result = angle % (2.0 * Math.PI);
        if (result < 0.0) result += 2.0 * Math.PI;
        return result;
    }

    /// <summary>
    /// Smooths a pose matrix using element-wise EMA + Gram-Schmidt re-orthonormalization.
    /// </summary>
    public static PoseMatrix SmoothPose(PoseMatrix? previous, PoseMatrix current, double alpha = 0.15)
    {
        if (previous is null) return current;
        var prev = previous.Value;
        var output = new double[9];
        for (int i = 0; i < 9; i++)
        {
            output[i] = prev.M[i] + alpha * (current.M[i] - prev.M[i]);
        }
        return new PoseMatrix(output).Orthonormalized();
    }
}
