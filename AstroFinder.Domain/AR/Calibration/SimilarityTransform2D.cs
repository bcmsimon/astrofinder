namespace AstroFinder.Domain.AR.Calibration;

/// <summary>
/// 2D similarity transform: scale, rotation, and translation.
/// </summary>
public readonly record struct SimilarityTransform2D(double Scale, double RotationRad, double Tx, double Ty)
{
    public static SimilarityTransform2D Identity => new(1.0, 0.0, 0.0, 0.0);

    public (double X, double Y) Apply(double x, double y)
    {
        var c = Math.Cos(RotationRad);
        var s = Math.Sin(RotationRad);
        var nx = (Scale * ((c * x) - (s * y))) + Tx;
        var ny = (Scale * ((s * x) + (c * y))) + Ty;
        return (nx, ny);
    }

    public SimilarityTransform2D LerpTo(SimilarityTransform2D target, double alpha)
    {
        alpha = Math.Clamp(alpha, 0.0, 1.0);

        // Circular interpolation for angle.
        var da = target.RotationRad - RotationRad;
        while (da > Math.PI) da -= (2.0 * Math.PI);
        while (da < -Math.PI) da += (2.0 * Math.PI);

        return new SimilarityTransform2D(
            Scale + ((target.Scale - Scale) * alpha),
            RotationRad + (da * alpha),
            Tx + ((target.Tx - Tx) * alpha),
            Ty + ((target.Ty - Ty) * alpha));
    }
}
