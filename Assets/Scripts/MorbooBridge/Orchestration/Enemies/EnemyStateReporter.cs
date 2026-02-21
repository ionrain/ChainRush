using UnityEngine;

/// <summary>
/// Reports a <see cref="StateSnapshot"/> for an enemy to the orchestration layer.
/// IMPORTANT: No gameplay control, no Update loop. Called on demand by orchestration.
/// C3.3: syncs authoritative entity model state before building snapshot metrics.
/// </summary>
public sealed class EnemyStateReporter : MonoBehaviour, IStateReporter, IOrchestrationActor, IEntityIdProvider
{
    [SerializeField] Enemy _enemy;
    [SerializeField] EnemyOrchestrationIdentity _identity;

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
            Debug.LogWarning("[EnemyStateReporter] Missing EnemyOrchestrationIdentity; EntityId is None.", this);
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

    /// <summary>
    /// IMPORTANT: If Health is null or MaximumHealth &lt;= 0 (not yet initialized),
    /// the entity is treated as alive to avoid false negatives during early lifecycle.
    /// </summary>
    public bool IsAlive()
    {
        return _enemy != null
            && (_enemy.Health == null || _enemy.Health.MaximumHealth <= 0 || _enemy.Health.CurrentHealth > 0);
    }

    void Reset()
    {
        _enemy = GetComponent<Enemy>();
        _identity = GetComponent<EnemyOrchestrationIdentity>();
    }

    /// <summary>
    /// Builds and returns a <see cref="StateSnapshot"/> reflecting the enemy's current state.
    /// RATIONALE: A single hasHealth guard is reused for both IsAlive and Hp01.
    /// When health is unknown we report IsAlive = true and omit Hp01.
    /// </summary>
    public StateSnapshot ReportState()
    {
        if (_cachedMetrics.Items != null) _cachedMetrics.Items.Clear();

        FactionAsset faction = _identity != null ? _identity.GetFactionAsset() : null;
        TryGetEntityState(out IEntityStateAccessor entityState);

        bool hasHealth = _enemy != null
            && _enemy.Health != null
            && _enemy.Health.MaximumHealth > 0;

        float legacyHp01 = hasHealth ? (_enemy.Health.CurrentHealth / _enemy.Health.MaximumHealth) : 0f;
        bool legacyIsAlive = _enemy != null && (!hasHealth || _enemy.Health.CurrentHealth > 0);

        SyncEntityState(entityState, legacyIsAlive);
        bool isAlive = entityState != null ? entityState.IsAlive : legacyIsAlive;

        if (hasHealth)
        {
            _cachedMetrics.SetFloat("Hp01", legacyHp01);
        }

        if (_enemy != null)
        {
            _cachedMetrics.SetString("EnemyType", _enemy.Type.ToString());
        }

        string roleTag = _identity != null
            ? _identity.GetRoleTag(_enemy)
            : "Enemy";

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

        entityState.SetAlive(isAlive);
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

}
