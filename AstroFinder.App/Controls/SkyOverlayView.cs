using Microsoft.Maui.Graphics;

namespace AstroFinder.App.Controls;

/// <summary>
/// Transparent <see cref="GraphicsView"/> that sits on top of the camera preview and
/// renders sky-catalog markers (stars, deep-sky objects) at their projected positions.
///
/// Usage:
/// <code>
///   var overlay = new SkyOverlayView();
///   overlay.Update(projectedItems);
///   overlay.Invalidate();
/// </code>
/// </summary>
public sealed class SkyOverlayView : GraphicsView, IDrawable
{
    private IReadOnlyList<ProjectedSkyItem> _items = [];
    private bool _isNightMode;
    private float _labelScale = 1f;

    public SkyOverlayView()
    {
        Drawable = this;
        BackgroundColor = Colors.Transparent;
    }

    /// <summary>Updates the list of objects to draw. Call <see cref="GraphicsView.Invalidate"/> afterwards.</summary>
    public void Update(IReadOnlyList<ProjectedSkyItem> items) => _items = items;

    /// <summary>Switches between night-mode (red palette) and normal rendering.</summary>
    public void SetNightMode(bool isNightMode) => _isNightMode = isNightMode;

    /// <summary>Sets the label font scale. 1.0 is default; pinch gesture drives this.</summary>
    public void SetLabelScale(float scale) => _labelScale = scale;

    // IDrawable -------------------------------------------------------------------

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
            return;

        var primary = _isNightMode
            ? Color.FromArgb("#FF7A7A")
            : ResolveToken("ColorPrimary", "#4A90E2");

        var accent = _isNightMode
            ? Color.FromArgb("#FFB36B")
            : ResolveToken("ColorAccent", "#F5A623");

        canvas.SaveState();

        // Pass 1: draw all markers; collect label requests.
        var labelRequests = new List<(PointF Centre, string Label, Color Color)>();

        foreach (var item in _items)
        {
            var cx = dirtyRect.Left + item.ScreenPosition.X * dirtyRect.Width;
            var cy = dirtyRect.Top + item.ScreenPosition.Y * dirtyRect.Height;
            var centre = new PointF(cx, cy);

            if (item.IsHighlighted)
            {
                DrawMarker(canvas, centre, Color.FromArgb("#FF3B30"));
                if (!string.IsNullOrEmpty(item.Label))
                    labelRequests.Add((centre, item.Label, Color.FromArgb("#FF3B30")));
            }
            else if (item.IsStar)
            {
                DrawStar(canvas, centre, item.Magnitude, primary);
            }
            else
            {
                DrawMarker(canvas, centre, accent);
                if (!string.IsNullOrEmpty(item.Label))
                    labelRequests.Add((centre, item.Label, accent));
            }
        }

        // Pass 2: place labels with greedy overlap avoidance.
        var placedRects = new List<RectF>(labelRequests.Count);
        var fontSize = 11f * _labelScale;
        // Use a generous width estimate for proportional fonts; better to over-reserve than under.
        const float charWidthFactor = 0.72f;
        const float lineHeightFactor = 1.4f;

        foreach (var (centre, label, color) in labelRequests)
        {
            var labelW = label.Length * fontSize * charWidthFactor;
            var labelH = fontSize * lineHeightFactor;
            var pos = FindLabelPosition(centre, labelW, labelH, placedRects);
            // Store a slightly expanded rect so adjacent labels have breathing room.
            placedRects.Add(new RectF(pos.X - 2f, pos.Y - 2f, labelW + 4f, labelH + 4f));

            canvas.FontColor = color;
            canvas.FontSize = fontSize;
            canvas.DrawString(label, pos.X, pos.Y, HorizontalAlignment.Left);
        }

        canvas.RestoreState();
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Greedy 8-direction label placement. Tries candidate positions around the
    /// marker in priority order and returns the first that does not intersect any
    /// already-placed label rectangle.  Falls back to the default right position
    /// if all candidates overlap.
    /// </summary>
    private static PointF FindLabelPosition(PointF centre, float w, float h, List<RectF> placed)
    {
        const float markerSize = 10f;
        const float gap = 3f;

        Span<PointF> candidates =
        [
            new(centre.X + markerSize + gap,         centre.Y - h * 0.5f),          // right
            new(centre.X + markerSize + gap,         centre.Y - markerSize - h),     // top-right
            new(centre.X - markerSize - gap - w,     centre.Y - h * 0.5f),           // left
            new(centre.X - w * 0.5f,                 centre.Y - markerSize - h - gap), // top
            new(centre.X + markerSize + gap,         centre.Y + markerSize),          // bottom-right
            new(centre.X - markerSize - gap - w,     centre.Y + markerSize),          // bottom-left
            new(centre.X - w * 0.5f,                 centre.Y + markerSize + gap),    // bottom
            new(centre.X - markerSize - gap - w,     centre.Y - markerSize - h),      // top-left
        ];

        foreach (var candidate in candidates)
        {
            var rect = new RectF(candidate.X, candidate.Y, w, h);
            var overlaps = false;
            foreach (var p in placed)
            {
                if (p.IntersectsWith(rect)) { overlaps = true; break; }
            }
            if (!overlaps)
                return candidate;
        }

        return candidates[0]; // fallback: default right
    }

    private static void DrawMarker(ICanvas canvas, PointF centre, Color color)
    {
        const float size = 10f;
        canvas.StrokeColor = color;
        canvas.StrokeSize = 1.5f;
        canvas.DrawCircle(centre, size);
        canvas.DrawLine(centre.X - size * 1.4f, centre.Y, centre.X - size * 0.6f, centre.Y);
        canvas.DrawLine(centre.X + size * 0.6f, centre.Y, centre.X + size * 1.4f, centre.Y);
        canvas.DrawLine(centre.X, centre.Y - size * 1.4f, centre.X, centre.Y - size * 0.6f);
        canvas.DrawLine(centre.X, centre.Y + size * 0.6f, centre.X, centre.Y + size * 1.4f);
    }

    private static void DrawStar(ICanvas canvas, PointF centre, double magnitude, Color color)
    {
        var radius = StarRadius(magnitude);
        canvas.FillColor = color.WithAlpha(0.85f);
        canvas.FillCircle(centre, radius);
    }

    private static float StarRadius(double magnitude)
    {
        // Brighter stars (lower magnitude) get larger dots.
        // Magnitude range roughly -1 (Sirius) to 6 (naked-eye limit).
        var clamped = Math.Clamp(magnitude, -1.5, 6.5);
        return (float)(5.0 - clamped * 0.6);
    }

    private static Color ResolveToken(string key, string fallback)
    {
        if (Application.Current?.Resources != null
            && Application.Current.Resources.TryGetValue(key, out var val)
            && val is Color color)
        {
            return color;
        }
        return Color.FromArgb(fallback);
    }
}
