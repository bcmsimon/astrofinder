using ARKit;
using AstroFinder.App.Controls;
using CoreGraphics;
using Foundation;
using SceneKit;
using UIKit;

namespace AstroFinder.App.Platforms.iOS.Ar;

/// <summary>
/// Minimal iOS AR diagnostics host.
/// RealityKit ARView is not exposed by the local .NET iOS bindings in this workspace,
/// so this uses ARKit world tracking with ARSCNView as the closest native fallback.
/// The diagnostic constraints stay the same: one session, one anchor, one marker.
/// </summary>
internal sealed class ArKitDiagnosticView : ARSCNView
{
    private readonly DiagnosticSceneDelegate _diagnosticDelegate;
    private bool _sessionStarted;

    public Action<string>? OnStatusMessage { get; set; }
    public Action<ArDiagnosticStatus>? OnDiagnosticsChanged { get; set; }

    public ArKitDiagnosticView()
        : base(CGRect.Empty)
    {
        Scene = new SCNScene();
        AutomaticallyUpdatesLighting = false;
        AutoenablesDefaultLighting = false;

        _diagnosticDelegate = new DiagnosticSceneDelegate(this);
        Delegate = _diagnosticDelegate;
        Session.Delegate = _diagnosticDelegate;

        StartSession();
    }

    private void StartSession()
    {
        if (_sessionStarted)
        {
            return;
        }

        if (!ARWorldTrackingConfiguration.IsSupported)
        {
            OnStatusMessage?.Invoke("ARKit world tracking is not supported on this device.");
            PublishDiagnostics(sessionInitialized: false, trackingState: "Unsupported", anchorPlacedCount: 0, markerPoseText: "not placed");
            return;
        }

        var configuration = new ARWorldTrackingConfiguration
        {
            WorldAlignment = ARWorldAlignment.Gravity,
            PlaneDetection = ARPlaneDetection.None,
            EnvironmentTexturing = AREnvironmentTexturing.None,
            LightEstimationEnabled = false,
            ProvidesAudioData = false,
            AutoFocusEnabled = true,
        };

        // The session starts exactly once for this diagnostic view.
        // Re-running or reconfiguring the session during normal updates can mask anchoring bugs.
        Session.Run(configuration, ARSessionRunOptions.ResetTracking | ARSessionRunOptions.RemoveExistingAnchors);
        _sessionStarted = true;

        System.Diagnostics.Debug.WriteLine("[ArKitDiagnostic] Session started.");
        OnStatusMessage?.Invoke("ARKit session started.");
        PublishDiagnostics(sessionInitialized: true, trackingState: "WaitingForTracking", anchorPlacedCount: 0, markerPoseText: "not placed");
    }

    public void StopSession()
    {
        Session.Delegate = null;
        Session.Pause();
    }

    internal void PublishStatus(string message)
    {
        OnStatusMessage?.Invoke(message);
    }

    internal void PublishDiagnostics(bool sessionInitialized, string trackingState, int anchorPlacedCount, string markerPoseText)
    {
        OnDiagnosticsChanged?.Invoke(new ArDiagnosticStatus(
            PlatformName: "iOS",
            SessionInitialized: sessionInitialized,
            TrackingState: trackingState,
            AnchorPlacedCount: anchorPlacedCount,
            MarkerPoseText: markerPoseText));
    }

    private sealed class DiagnosticSceneDelegate : ARSCNViewDelegate
    {
        private readonly ArKitDiagnosticView _owner;
        private ARAnchor? _markerAnchor;
        private string _lastTrackingState = "WaitingForTracking";
        private bool _markerPlaced;

        public DiagnosticSceneDelegate(ArKitDiagnosticView owner)
        {
            _owner = owner;
        }

        public override void CameraDidChangeTrackingState(ARSession session, ARCamera camera)
        {
            var trackingState = DescribeTrackingState(camera.TrackingState);
            if (trackingState == _lastTrackingState)
            {
                return;
            }

            _lastTrackingState = trackingState;
            System.Diagnostics.Debug.WriteLine($"[ArKitDiagnostic] Tracking state changed: {trackingState}");
            _owner.PublishStatus($"ARKit tracking: {trackingState}");
            _owner.PublishDiagnostics(true, trackingState, _markerPlaced ? 1 : 0, _markerPlaced ? DescribeAnchor(_markerAnchor) : "not placed");
        }

        public override void DidUpdateFrame(ARSession session, ARFrame frame)
        {
            if (_markerPlaced || frame.Camera.TrackingState != ARTrackingState.Normal)
            {
                return;
            }

            var pointOfView = _owner.PointOfView;
            if (pointOfView == null)
            {
                return;
            }

            // The marker must be placed once and then left alone.
            // If we keep recomputing from the camera transform, the marker will appear to follow the device.
            // In camera-like local space on iOS, forward is negative Z, so translating by -1.5 on Z
            // places the marker 1.5 meters in front of the device at the instant of placement.
            var forwardTranslation = SCNMatrix4.CreateTranslation(0f, 0f, -1.5f);
            var worldTransform = pointOfView.ConvertTransformToNode(forwardTranslation, null);
            var anchorTransform = new NMatrix4(
                worldTransform.M11, worldTransform.M12, worldTransform.M13, worldTransform.M14,
                worldTransform.M21, worldTransform.M22, worldTransform.M23, worldTransform.M24,
                worldTransform.M31, worldTransform.M32, worldTransform.M33, worldTransform.M34,
                worldTransform.M41, worldTransform.M42, worldTransform.M43, worldTransform.M44);

            _markerAnchor = new ARAnchor(anchorTransform);
            session.AddAnchor(_markerAnchor);
            _markerPlaced = true;

            var poseText = DescribeAnchor(_markerAnchor);
            System.Diagnostics.Debug.WriteLine("[ArKitDiagnostic] Marker placed.");
            System.Diagnostics.Debug.WriteLine($"[ArKitDiagnostic] Marker pose: {poseText}");
            _owner.PublishStatus("Marker placed once at 1.5m forward.");
            _owner.PublishDiagnostics(true, _lastTrackingState, 1, poseText);
        }

        public override SCNNode? GetNode(ISCNSceneRenderer renderer, ARAnchor anchor)
        {
            if (_markerAnchor == null || anchor.Identifier != _markerAnchor.Identifier)
            {
                return null;
            }

            return CreateCrossNode();
        }

        public override void DidFail(ARSession session, NSError error)
        {
            _owner.PublishStatus($"ARKit failure: {error.LocalizedDescription}");
        }

        private static string DescribeTrackingState(ARTrackingState trackingState)
        {
            return trackingState switch
            {
                ARTrackingState.Normal => "Normal",
                ARTrackingState.NotAvailable => "NotAvailable",
                ARTrackingState.Limited => "Limited",
                _ => trackingState.ToString(),
            };
        }

        private static string DescribeAnchor(ARAnchor? anchor)
        {
            if (anchor == null)
            {
                return "not placed";
            }

            var transform = anchor.Transform;
            return $"x={transform.M41:F2}, y={transform.M42:F2}, z={transform.M43:F2}";
        }

        private static SCNNode CreateCrossNode()
        {
            var horizontal = CreateBarNode(width: 0.08f, height: 0.01f, length: 0.01f);
            var vertical = CreateBarNode(width: 0.01f, height: 0.08f, length: 0.01f);

            var root = new SCNNode();
            root.AddChildNode(horizontal);
            root.AddChildNode(vertical);
            return root;
        }

        private static SCNNode CreateBarNode(float width, float height, float length)
        {
            var geometry = SCNBox.Create(width, height, length, 0f);
            geometry.FirstMaterial ??= new SCNMaterial();
            geometry.FirstMaterial!.Diffuse.Contents = UIColor.SystemRedColor;
            geometry.FirstMaterial.LightingModelName = SCNLightingModel.Constant;

            return new SCNNode
            {
                Geometry = geometry,
            };
        }
    }
}