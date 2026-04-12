using AstroApps.Equipment.Profiles.Models;

namespace AstroFinder.App.Services;

/// <summary>
/// Lightweight RA/Dec bucket index for quickly narrowing star candidates.
/// </summary>
public sealed class StarSpatialIndex
{
    private readonly Dictionary<(int DecBin, int RaBin), List<CatalogStar>> _bins = new();
    private readonly double _binSizeDeg;

    public StarSpatialIndex(IEnumerable<CatalogStar> stars, double binSizeDeg = 5.0)
    {
        _binSizeDeg = Math.Clamp(binSizeDeg, 1.0, 20.0);

        foreach (var star in stars)
        {
            var decBin = ToDecBin(star.DeclinationDeg);
            var raBin = ToRaBin(star.RightAscensionHours * 15.0);
            var key = (decBin, raBin);

            if (!_bins.TryGetValue(key, out var list))
            {
                list = [];
                _bins[key] = list;
            }

            list.Add(star);
        }
    }

    public IReadOnlyList<CatalogStar> Query(double centerRaHours, double centerDecDeg, double radiusDeg)
    {
        if (_bins.Count == 0)
        {
            return [];
        }

        radiusDeg = Math.Clamp(radiusDeg, 0.1, 180.0);

        var centerRaDeg = NormalizeRaDeg(centerRaHours * 15.0);
        var minDec = Math.Max(-90.0, centerDecDeg - radiusDeg);
        var maxDec = Math.Min(90.0, centerDecDeg + radiusDeg);

        // Wider RA window near poles where cos(dec) approaches 0.
        var cosDec = Math.Max(0.15, Math.Cos(centerDecDeg * Math.PI / 180.0));
        var raHalfWidth = Math.Min(180.0, radiusDeg / cosDec);

        var minDecBin = ToDecBin(minDec);
        var maxDecBin = ToDecBin(maxDec);

        var minRaDeg = NormalizeRaDeg(centerRaDeg - raHalfWidth);
        var maxRaDeg = NormalizeRaDeg(centerRaDeg + raHalfWidth);

        var result = new List<CatalogStar>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var decBin = minDecBin; decBin <= maxDecBin; decBin++)
        {
            if (minRaDeg <= maxRaDeg)
            {
                AddRaRange(decBin, minRaDeg, maxRaDeg, centerRaDeg, centerDecDeg, raHalfWidth, minDec, maxDec, result, seen);
            }
            else
            {
                // Wrapped around 0° RA.
                AddRaRange(decBin, 0.0, maxRaDeg, centerRaDeg, centerDecDeg, raHalfWidth, minDec, maxDec, result, seen);
                AddRaRange(decBin, minRaDeg, 360.0, centerRaDeg, centerDecDeg, raHalfWidth, minDec, maxDec, result, seen);
            }
        }

        return result;
    }

    private void AddRaRange(
        int decBin,
        double minRaDeg,
        double maxRaDeg,
        double centerRaDeg,
        double centerDecDeg,
        double raHalfWidth,
        double minDec,
        double maxDec,
        List<CatalogStar> output,
        HashSet<string> seen)
    {
        var minRaBin = ToRaBin(minRaDeg);
        var maxRaBin = ToRaBin(maxRaDeg);

        for (var raBin = minRaBin; raBin <= maxRaBin; raBin++)
        {
            if (!_bins.TryGetValue((decBin, raBin), out var list))
            {
                continue;
            }

            foreach (var star in list)
            {
                if (star.DeclinationDeg < minDec || star.DeclinationDeg > maxDec)
                {
                    continue;
                }

                var starRaDeg = NormalizeRaDeg(star.RightAscensionHours * 15.0);
                var deltaRa = Math.Abs(starRaDeg - centerRaDeg);
                deltaRa = Math.Min(deltaRa, 360.0 - deltaRa);

                if (deltaRa > raHalfWidth)
                {
                    continue;
                }

                if (seen.Add(star.Id))
                {
                    output.Add(star);
                }
            }
        }
    }

    private int ToDecBin(double decDeg) => (int)Math.Floor((decDeg + 90.0) / _binSizeDeg);

    private int ToRaBin(double raDeg) => (int)Math.Floor(NormalizeRaDeg(raDeg) / _binSizeDeg);

    private static double NormalizeRaDeg(double raDeg)
    {
        var normalized = raDeg % 360.0;
        if (normalized < 0)
        {
            normalized += 360.0;
        }

        return normalized;
    }
}
