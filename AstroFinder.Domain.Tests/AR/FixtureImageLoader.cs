using AstroFinder.Domain.AR.Calibration;

namespace AstroFinder.Domain.Tests.AR;

internal static class FixtureImageLoader
{
    public static GrayImageFrame LoadGrayFrame(string fixtureFileName)
    {
        var basePath = AppContext.BaseDirectory;
        var filePath = Path.Combine(basePath, "AR", "TestData", fixtureFileName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Fixture image was not found: {filePath}");
        }

        var rawLines = File.ReadAllLines(filePath)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (rawLines.Count < 2)
        {
            throw new InvalidDataException($"Fixture image '{fixtureFileName}' is missing size or pixel rows.");
        }

        var sizeParts = rawLines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sizeParts.Length != 2
            || !int.TryParse(sizeParts[0], out var width)
            || !int.TryParse(sizeParts[1], out var height)
            || width <= 0
            || height <= 0)
        {
            throw new InvalidDataException($"Fixture image '{fixtureFileName}' has an invalid size header.");
        }

        var rows = rawLines.Skip(1).ToList();
        if (rows.Count != height)
        {
            throw new InvalidDataException($"Fixture image '{fixtureFileName}' expected {height} rows but found {rows.Count}.");
        }

        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            var row = rows[y];
            if (row.Length != width)
            {
                throw new InvalidDataException($"Fixture image '{fixtureFileName}' row {y} expected {width} pixels but found {row.Length}.");
            }

            for (var x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = row[x] switch
                {
                    '#' => (byte)255,
                    '.' => (byte)0,
                    _ => throw new InvalidDataException($"Fixture image '{fixtureFileName}' contains unsupported pixel token '{row[x]}' at ({x}, {y}).")
                };
            }
        }

        return new GrayImageFrame(width, height, pixels);
    }
}