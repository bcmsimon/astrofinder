namespace AstroFinder.Domain.AR;

/// <summary>
/// Immutable 3D vector used throughout the AR projection pipeline.
/// </summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public double Norm() => Math.Sqrt(X * X + Y * Y + Z * Z);

    public Vec3 Normalized()
    {
        var n = Norm();
        if (n == 0.0) return this;
        return new Vec3(X / n, Y / n, Z / n);
    }

    public double Dot(Vec3 other) => X * other.X + Y * other.Y + Z * other.Z;

    public Vec3 Cross(Vec3 other) => new(
        Y * other.Z - Z * other.Y,
        Z * other.X - X * other.Z,
        X * other.Y - Y * other.X);

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(double s, Vec3 v) => new(s * v.X, s * v.Y, s * v.Z);
    public static Vec3 operator *(Vec3 v, double s) => new(v.X * s, v.Y * s, v.Z * s);
}
