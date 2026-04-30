using AstroApps.Equipment.Profiles.Enums;
using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.App.Views;

/// <summary>
/// Draws a star hop map using a theme-aware gnomonic projection.
/// </summary>
public sealed class StarMapDrawable : IDrawable
{
    private readonly StarMapData _data;
    private readonly StarMapViewportTransform _viewportTransform;

    private const float OuterMargin = 18f;
    private const float PlotInset = 14f;
    private const double DegToRad = Math.PI / 180.0;

    public StarMapDrawable(StarMapData data, StarMapViewportTransform viewportTransform)
    {
        _data = data;
        _viewportTransform = viewportTransform;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
        {
            return;
        }

        var pageBackground = ResolveColor("ColorBackground", "#F6F8FC");
        var plotBackground = Color.FromArgb("#111830");  // astronomy dark navy — always
        var borderColor = ResolveColor("ColorBorder", "#D4DBEA");
        var textSecondary = ResolveColor("ColorTextSecondary", "#344054");
        var primary = Color.FromArgb("#6B8AF5");
        var accent = Color.FromArgb("#FBBF24");
        var success = Color.FromArgb("#34D399");
        var error = Color.FromArgb("#F87171");
        var dimStar = Color.FromArgb("#C8D4F0");
        var gridColor = Color.FromRgba(255, 255, 255, 25);
        var labelColor = Color.FromArgb("#E4EAFF");
        var nearbyColor = Color.FromArgb("#22D3EE");

        canvas.FillColor = pageBackground;
        canvas.FillRectangle(dirtyRect);

        var plotRect = new RectF(
            dirtyRect.Left + OuterMargin,
            dirtyRect.Top + OuterMargin,
            Math.Max(0, dirtyRect.Width - (2 * OuterMargin)),
            Math.Max(0, dirtyRect.Height - (2 * OuterMargin)));

        if (plotRect.Width < 40 || plotRect.Height < 40)
        {
            return;
        }

        canvas.FillColor = plotBackground;
        canvas.FillRoundedRectangle(plotRect, 16);
        canvas.StrokeColor = borderColor;
        canvas.StrokeSize = 1f;
        canvas.DrawRoundedRectangle(plotRect, 16);

        var usableRect = CreateViewportRect(plotRect);

        var useObserverOrientation = _data.UseObserverOrientation
            && _data.ObserverLatitudeDeg.HasValue
            && _data.ObserverLongitudeDeg.HasValue;

        (double LonDeg, double LatDeg) ToProjectionCoordinates(double raHours, double decDegrees)
        {
            if (useObserverOrientation)
            {
                var horizontal = SkyProjection.EquatorialToHorizontal(
                    new EquatorialCoordinate(raHours, decDegrees),
                    _data.ObserverLatitudeDeg!.Value,
                    _data.ObserverLongitudeDeg!.Value,
                    _data.ObservationTime);

                return (horizontal.AzimuthDegrees, horizontal.AltitudeDegrees);
            }

            return (raHours * 15.0, decDegrees);
        }

        var allPoints = new List<(double lon, double lat)>();
        allPoints.Add(ToProjectionCoordinates(_data.Target.RaHours, _data.Target.DecDeg));

        foreach (var star in _data.AsterismStars)
        {
            allPoints.Add(ToProjectionCoordinates(star.RaHours, star.DecDeg));
        }

        foreach (var star in _data.HopSteps)
        {
            allPoints.Add(ToProjectionCoordinates(star.RaHours, star.DecDeg));
        }

        if (allPoints.Count == 0)
        {
            return;
        }

        double centerLonDeg = allPoints.Average(p => p.lon);
        double centerLatDeg = allPoints.Average(p => p.lat);

        var projected = allPoints
            .Select(p => Project(p.lon, p.lat, centerLonDeg, centerLatDeg))
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
            var (px, py) = Project(lonDegrees, latDegrees, centerLonDeg, centerLatDeg);
            var fittedX = usableRect.Left + centeredOffsetX + (float)((maxX - px) * scale);
            var fittedY = usableRect.Top + centeredOffsetY + (float)((maxY - py) * scale);
            return _viewportTransform.Apply(new PointF(fittedX, fittedY), usableRect);
        }

        PointF ToCanvas(double raHours, double decDegrees)
        {
            var projection = ToProjectionCoordinates(raHours, decDegrees);
            return ToCanvasProjected(projection.LonDeg, projection.LatDeg);
        }

        var centerPoint = ToCanvasProjected(centerLonDeg, centerLatDeg);
        canvas.StrokeColor = gridColor;
        canvas.StrokeSize = 1f;
        canvas.DrawLine(usableRect.Left, centerPoint.Y, usableRect.Right, centerPoint.Y);
        canvas.DrawLine(centerPoint.X, usableRect.Top, centerPoint.X, usableRect.Bottom);

        canvas.SaveState();
        canvas.ClipRectangle(usableRect);

        foreach (var star in _data.MapFillStars)
        {
            var pt = ToCanvas(star.RaHours, star.DecDeg);
            var r = StarRadius(star.Magnitude) * 1.1f;
            canvas.FillColor = dimStar.WithAlpha(BackgroundStarAlpha(star.Magnitude) * 0.5f);
            canvas.FillCircle(pt, r);
        }

        foreach (var star in _data.BackgroundStars)
        {
            var pt = ToCanvas(star.RaHours, star.DecDeg);
            var r = StarRadius(star.Magnitude) * 1.4f;
            canvas.FillColor = dimStar.WithAlpha(BackgroundStarAlpha(star.Magnitude));
            canvas.FillCircle(pt, r);
        }

        canvas.StrokeColor = primary;
        canvas.StrokeSize = 1.8f;
        canvas.Alpha = 0.8f;
        foreach (var (from, to) in _data.AsterismSegments)
        {
            if (from < 0 || from >= _data.AsterismStars.Count || to < 0 || to >= _data.AsterismStars.Count)
            {
                continue;
            }

            var s1 = _data.AsterismStars[from];
            var s2 = _data.AsterismStars[to];
            canvas.DrawLine(
                ToCanvas(s1.RaHours, s1.DecDeg),
                ToCanvas(s2.RaHours, s2.DecDeg));
        }
        canvas.Alpha = 1.0f;

        canvas.FillColor = primary.WithAlpha(0.9f);
        foreach (var star in _data.AsterismStars)
        {
            canvas.FillCircle(ToCanvas(star.RaHours, star.DecDeg), StarRadius(star.Magnitude));
        }

        if (_data.HopSteps.Count > 0)
        {
            canvas.StrokeColor = accent;
            canvas.StrokeSize = 2.5f;
            canvas.StrokeDashPattern = [8, 5];

            for (var i = 0; i < _data.HopSteps.Count - 1; i++)
            {
                var from = _data.HopSteps[i];
                var to = _data.HopSteps[i + 1];
                canvas.DrawLine(
                    ToCanvas(from.RaHours, from.DecDeg),
                    ToCanvas(to.RaHours, to.DecDeg));
            }

            var lastHop = _data.HopSteps[^1];
            canvas.DrawLine(
                ToCanvas(lastHop.RaHours, lastHop.DecDeg),
                ToCanvas(_data.Target.RaHours, _data.Target.DecDeg));

            canvas.StrokeDashPattern = null;

            canvas.FillColor = accent;
            for (var i = 1; i < _data.HopSteps.Count; i++)
            {
                var step = _data.HopSteps[i];
                canvas.FillCircle(ToCanvas(step.RaHours, step.DecDeg), StarRadius(step.Magnitude) + 1f);
            }
        }

        PointF? anchorPoint = null;
        if (_data.HopSteps.Count > 0)
        {
            var anchor = _data.HopSteps[0];
            anchorPoint = ToCanvas(anchor.RaHours, anchor.DecDeg);
            canvas.StrokeColor = success;
            canvas.StrokeSize = 2.2f;
            canvas.DrawCircle(anchorPoint.Value, StarRadius(anchor.Magnitude) + 5f);
        }

        // Nearby catalog targets — only draw those whose projection lands inside the viewport
        var displayRotationDeg = _data.UseObserverOrientation ? _data.DisplayRotationDegrees : 0.0;
        foreach (var nearby in _data.NearbyTargets)
        {
            var pt = ToCanvas(nearby.RaHours, nearby.DecDeg);
            if (!usableRect.Contains(pt))
                continue;
            if (nearby.Category == ShootingTargetCategory.Galaxy)
            {
                var pa = nearby.PositionAngleDeg ?? 0.0;
                var rotateDeg = (float)(180.0 + displayRotationDeg - pa);
                canvas.SaveState();
                canvas.Translate(pt.X, pt.Y);
                canvas.Rotate(rotateDeg);
                canvas.FillColor = nearbyColor.WithAlpha(0.15f);
                canvas.StrokeColor = nearbyColor;
                canvas.StrokeSize = 2f;
                canvas.FillEllipse(-6f, -14f, 12f, 28f);
                canvas.DrawEllipse(-6f, -14f, 12f, 28f);
                canvas.StrokeColor = nearbyColor.WithAlpha(0.5f);
                canvas.StrokeSize = 1.5f;
                canvas.DrawEllipse(-9f, -20f, 18f, 40f);
                canvas.RestoreState();
            }
            else
            {
                canvas.FillColor = nearbyColor.WithAlpha(0.15f);
                canvas.StrokeColor = nearbyColor;
                canvas.StrokeSize = 2f;
                canvas.FillCircle(pt, 7f);
                canvas.DrawCircle(pt, 7f);
                canvas.StrokeColor = nearbyColor.WithAlpha(0.5f);
                canvas.StrokeSize = 1.5f;
                canvas.DrawCircle(pt, 11f);
            }
        }

        var targetPoint = ToCanvas(_data.Target.RaHours, _data.Target.DecDeg);
        canvas.StrokeSize = 2.2f;
        var targetSize = 10f;

        if (_data.TargetCategory == ShootingTargetCategory.Galaxy)
        {
            var pa = _data.TargetPositionAngleDeg ?? 0.0;
            var rotateDeg = (float)(180.0 + displayRotationDeg - pa);
            canvas.SaveState();
            canvas.Translate(targetPoint.X, targetPoint.Y);
            canvas.Rotate(rotateDeg);
            canvas.FillColor = error.WithAlpha(0.12f);
            canvas.FillEllipse(-7f, -16f, 14f, 32f);
            canvas.StrokeColor = error;
            canvas.DrawEllipse(-7f, -16f, 14f, 32f);
            canvas.RestoreState();
            canvas.StrokeColor = error;
            var smallArm = targetSize * 0.6f;
            canvas.DrawLine(targetPoint.X - smallArm, targetPoint.Y, targetPoint.X + smallArm, targetPoint.Y);
            canvas.DrawLine(targetPoint.X, targetPoint.Y - smallArm, targetPoint.X, targetPoint.Y + smallArm);
        }
        else
        {
            canvas.StrokeColor = error;
            canvas.DrawLine(targetPoint.X - targetSize, targetPoint.Y, targetPoint.X + targetSize, targetPoint.Y);
            canvas.DrawLine(targetPoint.X, targetPoint.Y - targetSize, targetPoint.X, targetPoint.Y + targetSize);
            canvas.DrawCircle(targetPoint, targetSize * 0.7f);
        }

        canvas.RestoreState();

        canvas.FontColor = Color.FromArgb("#A0AABB");
        canvas.FontSize = 10;
        canvas.DrawString($"via {_data.AsterismName}", plotRect.Left + 12, plotRect.Top + 8, HorizontalAlignment.Left);
        canvas.DrawString("N", plotRect.Left + (plotRect.Width / 2f), plotRect.Top + 8, HorizontalAlignment.Center);
        canvas.DrawString("E", plotRect.Left + 8, plotRect.Top + (plotRect.Height / 2f), HorizontalAlignment.Left);
        canvas.DrawString("W", plotRect.Right - 8, plotRect.Top + (plotRect.Height / 2f), HorizontalAlignment.Right);

        var occupiedAreas = new List<RectF>
        {
            InflateRect(new RectF(usableRect.Left, centerPoint.Y - 1.5f, usableRect.Width, 3f), 1f),
            InflateRect(new RectF(centerPoint.X - 1.5f, usableRect.Top, 3f, usableRect.Height), 1f)
        };

        foreach (var star in _data.AsterismStars)
        {
            AddPointObstacle(occupiedAreas, ToCanvas(star.RaHours, star.DecDeg), StarRadius(star.Magnitude) + 4f);
        }

        foreach (var (from, to) in _data.AsterismSegments)
        {
            if (from < 0 || from >= _data.AsterismStars.Count || to < 0 || to >= _data.AsterismStars.Count)
            {
                continue;
            }

            var fromPoint = ToCanvas(_data.AsterismStars[from].RaHours, _data.AsterismStars[from].DecDeg);
            var toPoint = ToCanvas(_data.AsterismStars[to].RaHours, _data.AsterismStars[to].DecDeg);
            AddLineObstacle(occupiedAreas, fromPoint, toPoint, 4f);
        }

        if (_data.HopSteps.Count > 0)
        {
            for (var i = 0; i < _data.HopSteps.Count - 1; i++)
            {
                var from = _data.HopSteps[i];
                var to = _data.HopSteps[i + 1];
                var fromPoint = ToCanvas(from.RaHours, from.DecDeg);
                var toPoint = ToCanvas(to.RaHours, to.DecDeg);
                AddLineObstacle(occupiedAreas, fromPoint, toPoint, 5f);
            }

            var lastHop = _data.HopSteps[^1];
            AddLineObstacle(occupiedAreas, ToCanvas(lastHop.RaHours, lastHop.DecDeg), targetPoint, 5f);

            foreach (var step in _data.HopSteps)
            {
                AddPointObstacle(occupiedAreas, ToCanvas(step.RaHours, step.DecDeg), StarRadius(step.Magnitude) + 5f);
            }
        }

        AddPointObstacle(occupiedAreas, targetPoint, targetSize + 5f);
        if (anchorPoint.HasValue)
        {
            AddPointObstacle(occupiedAreas, anchorPoint.Value, StarRadius(_data.HopSteps[0].Magnitude) + 8f);
        }

        var placedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ls = _data.LabelScale;

        PlaceLabel(canvas, usableRect, occupiedAreas, targetPoint, _data.Target.Label, labelColor, plotBackground, borderColor, centerPoint, 12f * ls, targetSize + 4f, placedLabels);

        if (_data.HopSteps.Count > 0)
        {
            var anchor = _data.HopSteps[0];
            PlaceLabel(canvas, usableRect, occupiedAreas, anchorPoint ?? ToCanvas(anchor.RaHours, anchor.DecDeg), anchor.Label, labelColor, plotBackground, borderColor, centerPoint, 11f * ls, StarRadius(anchor.Magnitude) + 6f, placedLabels);
        }

        foreach (var step in _data.HopSteps.Skip(1))
        {
            PlaceLabel(canvas, usableRect, occupiedAreas, ToCanvas(step.RaHours, step.DecDeg), step.Label, labelColor, plotBackground, borderColor, centerPoint, 10f * ls, StarRadius(step.Magnitude) + 5f, placedLabels);
        }

        foreach (var star in _data.AsterismStars)
        {
            PlaceLabel(canvas, usableRect, occupiedAreas, ToCanvas(star.RaHours, star.DecDeg), star.Label, labelColor, plotBackground, borderColor, centerPoint, 10f * ls, StarRadius(star.Magnitude) + 4f, placedLabels);
        }

        foreach (var nearby in _data.NearbyTargets)
        {
            var nearbyPt = ToCanvas(nearby.RaHours, nearby.DecDeg);
            if (!usableRect.Contains(nearbyPt))
                continue;
            PlaceLabel(canvas, usableRect, occupiedAreas, nearbyPt, nearby.Label, nearbyColor, plotBackground, borderColor, centerPoint, 9f * ls, 9f, placedLabels);
        }
    }

    private static void PlaceLabel(
        ICanvas canvas,
        RectF bounds,
        List<RectF> occupiedAreas,
        PointF point,
        string? text,
        Color textColor,
        Color backgroundColor,
        Color borderColor,
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
            canvas.StrokeColor = textColor.WithAlpha(0.32f);
            canvas.StrokeSize = 1f;
            canvas.DrawLine(startX, startY, calloutPoint.X, calloutPoint.Y);
        }

        canvas.FontSize = fontSize;

        var shadowColor = backgroundColor.GetLuminosity() < 0.5f
            ? Colors.Black.WithAlpha(0.72f)
            : Colors.White.WithAlpha(0.68f);

        canvas.FontColor = shadowColor;
        canvas.DrawString(text, bestRect.Left + 2.5f, bestRect.Top + 1.5f, HorizontalAlignment.Left);

        canvas.FontColor = textColor;
        canvas.DrawString(text, bestRect.Left + 1.5f, bestRect.Top + 0.5f, HorizontalAlignment.Left);

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

    public static RectF CreateViewportRect(float width, float height)
    {
        var dirtyRect = new RectF(0, 0, width, height);
        var plotRect = new RectF(
            dirtyRect.Left + OuterMargin,
            dirtyRect.Top + OuterMargin,
            Math.Max(0, dirtyRect.Width - (2 * OuterMargin)),
            Math.Max(0, dirtyRect.Height - (2 * OuterMargin)));

        return CreateViewportRect(plotRect);
    }

    private static RectF CreateViewportRect(RectF plotRect)
    {
        return new RectF(
            plotRect.Left + PlotInset,
            plotRect.Top + PlotInset,
            Math.Max(0, plotRect.Width - (2 * PlotInset)),
            Math.Max(0, plotRect.Height - (2 * PlotInset)));
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

    private static Color ResolveColor(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
        {
            return color;
        }

        return Color.FromArgb(fallbackHex);
    }

    /// <summary>
    /// Gnomonic (tangent-plane) projection from sky coordinates to flat x,y.
    /// </summary>
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

    private static float BackgroundStarAlpha(double magnitude)
    {
        var alpha = 0.72f - ((float)magnitude * 0.07f);
        return Math.Clamp(alpha, 0.18f, 0.72f);
    }

    private static float StarRadius(double magnitude)
    {
        return Math.Max(1.5f, (float)(5.0 - (0.7 * magnitude)));
    }
}
