using Microsoft.Maui.Graphics;

namespace AstroFinder.App.Views;

/// <summary>
/// Maintains the interactive viewport state for the star map.
/// The drawable renders from logical map coordinates, and this transform
/// applies user-driven zoom and pan in a predictable, redraw-based way.
/// </summary>
public sealed class StarMapViewportTransform
{
    public StarMapViewportTransform(double minScale = 1.0, double maxScale = 4.0)
    {
        MinScale = minScale;
        MaxScale = maxScale;
        Scale = minScale;
    }

    public double MinScale { get; }

    public double MaxScale { get; }

    public double Scale { get; private set; }

    public double OffsetX { get; private set; }

    public double OffsetY { get; private set; }

    public void Reset()
    {
        Scale = MinScale;
        OffsetX = 0;
        OffsetY = 0;
    }

    public PointF Apply(PointF fittedPoint, RectF viewport)
    {
        var centerX = viewport.Left + (viewport.Width / 2f);
        var centerY = viewport.Top + (viewport.Height / 2f);

        return new PointF(
            centerX + (float)(((fittedPoint.X - centerX) * Scale) + OffsetX),
            centerY + (float)(((fittedPoint.Y - centerY) * Scale) + OffsetY));
    }

    public PointF Inverse(PointF screenPoint, RectF viewport)
    {
        var centerX = viewport.Left + (viewport.Width / 2f);
        var centerY = viewport.Top + (viewport.Height / 2f);

        return new PointF(
            centerX + (float)((screenPoint.X - centerX - OffsetX) / Scale),
            centerY + (float)((screenPoint.Y - centerY - OffsetY) / Scale));
    }

    public void ZoomAroundScreenPoint(double requestedScale, PointF screenPivot, RectF viewport)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        var fittedPivot = Inverse(screenPivot, viewport);
        Scale = Math.Clamp(requestedScale, MinScale, MaxScale);

        var adjustedPivot = Apply(fittedPivot, viewport);
        OffsetX += screenPivot.X - adjustedPivot.X;
        OffsetY += screenPivot.Y - adjustedPivot.Y;
        ClampToViewport(viewport);
    }

    public void PanBy(double dx, double dy, RectF viewport)
    {
        OffsetX += dx;
        OffsetY += dy;
        ClampToViewport(viewport);
    }

    private void ClampToViewport(RectF viewport)
    {
        var maxOffsetX = Math.Max(0.0, ((Scale - 1.0) * viewport.Width) / 2.0);
        var maxOffsetY = Math.Max(0.0, ((Scale - 1.0) * viewport.Height) / 2.0);

        OffsetX = Math.Clamp(OffsetX, -maxOffsetX, maxOffsetX);
        OffsetY = Math.Clamp(OffsetY, -maxOffsetY, maxOffsetY);
    }
}
