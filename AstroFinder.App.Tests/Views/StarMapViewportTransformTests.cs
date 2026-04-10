using AstroFinder.App.Views;
using Microsoft.Maui.Graphics;

namespace AstroFinder.App.Tests.Views;

public class StarMapViewportTransformTests
{
    [Fact]
    public void Apply_And_Inverse_RoundTrip_ToSamePoint()
    {
        var transform = new StarMapViewportTransform();
        var viewport = new RectF(0, 0, 320, 480);
        var pivot = new PointF(140, 180);

        transform.ZoomAroundScreenPoint(2.5, pivot, viewport);
        transform.PanBy(24, -18, viewport);

        var fittedPoint = new PointF(196, 212);
        var screenPoint = transform.Apply(fittedPoint, viewport);
        var roundTrip = transform.Inverse(screenPoint, viewport);

        Assert.InRange(roundTrip.X, fittedPoint.X - 0.01f, fittedPoint.X + 0.01f);
        Assert.InRange(roundTrip.Y, fittedPoint.Y - 0.01f, fittedPoint.Y + 0.01f);
    }

    [Fact]
    public void ZoomAroundScreenPoint_KeepsPointUnderFingerStable()
    {
        var transform = new StarMapViewportTransform();
        var viewport = new RectF(0, 0, 320, 480);
        var pinchPoint = new PointF(120, 160);
        var fittedPointAtFinger = transform.Inverse(pinchPoint, viewport);

        transform.ZoomAroundScreenPoint(3.0, pinchPoint, viewport);

        var screenAfterZoom = transform.Apply(fittedPointAtFinger, viewport);

        Assert.InRange(screenAfterZoom.X, pinchPoint.X - 0.01f, pinchPoint.X + 0.01f);
        Assert.InRange(screenAfterZoom.Y, pinchPoint.Y - 0.01f, pinchPoint.Y + 0.01f);
    }

    [Fact]
    public void PanBy_ClampsWithinZoomedViewport()
    {
        var transform = new StarMapViewportTransform();
        var viewport = new RectF(0, 0, 300, 200);

        transform.ZoomAroundScreenPoint(4.0, new PointF(150, 100), viewport);
        transform.PanBy(1000, -1000, viewport);

        Assert.Equal(450d, transform.OffsetX);
        Assert.Equal(-300d, transform.OffsetY);
    }
}
