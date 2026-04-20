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

    public ArCameraViewHandler() : base(Mapper) { }

    protected override ArCoreGLView CreatePlatformView()
    {
        var glView = new ArCoreGLView(Context);
        glView.OnStatusMessage = msg =>
            MainThread.BeginInvokeOnMainThread(() => VirtualView?.RaiseStatusMessage(msg));
        glView.OnDiagnosticsChanged = diagnostics =>
            MainThread.BeginInvokeOnMainThread(() => VirtualView?.RaiseDiagnosticsChanged(diagnostics));

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
    }

    protected override void DisconnectHandler(ArCoreGLView platformView)
    {
        platformView.PauseSession();
        platformView.DestroySession();
        platformView.OnStatusMessage = null;
        platformView.OnDiagnosticsChanged = null;
        base.DisconnectHandler(platformView);
    }
}
