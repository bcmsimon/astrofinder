using Android.Content;
using Android.Opengl;
using Google.AR.Core;
using Google.AR.Core.Exceptions;
using Javax.Microedition.Khronos.Opengles;
using Config = Google.AR.Core.Config;

namespace AstroFinder.App.Platforms.Android.Ar;

/// <summary>
/// A <see cref="GLSurfaceView"/> that runs an ARCore session,
/// renders the camera background, and provides per-frame camera pose callbacks.
/// </summary>
internal sealed class ArCoreGLView : GLSurfaceView, GLSurfaceView.IRenderer
{
    private Session? _session;
    private int _cameraTextureId;
    private bool _sessionResumed;
    private long _lastPoseCallbackMs;

    // Map overlay state (set from main thread, consumed on GL thread).
    private volatile global::Android.Graphics.Bitmap? _pendingBitmap;
    private float[]? _mapModelMatrix;
    private float _mapAlpha = 0.80f;
    private bool _mapOverlayEnabled;
    private TrackingState? _lastTrackingState = TrackingState.Stopped;
    private float[]? _lastViewMatrix;
    private float[]? _lastProjMatrix;

    /// <summary>
    /// Called on each frame that has valid tracking.
    /// Parameters: (column-major 4x4 camera pose matrix, camera image width, camera image height,
    ///              focal length X, focal length Y)
    /// </summary>
    public Action<float[], int, int, float, float, float>? OnFramePose { get; set; }

    /// <summary>
    /// Called when ARCore session is created/ready.
    /// </summary>
    public Action? OnSessionReady { get; set; }

    /// <summary>
    /// Called with status/error messages.
    /// </summary>
    public Action<string>? OnStatusMessage { get; set; }

    /// <summary>
    /// Set the map bitmap to be drawn as a world-space overlay.
    /// Thread-safe: can be called from the main thread.
    /// </summary>
    public void SetMapOverlay(global::Android.Graphics.Bitmap bitmap, float[] modelMatrix, float alpha = 0.80f)
    {
        _pendingBitmap = bitmap;
        _mapModelMatrix = modelMatrix;
        _mapAlpha = alpha;
        _mapOverlayEnabled = true;
    }

    public void ClearMapOverlay()
    {
        _mapOverlayEnabled = false;
        _pendingBitmap = null;
        _mapModelMatrix = null;
    }

    public ArCoreGLView(Context context) : base(context)
    {
        PreserveEGLContextOnPause = true;
        SetEGLContextClientVersion(2);
        SetEGLConfigChooser(8, 8, 8, 8, 16, 0);
        SetRenderer(this);
        RenderMode = Rendermode.Continuously;
    }

    // -------------------------------------------------------------------------
    // Session lifecycle (called from main thread)
    // -------------------------------------------------------------------------

    public bool TryCreateSession(Context context)
    {
        try
        {
            var availability = ArCoreApk.Instance.CheckAvailability(context);
            if (availability == ArCoreApk.Availability.UnsupportedDeviceNotCapable)
            {
                OnStatusMessage?.Invoke("ARCore is not supported on this device.");
                return false;
            }

            _session = new Session(context);
            var config = new Config(_session);
            config.SetUpdateMode(Config.UpdateMode.LatestCameraImage!);
            config.SetFocusMode(Config.FocusMode.Auto!);
            // We don't need planes, depth, or augmented images.
            config.SetPlaneFindingMode(Config.PlaneFindingMode.Disabled!);
            config.SetDepthMode(Config.DepthMode.Disabled!);
            _session.Configure(config);

            OnSessionReady?.Invoke();
            return true;
        }
        catch (UnavailableException ex)
        {
            OnStatusMessage?.Invoke($"ARCore unavailable: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ArCoreGLView] Session creation failed: {ex}");
            OnStatusMessage?.Invoke($"AR session failed: {ex.Message}");
            return false;
        }
    }

    public void ResumeSession()
    {
        if (_session == null) return;
        try
        {
            _session.Resume();
            _sessionResumed = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ArCoreGLView] Resume failed: {ex}");
        }
    }

    public void PauseSession()
    {
        if (_session == null || !_sessionResumed) return;
        try
        {
            _session.Pause();
            _sessionResumed = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ArCoreGLView] Pause failed: {ex}");
        }
    }

    public void DestroySession()
    {
        PauseSession();
        _session?.Close();
        _session = null;
    }

    // -------------------------------------------------------------------------
    // GLSurfaceView.IRenderer
    // -------------------------------------------------------------------------

    public void OnSurfaceCreated(IGL10? gl, Javax.Microedition.Khronos.Egl.EGLConfig? config)
    {
        GLES20.GlClearColor(0f, 0f, 0f, 1f);

        // Create the external OES texture for ARCore camera feed.
        _cameraTextureId = CameraBackgroundShader.CreateExternalTexture();
        CameraBackgroundShader.Initialize();
        MapOverlayShader.Initialize();

        // Tell ARCore which texture to render the camera into.
        _session?.SetCameraTextureName(_cameraTextureId);
    }

    public void OnSurfaceChanged(IGL10? gl, int width, int height)
    {
        GLES20.GlViewport(0, 0, width, height);
        var rotation = (int)(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
            ?.WindowManager?.DefaultDisplay?.Rotation ?? global::Android.Views.SurfaceOrientation.Rotation0);
        _session?.SetDisplayGeometry(rotation, width, height);
    }

    public void OnDrawFrame(IGL10? gl)
    {
        GLES20.GlClear(GLES20.GlColorBufferBit | GLES20.GlDepthBufferBit);

        if (_session == null || !_sessionResumed) return;

        try
        {
            var frame = _session.Update();
            var camera = frame?.Camera;
            if (camera == null) return;

            // Always draw camera background (even when not tracking, shows camera preview).
            float[] transformedUvs = new float[8];
            frame!.TransformCoordinates2d(
                Coordinates2d.OpenglNormalizedDeviceCoordinates!,
                QuadNdc,
                Coordinates2d.TextureNormalized!,
                transformedUvs);

            CameraBackgroundShader.Draw(_cameraTextureId, transformedUvs);

            // Upload pending bitmap if available (must happen on GL thread).
            var pending = _pendingBitmap;
            if (pending != null)
            {
                MapOverlayShader.UploadBitmap(pending);
                _pendingBitmap = null; // consumed
            }

            if (camera.TrackingState != _lastTrackingState)
            {
                _lastTrackingState = camera.TrackingState;
                var stateName = _lastTrackingState.ToString();
                MainThread.BeginInvokeOnMainThread(() => OnStatusMessage?.Invoke($"ARCore tracking: {stateName}"));
            }

            // Draw map overlay if enabled (even if paused, try to use last known matrices).
            if (_mapOverlayEnabled && _mapModelMatrix != null)
            {
                if (camera.TrackingState == TrackingState.Tracking)
                {
                    try
                    {
                        // Compute view matrix manually from DisplayOrientedPose
                        float[] ctw = new float[16];
                        camera.DisplayOrientedPose?.ToMatrix(ctw, 0);

                        float[] viewMatrix = new float[16];
                        // Transpose the 3x3 rotation part (R^T).
                        viewMatrix[0]  = ctw[0]; viewMatrix[1]  = ctw[4]; viewMatrix[2]  = ctw[8];  viewMatrix[3]  = 0f;
                        viewMatrix[4]  = ctw[1]; viewMatrix[5]  = ctw[5]; viewMatrix[6]  = ctw[9];  viewMatrix[7]  = 0f;
                        viewMatrix[8]  = ctw[2]; viewMatrix[9]  = ctw[6]; viewMatrix[10] = ctw[10]; viewMatrix[11] = 0f;
                        // For astronomical objects, map translation is locked to 0 so objects appear at infinity
                        viewMatrix[12] = 0f;
                        viewMatrix[13] = 0f;
                        viewMatrix[14] = 0f;
                        viewMatrix[15] = 1f;

                        _lastViewMatrix = viewMatrix;

                        float[] projMatrix = new float[16];
                        camera.GetProjectionMatrix(projMatrix, 0, 0.1f, 100f);
                        _lastProjMatrix = projMatrix;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ArCoreGLView] Get matrices error: {ex}");
                    }
                }

                if (_lastViewMatrix != null && _lastProjMatrix != null)
                {
                    MapOverlayShader.Draw(_lastViewMatrix, _lastProjMatrix, _mapModelMatrix, _mapAlpha);
                }
            }

            if (camera.TrackingState == TrackingState.Tracking)
            {
                // Throttle pose callbacks to ~15 fps to reduce main-thread pressure.
                long nowMs = Java.Lang.JavaSystem.CurrentTimeMillis();
                if (nowMs - _lastPoseCallbackMs < 66) return; // skip if <66ms since last
                _lastPoseCallbackMs = nowMs;

                // Extract camera-to-world pose as 4x4 column-major matrix.
                float[] poseMatrix = new float[16];
                camera.DisplayOrientedPose?.ToMatrix(poseMatrix, 0);

                // Also get raw sensor Pose pitch for diagnostic comparison.
                float[] rawPoseMatrix = new float[16];
                camera.Pose?.ToMatrix(rawPoseMatrix, 0);
                // Forward = -col2 of c2w. col2[Y] = m[1*4+2] = m[6] (col-major) or m[2*4+1]=m[9] row-major.
                // Column-major: col2 Y = rawPoseMatrix[1 + 2*4] = rawPoseMatrix[9]... no.
                // Column-major col j starts at j*4: col2 = m[8],m[9],m[10],m[11].
                // c2w[r,c] = m[c*4+r], so c2w[1,2] = m[2*4+1] = m[9].
                // Forward Y = -c2w[1,2] = -m[9] (col-major), or just -m[6] if row-major.
                // Compute both so we can tell: 
                float sensorPitchColMaj = (float)(Math.Asin(Math.Clamp(-rawPoseMatrix[9], -1f, 1f)) * 180.0 / Math.PI);

                // Extract camera intrinsics for accurate FOV.
                var intrinsics = camera.ImageIntrinsics;
                if (intrinsics != null)
                {
                    var focalLength = intrinsics.GetFocalLength();
                    var dims = intrinsics.GetImageDimensions();
                    if (focalLength != null && dims != null)
                    {
                        OnFramePose?.Invoke(poseMatrix, dims[0], dims[1], focalLength[0], focalLength[1], sensorPitchColMaj);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ArCoreGLView] Frame update error: {ex}");
        }
    }

    // NDC quad corners for UV transform query.
    private static readonly float[] QuadNdc = { -1f, -1f, 1f, -1f, -1f, 1f, 1f, 1f };
}
