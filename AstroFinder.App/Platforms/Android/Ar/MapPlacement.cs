namespace AstroFinder.App.Platforms.Android.Ar;

/// <summary>
/// Computes the 4x4 model matrix that places the map bitmap quad in ARCore world space
/// so it appears at the correct sky position.
///
/// <para><b>Strategy:</b></para>
/// <list type="number">
///   <item>Convert map center RA/Dec → Alt/Az for current time and location.</item>
///   <item>Convert Alt/Az → ARCore world direction using locked heading.</item>
///   <item>Place a quad at a fixed distance along that direction.</item>
///   <item>Size the quad so it subtends the correct angular width.</item>
///   <item>Orient the quad to face the camera and align "up" with sky north.</item>
/// </list>
/// </summary>
internal static class MapPlacement
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;
    private const double HoursToRad = Math.PI / 12.0;

    /// <summary>
    /// The distance from the camera origin to the map quad, in ARCore world meters.
    /// Arbitrary since the quad size is computed to subtend the correct angle.
    /// Use a moderate distance to avoid near-plane clipping.
    /// </summary>
    private const float PlacementDistance = 10f;

    /// <summary>
    /// Computes a 4x4 column-major model matrix for the map quad.
    /// </summary>
    /// <param name="centerRaHours">RA of map center in hours</param>
    /// <param name="centerDecDeg">Dec of map center in degrees</param>
    /// <param name="angularWidthDeg">Angular width of the map in degrees</param>
    /// <param name="observerLatDeg">Observer latitude in degrees</param>
    /// <param name="observerLonDeg">Observer longitude in degrees</param>
    /// <param name="headingRad">Locked compass heading in radians (from ArCorePoseProvider)</param>
    /// <param name="utcNow">Current UTC time</param>
    /// <returns>4x4 column-major model matrix, or null if map center is below horizon</returns>
    public static float[]? ComputeModelMatrix(
        double centerRaHours,
        double centerDecDeg,
        double angularWidthDeg,
        double observerLatDeg,
        double observerLonDeg,
        double headingRad,
        DateTimeOffset utcNow)
    {
        // 1. RA/Dec → Alt/Az.
        var (altDeg, azDeg) = RaDecToAltAz(centerRaHours, centerDecDeg, observerLatDeg, observerLonDeg, utcNow);

        // Don't require above horizon — user might be looking at something near horizon.
        // Just clamp alt to avoid placing it underground.
        if (altDeg < -10.0) return null;

        // 2. Alt/Az → ENU direction vector.
        double altRad = altDeg * DegToRad;
        double azRad = azDeg * DegToRad;
        double enuE = Math.Cos(altRad) * Math.Sin(azRad);
        double enuN = Math.Cos(altRad) * Math.Cos(azRad);
        double enuU = Math.Sin(altRad);

        // 3. ENU → ARCore world coordinates.
        // ARCore world: +Y=up, -Z=initial forward, +X=initial right.
        // Using the same heading transform as ArCorePoseProvider.ComputeEnuToArWorld.
        double cosH = Math.Cos(headingRad);
        double sinH = Math.Sin(headingRad);

        // v_arcore = M_enu2ar * v_enu
        // M_enu2ar columns: ENU east→(cosH, 0, -sinH), ENU north→(-sinH, 0, -cosH), ENU up→(0, 1, 0)
        double arX = cosH * enuE + (-sinH) * enuN;
        double arY = enuU; // up
        double arZ = (-sinH) * enuE + (-cosH) * enuN;

        // Normalize direction.
        double len = Math.Sqrt(arX * arX + arY * arY + arZ * arZ);
        if (len < 0.001) return null;
        arX /= len;
        arY /= len;
        arZ /= len;

        // 4. Position = direction * distance.
        float posX = (float)(arX * PlacementDistance);
        float posY = (float)(arY * PlacementDistance);
        float posZ = (float)(arZ * PlacementDistance);

        // 5. Compute quad size: the quad should subtend angularWidthDeg.
        // At distance d, size = 2 * d * tan(angle/2).
        float quadSize = 2f * PlacementDistance * (float)Math.Tan(angularWidthDeg * DegToRad / 2.0);

        // 6. Orient the quad: billboard facing the camera with correct sky orientation.
        //
        // "right" must be the viewer's right when looking at the quad: direction × refUp.
        // Using -direction (fwd toward camera) × refUp gives the OPPOSITE — a mirrored image.
        // "up" is the face normal × right, keeping up aligned with projected zenith.

        double refUpX = 0, refUpY = 1, refUpZ = 0;

        // Right = normalize(direction × refUp) — viewer's right direction.
        double rightX = arY * refUpZ - arZ * refUpY;
        double rightY = arZ * refUpX - arX * refUpZ;
        double rightZ = arX * refUpY - arY * refUpX;
        double rLen = Math.Sqrt(rightX * rightX + rightY * rightY + rightZ * rightZ);
        if (rLen < 0.001)
        {
            // Looking straight up or down — use -Z as reference instead.
            refUpX = 0; refUpY = 0; refUpZ = -1;
            rightX = arY * refUpZ - arZ * refUpY;
            rightY = arZ * refUpX - arX * refUpZ;
            rightZ = arX * refUpY - arY * refUpX;
            rLen = Math.Sqrt(rightX * rightX + rightY * rightY + rightZ * rightZ);
        }
        rightX /= rLen; rightY /= rLen; rightZ /= rLen;

        // Face normal = from quad toward camera = -direction.
        double fwdX = -arX, fwdY = -arY, fwdZ = -arZ;

        // Up = normalize(faceNormal × right) — projected zenith on the quad plane.
        double upX = fwdY * rightZ - fwdZ * rightY;
        double upY = fwdZ * rightX - fwdX * rightZ;
        double upZ = fwdX * rightY - fwdY * rightX;
        double uLen = Math.Sqrt(upX * upX + upY * upY + upZ * upZ);
        upX /= uLen; upY /= uLen; upZ /= uLen;

        // 7. Build 4x4 column-major model matrix = T * R * S.
        // Columns: [right*s, up*s, fwd*s, pos] (OpenGL column-major).
        float s = quadSize;
        float[] model = new float[16];

        // Column 0: right * scale
        model[0] = (float)(rightX * s);
        model[1] = (float)(rightY * s);
        model[2] = (float)(rightZ * s);
        model[3] = 0f;

        // Column 1: up * scale
        model[4] = (float)(upX * s);
        model[5] = (float)(upY * s);
        model[6] = (float)(upZ * s);
        model[7] = 0f;

        // Column 2: forward (no scale — determines face direction, not size)
        model[8] = (float)fwdX;
        model[9] = (float)fwdY;
        model[10] = (float)fwdZ;
        model[11] = 0f;

        // Column 3: translation
        model[12] = posX;
        model[13] = posY;
        model[14] = posZ;
        model[15] = 1f;

        return model;
    }

    // -------------------------------------------------------------------------
    // Astro math (same as ArMath but self-contained for the Android platform)
    // -------------------------------------------------------------------------

    private static (double AltDeg, double AzDeg) RaDecToAltAz(
        double raHours, double decDeg,
        double latDeg, double lonDeg,
        DateTimeOffset time)
    {
        double lat = latDeg * DegToRad;
        double dec = decDeg * DegToRad;
        long utcMillis = time.ToUniversalTime().ToUnixTimeMilliseconds();
        double jd = utcMillis / 86_400_000.0 + 2440587.5;
        double t = (jd - 2451545.0) / 36525.0;
        double gmstDeg = 280.46061837
            + 360.98564736629 * (jd - 2451545.0)
            + 0.000387933 * t * t
            - t * t * t / 38710000.0;
        double lstRad = (gmstDeg + lonDeg) * DegToRad;
        lstRad = lstRad % (2 * Math.PI);
        if (lstRad < 0) lstRad += 2 * Math.PI;
        double ha = lstRad - raHours * HoursToRad;
        ha = ha % (2 * Math.PI);
        if (ha < 0) ha += 2 * Math.PI;

        double sinAlt = Math.Sin(lat) * Math.Sin(dec) + Math.Cos(lat) * Math.Cos(dec) * Math.Cos(ha);
        double alt = Math.Asin(Math.Clamp(sinAlt, -1.0, 1.0));

        double y = -Math.Cos(dec) * Math.Sin(ha);
        double x = Math.Sin(dec) * Math.Cos(lat) - Math.Cos(dec) * Math.Sin(lat) * Math.Cos(ha);
        double az = Math.Atan2(y, x);
        if (az < 0) az += 2 * Math.PI;

        return (alt * RadToDeg, az * RadToDeg);
    }
}
