using AstroFinder.App.Controls;
using Microsoft.Maui.Handlers;

namespace AstroFinder.App.Platforms.iOS.Ar;

internal sealed class ArCameraViewHandler : ViewHandler<ArCameraView, ArKitDiagnosticView>
{
    public static readonly PropertyMapper<ArCameraView, ArCameraViewHandler> Mapper =
        new(ViewHandler.ViewMapper);

    public ArCameraViewHandler() : base(Mapper)
    {
    }

    protected override ArKitDiagnosticView CreatePlatformView()
    {
        var view = new ArKitDiagnosticView();
        view.OnStatusMessage = message => VirtualView?.RaiseStatusMessage(message);
        view.OnDiagnosticsChanged = diagnostics => VirtualView?.RaiseDiagnosticsChanged(diagnostics);
        return view;
    }

    protected override void DisconnectHandler(ArKitDiagnosticView platformView)
    {
        platformView.OnStatusMessage = null;
        platformView.OnDiagnosticsChanged = null;
        platformView.StopSession();
        base.DisconnectHandler(platformView);
    }
}