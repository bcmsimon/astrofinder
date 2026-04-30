namespace AstroFinder.App.Controls;

/// <summary>
/// Draws the guide ring and rotating arrowhead that indicates the direction
/// toward the navigation target when it is outside the current field of view.
///
/// Layout: fills the screen (InputTransparent = true).  The ring is drawn at
/// a fixed physical radius around the screen centre.  The solid-white triangle
/// arrowhead sits on the ring perimeter and points outward in the direction of
/// the target.
/// </summary>
public sealed class TargetGuideDrawable : IDrawable
{
    /// <summary>Degrees clockwise from screen top (up = 0°, right = 90°).</summary>
    public float AngleDegrees { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float cx = dirtyRect.Width / 2f;
        float cy = dirtyRect.Height / 2f;

        // Fixed physical ring radius regardless of screen size.
        // 130 dp gives a comfortable indicator on both phone sizes.
        float radius = 130f;

        // --- ring ---
        canvas.StrokeColor = Color.FromArgb("#BBFFFFFF");
        canvas.StrokeSize = 1.5f;
        canvas.DrawCircle(cx, cy, radius);

        // --- small centre crosshair ---
        float ch = 8f;
        canvas.StrokeColor = Color.FromArgb("#66FFFFFF");
        canvas.StrokeSize = 1f;
        canvas.DrawLine(cx - ch, cy, cx + ch, cy);
        canvas.DrawLine(cx, cy - ch, cx, cy + ch);

        // --- arrowhead at the ring edge ---
        float rad = AngleDegrees * MathF.PI / 180f;

        // Forward unit vector (outward from centre in screen-space).
        // Screen Y grows downward, so "up" = -Y.
        float fwdX = MathF.Sin(rad);
        float fwdY = -MathF.Cos(rad);

        // Perpendicular (left of forward)
        float perpX = -fwdY;
        float perpY = fwdX;

        // Tip slightly outside ring, base slightly inside.
        float tipX  = cx + (radius + 14f) * fwdX;
        float tipY  = cy + (radius + 14f) * fwdY;
        float baseX = cx + (radius -  6f) * fwdX;
        float baseY = cy + (radius -  6f) * fwdY;

        float halfW = 9f;

        var path = new PathF();
        path.MoveTo(tipX, tipY);
        path.LineTo(baseX + perpX * halfW, baseY + perpY * halfW);
        path.LineTo(baseX - perpX * halfW, baseY - perpY * halfW);
        path.Close();

        canvas.FillColor = Colors.White;
        canvas.FillPath(path);

        // Small arc highlight on the ring near the arrow to make it pop.
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 2.5f;
        float arcHalfDeg = 18f;
        DrawArc(canvas, cx, cy, radius, AngleDegrees - arcHalfDeg, AngleDegrees + arcHalfDeg);
    }

    private static void DrawArc(ICanvas canvas, float cx, float cy, float r,
        float startDeg, float endDeg)
    {
        const int Steps = 16;
        float startRad = startDeg * MathF.PI / 180f;
        float endRad   = endDeg   * MathF.PI / 180f;
        float step = (endRad - startRad) / Steps;

        var path = new PathF();
        for (int i = 0; i <= Steps; i++)
        {
            float t = startRad + i * step;
            float x = cx + r * MathF.Sin(t);
            float y = cy - r * MathF.Cos(t);
            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }
        canvas.DrawPath(path);
    }
}

/// <summary>
/// Full-screen transparent overlay that hosts the <see cref="TargetGuideDrawable"/>.
/// Call <see cref="SetAngle"/> to update the pointer direction; set
/// IsVisible to false when the target is on-screen.
/// </summary>
public sealed class TargetGuideView : ContentView
{
    private readonly TargetGuideDrawable _drawable = new();
    private readonly GraphicsView _graphicsView;

    public TargetGuideView()
    {
        _graphicsView = new GraphicsView
        {
            Drawable = _drawable,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            BackgroundColor = Colors.Transparent,
        };

        Content = _graphicsView;
        InputTransparent = true;
        IsVisible = false;
    }

    /// <summary>
    /// Updates the arrowhead direction and makes the guide visible.
    /// </summary>
    public void SetAngle(float angleDegrees)
    {
        _drawable.AngleDegrees = angleDegrees;
        IsVisible = true;
        _graphicsView.Invalidate();
    }

    /// <summary>Hides the guide (target is on-screen).</summary>
    public void Hide()
    {
        IsVisible = false;
    }
}
