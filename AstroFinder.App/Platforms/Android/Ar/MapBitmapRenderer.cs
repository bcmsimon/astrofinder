using Android.Graphics;
using Color = Android.Graphics.Color;
using Paint = Android.Graphics.Paint;
using PointF = Android.Graphics.PointF;

namespace AstroFinder.App.Platforms.Android.Ar;

/// <summary>
/// Renders a star hop map directly to an Android <see cref="Bitmap"/>.
/// Uses the same gnomonic projection and visual style as the 2D star map,
/// but renders onto a transparent-background bitmap suitable for AR overlay.
/// </summary>
internal static class MapBitmapRenderer
{
    private const int BitmapSize = 2048;
    private const float Margin = 40f;
    private const double DegToRad = Math.PI / 180.0;

    // AR-specific colors: bright on transparent for sky overlay.
    private static readonly Color AsterismColor = Color.Argb(230, 100, 160, 255);       // bright blue
    private static readonly Color AsterismLineColor = Color.Argb(180, 80, 140, 230);    // blue lines
    private static readonly Color HopRouteColor = Color.Argb(230, 255, 180, 50);        // orange
    private static readonly Color AnchorColor = Color.Argb(230, 60, 220, 100);          // green
    private static readonly Color TargetColor = Color.Argb(240, 255, 80, 80);           // red
    private static readonly Color BackgroundStarColor = Color.Argb(120, 200, 200, 220); // dim white
    private static readonly Color LabelTextColor = Color.Argb(230, 255, 255, 255);      // white
    private static readonly Color LabelShadowColor = Color.Argb(160, 0, 0, 0);         // dark shadow

    /// <summary>
    /// Information about the rendered bitmap needed for AR placement.
    /// </summary>
    public sealed record MapBitmapResult(
        Bitmap Bitmap,
        double CenterRaHours,
        double CenterDecDeg,
        double AngularWidthDeg);

    /// <summary>
    /// Renders the star hop map data into a transparent-background bitmap.
    /// </summary>
    public static MapBitmapResult Render(Views.StarMapData data)
    {
        var bitmap = Bitmap.CreateBitmap(BitmapSize, BitmapSize, Bitmap.Config.Argb8888!)!;
        var canvas = new Canvas(bitmap);

        // Transparent background — this will float over the camera feed.
        canvas.DrawColor(Color.Transparent, PorterDuff.Mode.Clear!);

        // Collect all map points for projection bounds.
        var allPoints = new List<(double raDeg, double decDeg)>
        {
            (data.Target.RaHours * 15.0, data.Target.DecDeg)
        };

        foreach (var star in data.AsterismStars)
            allPoints.Add((star.RaHours * 15.0, star.DecDeg));
        foreach (var star in data.HopSteps)
            allPoints.Add((star.RaHours * 15.0, star.DecDeg));

        var centerRaDeg = allPoints.Average(p => p.raDeg);
        var centerDecDeg = allPoints.Average(p => p.decDeg);

        // Project all main points to compute bounds.
        var projected = allPoints
            .Select(p => GnomonicProject(p.raDeg, p.decDeg, centerRaDeg, centerDecDeg))
            .ToList();

        double minX = projected.Min(p => p.x);
        double maxX = projected.Max(p => p.x);
        double minY = projected.Min(p => p.y);
        double maxY = projected.Max(p => p.y);

        // Add 22% padding so the map has breathing room.
        double rangeX = Math.Max(0.001, maxX - minX);
        double rangeY = Math.Max(0.001, maxY - minY);
        const double padFraction = 0.22;
        minX -= rangeX * padFraction;
        maxX += rangeX * padFraction;
        minY -= rangeY * padFraction;
        maxY += rangeY * padFraction;
        rangeX = maxX - minX;
        rangeY = maxY - minY;

        // Compute angular width: gnomonic projection x values are tan(angle),
        // so the angular span ≈ 2 * atan(range/2) in radians → degrees.
        double maxRange = Math.Max(rangeX, rangeY);
        double angularWidthDeg = 2.0 * Math.Atan(maxRange / 2.0) * (180.0 / Math.PI);

        // Fit to square bitmap.
        double scale = (BitmapSize - 2 * Margin) / Math.Max(rangeX, rangeY);
        float offsetX = (float)((BitmapSize - rangeX * scale) / 2.0);
        float offsetY = (float)((BitmapSize - rangeY * scale) / 2.0);

        PointF ToCanvas(double raHours, double decDeg)
        {
            var (px, py) = GnomonicProject(raHours * 15.0, decDeg, centerRaDeg, centerDecDeg);
            // Mirror X so east is left (matching sky view).
            var x = offsetX + (float)((maxX - px) * scale);
            var y = offsetY + (float)((maxY - py) * scale);
            return new PointF(x, y);
        }

        // --- Draw background stars ---
        using var bgStarPaint = new Paint(PaintFlags.AntiAlias);
        foreach (var star in data.BackgroundStars)
        {
            var pt = ToCanvas(star.RaHours, star.DecDeg);
            var r = StarRadius(star.Magnitude) * 1.8f;
            bgStarPaint.SetStyle(Paint.Style.Fill!);
            bgStarPaint.Color = BackgroundStarColor.WithAlpha(BackgroundStarAlpha(star.Magnitude));
            canvas.DrawCircle(pt.X, pt.Y, r, bgStarPaint);
        }

        // --- Draw asterism segments ---
        using var asterismLinePaint = new Paint(PaintFlags.AntiAlias);
        asterismLinePaint.SetStyle(Paint.Style.Stroke!);
        asterismLinePaint.Color = AsterismLineColor;
        asterismLinePaint.StrokeWidth = 4f;
        foreach (var (from, to) in data.AsterismSegments)
        {
            if (from < 0 || from >= data.AsterismStars.Count || to < 0 || to >= data.AsterismStars.Count)
                continue;
            var p1 = ToCanvas(data.AsterismStars[from].RaHours, data.AsterismStars[from].DecDeg);
            var p2 = ToCanvas(data.AsterismStars[to].RaHours, data.AsterismStars[to].DecDeg);
            canvas.DrawLine(p1.X, p1.Y, p2.X, p2.Y, asterismLinePaint);
        }

        // --- Draw asterism stars ---
        using var asterismPaint = new Paint(PaintFlags.AntiAlias);
        asterismPaint.SetStyle(Paint.Style.Fill!);
        asterismPaint.Color = AsterismColor;
        foreach (var star in data.AsterismStars)
        {
            var pt = ToCanvas(star.RaHours, star.DecDeg);
            canvas.DrawCircle(pt.X, pt.Y, StarRadius(star.Magnitude) * 1.6f, asterismPaint);
        }

        // --- Draw hop route ---
        if (data.HopSteps.Count > 0)
        {
            using var hopPaint = new Paint(PaintFlags.AntiAlias);
            hopPaint.SetStyle(Paint.Style.Stroke!);
            hopPaint.Color = HopRouteColor;
            hopPaint.StrokeWidth = 5f;
            hopPaint.SetPathEffect(new DashPathEffect(new float[] { 28f, 16f }, 0f));

            for (int i = 0; i < data.HopSteps.Count - 1; i++)
            {
                var from = ToCanvas(data.HopSteps[i].RaHours, data.HopSteps[i].DecDeg);
                var to = ToCanvas(data.HopSteps[i + 1].RaHours, data.HopSteps[i + 1].DecDeg);
                canvas.DrawLine(from.X, from.Y, to.X, to.Y, hopPaint);
            }

            // Last hop to target.
            var lastHop = ToCanvas(data.HopSteps[^1].RaHours, data.HopSteps[^1].DecDeg);
            var targetPt = ToCanvas(data.Target.RaHours, data.Target.DecDeg);
            canvas.DrawLine(lastHop.X, lastHop.Y, targetPt.X, targetPt.Y, hopPaint);

            // Intermediate hop dots.
            using var hopDotPaint = new Paint(PaintFlags.AntiAlias);
            hopDotPaint.SetStyle(Paint.Style.Fill!);
            hopDotPaint.Color = HopRouteColor;
            for (int i = 1; i < data.HopSteps.Count; i++)
            {
                var pt = ToCanvas(data.HopSteps[i].RaHours, data.HopSteps[i].DecDeg);
                canvas.DrawCircle(pt.X, pt.Y, StarRadius(data.HopSteps[i].Magnitude) * 1.6f + 2f, hopDotPaint);
            }
        }

        // --- Draw anchor star (first hop step) ---
        if (data.HopSteps.Count > 0)
        {
            var anchor = data.HopSteps[0];
            var anchorPt = ToCanvas(anchor.RaHours, anchor.DecDeg);
            var anchorRadius = StarRadius(anchor.Magnitude) * 1.6f + 8f;

            using var anchorRingPaint = new Paint(PaintFlags.AntiAlias);
            anchorRingPaint.SetStyle(Paint.Style.Stroke!);
            anchorRingPaint.Color = AnchorColor;
            anchorRingPaint.StrokeWidth = 4f;
            canvas.DrawCircle(anchorPt.X, anchorPt.Y, anchorRadius, anchorRingPaint);

            using var anchorFillPaint = new Paint(PaintFlags.AntiAlias);
            anchorFillPaint.SetStyle(Paint.Style.Fill!);
            anchorFillPaint.Color = AnchorColor;
            canvas.DrawCircle(anchorPt.X, anchorPt.Y, StarRadius(anchor.Magnitude) * 1.6f, anchorFillPaint);
        }

        // --- Draw target crosshair ---
        {
            var tgt = ToCanvas(data.Target.RaHours, data.Target.DecDeg);
            float sz = 18f;

            using var targetPaint = new Paint(PaintFlags.AntiAlias);
            targetPaint.SetStyle(Paint.Style.Stroke!);
            targetPaint.Color = TargetColor;
            targetPaint.StrokeWidth = 4.5f;

            canvas.DrawLine(tgt.X - sz, tgt.Y, tgt.X + sz, tgt.Y, targetPaint);
            canvas.DrawLine(tgt.X, tgt.Y - sz, tgt.X, tgt.Y + sz, targetPaint);
            canvas.DrawCircle(tgt.X, tgt.Y, sz * 0.7f, targetPaint);
        }

        // --- Draw labels ---
        using var labelPaint = new Paint(PaintFlags.AntiAlias);
        labelPaint.TextSize = 38f;
        labelPaint.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal));
        labelPaint.SetShadowLayer(6f, 2f, 2f, LabelShadowColor);
        labelPaint.Color = LabelTextColor;

        // Target label.
        if (!string.IsNullOrEmpty(data.Target.Label))
        {
            var tgt = ToCanvas(data.Target.RaHours, data.Target.DecDeg);
            labelPaint.Color = TargetColor;
            labelPaint.TextSize = 44f;
            canvas.DrawText(data.Target.Label, tgt.X + 24f, tgt.Y - 20f, labelPaint);
        }

        // Anchor label.
        if (data.HopSteps.Count > 0 && !string.IsNullOrEmpty(data.HopSteps[0].Label))
        {
            var anchor = data.HopSteps[0];
            var pt = ToCanvas(anchor.RaHours, anchor.DecDeg);
            labelPaint.Color = AnchorColor;
            labelPaint.TextSize = 40f;
            canvas.DrawText(anchor.Label ?? "", pt.X + 20f, pt.Y - 16f, labelPaint);
        }

        // Asterism star labels.
        labelPaint.Color = LabelTextColor;
        labelPaint.TextSize = 36f;
        foreach (var star in data.AsterismStars)
        {
            if (string.IsNullOrEmpty(star.Label)) continue;
            var pt = ToCanvas(star.RaHours, star.DecDeg);
            canvas.DrawText(star.Label, pt.X + 16f, pt.Y - 12f, labelPaint);
        }

        return new MapBitmapResult(
            bitmap,
            centerRaDeg / 15.0,
            centerDecDeg,
            angularWidthDeg);
    }

    /// <summary>
    /// Gnomonic (tangent-plane) projection — same math as StarMapHtmlBuilder.
    /// </summary>
    private static (double x, double y) GnomonicProject(double raDeg, double decDeg, double centerRaDeg, double centerDecDeg)
    {
        var ra = raDeg * DegToRad;
        var dec = decDeg * DegToRad;
        var ra0 = centerRaDeg * DegToRad;
        var dec0 = centerDecDeg * DegToRad;

        var cosDec = Math.Cos(dec);
        var sinDec = Math.Sin(dec);
        var cosDec0 = Math.Cos(dec0);
        var sinDec0 = Math.Sin(dec0);
        var cosDra = Math.Cos(ra - ra0);

        var denom = sinDec0 * sinDec + cosDec0 * cosDec * cosDra;
        if (denom < 0.001) denom = 0.001;

        var x = (cosDec * Math.Sin(ra - ra0)) / denom;
        var y = (cosDec0 * sinDec - sinDec0 * cosDec * cosDra) / denom;

        return (x, y);
    }

    private static float StarRadius(double magnitude) =>
        Math.Max(3f, (float)(8.0 - magnitude));

    private static int BackgroundStarAlpha(double magnitude) =>
        Math.Clamp((int)(180 - magnitude * 18), 40, 180);

    private static Color WithAlpha(this Color color, int alpha) =>
        Color.Argb(alpha, color.R, color.G, color.B);
}
