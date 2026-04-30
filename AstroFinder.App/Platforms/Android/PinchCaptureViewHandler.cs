using Android.Content;
using Android.Views;
using AstroFinder.App.Controls;
using Microsoft.Maui.Handlers;

namespace AstroFinder.App.Platforms.Android;

public class PinchCaptureViewHandler : ViewHandler<PinchCaptureView, PinchNativeView>
{
    public static readonly IPropertyMapper<PinchCaptureView, PinchCaptureViewHandler> Mapper =
        new PropertyMapper<PinchCaptureView, PinchCaptureViewHandler>(ViewMapper);

    public PinchCaptureViewHandler() : base(Mapper) { }

    protected override PinchNativeView CreatePlatformView() =>
        new(Context!, VirtualView);
}

public sealed class PinchNativeView : global::Android.Views.View
{
    private readonly PinchCaptureView _virtualView;
    private readonly ScaleGestureDetector _scaleDetector;
    private float _gestureAccumulation = 1f;

    public PinchNativeView(Context context, PinchCaptureView virtualView) : base(context)
    {
        _virtualView = virtualView;
        _scaleDetector = new ScaleGestureDetector(context, new ScaleListener(this));
        // Must not be fully transparent background — Android skips touch for alpha=0 views.
        SetBackgroundColor(global::Android.Graphics.Color.Argb(1, 0, 0, 0));
        Clickable = true;
        Focusable = false;
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e == null) return false;
        _scaleDetector.OnTouchEvent(e);
        return true; // consume to prevent camera view stealing multi-touch
    }

    private void NotifyScale(float cumulative, bool isStart) =>
        _virtualView.PinchUpdated?.Invoke(cumulative, isStart);

    private sealed class ScaleListener : ScaleGestureDetector.SimpleOnScaleGestureListener
    {
        private readonly PinchNativeView _owner;

        public ScaleListener(PinchNativeView owner) => _owner = owner;

        public override bool OnScaleBegin(ScaleGestureDetector detector)
        {
            _owner._gestureAccumulation = 1f;
            _owner.NotifyScale(1f, true);
            return true;
        }

        public override bool OnScale(ScaleGestureDetector detector)
        {
            _owner._gestureAccumulation *= detector.ScaleFactor;
            _owner.NotifyScale(_owner._gestureAccumulation, false);
            return true;
        }
    }
}
