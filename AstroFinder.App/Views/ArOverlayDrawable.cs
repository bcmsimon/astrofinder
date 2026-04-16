using AstroFinder.Domain.AR;
using Microsoft.Maui.Graphics;

namespace AstroFinder.App.Views;

/// <summary>
/// Renders the AR star-hop overlay on a transparent <see cref="GraphicsView"/>.
/// Coordinates in <see cref="ProjectedSkyObject"/> are already in screen-pixel space
/// matching <see cref="CameraIntrinsics"/>; this drawable scales them to the actual
/// canvas <see cref="RectF"/> provided by the graphics subsystem.
/// </summary>
public sealed class ArOverlayDrawable : IDrawable
{
    private ArOverlayFrame? _frame;
    private bool _isNightMode;

    public bool IsMapOverlayActive { get; set; }

    public void Update(ArOverlayFrame frame) => _frame = frame;
    public void SetNightMode(bool isNightMode) => _isNightMode = isNightMode;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_frame is null || dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
            return;

        var scaleX = (float)(dirtyRect.Width / _frame.Intrinsics.WidthPx);
        var scaleY = (float)(dirtyRect.Height / _frame.Intrinsics.HeightPx);

        PointF ToCanvas(ArScreenPoint sp) =>
            new(dirtyRect.Left + sp.XPx * scaleX,
                dirtyRect.Top + sp.YPx * scaleY);

        bool IsVisible(ProjectedSkyObject obj) =>
            obj.InFrontOfCamera && obj.WithinViewport && obj.ScreenPoint.HasValue;

        bool HasScreen(ProjectedSkyObject obj) =>
            obj.InFrontOfCamera && obj.ScreenPoint.HasValue;

        var primary = _isNightMode ? Color.FromArgb("#FF7A7A") : ResolveColor("ColorPrimary", "#4A90E2");
        var accent  = _isNightMode ? Color.FromArgb("#FFB36B") : ResolveColor("ColorAccent", "#F5A623");
        var success = _isNightMode ? Color.FromArgb("#FF5F5F") : ResolveColor("ColorSuccess", "#7ED321");
        var error   = _isNightMode ? Color.FromArgb("#FF9B9B") : ResolveColor("ColorError", "#FF3B30");

        canvas.SaveState();

        if (!IsMapOverlayActive)
        {
            // --- Asterism outline lines ---
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
                    if (!HasScreen(s1) || !HasScreen(s2)) continue;
                    canvas.DrawLine(ToCanvas(s1.ScreenPoint!.Value), ToCanvas(s2.ScreenPoint!.Value));
                }
            }

            // --- Asterism stars ---
            canvas.FillColor = primary.WithAlpha(0.85f);
            foreach (var star in _frame.AsterismStars)
            {
                if (!IsVisible(star)) continue;
                canvas.FillCircle(ToCanvas(star.ScreenPoint!.Value), StarRadius(star.Magnitude));
            }

            // --- Hop route lines ---
            if (_frame.HopSteps.Count > 0)
            {
                canvas.StrokeColor = accent;
                canvas.StrokeSize = 2.0f;
                canvas.StrokeDashPattern = [8, 5];

                for (var i = 0; i < _frame.HopSteps.Count - 1; i++)
                {
                    var a = _frame.HopSteps[i];
                    var b = _frame.HopSteps[i + 1];
                    if (HasScreen(a) && HasScreen(b))
                        canvas.DrawLine(
                            ToCanvas(a.ScreenPoint!.Value),
                            ToCanvas(b.ScreenPoint!.Value));
                }

                var lastHop = _frame.HopSteps[^1];
                if (HasScreen(lastHop) && HasScreen(_frame.Target))
                    canvas.DrawLine(
                        ToCanvas(lastHop.ScreenPoint!.Value),
                        ToCanvas(_frame.Target.ScreenPoint!.Value));

                canvas.StrokeDashPattern = null;

                canvas.FillColor = accent;
                foreach (var hop in _frame.HopSteps.Skip(1))
                {
                    if (!IsVisible(hop)) continue;
                    var pt = ToCanvas(hop.ScreenPoint!.Value);
                    canvas.FillCircle(pt, StarRadius(hop.Magnitude) + 1f);
                    DrawLabel(canvas, pt, hop.Label, accent);
                }
            }

            // --- Anchor star ---
            if (_frame.HopSteps.Count > 0)
            {
                var anchor = _frame.HopSteps[0];
                if (IsVisible(anchor))
                {
                    var pt = ToCanvas(anchor.ScreenPoint!.Value);
                    canvas.FillColor = success;
                    canvas.FillCircle(pt, StarRadius(anchor.Magnitude));
                    canvas.StrokeColor = success;
                    canvas.StrokeSize = 2.2f;
                    canvas.DrawCircle(pt, StarRadius(anchor.Magnitude) + 6f);
                    DrawLabel(canvas, pt, anchor.Label, success);
                }
            }

            // --- Target crosshair ---
            var targetVisible = IsVisible(_frame.Target);
            if (targetVisible && _frame.Target.ScreenPoint.HasValue)
            {
                var tp = ToCanvas(_frame.Target.ScreenPoint.Value);
                const float sz = 14f;
                canvas.StrokeColor = error;
                canvas.StrokeSize = 2.5f;
                canvas.DrawLine(tp.X - sz, tp.Y, tp.X + sz, tp.Y);
                canvas.DrawLine(tp.X, tp.Y - sz, tp.X, tp.Y + sz);
                canvas.DrawCircle(tp, sz * 0.65f);
                DrawLabel(canvas, tp, _frame.Target.Label, error, offsetY: sz + 6f);
            }
        } // end if (!IsMapOverlayActive)

        // --- Off-screen pointer arrow ---
        DrawTargetPointer(canvas, dirtyRect, IsVisible(_frame.Target), error);

        // --- Route segment lines (from ArProjectionService) ---
        if (!IsMapOverlayActive && _frame.RouteSegments.Count > 0)
        {
            canvas.StrokeColor = accent.WithAlpha(0.5f);
            canvas.StrokeSize = 1f;
            foreach (var (from, to) in _frame.RouteSegments)
            {
                canvas.DrawLine(ToCanvas(from), ToCanvas(to));
            }
        }

        canvas.RestoreState();
    }

    private void DrawTargetPointer(ICanvas canvas, RectF dirtyRect, bool targetInViewport, Color color)
    {
        if (targetInViewport || _frame?.OffscreenArrow is null)
            return;

        var arrow = _frame.OffscreenArrow.Value;

        var center = new PointF(
            dirtyRect.Left + dirtyRect.Width / 2f,
            dirtyRect.Top + dirtyRect.Height / 2f);

        const float circleRadius = 80f;
        const float arrowTipOffset = 14f;
        const float arrowBaseHalfWidth = 11f;
        const float arrowHeight = 20f;

        canvas.StrokeColor = color.WithAlpha(0.55f);
        canvas.StrokeSize = 3f;
        canvas.DrawCircle(center, circleRadius);

        canvas.FontColor = Colors.White.WithAlpha(0.85f);
        canvas.FontSize = 18f;
        canvas.DrawString($"{arrow.DistanceDeg:F0}\u00B0", center.X, center.Y - 9f, HorizontalAlignment.Center);

        var angleRad = arrow.AngleRadians;
        var dirX = MathF.Sin(angleRad);
        var dirY = -MathF.Cos(angleRad);

        var tipX = center.X + dirX * (circleRadius + arrowTipOffset);
        var tipY = center.Y + dirY * (circleRadius + arrowTipOffset);

        var baseX = center.X + dirX * (circleRadius + arrowTipOffset - arrowHeight);
        var baseY = center.Y + dirY * (circleRadius + arrowTipOffset - arrowHeight);

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

    private static void DrawLabel(ICanvas canvas, PointF point, string? text, Color color, float offsetX = 10f, float offsetY = 0f)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        canvas.FontColor = Colors.White;
        canvas.FontSize = 11f;
        var labelX = point.X + offsetX;
        var labelY = point.Y - 11f - offsetY;
        canvas.FillColor = Colors.Black.WithAlpha(0.45f);
        canvas.FillRoundedRectangle(labelX - 2f, labelY - 1f, text.Length * 6.2f + 4f, 15f, 3f);
        canvas.DrawString(text, labelX, labelY, HorizontalAlignment.Left);
    }

    private static float StarRadius(double magnitude) =>
        (float)Math.Clamp(5.5 - magnitude * 0.55, 1.5, 6.0);

    private static Color ResolveColor(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
            return color;
        return Color.FromArgb(fallbackHex);
    }
}
