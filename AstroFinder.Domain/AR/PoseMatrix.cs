namespace AstroFinder.Domain.AR;

/// <summary>
/// Row-major 3×3 matrix that transforms ENU world vectors into camera coordinates.
/// <para>
/// Row 0 = camera-right in ENU,
/// Row 1 = camera-up in ENU,
/// Row 2 = camera-forward in ENU (through back camera).
/// </para>
/// </summary>
public readonly struct PoseMatrix
{
    public readonly double[] M;

    public PoseMatrix(double[] m)
    {
        if (m.Length != 9)
            throw new ArgumentException("PoseMatrix must contain 9 values.", nameof(m));
        M = m;
    }

    /// <summary>
    /// Transforms an ENU world vector into camera coordinates.
    /// </summary>
    public Vec3 Multiply(Vec3 v) => new(
        M[0] * v.X + M[1] * v.Y + M[2] * v.Z,
        M[3] * v.X + M[4] * v.Y + M[5] * v.Z,
        M[6] * v.X + M[7] * v.Y + M[8] * v.Z);

    public static PoseMatrix Identity() => new(new double[]
    {
        1.0, 0.0, 0.0,
        0.0, 1.0, 0.0,
        0.0, 0.0, 1.0,
    });

    /// <summary>
    /// Multiplies two 3×3 matrices: result = this * other.
    /// </summary>
    public PoseMatrix Multiply(PoseMatrix other)
    {
        var result = new double[9];
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                result[r * 3 + c] =
                    M[r * 3 + 0] * other.M[0 * 3 + c] +
                    M[r * 3 + 1] * other.M[1 * 3 + c] +
                    M[r * 3 + 2] * other.M[2 * 3 + c];
            }
        }
        return new PoseMatrix(result);
    }

    /// <summary>
    /// Re-orthonormalizes the matrix using Gram-Schmidt to correct accumulated drift.
    /// </summary>
    public PoseMatrix Orthonormalized()
    {
        // Extract columns (each column is the world-space direction of a camera axis).
        var x = new Vec3(M[0], M[3], M[6]).Normalized();
        var y = new Vec3(M[1], M[4], M[7]);
        var dotXY = x.Dot(y);
        y = new Vec3(
            y.X - dotXY * x.X,
            y.Y - dotXY * x.Y,
            y.Z - dotXY * x.Z).Normalized();
        var z = x.Cross(y).Normalized();

        return new PoseMatrix(new[]
        {
            x.X, y.X, z.X,
            x.Y, y.Y, z.Y,
            x.Z, y.Z, z.Z,
        });
    }
}
