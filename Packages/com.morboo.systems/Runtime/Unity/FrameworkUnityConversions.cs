using UnityEngine;

/// <summary>
/// Extension methods for converting between Framework value types and Unity types.
/// IMPORTANT: Used only at Integration/RuntimeHost boundaries.
/// </summary>
public enum SpatialProjectionPlane
{
    XY = 0,
    XZ = 1,
    YZ = 2
}

public static class FrameworkUnityConversions
{
    public static Float2 ToFloat2(this Vector2 v) => new Float2(v.x, v.y);
    public static Float2 ToFloat2(this Vector3 v) => new Float2(v.x, v.y);
    public static Vector2 ToVector2(this Float2 f) => new Vector2(f.X, f.Y);
    public static Vector3 ToVector3(this Float2 f, float z = 0f) => new Vector3(f.X, f.Y, z);
    public static Float3 ToFloat3(this Vector3 v) => new Float3(v.x, v.y, v.z);
    public static Float3 ToFloat3(this Vector2 v, float z = 0f) => new Float3(v.x, v.y, z);
    public static Vector3 ToVector3(this Float3 f) => new Vector3(f.X, f.Y, f.Z);

    /// <summary>
    /// Explicit 3D->2D projection helper. Keep planar cuts centralized at boundary code.
    /// </summary>
    public static Float2 ProjectToFloat2(this Float3 v, SpatialProjectionPlane plane)
    {
        switch (plane)
        {
            case SpatialProjectionPlane.XY: return new Float2(v.X, v.Y);
            case SpatialProjectionPlane.XZ: return new Float2(v.X, v.Z);
            case SpatialProjectionPlane.YZ: return new Float2(v.Y, v.Z);
            default: return new Float2(v.X, v.Y);
        }
    }

    /// <summary>
    /// Explicit 3D->2D projection helper for Unity vectors.
    /// </summary>
    public static Float2 ProjectToFloat2(this Vector3 v, SpatialProjectionPlane plane)
    {
        switch (plane)
        {
            case SpatialProjectionPlane.XY: return new Float2(v.x, v.y);
            case SpatialProjectionPlane.XZ: return new Float2(v.x, v.z);
            case SpatialProjectionPlane.YZ: return new Float2(v.y, v.z);
            default: return new Float2(v.x, v.y);
        }
    }

    public static AABB2D ToAABB2D(this Bounds b) =>
        new AABB2D(
            new Float2(b.min.x, b.min.y),
            new Float2(b.max.x, b.max.y));

    public static AABB3D ToAABB3D(this Bounds b) =>
        new AABB3D(
            new Float3(b.min.x, b.min.y, b.min.z),
            new Float3(b.max.x, b.max.y, b.max.z));

    public static Bounds ToBounds(this AABB2D a) =>
        new Bounds(
            new Vector3((a.Min.X + a.Max.X) * 0.5f, (a.Min.Y + a.Max.Y) * 0.5f, 0f),
            new Vector3(a.Max.X - a.Min.X, a.Max.Y - a.Min.Y, 0f));

    public static Bounds ToBounds(this AABB3D a) =>
        new Bounds(
            new Vector3(
                (a.Min.X + a.Max.X) * 0.5f,
                (a.Min.Y + a.Max.Y) * 0.5f,
                (a.Min.Z + a.Max.Z) * 0.5f),
            new Vector3(
                a.Max.X - a.Min.X,
                a.Max.Y - a.Min.Y,
                a.Max.Z - a.Min.Z));
}
