namespace AstroFinder.App.Controls;

/// <summary>
/// Draws a figure-8 (lemniscate of Bernoulli) outline with an animated dot
/// tracing the path, prompting the user to perform the compass calibration gesture.
/// </summary>
public sealed class FigureEightDrawable : IDrawable
{
    private const int PathPoints = 120;
    private const float ScaleX = 0.38f;  // fraction of canvas width for half-width
    private const float ScaleY = 0.30f;  // fraction of canvas height for half-height

    /// <summary>Normalised position of the tracing dot (0.0 – 1.0).</summary>
    public float TracePosition { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float cx = dirtyRect.Width / 2f;
        float cy = dirtyRect.Height / 2f;
        float rx = dirtyRect.Width * ScaleX;
        float ry = dirtyRect.Height * ScaleY;

        // --- path outline ---
        canvas.StrokeColor = Color.FromArgb("#66FFFFFF");
        canvas.StrokeSize = 2.5f;
        canvas.StrokeLineCap = LineCap.Round;

        var path = new PathF();
        for (int i = 0; i <= PathPoints; i++)
        {
            float t = (float)i / PathPoints * MathF.PI * 2f;
            float px = cx + rx * MathF.Sin(t);
            float py = cy + ry * MathF.Sin(2f * t) / 2f;
            if (i == 0) path.MoveTo(px, py);
            else path.LineTo(px, py);
        }
        canvas.DrawPath(path);

        // --- animated dot ---
        float dotT = TracePosition * MathF.PI * 2f;
        float dotX = cx + rx * MathF.Sin(dotT);
        float dotY = cy + ry * MathF.Sin(2f * dotT) / 2f;

        canvas.FillColor = Color.FromArgb("#FFFFFFFF");
        canvas.FillCircle(dotX, dotY, 6f);
    }
}

/// <summary>
/// Semi-transparent overlay that prompts the user to calibrate the compass
/// by moving the phone in a figure-of-8 gesture.  Hides automatically once
/// the magnetometer accuracy improves.
/// </summary>
public sealed class CompassCalibrationView : ContentView
{
    private readonly FigureEightDrawable _drawable = new();
    private readonly GraphicsView _graphicsView;
    private Animation? _animation;

    public CompassCalibrationView()
    {
        _graphicsView = new GraphicsView
        {
            Drawable = _drawable,
            HeightRequest = 160,
            WidthRequest = 260,
            HorizontalOptions = LayoutOptions.Center
        };

        var title = new Label
        {
            Text = "Compass calibration needed",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var subtitle = new Label
        {
            Text = "Move your phone slowly in a figure-8 shape",
            FontSize = 12,
            TextColor = Color.FromArgb("#CCFFFFFF"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        var inner = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(24, 20),
            Children = { title, _graphicsView, subtitle }
        };

        var card = new Border
        {
            Background = Color.FromArgb("#CC000000"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Stroke = Color.FromArgb("#33FFFFFF"),
            Content = inner,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(24, 0)
        };

        Content = card;
        InputTransparent = false;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is not null)
            StartAnimation();
        else
            StopAnimation();
    }

    private void StartAnimation()
    {
        _animation = new Animation(t =>
        {
            _drawable.TracePosition = (float)t;
            _graphicsView.Invalidate();
        }, 0, 1);

        _animation.Commit(this, "FigureEight", 16, 3000,
            Easing.Linear, repeat: () => true);
    }

    private void StopAnimation()
    {
        this.AbortAnimation("FigureEight");
        _animation = null;
    }
}
