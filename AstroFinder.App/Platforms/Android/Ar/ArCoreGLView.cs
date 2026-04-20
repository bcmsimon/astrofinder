using Android.Content;
using Android.Opengl;
using AstroFinder.App.Controls;
using Google.AR.Core;
using Google.AR.Core.Exceptions;
using Javax.Microedition.Khronos.Opengles;
using ArFrame = Google.AR.Core.Frame;
using Config = Google.AR.Core.Config;

namespace AstroFinder.App.Platforms.Android.Ar;

/// <summary>
/// A <see cref="GLSurfaceView"/> that runs the minimal ARCore diagnostics path.
/// Only the camera feed and one anchored cross are rendered so world-lock issues
/// are not hidden behind overlay logic.
/// </summary>
internal sealed class ArCoreGLView : GLSurfaceView, GLSurfaceView.IRenderer
{
    private Session? _session;
    private Anchor? _markerAnchor;
    private int _cameraTextureId;
    private bool _sessionResumed;
    private bool _sessionInitialized;
    private bool _markerPlaced;
    private string _trackingState = "Stopped";
    private string _markerPoseText = "not placed";
    private TrackingState? _lastTrackingState = TrackingState.Stopped;

    public Action<string>? OnStatusMessage { get; set; }
    public Action<ArDiagnosticStatus>? OnDiagnosticsChanged { get; set; }

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
                PublishDiagnostics();
                return false;
            }

            _session = new Session(context);
            var config = new Config(_session);
            config.SetUpdateMode(Config.UpdateMode.LatestCameraImage!);
            config.SetFocusMode(Config.FocusMode.Auto!);
            config.SetPlaneFindingMode(Config.PlaneFindingMode.Disabled!);
            config.SetDepthMode(Config.DepthMode.Disabled!);
            config.SetAugmentedImageDatabase(null);
            config.GeospatialMode = Config.GeospatialMode.Disabled;
            config.CloudAnchorMode = Config.CloudAnchorMode.Disabled;
            _session.Configure(config);

            _sessionInitialized = true;
            System.Diagnostics.Debug.WriteLine("[ArCoreDiagnostic] Session started.");
            OnStatusMessage?.Invoke("ARCore session started.");
            PublishDiagnostics();
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
        _markerAnchor?.Detach();
        _markerAnchor?.Dispose();
        _markerAnchor = null;
        _session?.Close();
        _session = null;
        _sessionInitialized = false;
    }

    // -------------------------------------------------------------------------
    // GLSurfaceView.IRenderer
    // -------------------------------------------------------------------------

    public void OnSurfaceCreated(IGL10? gl, Javax.Microedition.Khronos.Egl.EGLConfig? config)
    {
        GLES20.GlClearColor(0f, 0f, 0f, 1f);

        _cameraTextureId = CameraBackgroundShader.CreateExternalTexture();
        CameraBackgroundShader.Initialize();
        DiagnosticCrossShader.Initialize();

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

            float[] transformedUvs = new float[8];
            frame!.TransformCoordinates2d(
                Coordinates2d.OpenglNormalizedDeviceCoordinates!,
                QuadNdc,
                Coordinates2d.TextureNormalized!,
                transformedUvs);

            CameraBackgroundShader.Draw(_cameraTextureId, transformedUvs);

            if (camera.TrackingState != _lastTrackingState)
            {
                _lastTrackingState = camera.TrackingState;
                _trackingState = _lastTrackingState.ToString();
                System.Diagnostics.Debug.WriteLine($"[ArCoreDiagnostic] Tracking state changed: {_trackingState}");
                MainThread.BeginInvokeOnMainThread(() => OnStatusMessage?.Invoke($"ARCore tracking: {_trackingState}"));
                PublishDiagnostics();
            }

            if (camera.TrackingState == TrackingState.Tracking)
            {
                if (!_markerPlaced)
                {
                    PlaceMarker(camera.Pose);
                }

                if (_markerAnchor?.TrackingState == TrackingState.Tracking)
                {
                    var viewMatrix = new float[16];
                    var projectionMatrix = new float[16];
                    var modelMatrix = new float[16];
                    camera.GetViewMatrix(viewMatrix, 0);
                    camera.GetProjectionMatrix(projectionMatrix, 0, 0.1f, 100f);
                    _markerAnchor.Pose?.ToMatrix(modelMatrix, 0);
                    DiagnosticCrossShader.Draw(viewMatrix, projectionMatrix, modelMatrix);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ArCoreGLView] Frame update error: {ex}");
        }
    }

    private static readonly float[] QuadNdc = { -1f, -1f, 1f, -1f, -1f, 1f, 1f, 1f };

    private void PlaceMarker(Pose? cameraPose)
    {
        if (_session == null || cameraPose == null)
        {
            return;
        }

        // Place the marker exactly once. If this logic runs every frame, the marker
        // will appear to follow the device because it is being recreated from the camera pose.
        // In ARCore camera-local space, forward is negative Z, so translating by (0, 0, -1.5)
        // moves the anchor 1.5 meters in front of the device at the moment of placement.
        var markerPose = cameraPose.Compose(Pose.MakeTranslation(0f, 0f, -1.5f));
        _markerAnchor = _session.CreateAnchor(markerPose);
        _markerPlaced = true;
        _markerPoseText = $"x={markerPose.Tx():F2}, y={markerPose.Ty():F2}, z={markerPose.Tz():F2}";

        System.Diagnostics.Debug.WriteLine("[ArCoreDiagnostic] Marker placed.");
        System.Diagnostics.Debug.WriteLine($"[ArCoreDiagnostic] Marker pose: {_markerPoseText}");
        OnStatusMessage?.Invoke("Marker placed once at 1.5m forward.");
        PublishDiagnostics();
    }

    private void PublishDiagnostics()
    {
        OnDiagnosticsChanged?.Invoke(new ArDiagnosticStatus(
            PlatformName: "Android",
            SessionInitialized: _sessionInitialized,
            TrackingState: _trackingState,
            AnchorPlacedCount: _markerPlaced ? 1 : 0,
            MarkerPoseText: _markerPoseText));
    }
}
