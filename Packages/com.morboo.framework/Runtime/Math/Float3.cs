using System;

/// <summary>
/// Engine-agnostic 3D float vector.
/// IMPORTANT: This is a Framework type — no UnityEngine dependency.
/// </summary>
public readonly struct Float3 : IEquatable<Float3>
{
    public readonly float X;
    public readonly float Y;
    public readonly float Z;

    public Float3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static readonly Float3 Zero = default;
    public static readonly Float3 One = new Float3(1f, 1f, 1f);

    public float SqrMagnitude => X * X + Y * Y + Z * Z;
    public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

    public Float3 Normalized
    {
        get
        {
            float mag = Magnitude;
            return mag > 1e-8f ? new Float3(X / mag, Y / mag, Z / mag) : Zero;
        }
    }

    public static float Dot(Float3 a, Float3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static float DistanceSqr(Float3 a, Float3 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    public static float Distance(Float3 a, Float3 b) => (float)Math.Sqrt(DistanceSqr(a, b));

    public static Float3 operator +(Float3 a, Float3 b) => new Float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Float3 operator -(Float3 a, Float3 b) => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Float3 operator -(Float3 a) => new Float3(-a.X, -a.Y, -a.Z);
    public static Float3 operator *(Float3 a, float s) => new Float3(a.X * s, a.Y * s, a.Z * s);
    public static Float3 operator *(float s, Float3 a) => new Float3(a.X * s, a.Y * s, a.Z * s);
    public static Float3 operator /(Float3 a, float s) => new Float3(a.X / s, a.Y / s, a.Z / s);

    public static bool operator ==(Float3 a, Float3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    public static bool operator !=(Float3 a, Float3 b) => a.X != b.X || a.Y != b.Y || a.Z != b.Z;

    public bool Equals(Float3 other) => X == other.X && Y == other.Y && Z == other.Z;
    public override bool Equals(object obj) => obj is Float3 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}
