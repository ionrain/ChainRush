/// <summary>
/// Immutable world frame metadata.
/// IMPORTANT: Framework type — no UnityEngine dependency.
/// </summary>
public readonly struct WorldSnapshot
{
    public readonly Float2 Anchor;
    public readonly float Now;
    public readonly int ActorCount;

    public WorldSnapshot(Float2 anchor, float now, int actorCount)
    {
        Anchor = anchor;
        Now = now;
        ActorCount = actorCount;
    }
}
