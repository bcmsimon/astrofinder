using System.Net;
using System.Text;
using AstroApps.Equipment.Profiles.Enums;
using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;
using Microsoft.Maui.Graphics;

namespace AstroFinder.App.Views;

/// <summary>
/// Builds a static HTML/SVG representation of the star map so the platform WebView
/// can use its native pinch-zoom and panning behavior.
/// </summary>
internal static class StarMapHtmlBuilder
{
    private const float CanvasWidth = 1400f;
    private const float CanvasHeight = 1400f;
    private const float OuterMargin = 18f;
    private const float PlotInset = 14f;
    private const double DegToRad = Math.PI / 180.0;

    public static string BuildHtml(StarMapData data)
    {
        var pageBackground = ResolveColor("ColorBackground", "#F6F8FC");
        var plotBackground = ResolveColor("ColorSurface", "#FFFFFF");
        var borderColor = ResolveColor("ColorBorder", "#D4DBEA");
        var textPrimary = ResolveColor("ColorTextPrimary", "#101828");
        var textSecondary = ResolveColor("ColorTextSecondary", "#344054");
        var primary = ResolveColor("ColorPrimary", "#2457D6");
        var accent = ResolveColor("ColorAccent", "#D97706");
        var success = ResolveColor("ColorSuccess", "#15803D");
        var error = ResolveColor("ColorError", "#B91C1C");

        var svg = BuildSvg(data, pageBackground, plotBackground, borderColor, textPrimary, textSecondary, primary, accent, success, error);

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0, minimum-scale=1.0, maximum-scale=8.0, user-scalable=yes" />
  <style>
    html, body {
      margin: 0;
      padding: 0;
      width: 100%;
      min-height: 100%;
      background: {{ToCss(pageBackground)}};
      overflow: auto;
      touch-action: pan-x pan-y pinch-zoom;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
    }

    .wrap {
      width: 100%;
      max-width: {{CanvasWidth}}px;
      margin: 0 auto;
      background: {{ToCss(pageBackground)}};
    }

    svg {
      display: block;
      width: 100%;
      max-width: {{CanvasWidth}}px;
      height: auto;
      aspect-ratio: 1 / 1;
      background: {{ToCss(pageBackground)}};
    }
  </style>
</head>
<body>
  <div class="wrap">
    {{svg}}
  </div>
</body>
</html>
""";
    }

    private static string BuildSvg(
        StarMapData data,
        Color pageBackground,
        Color plotBackground,
        Color borderColor,
        Color textPrimary,
        Color textSecondary,
        Color primary,
        Color accent,
        Color success,
        Color error)
    {
        var dirtyRect = new RectF(0, 0, CanvasWidth, CanvasHeight);
        var plotRect = new RectF(
            dirtyRect.Left + OuterMargin,
            dirtyRect.Top + OuterMargin,
            Math.Max(0, dirtyRect.Width - (2 * OuterMargin)),
            Math.Max(0, dirtyRect.Height - (2 * OuterMargin)));

        var usableRect = new RectF(
            plotRect.Left + PlotInset,
            plotRect.Top + PlotInset,
            Math.Max(0, plotRect.Width - (2 * PlotInset)),
            Math.Max(0, plotRect.Height - (2 * PlotInset)));

        // Keep the star map in equatorial chart space and rotate that chart by the
        // already-computed display angle. This preserves star geometry better than
        // drawing directly in horizontal coordinates.
        (double LonDeg, double LatDeg) ToProjectionCoordinates(double raHours, double decDegrees)
            => (raHours * 15.0, decDegrees);

        var displayRotationRadians = data.UseObserverOrientation
            ? SkyOrientationService.DegreesToRadians(data.DisplayRotationDegrees)
            : 0.0;

        (double x, double y) ProjectForDisplay(double lonDegrees, double latDegrees, double chartCenterLonDeg, double chartCenterLatDeg)
        {
            var (x, y) = Project(lonDegrees, latDegrees, chartCenterLonDeg, chartCenterLatDeg);
            if (!data.UseObserverOrientation)
            {
                return (x, y);
            }

            var rotated = SkyOrientationService.RotatePoint(new PointD(x, y), displayRotationRadians);
            return (rotated.X, rotated.Y);
        }

        var allPoints = new List<(double lon, double lat)>
        {
            ToProjectionCoordinates(data.Target.RaHours, data.Target.DecDeg)
        };

        foreach (var star in data.AsterismStars)
        {
            allPoints.Add(ToProjectionCoordinates(star.RaHours, star.DecDeg));
        }

        foreach (var star in data.HopSteps)
        {
            allPoints.Add(ToProjectionCoordinates(star.RaHours, star.DecDeg));
        }

        var centerLonDeg = allPoints.Average(p => p.lon);
        var centerLatDeg = allPoints.Average(p => p.lat);

        var projected = allPoints
            .Select(p => ProjectForDisplay(p.lon, p.lat, centerLonDeg, centerLatDeg))
            .ToList();

        double minX = projected.Min(p => p.x);
        double maxX = projected.Max(p => p.x);
        double minY = projected.Min(p => p.y);
        double maxY = projected.Max(p => p.y);

        double rangeX = Math.Max(0.001, maxX - minX);
        double rangeY = Math.Max(0.001, maxY - minY);

        const double padFraction = 0.18;
        minX -= rangeX * padFraction;
        maxX += rangeX * padFraction;
        minY -= rangeY * padFraction;
        maxY += rangeY * padFraction;
        rangeX = Math.Max(0.001, maxX - minX);
        rangeY = Math.Max(0.001, maxY - minY);

        double scale = Math.Min(usableRect.Width / rangeX, usableRect.Height / rangeY);
        float centeredOffsetX = (float)((usableRect.Width - (rangeX * scale)) / 2.0);
        float centeredOffsetY = (float)((usableRect.Height - (rangeY * scale)) / 2.0);

        PointF ToCanvasProjected(double lonDegrees, double latDegrees)
        {
            var (px, py) = ProjectForDisplay(lonDegrees, latDegrees, centerLonDeg, centerLatDeg);
            var x = usableRect.Left + centeredOffsetX + (float)((maxX - px) * scale);
            var y = usableRect.Top + centeredOffsetY + (float)((maxY - py) * scale);
            return new PointF(x, y);
        }

        PointF ToCanvas(double raHours, double decDegrees)
        {
            var projection = ToProjectionCoordinates(raHours, decDegrees);
            return ToCanvasProjected(projection.LonDeg, projection.LatDeg);
        }

        var centerPoint = ToCanvasProjected(centerLonDeg, centerLatDeg);
        var targetPoint = ToCanvas(data.Target.RaHours, data.Target.DecDeg);
        var targetSize = 10f;

        PointF? anchorPoint = null;
        if (data.HopSteps.Count > 0)
        {
            var anchor = data.HopSteps[0];
            anchorPoint = ToCanvas(anchor.RaHours, anchor.DecDeg);
        }

        var occupiedAreas = new List<RectF>
        {
            InflateRect(new RectF(usableRect.Left, centerPoint.Y - 1.5f, usableRect.Width, 3f), 1f),
            InflateRect(new RectF(centerPoint.X - 1.5f, usableRect.Top, 3f, usableRect.Height), 1f)
        };

        foreach (var star in data.AsterismStars)
        {
            AddPointObstacle(occupiedAreas, ToCanvas(star.RaHours, star.DecDeg), StarRadius(star.Magnitude) + 4f);
        }

        foreach (var (from, to) in data.AsterismSegments)
        {
            if (from < 0 || from >= data.AsterismStars.Count || to < 0 || to >= data.AsterismStars.Count)
            {
                continue;
            }

            var fromPoint = ToCanvas(data.AsterismStars[from].RaHours, data.AsterismStars[from].DecDeg);
            var toPoint = ToCanvas(data.AsterismStars[to].RaHours, data.AsterismStars[to].DecDeg);
            AddLineObstacle(occupiedAreas, fromPoint, toPoint, 4f);
        }

        if (data.HopSteps.Count > 0)
        {
            for (var i = 0; i < data.HopSteps.Count - 1; i++)
            {
                var from = data.HopSteps[i];
                var to = data.HopSteps[i + 1];
                var fromPoint = ToCanvas(from.RaHours, from.DecDeg);
                var toPoint = ToCanvas(to.RaHours, to.DecDeg);
                AddLineObstacle(occupiedAreas, fromPoint, toPoint, 5f);
            }

            var lastHop = data.HopSteps[^1];
            AddLineObstacle(occupiedAreas, ToCanvas(lastHop.RaHours, lastHop.DecDeg), targetPoint, 5f);

            foreach (var step in data.HopSteps)
            {
                AddPointObstacle(occupiedAreas, ToCanvas(step.RaHours, step.DecDeg), StarRadius(step.Magnitude) + 5f);
            }
        }

        AddPointObstacle(occupiedAreas, targetPoint, targetSize + 5f);
        if (anchorPoint.HasValue && data.HopSteps.Count > 0)
        {
            AddPointObstacle(occupiedAreas, anchorPoint.Value, StarRadius(data.HopSteps[0].Magnitude) + 8f);
        }

        var sb = new StringBuilder();
        var plotId = "plotClip";
        var skyGradId = "skyGlow";

        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {CanvasWidth} {CanvasHeight}' width='{CanvasWidth}' height='{CanvasHeight}'>");
        sb.Append($"<defs><clipPath id='{plotId}'><rect x='{usableRect.Left:F1}' y='{usableRect.Top:F1}' width='{usableRect.Width:F1}' height='{usableRect.Height:F1}' rx='18' ry='18' /></clipPath><radialGradient id='{skyGradId}' cx='50%' cy='50%' r='50%' fx='50%' fy='50%'><stop offset='0%' stop-color='#1e2654'/><stop offset='60%' stop-color='#111530'/><stop offset='100%' stop-color='#090c1e'/></radialGradient></defs>");
        sb.Append($"<rect x='0' y='0' width='{CanvasWidth}' height='{CanvasHeight}' fill='{ToCss(pageBackground)}' />");
        sb.Append($"<rect x='{plotRect.Left:F1}' y='{plotRect.Top:F1}' width='{plotRect.Width:F1}' height='{plotRect.Height:F1}' rx='16' ry='16' fill='url(#{skyGradId})' stroke='{ToCss(borderColor)}' stroke-width='2' />");

        sb.Append($"<g clip-path='url(#{plotId})'>");
        sb.Append($"<line x1='{usableRect.Left:F1}' y1='{centerPoint.Y:F1}' x2='{usableRect.Right:F1}' y2='{centerPoint.Y:F1}' stroke='#FFFFFF' stroke-opacity='0.10' stroke-width='1.5' />");
        sb.Append($"<line x1='{centerPoint.X:F1}' y1='{usableRect.Top:F1}' x2='{centerPoint.X:F1}' y2='{usableRect.Bottom:F1}' stroke='#FFFFFF' stroke-opacity='0.10' stroke-width='1.5' />");

        foreach (var star in data.MapFillStars)
        {
            var pt = ToCanvas(star.RaHours, star.DecDeg);
            var r = StarRadius(star.Magnitude) * 1.1f;
            sb.Append($"<circle cx='{pt.X:F1}' cy='{pt.Y:F1}' r='{r:F1}' fill='#C8D4F0' fill-opacity='{BackgroundStarAlpha(star.Magnitude) * 0.5f:F2}' />");
        }

        foreach (var star in data.BackgroundStars)
        {
            var pt = ToCanvas(star.RaHours, star.DecDeg);
            var r = StarRadius(star.Magnitude) * 1.4f;
            sb.Append($"<circle cx='{pt.X:F1}' cy='{pt.Y:F1}' r='{r:F1}' fill='#C8D4F0' fill-opacity='{BackgroundStarAlpha(star.Magnitude):F2}' />");
        }

        foreach (var (from, to) in data.AsterismSegments)
        {
            if (from < 0 || from >= data.AsterismStars.Count || to < 0 || to >= data.AsterismStars.Count)
            {
                continue;
            }

            var s1 = data.AsterismStars[from];
            var s2 = data.AsterismStars[to];
            var p1 = ToCanvas(s1.RaHours, s1.DecDeg);
            var p2 = ToCanvas(s2.RaHours, s2.DecDeg);
            sb.Append($"<line x1='{p1.X:F1}' y1='{p1.Y:F1}' x2='{p2.X:F1}' y2='{p2.Y:F1}' stroke='#6B8AF5' stroke-opacity='0.85' stroke-width='2.4' />");
        }

        foreach (var star in data.AsterismStars)
        {
            var pt = ToCanvas(star.RaHours, star.DecDeg);
            sb.Append($"<circle cx='{pt.X:F1}' cy='{pt.Y:F1}' r='{StarRadius(star.Magnitude):F1}' fill='#6B8AF5' fill-opacity='0.92' />");
        }

        if (data.HopSteps.Count > 0)
        {
            for (var i = 0; i < data.HopSteps.Count - 1; i++)
            {
                var from = ToCanvas(data.HopSteps[i].RaHours, data.HopSteps[i].DecDeg);
                var to = ToCanvas(data.HopSteps[i + 1].RaHours, data.HopSteps[i + 1].DecDeg);
                sb.Append($"<line x1='{from.X:F1}' y1='{from.Y:F1}' x2='{to.X:F1}' y2='{to.Y:F1}' stroke='#FBBF24' stroke-width='3' stroke-dasharray='16 10' />");
            }

            var lastHop = data.HopSteps[^1];
            var lastHopPoint = ToCanvas(lastHop.RaHours, lastHop.DecDeg);
            sb.Append($"<line x1='{lastHopPoint.X:F1}' y1='{lastHopPoint.Y:F1}' x2='{targetPoint.X:F1}' y2='{targetPoint.Y:F1}' stroke='#FBBF24' stroke-width='3' stroke-dasharray='16 10' />");

            for (var i = 1; i < data.HopSteps.Count; i++)
            {
                var pt = ToCanvas(data.HopSteps[i].RaHours, data.HopSteps[i].DecDeg);
                sb.Append($"<circle cx='{pt.X:F1}' cy='{pt.Y:F1}' r='{(StarRadius(data.HopSteps[i].Magnitude) + 1f):F1}' fill='#FBBF24' />");
            }
        }

        if (anchorPoint.HasValue && data.HopSteps.Count > 0)
        {
            var anchorRadius = StarRadius(data.HopSteps[0].Magnitude) + 5f;
            sb.Append($"<circle cx='{anchorPoint.Value.X:F1}' cy='{anchorPoint.Value.Y:F1}' r='{anchorRadius:F1}' fill='none' stroke='#34D399' stroke-width='2.2' />");
        }

        // Nearby catalog targets — only draw those whose projection lands inside the viewport
        var displayRotationDeg = data.UseObserverOrientation ? data.DisplayRotationDegrees : 0.0;
        foreach (var nearby in data.NearbyTargets)
        {
            var pt = ToCanvas(nearby.RaHours, nearby.DecDeg);
            if (!usableRect.Contains(pt))
                continue;
            if (nearby.Category == ShootingTargetCategory.Galaxy)
            {
                var pa = nearby.PositionAngleDeg ?? 0.0;
                var svgRot = 180.0 + displayRotationDeg - pa;
                // Filled inner body + outer ring for visibility
                sb.Append($"<ellipse cx='{pt.X:F1}' cy='{pt.Y:F1}' rx='6' ry='14' fill='#22D3EE' fill-opacity='0.15' stroke='#22D3EE' stroke-width='2' transform='rotate({svgRot:F1},{pt.X:F1},{pt.Y:F1})' />");
                sb.Append($"<ellipse cx='{pt.X:F1}' cy='{pt.Y:F1}' rx='9' ry='20' fill='none' stroke='#22D3EE' stroke-width='1.5' stroke-opacity='0.5' transform='rotate({svgRot:F1},{pt.X:F1},{pt.Y:F1})' />");
            }
            else
            {
                sb.Append($"<circle cx='{pt.X:F1}' cy='{pt.Y:F1}' r='7' fill='#22D3EE' fill-opacity='0.15' stroke='#22D3EE' stroke-width='2' />");
                sb.Append($"<circle cx='{pt.X:F1}' cy='{pt.Y:F1}' r='11' fill='none' stroke='#22D3EE' stroke-width='1.5' stroke-opacity='0.5' />");
            }

            AddPointObstacle(occupiedAreas, pt, 14f);
        }

        // Primary target — galaxy draws as oriented ellipse; other types draw standard crosshair+circle
        if (data.TargetCategory == ShootingTargetCategory.Galaxy)
        {
            var pa = data.TargetPositionAngleDeg ?? 0.0;
            var svgRot = 180.0 + displayRotationDeg - pa;
            sb.Append($"<ellipse cx='{targetPoint.X:F1}' cy='{targetPoint.Y:F1}' rx='7' ry='16' fill='#F87171' fill-opacity='0.12' stroke='#F87171' stroke-width='2.2' transform='rotate({svgRot:F1},{targetPoint.X:F1},{targetPoint.Y:F1})' />");
            sb.Append($"<line x1='{targetPoint.X - targetSize * 0.6f:F1}' y1='{targetPoint.Y:F1}' x2='{targetPoint.X + targetSize * 0.6f:F1}' y2='{targetPoint.Y:F1}' stroke='#F87171' stroke-width='1.8' />");
            sb.Append($"<line x1='{targetPoint.X:F1}' y1='{targetPoint.Y - targetSize * 0.6f:F1}' x2='{targetPoint.X:F1}' y2='{targetPoint.Y + targetSize * 0.6f:F1}' stroke='#F87171' stroke-width='1.8' />");
        }
        else
        {
            sb.Append($"<line x1='{targetPoint.X - targetSize:F1}' y1='{targetPoint.Y:F1}' x2='{targetPoint.X + targetSize:F1}' y2='{targetPoint.Y:F1}' stroke='#F87171' stroke-width='2.6' />");
            sb.Append($"<line x1='{targetPoint.X:F1}' y1='{targetPoint.Y - targetSize:F1}' x2='{targetPoint.X:F1}' y2='{targetPoint.Y + targetSize:F1}' stroke='#F87171' stroke-width='2.6' />");
            sb.Append($"<circle cx='{targetPoint.X:F1}' cy='{targetPoint.Y:F1}' r='{(targetSize * 0.7f):F1}' fill='none' stroke='#F87171' stroke-width='2.2' />");
        }
        sb.Append("</g>");

        sb.Append($"<text x='{plotRect.Left + 12:F1}' y='{plotRect.Top + 22:F1}' fill='#A0AABB' font-size='22'>via {WebUtility.HtmlEncode(data.AsterismName)}</text>");
        if (data.UseObserverOrientation)
        {
            AppendNorthPointer(sb, plotRect, textSecondary, accent, displayRotationRadians, data.IsTargetAboveHorizon);
        }
        else
        {
            sb.Append($"<text x='{plotRect.Left + (plotRect.Width / 2f):F1}' y='{plotRect.Top + 22:F1}' fill='#A0AABB' font-size='22' text-anchor='middle'>N</text>");
            sb.Append($"<text x='{plotRect.Left + 12:F1}' y='{plotRect.Top + (plotRect.Height / 2f):F1}' fill='#A0AABB' font-size='22'>E</text>");
            sb.Append($"<text x='{plotRect.Right - 12:F1}' y='{plotRect.Top + (plotRect.Height / 2f):F1}' fill='#A0AABB' font-size='22' text-anchor='end'>W</text>");
        }

        var darkPlotBg = Color.FromArgb("#0d1030");
        var labelColor = Color.FromArgb("#E4EAFF");
        var placedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ls = data.LabelScale;
        AppendLabel(sb, usableRect, occupiedAreas, targetPoint, data.Target.Label, labelColor, darkPlotBg, centerPoint, 28f * ls, targetSize + 4f, placedLabels);

        if (data.HopSteps.Count > 0)
        {
            var anchor = data.HopSteps[0];
            AppendLabel(sb, usableRect, occupiedAreas, anchorPoint ?? ToCanvas(anchor.RaHours, anchor.DecDeg), anchor.Label, labelColor, darkPlotBg, centerPoint, 24f * ls, StarRadius(anchor.Magnitude) + 6f, placedLabels);
        }

        foreach (var step in data.HopSteps.Skip(1))
        {
            AppendLabel(sb, usableRect, occupiedAreas, ToCanvas(step.RaHours, step.DecDeg), step.Label, labelColor, darkPlotBg, centerPoint, 22f * ls, StarRadius(step.Magnitude) + 5f, placedLabels);
        }

        foreach (var star in data.AsterismStars)
        {
            AppendLabel(sb, usableRect, occupiedAreas, ToCanvas(star.RaHours, star.DecDeg), star.Label, labelColor, darkPlotBg, centerPoint, 22f * ls, StarRadius(star.Magnitude) + 4f, placedLabels);
        }

        var nearbyLabelColor = Color.FromArgb("#67E8F9");
        foreach (var nearby in data.NearbyTargets)
        {
            var nearbyPt = ToCanvas(nearby.RaHours, nearby.DecDeg);
            if (!usableRect.Contains(nearbyPt))
                continue;
            AppendLabel(sb, usableRect, occupiedAreas, nearbyPt, nearby.Label, nearbyLabelColor, darkPlotBg, centerPoint, 20f * ls, 9f, placedLabels);
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static void AppendNorthPointer(
        StringBuilder sb,
        RectF plotRect,
        Color textColor,
        Color accent,
        double displayRotationRadians,
        bool isTargetAboveHorizon)
    {
        var northDirection = SkyOrientationService.RotatePoint(new PointD(0.0, 1.0), displayRotationRadians);
        var screenVector = new PointD(-northDirection.X, -northDirection.Y);
        var pointerCenter = new PointF(plotRect.Right - 96f, plotRect.Top + 114f);
        var perpendicular = new PointD(-screenVector.Y, screenVector.X);

        const float tipLength = 62f;
        const float tailLength = 30f;
        const float wingOffset = 18f;
        const float wingPullback = 8f;

        var tip = new PointF(
            pointerCenter.X + (float)(screenVector.X * tipLength),
            pointerCenter.Y + (float)(screenVector.Y * tipLength));
        var tail = new PointF(
            pointerCenter.X - (float)(screenVector.X * tailLength),
            pointerCenter.Y - (float)(screenVector.Y * tailLength));
        var leftWing = new PointF(
            pointerCenter.X - (float)(perpendicular.X * wingOffset) - (float)(screenVector.X * wingPullback),
            pointerCenter.Y - (float)(perpendicular.Y * wingOffset) - (float)(screenVector.Y * wingPullback));
        var rightWing = new PointF(
            pointerCenter.X + (float)(perpendicular.X * wingOffset) - (float)(screenVector.X * wingPullback),
            pointerCenter.Y + (float)(perpendicular.Y * wingOffset) - (float)(screenVector.Y * wingPullback));

        sb.Append("<g id='north-pointer'>");
        sb.Append($"<polygon points='{tip.X:F1},{tip.Y:F1} {leftWing.X:F1},{leftWing.Y:F1} {tail.X:F1},{tail.Y:F1}' fill='#FFFFFF' fill-opacity='0.85' stroke='#FBBF24' stroke-width='4' stroke-linejoin='round' />");
        sb.Append($"<polygon points='{tip.X:F1},{tip.Y:F1} {rightWing.X:F1},{rightWing.Y:F1} {tail.X:F1},{tail.Y:F1}' fill='#FBBF24' stroke='#FBBF24' stroke-width='4' stroke-linejoin='round' />");
        sb.Append($"<text x='{tip.X:F1}' y='{(tip.Y - 14f):F1}' fill='#E4EAFF' font-size='28' font-weight='700' text-anchor='middle'>N</text>");

        if (!isTargetAboveHorizon)
        {
            sb.Append($"<text x='{pointerCenter.X:F1}' y='{(pointerCenter.Y + 58f):F1}' fill='#A0AABB' font-size='16' text-anchor='middle'>Target below horizon</text>");
        }

        sb.Append("</g>");
    }

    private static void AppendLabel(
        StringBuilder sb,
        RectF bounds,
        List<RectF> occupiedAreas,
        PointF point,
        string? text,
        Color textColor,
        Color backgroundColor,
        PointF centerPoint,
        float fontSize,
        float markerRadius,
        ISet<string> placedLabels)
    {
        if (string.IsNullOrWhiteSpace(text) || !placedLabels.Add(text))
        {
            return;
        }

        var labelWidth = MathF.Min(bounds.Width * 0.34f, 8f + (text.Length * fontSize * 0.52f));
        var labelHeight = fontSize + 6f;

        RectF bestRect = default;
        var foundPerfectFit = false;
        var bestScore = int.MaxValue;
        var bestDistance = float.MaxValue;

        foreach (var (dx, dy) in BuildCandidateOffsets(point, centerPoint, labelWidth, labelHeight, markerRadius))
        {
            var candidate = ClampRectToBounds(new RectF(point.X + dx, point.Y + dy, labelWidth, labelHeight), bounds, 4f);
            var expanded = InflateRect(candidate, 2f);
            var overlapCount = 0;

            foreach (var obstacle in occupiedAreas)
            {
                if (RectsIntersect(expanded, obstacle))
                {
                    overlapCount++;
                }
            }

            var distance = MathF.Abs(candidate.X - point.X) + MathF.Abs(candidate.Y - point.Y);
            if (overlapCount == 0)
            {
                bestRect = candidate;
                foundPerfectFit = true;
                break;
            }

            if (overlapCount < bestScore || (overlapCount == bestScore && distance < bestDistance))
            {
                bestRect = candidate;
                bestScore = overlapCount;
                bestDistance = distance;
            }
        }

        if (!foundPerfectFit && bestScore == int.MaxValue)
        {
            return;
        }

        var calloutPoint = ClosestPointOnRect(bestRect, point);
        var vectorX = calloutPoint.X - point.X;
        var vectorY = calloutPoint.Y - point.Y;
        var vectorLength = MathF.Sqrt((vectorX * vectorX) + (vectorY * vectorY));

        if (vectorLength > markerRadius + 2f)
        {
            var startX = point.X + ((vectorX / vectorLength) * (markerRadius + 1.5f));
            var startY = point.Y + ((vectorY / vectorLength) * (markerRadius + 1.5f));
            sb.Append($"<line x1='{startX:F1}' y1='{startY:F1}' x2='{calloutPoint.X:F1}' y2='{calloutPoint.Y:F1}' stroke='{ToCss(textColor, 0.32f)}' stroke-width='1.2' />");
        }

        var encodedText = WebUtility.HtmlEncode(text);
        var shadowColor = backgroundColor.GetLuminosity() < 0.5f
            ? Colors.Black.WithAlpha(0.72f)
            : Colors.White.WithAlpha(0.68f);

        sb.Append($"<text x='{bestRect.Left + 2.5f:F1}' y='{bestRect.Bottom - 2f:F1}' fill='{ToCss(shadowColor)}' font-size='{fontSize:F1}'>{encodedText}</text>");
        sb.Append($"<text x='{bestRect.Left + 1.5f:F1}' y='{bestRect.Bottom - 3f:F1}' fill='{ToCss(textColor)}' font-size='{fontSize:F1}'>{encodedText}</text>");

        occupiedAreas.Add(InflateRect(bestRect, 5f));
    }

    private static IEnumerable<(float dx, float dy)> BuildCandidateOffsets(PointF point, PointF centerPoint, float labelWidth, float labelHeight, float markerRadius)
    {
        var gap = Math.Max(8f, markerRadius + 6f);
        var outwardX = point.X >= centerPoint.X ? gap : -labelWidth - gap;
        var inwardX = point.X >= centerPoint.X ? -labelWidth - gap : gap;
        var outwardY = point.Y >= centerPoint.Y ? gap : -labelHeight - gap;
        var inwardY = point.Y >= centerPoint.Y ? -labelHeight - gap : gap;

        yield return (-labelWidth * 0.5f, inwardY);
        yield return (outwardX, inwardY);
        yield return (outwardX, -labelHeight * 0.5f);
        yield return (outwardX, outwardY);
        yield return (-labelWidth * 0.5f, outwardY);
        yield return (inwardX, inwardY);
        yield return (inwardX, -labelHeight * 0.5f);
        yield return (inwardX, outwardY);
        yield return (gap, -labelHeight - gap);
        yield return (gap, gap);
        yield return (-labelWidth - gap, -labelHeight - gap);
        yield return (-labelWidth - gap, gap);
    }

    private static RectF ClampRectToBounds(RectF rect, RectF bounds, float inset)
    {
        var x = Math.Clamp(rect.X, bounds.Left + inset, bounds.Right - rect.Width - inset);
        var y = Math.Clamp(rect.Y, bounds.Top + inset, bounds.Bottom - rect.Height - inset);
        return new RectF(x, y, rect.Width, rect.Height);
    }

    private static PointF ClosestPointOnRect(RectF rect, PointF point)
    {
        var x = Math.Clamp(point.X, rect.Left, rect.Right);
        var y = Math.Clamp(point.Y, rect.Top, rect.Bottom);
        return new PointF(x, y);
    }

    private static void AddPointObstacle(List<RectF> obstacles, PointF point, float radius)
    {
        obstacles.Add(new RectF(point.X - radius, point.Y - radius, radius * 2f, radius * 2f));
    }

    private static void AddLineObstacle(List<RectF> obstacles, PointF from, PointF to, float padding)
    {
        var left = Math.Min(from.X, to.X) - padding;
        var top = Math.Min(from.Y, to.Y) - padding;
        var width = Math.Abs(from.X - to.X) + (padding * 2f);
        var height = Math.Abs(from.Y - to.Y) + (padding * 2f);
        obstacles.Add(new RectF(left, top, width, height));
    }

    private static RectF InflateRect(RectF rect, float amount)
    {
        return new RectF(rect.X - amount, rect.Y - amount, rect.Width + (amount * 2f), rect.Height + (amount * 2f));
    }

    private static bool RectsIntersect(RectF a, RectF b)
    {
        return a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;
    }

    private static (double x, double y) Project(double raDeg, double decDeg, double centerRaDeg, double centerDecDeg)
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
        if (denom < 0.001)
        {
            denom = 0.001;
        }

        var x = (cosDec * Math.Sin(ra - ra0)) / denom;
        var y = (cosDec0 * sinDec - sinDec0 * cosDec * cosDra) / denom;

        return (x, y);
    }

    private static float StarRadius(double magnitude)
    {
        return Math.Max(1.5f, (float)(5.0 - (0.7 * magnitude)));
    }

    private static float BackgroundStarAlpha(double magnitude)
    {
        var alpha = 0.72f - ((float)magnitude * 0.07f);
        return Math.Clamp(alpha, 0.18f, 0.72f);
    }

    private static Color ResolveColor(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
        {
            return color;
        }

        return Color.FromArgb(fallbackHex);
    }

    private static string ToCss(Color color, float alphaMultiplier = 1f)
    {
        var red = (int)Math.Round(color.Red * 255);
        var green = (int)Math.Round(color.Green * 255);
        var blue = (int)Math.Round(color.Blue * 255);
        var alpha = Math.Clamp(color.Alpha * alphaMultiplier, 0f, 1f);
        return $"rgba({red}, {green}, {blue}, {alpha.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)})";
    }
}
