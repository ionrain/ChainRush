using UnityEngine;

/// <summary>
/// Immutable world frame metadata.
/// </summary>
public readonly struct WorldSnapshot
{
    public readonly Vector2 Anchor;
    public readonly float Now;
    public readonly int ActorCount;

    public WorldSnapshot(Vector2 anchor, float now, int actorCount)
    {
        Anchor = anchor;
        Now = now;
        ActorCount = actorCount;
    }
}
