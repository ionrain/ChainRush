using UnityEngine;

/// <summary>
/// Holds a unit's orchestration identity: faction, role, and stable <see cref="EntityId"/>.
/// IMPORTANT: Read-only identity for the orchestration layer. Does not drive gameplay.
/// <para>
/// IMPORTANT — EntityId is assigned once in <see cref="Awake"/> via
/// <see cref="EntityIdAllocator.Create"/>. Stable across OnEnable/OnDisable (pool-safe).
/// All other components resolve EntityId from this identity.
/// </para>
/// </summary>
public sealed class UnitOrchestrationIdentity : MonoBehaviour, IEntityIdProvider
{
    [Header("Faction")]
    [SerializeField] FactionAsset faction;

    [Header("Role")]
    [Tooltip("Typed role assignment. Used by arbiter for per-role policy dispatch. " +
             "Currently per-prefab/per-instance; later can be sourced from UnitData.")]
    [SerializeField] RoleAsset roleOverride;

    EntityId _entityId;

    void Awake()
    {
        _entityId = EntityIdAllocator.Create();
    }

    /// <summary>
    /// Returns the stable EntityId assigned in Awake. Pool-safe.
    /// </summary>
    public EntityId GetEntityId() => _entityId;

    /// <summary>
    /// Returns the typed faction asset, or null if not assigned.
    /// IMPORTANT: Combat orchestration runtime uses this exclusively.
    /// </summary>
    public FactionAsset GetFactionAsset() => faction;

    /// <summary>
    /// Returns the typed role asset for orchestration routing, or null if not assigned.
    /// IMPORTANT: Routing uses ReferenceEquals on this asset — never string comparison.
    /// </summary>
    public RoleAsset GetRoleAsset() => roleOverride;

    /// <summary>
    /// Sets the typed faction asset at runtime (e.g. from <see cref="UnitAISetup"/>).
    /// </summary>
    public void SetFactionAsset(FactionAsset f) { faction = f; }

    /// <summary>
    /// Sets the typed role asset at runtime (e.g. from <see cref="UnitAISetup"/>).
    /// </summary>
    public void SetRoleAsset(RoleAsset r) { roleOverride = r; }

    /// <summary>
    /// Returns the unit's role tag for debug/metrics reporting.
    /// Priority: RoleAsset.Id > UnitData.unitClass > "Unit" fallback.
    /// <para>
    /// IMPORTANT: Debug/metrics only. Do not use for orchestration routing.
    /// Routing is RoleAsset-only (ReferenceEquals).
    /// </para>
    /// </summary>
    public string GetRoleTag(Unit unit)
    {
        if (roleOverride != null)
            return roleOverride.Id;

        if (unit != null && unit.Data != null)
            return unit.Data.unitClass.ToString();

        return "Unit";
    }

    void OnValidate()
    {
        if (faction == null)
            Debug.LogWarning($"[{name}] UnitOrchestrationIdentity has no FactionAsset assigned.", this);
        if (roleOverride == null)
            Debug.LogWarning($"[{name}] UnitOrchestrationIdentity has no RoleAsset assigned. " +
                             "Arbiter will use fallback behavior (Idle Hold, combat default).", this);
    }
}
