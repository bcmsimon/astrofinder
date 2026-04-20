using AstroFinder.App.Controls;
using Microsoft.Maui.Handlers;
using UIKit;

namespace AstroFinder.App.Platforms.iOS.Ar;

internal sealed class ArCameraViewHandler : ViewHandler<ArCameraView, UIView>
{
    private IArDiagnosticNativeBridge? _bridge;

    public static readonly PropertyMapper<ArCameraView, ArCameraViewHandler> Mapper =
        new(ViewHandler.ViewMapper);

    public ArCameraViewHandler() : base(Mapper)
    {
    }

    protected override UIView CreatePlatformView()
    {
        _bridge = new ArDiagnosticNativeBridge();
        _bridge.StatusMessage += message => VirtualView?.RaiseStatusMessage(message);
        _bridge.DiagnosticsChanged += diagnostics => VirtualView?.RaiseDiagnosticsChanged(diagnostics);
        return _bridge.PlatformView;
    }

    protected override void DisconnectHandler(UIView platformView)
    {
        _bridge?.StopSession();
        _bridge = null;
        base.DisconnectHandler(platformView);
    }
}