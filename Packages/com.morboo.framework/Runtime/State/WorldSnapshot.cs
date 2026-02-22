/// <summary>
/// Immutable world frame metadata.
/// IMPORTANT: Framework type — no UnityEngine dependency.
/// </summary>
public readonly struct WorldSnapshot
{
    public readonly Float2 Anchor;
    public readonly Float3 WorldAnchor;
    public readonly float Now;
    public readonly int ActorCount;

    public WorldSnapshot(Float2 anchor, float now, int actorCount)
        : this(new Float3(anchor.X, anchor.Y, 0f), now, actorCount)
    {
    }

    public WorldSnapshot(Float3 anchor, float now, int actorCount)
    {
        WorldAnchor = anchor;
        Anchor = new Float2(anchor.X, anchor.Y);
        Now = now;
        ActorCount = actorCount;
    }
}
