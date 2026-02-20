/// <summary>
/// Interface for entities that provide a typed <see cref="FactionAsset"/>.
/// Enables typed relation lookups via <see cref="FactionRelationTableAsset"/>.
/// <para>
/// IMPORTANT: Returning null is valid and signals "no faction configured" —
/// callers should treat null as unconfigured and fail closed.
/// </para>
/// </summary>
public interface IFactionAssetProvider
{
    FactionAsset GetFactionAsset();
}
