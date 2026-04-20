namespace AstroFinder.App.Controls;

/// <summary>
/// Cross-platform MAUI view that hosts the minimal AR diagnostics surface.
/// The native implementations intentionally expose only camera passthrough,
/// tracking state, and a single world-locked marker.
/// </summary>
public class ArCameraView : View
{
    public event EventHandler<string>? StatusMessage;
    public event EventHandler<ArDiagnosticStatus>? DiagnosticsChanged;

    internal void RaiseStatusMessage(string message) =>
        StatusMessage?.Invoke(this, message);

    internal void RaiseDiagnosticsChanged(ArDiagnosticStatus diagnostics) =>
        DiagnosticsChanged?.Invoke(this, diagnostics);
}

/// <summary>
/// Minimal diagnostics payload for the debug HUD.
/// </summary>
public sealed record ArDiagnosticStatus(
    string PlatformName,
    bool SessionInitialized,
    string TrackingState,
    int AnchorPlacedCount,
    string MarkerPoseText);
