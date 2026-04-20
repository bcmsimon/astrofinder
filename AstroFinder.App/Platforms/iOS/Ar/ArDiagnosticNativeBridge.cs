using AstroFinder.App.Controls;
using UIKit;

namespace AstroFinder.App.Platforms.iOS.Ar;

internal interface IArDiagnosticNativeBridge
{
    UIView PlatformView { get; }
    event Action<string>? StatusMessage;
    event Action<ArDiagnosticStatus>? DiagnosticsChanged;
    void StopSession();
}

internal sealed class ArDiagnosticNativeBridge : IArDiagnosticNativeBridge
{
    private readonly ArKitDiagnosticView _sceneKitView;

    public ArDiagnosticNativeBridge()
    {
        // Current implementation uses the SceneKit host because RealityKit bindings are not
        // available in this .NET iOS toolchain. A future Swift RealityKit host should implement
        // the same bridge interface and can be swapped here without changing the handler surface.
        _sceneKitView = new ArKitDiagnosticView();
        _sceneKitView.OnStatusMessage = message => StatusMessage?.Invoke(message);
        _sceneKitView.OnDiagnosticsChanged = diagnostics => DiagnosticsChanged?.Invoke(diagnostics);
    }

    public UIView PlatformView => _sceneKitView;

    public event Action<string>? StatusMessage;
    public event Action<ArDiagnosticStatus>? DiagnosticsChanged;

    public void StopSession()
    {
        _sceneKitView.OnStatusMessage = null;
        _sceneKitView.OnDiagnosticsChanged = null;
        _sceneKitView.StopSession();
    }
}