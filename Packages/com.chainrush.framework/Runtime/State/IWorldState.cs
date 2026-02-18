/// <summary>
/// Mutable world state root that can emit immutable snapshots.
/// </summary>
public interface IWorldState
{
    WorldSnapshot Snapshot();
}
