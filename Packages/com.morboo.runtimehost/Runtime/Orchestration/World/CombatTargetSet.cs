using UnityEngine;

/// <summary>
/// StrategyCombat-specific extension of <see cref="DomainTargetSet"/> that carries combat-only
/// metadata (currently faction ownership). The generic target-candidate carrier behavior
/// lives in <see cref="DomainTargetSet"/>.
/// <para>
/// IMPORTANT: New orchestration code should prefer <see cref="DomainTargetSet"/> contracts.
/// This class remains as a domain extension/seam while C06B resolves final target-set ownership.
/// </para>
/// </summary>
public class CombatTargetSet : DomainTargetSet, IFactionAssetProvider
{
    [SerializeField] FactionAsset faction;

    /// <summary>
    /// Returns the typed faction asset, or null if not assigned.
    /// IMPORTANT: Metadata only; target-set ownership is resolved explicitly by combat-domain providers.
    /// </summary>
    public FactionAsset GetFactionAsset() => faction;

    void OnValidate()
    {
        if (faction == null)
            Debug.LogWarning($"[{name}] CombatTargetSet has no FactionAsset assigned.", this);
    }
}
