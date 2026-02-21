using UnityEngine;

/// <summary>
/// Data-driven bridge that wires orchestration components (faction, role, capabilities,
/// policy overrides) from <see cref="UnitData"/> and map assets on spawn/enable.
/// Replaces manual per-prefab wiring of orchestration identity and policies.
/// <para>
/// IMPORTANT — No Update loop. Safe with pooling: OnEnable re-applies all bindings.
/// </para>
/// <para>
/// RATIONALE: Domain policies (idle/combat) are routed per-role by the arbiter via
/// <see cref="IdleRolePolicyMapAsset"/> and <see cref="CombatRolePolicyMapAsset"/>.
/// This component only sets the role (via a UnitClass-to-role map) and
/// optional per-unit overrides. No per-class policy maps needed.
/// </para>
/// </summary>
public sealed class UnitAISetup : MonoBehaviour, IPostSpawnSetup
{
    [SerializeField] bool applyOnEnable = true;
    // ──────────────────────────────────────────────────────────────────
    //  Serialized — Component references
    // ──────────────────────────────────────────────────────────────────

    [Header("Components")]
    [SerializeField] Unit unit;
    [SerializeField] UnitOrchestrationIdentity identity;
    [SerializeField] UnitCapabilityProvider capabilityProvider;
    [SerializeField] UnitIdlePolicySelector idlePolicySelector;
    [SerializeField] UnitCombatTargetSelector combatTargetSelector;

    // ──────────────────────────────────────────────────────────────────
    //  Serialized — Maps
    // ──────────────────────────────────────────────────────────────────

    [Header("Maps")]
    [Tooltip("UnitClass → RoleAsset. Single source of role truth.")]
    [SerializeField] UnitRoleMapAssetBase roleMap;

    [Tooltip("RoleAsset → CapabilitiesProfile. Resolved via unit's role identity.")]
    [SerializeField] RoleCapabilitiesMapAssetBase roleCapabilitiesMap;

    // ──────────────────────────────────────────────────────────────────
    //  Serialized — Faction
    // ──────────────────────────────────────────────────────────────────

    [Header("Faction")]
    [Tooltip("Faction to assign. Per-prefab only in this step.")]
    [SerializeField] FactionAsset faction;
    // TODO: Multi-faction spawning will require spawner/manager injection
    // or a UnitClass→Faction map. Current single FactionAsset field is per-prefab only.

    // ──────────────────────────────────────────────────────────────────
    //  Serialized — Optional per-unit policy overrides
    // ──────────────────────────────────────────────────────────────────

    [Header("Per-Unit Policy Overrides (Optional)")]
    [Tooltip("If set, assigned as highest-priority idle policy override for this unit.")]
    [SerializeField] IdlePolicyAsset idleOverridePolicy;

    [Tooltip("If set, assigned as highest-priority combat targeting policy override for this unit.")]
    [SerializeField] CombatTargetingPolicyAsset combatOverridePolicy;    

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — One-shot warning flags (avoid log spam with pooling)
    // ──────────────────────────────────────────────────────────────────

    bool _warnedMissingUnit;
    bool _warnedMissingIdentity;
    bool _warnedMissingUnitData;
    bool _warnedMissingRoleMapping;

    // ──────────────────────────────────────────────────────────────────
    //  Editor convenience
    // ──────────────────────────────────────────────────────────────────

    void Reset()
    {
        unit = GetComponent<Unit>();
        identity = GetComponent<UnitOrchestrationIdentity>();
        capabilityProvider = GetComponent<UnitCapabilityProvider>();
        idlePolicySelector = GetComponent<UnitIdlePolicySelector>();
        combatTargetSelector = GetComponent<UnitCombatTargetSelector>();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (applyOnEnable)
            Apply();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Apply — wires all orchestration bindings
    // ──────────────────────────────────────────────────────────────────

    public void Apply()
    {
        // ── Guard: unit reference ─────────────────────────────────────
        if (unit == null)
        {
            if (!_warnedMissingUnit)
            {
                _warnedMissingUnit = true;
                Debug.LogWarning("[UnitAISetup] Missing Unit reference.", this);
            }
            return;
        }

        // ── Guard: unit data ──────────────────────────────────────────
        if (unit.Data == null)
        {
            if (!_warnedMissingUnitData)
            {
                _warnedMissingUnitData = true;
                Debug.LogWarning("[UnitAISetup] Unit.Data is null; setup deferred.", this);
            }
            return;
        }

        UnitClass unitClass = unit.Data.unitClass;

        // ── Identity: faction + role ──────────────────────────────────
        if (identity != null)
        {
            if (faction != null)
                identity.SetFactionAsset(faction);

            if (roleMap != null)
            {
                RoleAsset role;
                if (roleMap.TryGet(unitClass, out role))
                {
                    identity.SetRoleAsset(role);
                }
                else if (!_warnedMissingRoleMapping)
                {
                    _warnedMissingRoleMapping = true;
                    Debug.LogWarning(string.Concat(
                        "[UnitAISetup] No Role mapping for UnitClass ",
                        unitClass.ToString(),
                        "; orchestration routing will not work (RoleAsset null)."), this);
                }
            }
        }
        else if (!_warnedMissingIdentity)
        {
            _warnedMissingIdentity = true;
            Debug.LogWarning("[UnitAISetup] Missing UnitOrchestrationIdentity reference.", this);
        }

        // ── Capabilities ──────────────────────────────────────────────
        // IMPORTANT: Always call SetRoleMap even if null, to clear stale refs on pooled instances.
        if (capabilityProvider != null)
            capabilityProvider.SetRoleMap(roleCapabilitiesMap);

        // ── Per-unit policy overrides ─────────────────────────────────
        if (idleOverridePolicy != null && idlePolicySelector != null)
            idlePolicySelector.SetOverridePolicy(idleOverridePolicy);

        if (combatOverridePolicy != null && combatTargetSelector != null)
            combatTargetSelector.SetOverridePolicy(combatOverridePolicy);
    }
}
