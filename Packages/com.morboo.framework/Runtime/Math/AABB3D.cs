/// <summary>
/// Engine-agnostic 3D axis-aligned bounding box.
/// IMPORTANT: This is a Framework type — no UnityEngine dependency.
/// </summary>
public readonly struct AABB3D
{
    public readonly Float3 Min;
    public readonly Float3 Max;

    public AABB3D(Float3 min, Float3 max)
    {
        Min = min;
        Max = max;
    }

    public Float3 Center => new Float3(
        (Min.X + Max.X) * 0.5f,
        (Min.Y + Max.Y) * 0.5f,
        (Min.Z + Max.Z) * 0.5f);

    public Float3 Size => new Float3(
        Max.X - Min.X,
        Max.Y - Min.Y,
        Max.Z - Min.Z);

    public Float3 Extents => new Float3(
        (Max.X - Min.X) * 0.5f,
        (Max.Y - Min.Y) * 0.5f,
        (Max.Z - Min.Z) * 0.5f);

    public bool Contains(Float3 point) =>
        point.X >= Min.X && point.X <= Max.X &&
        point.Y >= Min.Y && point.Y <= Max.Y &&
        point.Z >= Min.Z && point.Z <= Max.Z;

    public static AABB3D FromCenterSize(Float3 center, Float3 size)
    {
        Float3 half = new Float3(size.X * 0.5f, size.Y * 0.5f, size.Z * 0.5f);
        return new AABB3D(center - half, center + half);
    }

    public override string ToString() => $"AABB3D(Min={Min}, Max={Max})";
}
