using UnityEngine;

/// <summary>
/// Reports a <see cref="StateSnapshot"/> for a unit to the orchestration layer.
/// IMPORTANT: Read-only reporter. No gameplay control, no Update loop.
/// Called on demand by the orchestration layer.
/// </summary>
public sealed class UnitStateReporter : MonoBehaviour, IStateReporter, IOrchestrationActor
{
    [SerializeField] Unit _unit;
    [SerializeField] UnitOrchestrationIdentity _identity;

    /// <summary>
    /// PERF: Cached metrics ParamSet. The list object is allocated on first call
    /// (capacity 4) and reused thereafter. Each call clears then re-sets values
    /// to avoid stale or duplicate keys.
    /// </summary>
    ParamSet _cachedMetrics;

    void OnEnable() => OrchestrationRegistry.Register((IStateReporter)this);
    void OnDisable() => OrchestrationRegistry.Unregister((IStateReporter)this);

    // ──────────────────────────────────────────────────────────────────
    //  IOrchestrationActor
    // ──────────────────────────────────────────────────────────────────

    public FactionAsset GetFactionAsset()
    {
        return _identity != null ? _identity.GetFactionAsset() : null;
    }

    public Transform GetTransform() => transform;

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

        bool isAlive = true;
        bool hasHealth = _unit != null && _unit.MaxHealth > 0;

        if (hasHealth)
        {
            isAlive = _unit.CurrentHealth > 0;
            _cachedMetrics.SetFloat("Hp01", _unit.CurrentHealth / _unit.MaxHealth);
        }

        if (_unit != null)
        {
            _cachedMetrics.SetInt("MergeState", _unit.MergeState);

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
            GetInstanceID(),
            (Vector2)transform.position,
            isAlive,
            roleTag,
            _cachedMetrics);
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
