namespace AstroFinder.Domain.AR.Calibration;

/// <summary>
/// Grayscale camera frame used for lightweight star detection.
/// Pixels are row-major luminance values in [0,255].
/// </summary>
public sealed record GrayImageFrame(int Width, int Height, IReadOnlyList<byte> Pixels);
