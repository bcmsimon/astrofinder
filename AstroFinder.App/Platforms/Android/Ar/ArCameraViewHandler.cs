using AstroFinder.App.Controls;
using Microsoft.Maui.Handlers;

namespace AstroFinder.App.Platforms.Android.Ar;

/// <summary>
/// MAUI handler that maps <see cref="ArCameraView"/> to <see cref="ArCoreGLView"/> on Android.
/// </summary>
internal class ArCameraViewHandler : ViewHandler<ArCameraView, ArCoreGLView>
{
    public static readonly PropertyMapper<ArCameraView, ArCameraViewHandler> Mapper =
        new(ViewHandler.ViewMapper);

    private ArCorePoseProvider? _poseProvider;

    public ArCameraViewHandler() : base(Mapper) { }

    protected override ArCoreGLView CreatePlatformView()
    {
        var glView = new ArCoreGLView(Context);

        // Resolve the pose provider from DI.
        _poseProvider = MauiContext!.Services.GetRequiredService<global::AstroFinder.App.Services.IDeviceOrientationService>()
            as ArCorePoseProvider;

        // Wire ARCore frame poses to the provider.
        glView.OnFramePose = (m, w, h, fx, fy, sensorPitch) => _poseProvider?.OnArCorePose(m, w, h, fx, fy, sensorPitch);
        glView.OnGrayFrame = frame => VirtualView?.SetLatestGrayFrame(frame);
        glView.OnStatusMessage = msg =>
            MainThread.BeginInvokeOnMainThread(() => VirtualView?.RaiseStatusMessage(msg));

        // Create and resume the ARCore session.
        if (glView.TryCreateSession(Context))
        {
            glView.ResumeSession();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() =>
                VirtualView?.RaiseStatusMessage("ARCore session could not be created."));
        }

        return glView;
    }

    protected override void ConnectHandler(ArCoreGLView platformView)
    {
        base.ConnectHandler(platformView);
        if (VirtualView != null)
        {
            VirtualView.SessionPauseRequested += OnSessionPauseRequested;
            VirtualView.SessionResumeRequested += OnSessionResumeRequested;
            VirtualView.MapOverlayRequested += OnMapOverlayRequested;
        }
    }

    protected override void DisconnectHandler(ArCoreGLView platformView)
    {
        if (VirtualView != null)
        {
            VirtualView.SessionPauseRequested -= OnSessionPauseRequested;
            VirtualView.SessionResumeRequested -= OnSessionResumeRequested;
            VirtualView.MapOverlayRequested -= OnMapOverlayRequested;
        }
        platformView.ClearMapOverlay();
        platformView.PauseSession();
        platformView.DestroySession();
        platformView.OnFramePose = null;
        platformView.OnGrayFrame = null;
        platformView.OnStatusMessage = null;
        base.DisconnectHandler(platformView);
    }

    private void OnSessionPauseRequested(object? sender, EventArgs e)
    {
        PlatformView?.PauseSession();
    }

    private void OnSessionResumeRequested(object? sender, EventArgs e)
    {
        PlatformView?.ResumeSession();
    }

    private void OnMapOverlayRequested(object? sender, MapOverlayArgs? args)
    {
        if (args == null)
        {
            PlatformView?.ClearMapOverlay();
        }
        else if (args.Bitmap is global::Android.Graphics.Bitmap bitmap)
        {
            PlatformView?.SetMapOverlay(bitmap, args.ModelMatrix, args.Alpha);
        }
    }
}
