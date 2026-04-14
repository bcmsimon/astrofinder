namespace AstroFinder.Domain.AR.Calibration;

/// <summary>
/// Lightweight bright-star detector for grayscale camera frames.
/// </summary>
public sealed class StarDetector
{
    public sealed record Options(
        int BlurKernelRadius = 1,
        double SigmaThreshold = 2.8,
        int LocalWindowRadius = 2,
        int MinBlobPixels = 2,
        int MaxBlobPixels = 90,
        double MinRoundness = 0.35,
        int MaxDetections = 80);

    private readonly Options _options;

    public StarDetector(Options? options = null)
    {
        _options = options ?? new Options();
    }

    public IReadOnlyList<DetectedStarPoint> Detect(GrayImageFrame frame)
    {
        if (frame.Width <= 2 || frame.Height <= 2 || frame.Pixels.Count != frame.Width * frame.Height)
        {
            return [];
        }

        var luma = ToDouble(frame.Pixels);
        if (_options.BlurKernelRadius > 0)
        {
            luma = BoxBlur(luma, frame.Width, frame.Height, _options.BlurKernelRadius);
        }

        var (mean, stdDev) = MeanStdDev(luma);
        var threshold = mean + (_options.SigmaThreshold * stdDev);

        var detections = new List<DetectedStarPoint>();

        var r = Math.Max(1, _options.LocalWindowRadius);
        for (var y = r; y < frame.Height - r; y++)
        {
            for (var x = r; x < frame.Width - r; x++)
            {
                var idx = (y * frame.Width) + x;
                var center = luma[idx];
                if (center < threshold)
                {
                    continue;
                }

                if (!IsLocalMax(luma, frame.Width, frame.Height, x, y, r))
                {
                    continue;
                }

                var component = FloodComponent(luma, frame.Width, frame.Height, x, y, threshold);
                if (component.Count < _options.MinBlobPixels || component.Count > _options.MaxBlobPixels)
                {
                    continue;
                }

                var centroid = ComputeWeightedCentroid(component, luma, frame.Width);
                var roundness = ComputeRoundness(component);
                if (roundness < _options.MinRoundness)
                {
                    continue;
                }

                var peak = component.Max(p => luma[(p.Y * frame.Width) + p.X]);
                detections.Add(new DetectedStarPoint(centroid.X, centroid.Y, peak, mean, roundness));
            }
        }

        return detections
            .OrderByDescending(d => d.PeakIntensity)
            .Take(_options.MaxDetections)
            .ToList();
    }

    private static double[] ToDouble(IReadOnlyList<byte> pixels)
    {
        var dst = new double[pixels.Count];
        for (var i = 0; i < pixels.Count; i++)
        {
            dst[i] = pixels[i];
        }

        return dst;
    }

    private static double[] BoxBlur(double[] src, int w, int h, int radius)
    {
        var dst = new double[src.Length];
        var size = (2 * radius) + 1;
        var area = size * size;

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                double sum = 0;
                for (var ky = -radius; ky <= radius; ky++)
                {
                    var yy = Math.Clamp(y + ky, 0, h - 1);
                    for (var kx = -radius; kx <= radius; kx++)
                    {
                        var xx = Math.Clamp(x + kx, 0, w - 1);
                        sum += src[(yy * w) + xx];
                    }
                }

                dst[(y * w) + x] = sum / area;
            }
        }

        return dst;
    }

    private static (double Mean, double StdDev) MeanStdDev(double[] values)
    {
        if (values.Length == 0)
        {
            return (0, 0);
        }

        var mean = values.Average();
        var variance = values.Sum(v => (v - mean) * (v - mean)) / values.Length;
        return (mean, Math.Sqrt(Math.Max(variance, 0.0)));
    }

    private static bool IsLocalMax(double[] luma, int w, int h, int x, int y, int r)
    {
        var center = luma[(y * w) + x];
        for (var yy = y - r; yy <= y + r; yy++)
        {
            for (var xx = x - r; xx <= x + r; xx++)
            {
                if (xx == x && yy == y)
                {
                    continue;
                }

                if (luma[(yy * w) + xx] > center)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static HashSet<(int X, int Y)> FloodComponent(double[] luma, int w, int h, int startX, int startY, double threshold)
    {
        var result = new HashSet<(int X, int Y)>();
        var stack = new Stack<(int X, int Y)>();
        stack.Push((startX, startY));

        while (stack.Count > 0)
        {
            var p = stack.Pop();
            if (!result.Add(p))
            {
                continue;
            }

            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    var nx = p.X + dx;
                    var ny = p.Y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                    {
                        continue;
                    }

                    if (luma[(ny * w) + nx] >= threshold)
                    {
                        stack.Push((nx, ny));
                    }
                }
            }
        }

        return result;
    }

    private static (double X, double Y) ComputeWeightedCentroid(HashSet<(int X, int Y)> component, double[] luma, int w)
    {
        double wx = 0;
        double wy = 0;
        double total = 0;

        foreach (var p in component)
        {
            var weight = Math.Max(1e-6, luma[(p.Y * w) + p.X]);
            wx += p.X * weight;
            wy += p.Y * weight;
            total += weight;
        }

        if (total <= 0)
        {
            return (component.Average(p => p.X), component.Average(p => p.Y));
        }

        return (wx / total, wy / total);
    }

    private static double ComputeRoundness(HashSet<(int X, int Y)> component)
    {
        var minX = component.Min(p => p.X);
        var maxX = component.Max(p => p.X);
        var minY = component.Min(p => p.Y);
        var maxY = component.Max(p => p.Y);
        var dx = Math.Max(1, maxX - minX + 1);
        var dy = Math.Max(1, maxY - minY + 1);
        var ratio = Math.Min(dx, dy) / (double)Math.Max(dx, dy);
        return ratio;
    }
}
