namespace AstroFinder.App.Controls;

/// <summary>
/// Full-screen transparent view whose sole purpose is to capture native pinch gestures
/// on Android. MAUI's PinchGestureRecognizer is bypassed by CameraView's SurfaceView;
/// this control uses a platform handler with ScaleGestureDetector instead.
///
/// Set <see cref="PinchUpdated"/> to receive (cumulativeScale, isStart) callbacks.
/// </summary>
public class PinchCaptureView : View
{
    /// <summary>
    /// Called on each pinch update.
    /// <para><c>cumulativeScale</c>: scale factor accumulated since gesture started (1.0 = no change).</para>
    /// <para><c>isStart</c>: true on gesture begin, false on each subsequent update.</para>
    /// </summary>
    public Action<float, bool>? PinchUpdated { get; set; }
}
