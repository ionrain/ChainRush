using UnityEngine;

/// <summary>
/// Extension methods for converting between Framework value types and Unity types.
/// IMPORTANT: Used only at Integration/RuntimeHost boundaries.
/// </summary>
public static class FrameworkUnityConversions
{
    public static Float2 ToFloat2(this Vector2 v) => new Float2(v.x, v.y);
    public static Float2 ToFloat2(this Vector3 v) => new Float2(v.x, v.y);
    public static Vector2 ToVector2(this Float2 f) => new Vector2(f.X, f.Y);
    public static Vector3 ToVector3(this Float2 f, float z = 0f) => new Vector3(f.X, f.Y, z);

    public static AABB2D ToAABB2D(this Bounds b) =>
        new AABB2D(
            new Float2(b.min.x, b.min.y),
            new Float2(b.max.x, b.max.y));

    public static Bounds ToBounds(this AABB2D a) =>
        new Bounds(
            new Vector3((a.Min.X + a.Max.X) * 0.5f, (a.Min.Y + a.Max.Y) * 0.5f, 0f),
            new Vector3(a.Max.X - a.Min.X, a.Max.Y - a.Min.Y, 0f));
}
