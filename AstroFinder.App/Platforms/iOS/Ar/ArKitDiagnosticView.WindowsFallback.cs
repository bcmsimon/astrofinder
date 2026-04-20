// This file is compiled only when building net8.0-ios on Windows,
// where the RealityKit binding assembly is not available.
// It provides the same interface as ArKitDiagnosticView so the iOS target compiles.
// It will never run on a real device — iOS apps built for device deployment must use a Mac.

using AstroFinder.App.Controls;
using CoreGraphics;
using UIKit;

namespace AstroFinder.App.Platforms.iOS.Ar;

internal sealed class ArKitDiagnosticView : UIView
{
    public Action<string>? OnStatusMessage { get; set; }
    public Action<ArDiagnosticStatus>? OnDiagnosticsChanged { get; set; }

    public ArKitDiagnosticView()
        : base(CGRect.Empty)
    {
        OnStatusMessage?.Invoke("RealityKit AR not available in Windows build.");
        OnDiagnosticsChanged?.Invoke(new ArDiagnosticStatus(
            PlatformName: "iOS (Windows stub)",
            SessionInitialized: false,
            TrackingState: "Unavailable",
            AnchorPlacedCount: 0,
            MarkerPoseText: "not placed"));
    }

    public void StopSession() { }
}
