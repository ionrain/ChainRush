using System;

/// <summary>
/// Engine-agnostic 2D float vector.
/// IMPORTANT: This is a Framework type — no UnityEngine dependency.
/// </summary>
public readonly struct Float2 : IEquatable<Float2>
{
    public readonly float X;
    public readonly float Y;

    public Float2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static readonly Float2 Zero = default;
    public static readonly Float2 One = new Float2(1f, 1f);

    public float SqrMagnitude => X * X + Y * Y;
    public float Magnitude => (float)Math.Sqrt(X * X + Y * Y);

    public Float2 Normalized
    {
        get
        {
            float mag = Magnitude;
            return mag > 1e-8f ? new Float2(X / mag, Y / mag) : Zero;
        }
    }

    public static float Dot(Float2 a, Float2 b) => a.X * b.X + a.Y * b.Y;
    public static float DistanceSqr(Float2 a, Float2 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
    public static float Distance(Float2 a, Float2 b) => (float)Math.Sqrt(DistanceSqr(a, b));

    public static Float2 operator +(Float2 a, Float2 b) => new Float2(a.X + b.X, a.Y + b.Y);
    public static Float2 operator -(Float2 a, Float2 b) => new Float2(a.X - b.X, a.Y - b.Y);
    public static Float2 operator -(Float2 a) => new Float2(-a.X, -a.Y);
    public static Float2 operator *(Float2 a, float s) => new Float2(a.X * s, a.Y * s);
    public static Float2 operator *(float s, Float2 a) => new Float2(a.X * s, a.Y * s);
    public static Float2 operator /(Float2 a, float s) => new Float2(a.X / s, a.Y / s);

    public static bool operator ==(Float2 a, Float2 b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Float2 a, Float2 b) => a.X != b.X || a.Y != b.Y;

    public bool Equals(Float2 other) => X == other.X && Y == other.Y;
    public override bool Equals(object obj) => obj is Float2 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X:F2}, {Y:F2})";
}
