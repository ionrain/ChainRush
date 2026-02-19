using UnityEngine;

/// <summary>
/// Reports a <see cref="StateSnapshot"/> for an enemy to the orchestration layer.
/// IMPORTANT: Read-only reporter. No gameplay control, no Update loop.
/// Called on demand by the orchestration layer.
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

        bool hasHealth = _enemy != null
            && _enemy.Health != null
            && _enemy.Health.MaximumHealth > 0;

        bool isAlive = hasHealth ? _enemy.Health.CurrentHealth > 0 : true;

        if (hasHealth)
        {
            _cachedMetrics.SetFloat("Hp01",
                _enemy.Health.CurrentHealth / _enemy.Health.MaximumHealth);
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
}
