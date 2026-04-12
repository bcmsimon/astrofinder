using AstroFinder.Domain.AR;
using Microsoft.Maui.Graphics;

namespace AstroFinder.App.Views;

/// <summary>
/// Renders the AR star-hop overlay on a transparent <see cref="GraphicsView"/>.
/// Coordinates emitted by <see cref="ArProjectionService"/> are in the reference
/// pixel space of <see cref="ArOverlayFrame.Viewport"/>; this drawable scales them
/// to the actual canvas <see cref="RectF"/> provided by the graphics subsystem.
///
/// Thread-safety: <see cref="Update"/> and <see cref="Draw"/> are both called
/// on the main thread (sensor events use SensorSpeed.UI and MAUI invalidates
/// GraphicsView on the main thread).
/// </summary>
public sealed class ArOverlayDrawable : IDrawable
{
    private const double BearingSmoothAlpha = 0.18;
    private const double DegToRad = Math.PI / 180.0;

    private ArOverlayFrame? _frame;
    private bool _isNightMode;
    private double _smoothedBearingDeg = double.NaN;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Replaces the current overlay frame with <paramref name="frame"/>.
    /// Call <c>OverlayView.Invalidate()</c> afterwards to request a redraw.
    /// </summary>
    public void Update(ArOverlayFrame frame) => _frame = frame;

    public void SetNightMode(bool isNightMode) => _isNightMode = isNightMode;

    // -------------------------------------------------------------------------
    // IDrawable
    // -------------------------------------------------------------------------

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_frame is null || dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
            return;

        // Scale from the reference viewport space to the actual canvas pixel space.
        var scaleX = (float)(dirtyRect.Width / _frame.Viewport.WidthPx);
        var scaleY = (float)(dirtyRect.Height / _frame.Viewport.HeightPx);

        PointF ToCanvas(ArProjectedPoint p) =>
            new(
                dirtyRect.Left + (float)(p.ScreenX * scaleX),
                dirtyRect.Top + (float)(p.ScreenY * scaleY));

        // Resolve semantic colours (with hard-coded fallbacks for early/dark frames).
        var primary = _isNightMode ? Color.FromArgb("#FF7A7A") : ResolveColor("ColorPrimary", "#4A90E2");
        var accent = _isNightMode ? Color.FromArgb("#FFB36B") : ResolveColor("ColorAccent", "#F5A623");
        var success = _isNightMode ? Color.FromArgb("#FF5F5F") : ResolveColor("ColorSuccess", "#7ED321");
        var error = _isNightMode ? Color.FromArgb("#FF9B9B") : ResolveColor("ColorError", "#FF3B30");
        var dimStar = (_isNightMode ? Color.FromArgb("#FFD0D0") : Colors.White).WithAlpha(0.35f);

        canvas.SaveState();

        // --- Background stars ---------------------------------------------------
        foreach (var star in _frame.BackgroundStars)
        {
            if (!star.IsVisible) continue;
            canvas.FillColor = dimStar;
            canvas.FillCircle(ToCanvas(star), StarRadius(star.Magnitude) * 0.6f);
        }

        // --- Asterism outline lines --------------------------------------------
        if (_frame.AsterismStars.Count > 0 && _frame.AsterismSegments.Count > 0)
        {
            canvas.StrokeColor = primary.WithAlpha(0.7f);
            canvas.StrokeSize = 1.5f;
            foreach (var (from, to) in _frame.AsterismSegments)
            {
                if (from < 0 || from >= _frame.AsterismStars.Count) continue;
                if (to < 0 || to >= _frame.AsterismStars.Count) continue;
                var s1 = _frame.AsterismStars[from];
                var s2 = _frame.AsterismStars[to];
                if (!s1.IsVisible && !s2.IsVisible) continue;
                canvas.DrawLine(ToCanvas(s1), ToCanvas(s2));
            }
        }

        // --- Asterism stars ---------------------------------------------------
        canvas.FillColor = primary.WithAlpha(0.85f);
        foreach (var star in _frame.AsterismStars)
        {
            if (!star.IsVisible) continue;
            canvas.FillCircle(ToCanvas(star), StarRadius(star.Magnitude));
        }

        // --- Hop route lines ---------------------------------------------------
        if (_frame.HopSteps.Count > 0)
        {
            canvas.StrokeColor = accent;
            canvas.StrokeSize = 2.0f;
            canvas.StrokeDashPattern = [8, 5];

            for (var i = 0; i < _frame.HopSteps.Count - 1; i++)
            {
                var from = _frame.HopSteps[i];
                var to = _frame.HopSteps[i + 1];
                if (from.IsVisible || to.IsVisible)
                    canvas.DrawLine(ToCanvas(from), ToCanvas(to));
            }

            // Line from last hop to target.
            var lastHop = _frame.HopSteps[^1];
            if (lastHop.IsVisible || _frame.Target.IsVisible)
                canvas.DrawLine(ToCanvas(lastHop), ToCanvas(_frame.Target));

            canvas.StrokeDashPattern = null;

            // Intermediate hop stars (skip index 0 = anchor; drawn separately below).
            canvas.FillColor = accent;
            foreach (var hop in _frame.HopSteps.Skip(1))
            {
                if (!hop.IsVisible) continue;
                canvas.FillCircle(ToCanvas(hop), StarRadius(hop.Magnitude) + 1f);
                DrawLabel(canvas, ToCanvas(hop), hop.Label, accent);
            }
        }

        // --- Anchor star (ring + fill) ----------------------------------------
        if (_frame.HopSteps.Count > 0)
        {
            var anchor = _frame.HopSteps[0];
            if (anchor.IsVisible)
            {
                var pt = ToCanvas(anchor);
                canvas.FillColor = success;
                canvas.FillCircle(pt, StarRadius(anchor.Magnitude));
                canvas.StrokeColor = success;
                canvas.StrokeSize = 2.2f;
                canvas.DrawCircle(pt, StarRadius(anchor.Magnitude) + 6f);
                DrawLabel(canvas, pt, anchor.Label, success);
            }
        }

        // --- Target crosshair -------------------------------------------------
        var targetPoint = ToCanvas(_frame.Target);
        if (_frame.Target.IsVisible)
        {
            var sz = 14f;
            canvas.StrokeColor = error;
            canvas.StrokeSize = 2.5f;
            canvas.DrawLine(targetPoint.X - sz, targetPoint.Y, targetPoint.X + sz, targetPoint.Y);
            canvas.DrawLine(targetPoint.X, targetPoint.Y - sz, targetPoint.X, targetPoint.Y + sz);
            canvas.DrawCircle(targetPoint, sz * 0.65f);
            DrawLabel(canvas, targetPoint, _frame.Target.Label, error, offsetY: sz + 6f);
        }

        DrawTargetPointer(canvas, dirtyRect, _frame.Target.IsVisible, _frame.TargetBearingDegrees, _frame.TargetAngularDistanceDegrees, error);

        canvas.RestoreState();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void DrawTargetPointer(
        ICanvas canvas,
        RectF dirtyRect,
        bool targetInViewport,
        double bearingDeg,
        double angularDistanceDeg,
        Color color)
    {
        if (targetInViewport)
        {
            _smoothedBearingDeg = double.NaN;
            return;
        }

        // Apply circular EMA smoothing to the bearing to eliminate jitter.
        if (double.IsNaN(_smoothedBearingDeg))
        {
            _smoothedBearingDeg = bearingDeg;
        }
        else
        {
            var diff = bearingDeg - _smoothedBearingDeg;
            if (diff > 180.0) diff -= 360.0;
            if (diff < -180.0) diff += 360.0;
            _smoothedBearingDeg = (_smoothedBearingDeg + BearingSmoothAlpha * diff + 360.0) % 360.0;
        }

        var center = new PointF(
            dirtyRect.Left + (dirtyRect.Width / 2f),
            dirtyRect.Top + (dirtyRect.Height / 2f));

        const float circleRadius = 80f;
        const float arrowTipOffset = 14f;
        const float arrowBaseHalfWidth = 11f;
        const float arrowHeight = 20f;

        // Fixed circle at screen center.
        canvas.StrokeColor = color.WithAlpha(0.55f);
        canvas.StrokeSize = 3f;
        canvas.DrawCircle(center, circleRadius);

        // Angular distance text centered inside the circle.
        canvas.FontColor = Colors.White.WithAlpha(0.85f);
        canvas.FontSize = 18f;
        var distText = $"{angularDistanceDeg:F0}°";
        canvas.DrawString(distText, center.X, center.Y - 9f, HorizontalAlignment.Center);

        // Rotating arrow on the circumference.
        // Bearing: 0° = up, 90° = right, 180° = down, 270° = left.
        var bearingRad = (float)(_smoothedBearingDeg * DegToRad);
        var dirX = MathF.Sin(bearingRad);
        var dirY = -MathF.Cos(bearingRad);

        // Arrow tip is just outside the circle.
        var tipX = center.X + dirX * (circleRadius + arrowTipOffset);
        var tipY = center.Y + dirY * (circleRadius + arrowTipOffset);

        // Arrow base center is inside the circle at the edge.
        var baseX = center.X + dirX * (circleRadius + arrowTipOffset - arrowHeight);
        var baseY = center.Y + dirY * (circleRadius + arrowTipOffset - arrowHeight);

        // Perpendicular direction for the base corners.
        var perpX = -dirY;
        var perpY = dirX;

        var left = new PointF(baseX + perpX * arrowBaseHalfWidth, baseY + perpY * arrowBaseHalfWidth);
        var right = new PointF(baseX - perpX * arrowBaseHalfWidth, baseY - perpY * arrowBaseHalfWidth);

        var path = new PathF();
        path.MoveTo(tipX, tipY);
        path.LineTo(left);
        path.LineTo(right);
        path.Close();

        canvas.FillColor = color.WithAlpha(0.92f);
        canvas.FillPath(path);
    }

    private static void DrawLabel(
        ICanvas canvas,
        PointF point,
        string? text,
        Color color,
        float offsetX = 10f,
        float offsetY = 0f)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        canvas.FontColor = Colors.White;
        canvas.FontSize = 11f;
        // Draw a semi-transparent backing rectangle first for legibility over the camera feed.
        var labelX = point.X + offsetX;
        var labelY = point.Y - 11f - offsetY;
        canvas.FillColor = Colors.Black.WithAlpha(0.45f);
        canvas.FillRoundedRectangle(labelX - 2f, labelY - 1f, text.Length * 6.2f + 4f, 15f, 3f);
        canvas.DrawString(text, labelX, labelY, HorizontalAlignment.Left);
    }

    private static float StarRadius(double magnitude) =>
        (float)Math.Clamp(5.5 - (magnitude * 0.55), 1.5, 6.0);

    private static Color ResolveColor(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
            return color;
        return Color.FromArgb(fallbackHex);
    }
}
