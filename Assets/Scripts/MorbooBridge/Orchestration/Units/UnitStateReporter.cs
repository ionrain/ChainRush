using UnityEngine;

/// <summary>
/// Reports a <see cref="StateSnapshot"/> for a unit to the orchestration layer.
/// IMPORTANT: No gameplay control, no Update loop. Called on demand by orchestration.
/// C3.3: syncs authoritative entity model state before building snapshot metrics.
/// </summary>
public sealed class UnitStateReporter : MonoBehaviour, IStateReporter, IOrchestrationActor, IEntityIdProvider
{
    [SerializeField] Unit _unit;
    [SerializeField] UnitOrchestrationIdentity _identity;

    /// <summary>
    /// PERF: Cached metrics ParamSet. The list object is allocated on first call
    /// (capacity 4) and reused thereafter. Each call clears then re-sets values
    /// to avoid stale or duplicate keys.
    /// </summary>
    ParamSet _cachedMetrics;

    bool _warnedMissingIdentity;

    void OnEnable() => OrchestrationRegistry.Register((IStateReporter)this);
    void OnDisable() => OrchestrationRegistry.Unregister((IStateReporter)this);

    // ──────────────────────────────────────────────────────────────────
    //  IEntityIdProvider
    // ──────────────────────────────────────────────────────────────────

    public EntityId GetEntityId()
    {
        if (_identity != null) return _identity.GetEntityId();
        if (!_warnedMissingIdentity)
        {
            _warnedMissingIdentity = true;
            Debug.LogWarning("[UnitStateReporter] Missing UnitOrchestrationIdentity; EntityId is None.", this);
        }
        return EntityId.None;
    }

    // ──────────────────────────────────────────────────────────────────
    //  IOrchestrationActor
    // ──────────────────────────────────────────────────────────────────

    public FactionAsset GetFactionAsset()
    {
        return _identity != null ? _identity.GetFactionAsset() : null;
    }

    public Transform GetTransform() => transform;

    public EntityLifecycleState GetLifecycleState()
    {
        return IsAlive() ? EntityLifecycleState.Active : EntityLifecycleState.Inactive;
    }

    /// <summary>
    /// IMPORTANT: If MaxHealth &lt;= 0 (health not yet initialized), the entity is
    /// treated as alive to avoid false negatives during early lifecycle.
    /// </summary>
    public bool IsAlive()
    {
        return _unit != null && (_unit.MaxHealth <= 0 || _unit.CurrentHealth > 0);
    }

    void Reset()
    {
        _unit = GetComponent<Unit>();
        _identity = GetComponent<UnitOrchestrationIdentity>();
    }

    /// <summary>
    /// Builds and returns a <see cref="StateSnapshot"/> reflecting the unit's current state.
    /// RATIONALE: MaxHealth > 0 guards against the case where the Health component
    /// is not yet initialized (Unit.CurrentHealth returns 0 when _health is null).
    /// When health is unknown we report IsAlive = true and omit Hp01.
    /// </summary>
    public StateSnapshot ReportState()
    {
        if (_cachedMetrics.Items != null) _cachedMetrics.Items.Clear();

        FactionAsset faction = _identity != null ? _identity.GetFactionAsset() : null;
        TryGetEntityState(out IEntityStateAccessor entityState);

        bool hasHealth = _unit != null && _unit.MaxHealth > 0;
        float legacyHp01 = hasHealth ? _unit.CurrentHealth / _unit.MaxHealth : 0f;
        bool legacyIsAlive = _unit != null && (!hasHealth || _unit.CurrentHealth > 0);

        SyncEntityState(entityState, legacyIsAlive);
        bool isAlive = entityState != null ? entityState.IsAlive : legacyIsAlive;

        if (hasHealth)
        {
            _cachedMetrics.SetFloat("Hp01", legacyHp01);
        }

        if (_unit != null)
        {
            _cachedMetrics.SetInt("MergeState", _unit.MergeState);
        }

        if (_unit != null)
        {
            if (_unit.Data != null)
            {
                UnitClass unitClass = _unit.Data.unitClass;
                _cachedMetrics.SetString("UnitClass", unitClass.ToString());
                _cachedMetrics.SetBool("IsMelee", IsMeleeClass(unitClass));
            }
        }

        string roleTag = _identity != null
            ? _identity.GetRoleTag(_unit)
            : "Unit";

        return StateSnapshot.Create(
            faction,
            GetEntityId(),
            (Vector2)transform.position,
            isAlive,
            roleTag,
            _cachedMetrics);
    }

    void SyncEntityState(IEntityStateAccessor entityState, bool isAlive)
    {
        if (entityState == null)
            return;

        entityState.SetLifecycleState(isAlive ? EntityLifecycleState.Active : EntityLifecycleState.Inactive);
    }

    bool TryGetEntityState(out IEntityStateAccessor entityState)
    {
        entityState = null;

        EntityId entityId = GetEntityId();
        if (entityId.IsNone)
            return false;

        IEntityStateQuery query = EntityBackboneRuntimeContext.StateQuery;
        if (query == null)
            return false;

        return query.TryGetState(entityId, out entityState);
    }

    /// <summary>
    /// Melee heuristic: Warrior, Tank, Assassin are melee; Range, Mage, Support are not.
    /// </summary>
    static bool IsMeleeClass(UnitClass unitClass)
    {
        return unitClass == UnitClass.Warrior
            || unitClass == UnitClass.Tank
            || unitClass == UnitClass.Assassin;
    }
}
