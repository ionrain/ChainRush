/// <summary>
/// Engine-agnostic 2D axis-aligned bounding box.
/// IMPORTANT: This is a Framework type — no UnityEngine dependency.
/// </summary>
public readonly struct AABB2D
{
    public readonly Float2 Min;
    public readonly Float2 Max;

    public AABB2D(Float2 min, Float2 max)
    {
        Min = min;
        Max = max;
    }

    public Float2 Center => new Float2((Min.X + Max.X) * 0.5f, (Min.Y + Max.Y) * 0.5f);
    public Float2 Size => new Float2(Max.X - Min.X, Max.Y - Min.Y);
    public Float2 Extents => new Float2((Max.X - Min.X) * 0.5f, (Max.Y - Min.Y) * 0.5f);

    public bool Contains(Float2 point) =>
        point.X >= Min.X && point.X <= Max.X &&
        point.Y >= Min.Y && point.Y <= Max.Y;

    public static AABB2D FromCenterSize(Float2 center, Float2 size)
    {
        Float2 half = new Float2(size.X * 0.5f, size.Y * 0.5f);
        return new AABB2D(center - half, center + half);
    }

    public override string ToString() => $"AABB2D(Min={Min}, Max={Max})";
}
