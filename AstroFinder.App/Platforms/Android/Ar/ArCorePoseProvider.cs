using Android.Content;
using Android.Hardware;
using AstroFinder.Domain.AR;
using Microsoft.Maui.ApplicationModel;

namespace AstroFinder.App.Platforms.Android.Ar;

/// <summary>
/// Implements <see cref="Services.IDeviceOrientationService"/> using ARCore camera poses
/// rather than raw sensor data. Uses the device compass once at session start to
/// establish absolute heading, then relies on ARCore's visual-inertial odometry.
/// </summary>
public sealed class ArCorePoseProvider : Java.Lang.Object, global::AstroFinder.App.Services.IDeviceOrientationService, ISensorEventListener
{
    private SensorManager? _sensorManager;
    private Sensor? _rotationSensor;
    private bool _isRunning;

    // Compass state: continuously updated until first ARCore tracking frame locks it.
    private double _latestHeadingRad;
    private double? _lockedHeadingRad;

    // ENU → ARCore world rotation matrix (3x3 row-major), computed once from locked heading.
    private double[]? _enuToArWorld;

    private PoseMatrix? _smoothedPose;

    // Full diagnostics — expose everything so HUD can determine matrix format empirically.
    private float[] _raw4x4 = new float[16];
    private double _headingDeg;
    private double[] _extractedW2C = new double[9];
    private double[] _finalPose = new double[9];
    private double _arPitchDeg; // pitch from raw ARCore DisplayOrientedPose, before ENU conversion
    private double _sensorPitchDeg; // pitch from raw ARCore Camera.Pose (non-display-oriented)

    public PoseMatrix CurrentPose => _smoothedPose ?? PoseMatrix.Identity();
    public bool IsAvailable => true; // checked separately via ArCoreApk

    /// <summary>The locked compass heading in radians, or null if not yet locked.</summary>
    public double? LockedHeadingRad => _lockedHeadingRad;

    /// <summary>Full diagnostic snapshot for HUD display.</summary>
    public ArCoreDiagnostics GetDiagnostics() => new(
        _raw4x4, _headingDeg, _arPitchDeg, _sensorPitchDeg, _extractedW2C, _finalPose);

    public event EventHandler<PoseMatrix>? PoseChanged;

    // -------------------------------------------------------------------------
    // IDeviceOrientationService lifecycle
    // -------------------------------------------------------------------------

    public Task<bool> StartAsync()
    {
        if (_isRunning) return Task.FromResult(true);

        _lockedHeadingRad = null;
        _enuToArWorld = null;
        _smoothedPose = null;

        // Start compass (TYPE_ROTATION_VECTOR includes magnetometer → absolute heading).
        var ctx = global::Android.App.Application.Context;
        _sensorManager = ctx.GetSystemService(Context.SensorService) as SensorManager;
        _rotationSensor = _sensorManager?.GetDefaultSensor(SensorType.RotationVector);

        if (_rotationSensor != null)
        {
            _sensorManager!.RegisterListener(this, _rotationSensor, SensorDelay.Ui);
        }

        _isRunning = true;
        return Task.FromResult(true);
    }

    public void Stop()
    {
        if (!_isRunning) return;

        try
        {
            _sensorManager?.UnregisterListener(this);
        }
        catch { }

        _isRunning = false;
        _lockedHeadingRad = null;
        _enuToArWorld = null;
        _smoothedPose = null;
    }

    /// <summary>
    /// Re-reads the current compass heading and updates the ENU→ARCore mapping.
    /// This corrects any compass drift since the session started.
    /// </summary>
    public void Recenter()
    {
        // Temporarily re-enable compass for fresh reading.
        if (_rotationSensor == null || _sensorManager == null) return;

        _lockedHeadingRad = null;
        _enuToArWorld = null;
        _sensorManager.RegisterListener(this, _rotationSensor, SensorDelay.Ui);
    }

    // -------------------------------------------------------------------------
    // ISensorEventListener (compass heading)
    // -------------------------------------------------------------------------

    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy) { }

    public void OnSensorChanged(SensorEvent? e)
    {
        if (e?.Values == null || e.Values.Count < 4) return;
        if (_lockedHeadingRad.HasValue) return; // already locked, stop processing

        var values = new float[e.Values.Count];
        for (int i = 0; i < values.Length; i++)
            values[i] = e.Values[i];

        var R = new float[9];
        SensorManager.GetRotationMatrixFromVector(R, values);
        var orientation = new float[3];
        SensorManager.GetOrientation(R, orientation);

        // orientation[0] = azimuth in radians (0 = north, positive clockwise)
        _latestHeadingRad = orientation[0];
    }

    // -------------------------------------------------------------------------
    // Called from ArCoreGLView on each tracked frame (GL thread)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Receives the ARCore camera-to-world pose and converts it to an ENU PoseMatrix.
    /// </summary>
    /// <param name="cameraPose4x4">Column-major 4x4 from Camera.DisplayOrientedPose.ToMatrix</param>
    /// <param name="imageWidth">Camera image width in pixels</param>
    /// <param name="imageHeight">Camera image height in pixels</param>
    /// <param name="fx">Focal length X in pixels</param>
    /// <param name="fy">Focal length Y in pixels</param>
    /// <param name="sensorPitchDeg">Pitch angle from Camera.Pose (non-display-oriented), for diagnostic comparison</param>
    public void OnArCorePose(float[] cameraPose4x4, int imageWidth, int imageHeight, float fx, float fy, float sensorPitchDeg)
    {
        // Lock heading on first tracking frame.
        if (!_lockedHeadingRad.HasValue)
        {
            _lockedHeadingRad = _latestHeadingRad;
            _enuToArWorld = ComputeEnuToArWorld(_lockedHeadingRad.Value);

            // Stop compass updates — ARCore tracks rotation from here.
            try { _sensorManager?.UnregisterListener(this); }
            catch { }
        }

        if (_enuToArWorld == null) return;

        // Stash raw 4x4 for diagnostics.
        Array.Copy(cameraPose4x4, _raw4x4, 16);
        _headingDeg = (_lockedHeadingRad ?? 0) * 180.0 / Math.PI;
        _sensorPitchDeg = sensorPitchDeg;

        // Extract camera-to-world 3×3 from column-major 4x4.
        // Column-major: column j starts at index j*4. rC2W[r,c] = m[c*4+r].
        var rC2W = new double[9];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                rC2W[r * 3 + c] = cameraPose4x4[c * 4 + r];

        // Compute ARCore camera forward direction in ARCore world.
        // Camera forward = -col2 of c2w = -(c2w[0,2], c2w[1,2], c2w[2,2]).
        // ARCore Y = up, so pitch = asin(forward.Y).
        double arFwdX = -rC2W[0 * 3 + 2];
        double arFwdY = -rC2W[1 * 3 + 2];
        double arFwdZ = -rC2W[2 * 3 + 2];
        _arPitchDeg = Math.Asin(Math.Clamp(arFwdY, -1, 1)) * 180.0 / Math.PI;

        // Transpose to get world-to-camera: w2c = c2w^T (rotation matrices are orthogonal).
        var rW2C = new double[9];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                rW2C[r * 3 + c] = rC2W[c * 3 + r];

        Array.Copy(rW2C, _extractedW2C, 9);

        // ENU-to-camera = R_w2c * M_enu_to_arworld
        var enuToCamArcore = Multiply3x3(rW2C, _enuToArWorld);

        // Flip camera Z: ARCore camera -Z = forward, our convention +Z = forward.
        // Negate row 2 of the matrix.
        enuToCamArcore[6] = -enuToCamArcore[6];
        enuToCamArcore[7] = -enuToCamArcore[7];
        enuToCamArcore[8] = -enuToCamArcore[8];

        var pose = new PoseMatrix(enuToCamArcore);
        Array.Copy(enuToCamArcore, _finalPose, 9);

        // Disable smoothing for diagnostics — show raw pose directly.
        _smoothedPose = pose;

        var result = _smoothedPose.Value;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PoseChanged?.Invoke(this, result);
        });
    }

    // -------------------------------------------------------------------------
    // Math helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Computes the 3x3 row-major matrix that transforms ENU vectors to ARCore world vectors.
    /// </summary>
    /// <remarks>
    /// ARCore world at session start:
    ///   +Y = up (gravity-aligned, always)
    ///   -Z = initial camera forward direction (horizontal projection)
    ///   +X = right of camera
    ///
    /// ENU: +X = east, +Y = north, +Z = up
    ///
    /// If initial heading is H (radians from north, clockwise):
    ///   ARCore +X in ENU = (cos H, -sin H, 0)  [camera right]
    ///   ARCore +Y in ENU = (0, 0, 1)            [up]
    ///   ARCore +Z in ENU = (-sin H, -cos H, 0)  [camera backward]
    ///
    /// v_arcore = M * v_enu where M columns = ENU basis in ARCore coords:
    ///   ENU east  (1,0,0) -> ARCore (cosH, 0, -sinH)
    ///   ENU north (0,1,0) -> ARCore (-sinH, 0, -cosH)
    ///   ENU up    (0,0,1) -> ARCore (0, 1, 0)
    /// </remarks>
    private static double[] ComputeEnuToArWorld(double headingRad)
    {
        double cosH = Math.Cos(headingRad);
        double sinH = Math.Sin(headingRad);

        return new double[]
        {
             cosH, -sinH,  0.0,
             0.0,   0.0,   1.0,
            -sinH, -cosH,  0.0,
        };
    }

    private static double[] Multiply3x3(double[] a, double[] b)
    {
        var result = new double[9];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                result[r * 3 + c] =
                    a[r * 3 + 0] * b[0 * 3 + c] +
                    a[r * 3 + 1] * b[1 * 3 + c] +
                    a[r * 3 + 2] * b[2 * 3 + c];
        return result;
    }
}

/// <summary>Snapshot of all intermediate ARCore pose conversion values for HUD diagnostics.</summary>
public sealed class ArCoreDiagnostics
{
    public float[] Raw4x4 { get; }
    public double HeadingDeg { get; }
    public double ArPitchDeg { get; }
    public double SensorPitchDeg { get; }
    public double[] ExtractedW2C { get; }
    public double[] FinalPose { get; }

    public ArCoreDiagnostics(float[] raw4x4, double headingDeg, double arPitchDeg, double sensorPitchDeg, double[] extractedW2C, double[] finalPose)
    {
        Raw4x4 = (float[])raw4x4.Clone();
        HeadingDeg = headingDeg;
        ArPitchDeg = arPitchDeg;
        SensorPitchDeg = sensorPitchDeg;
        ExtractedW2C = (double[])extractedW2C.Clone();
        FinalPose = (double[])finalPose.Clone();
    }

    /// <summary>Format the full raw 4x4 as 4 rows for display.</summary>
    public string Raw4x4Text()
    {
        var m = Raw4x4;
        return $"[{m[0]:F2} {m[1]:F2} {m[2]:F2} {m[3]:F2}]\n" +
               $"[{m[4]:F2} {m[5]:F2} {m[6]:F2} {m[7]:F2}]\n" +
               $"[{m[8]:F2} {m[9]:F2} {m[10]:F2} {m[11]:F2}]\n" +
               $"[{m[12]:F2} {m[13]:F2} {m[14]:F2} {m[15]:F2}]";
    }

    /// <summary>Format the extracted 3x3 w2c matrix.</summary>
    public string W2CText()
    {
        var m = ExtractedW2C;
        return $"[{m[0]:F2} {m[1]:F2} {m[2]:F2}]\n" +
               $"[{m[3]:F2} {m[4]:F2} {m[5]:F2}]\n" +
               $"[{m[6]:F2} {m[7]:F2} {m[8]:F2}]";
    }
}
