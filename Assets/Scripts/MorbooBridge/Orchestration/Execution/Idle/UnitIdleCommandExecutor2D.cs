using UnityEngine;

/// <summary>
/// Applies idle commands to a <see cref="Unit"/> by setting its AIBrain target.
/// <para>
/// IMPORTANT — Ownership contract (same as combat executor):
/// Orchestration decides WHAT target to pursue (idle waypoint or null).
/// AIBrain / AIDecisions decide HOW to move and WHEN to transition states.
/// This executor never drives movement directly.
/// </para>
/// <para>
/// IMPORTANT — Idle bounds are resolved at runtime via <see cref="IdleBoundsRegistry"/>
/// keyed by the unit's <see cref="RoleAsset"/>. No inspector wiring of bounds providers needed.
/// Supports hot-rebind: if the zone registers after the unit, backoff retry resolves it.
/// </para>
/// <para>
/// IMPORTANT — <see cref="IdleCommand.StopDistance"/> is data-only in Step R1.
/// The executor does NOT enforce distance checks. No Update loop. No per-frame work.
/// </para>
/// </summary>
public sealed class UnitIdleCommandExecutor2D : MonoBehaviour, IIdleCommandReceiver, IFactionAssetProvider, IOrchestrationActor, IRoleContextProvider
{
    [SerializeField] Unit _unit;
    [SerializeField] UnitOrchestrationIdentity _identity;

    [Header("Policy Selector")]
    [SerializeField] UnitIdlePolicySelector _selector;

    [Header("Fallback Bounds")]
    [Tooltip("Fallback bounds size if no provider registered for this unit's role.")]
    [SerializeField] Vector2 fallbackBoundsSize = new Vector2(6f, 3f);

    [Header("Debug")]
    [SerializeField] bool debugLog;

    Transform _waypoint;

    // ──────────────────────────────────────────────────────────────────
    //  Hot-resolve state: bounds provider from IdleBoundsRegistry
    // ──────────────────────────────────────────────────────────────────

    IIdleBoundsProvider _resolvedProvider;
    bool _warnedMissingRole;
    bool _warnedMissingProvider;
    float _nextResolveTime;
    int _resolveAttempts;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void Reset()
    {
        if (_unit == null) _unit = GetComponent<Unit>();
        if (_identity == null) _identity = GetComponent<UnitOrchestrationIdentity>();
        if (_selector == null) _selector = GetComponent<UnitIdlePolicySelector>();
    }

    void Awake()
    {
        if (_unit == null) _unit = GetComponent<Unit>();
        if (_identity == null) _identity = GetComponent<UnitOrchestrationIdentity>();
        if (_selector == null) _selector = GetComponent<UnitIdlePolicySelector>();
        _waypoint = FindWaypoint();
    }

    void OnEnable()
    {
        // Pooling-safe: reset resolve state so re-enabled units re-resolve.
        // Keep warning flags true across enables to avoid spam on pooled re-enable.
        _resolvedProvider = null;
        _nextResolveTime = 0f;
        _resolveAttempts = 0;

        OrchestrationRegistry.Register((IIdleCommandReceiver)this);
    }

    void OnDisable()
    {
        OrchestrationRegistry.Unregister((IIdleCommandReceiver)this);
    }

    // ──────────────────────────────────────────────────────────────────
    //  IIdleCommandReceiver
    // ──────────────────────────────────────────────────────────────────

    public void ApplyIdleCommand(IdleCommand command)
    {
        if (_unit == null) return;

        switch (command.Type)
        {
            case IdleCommandType.None:
                return;

            case IdleCommandType.Hold:
                _unit.SetTarget(null);
                break;

            case IdleCommandType.MoveToPoint:
                EnsureWaypoint();
                _waypoint.position = new Vector3(command.TargetPoint.X, command.TargetPoint.Y, 0f);
                _unit.SetTarget(_waypoint);
                break;

            default:
                _unit.SetTarget(null);
                break;
        }

        if (debugLog)
        {
            Debug.Log(string.Concat(
                "[IdleExecutor] ", name,
                " cmd=", command.Type.ToString(),
                " point=", command.TargetPoint.ToString(),
                " label=", command.DebugLabel ?? ""));
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Policy access (convenience for orchestrator)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the resolved idle policy for this unit, or null if none assigned.
    /// </summary>
    public IdlePolicyAsset ResolveIdlePolicy()
    {
        return _selector != null ? _selector.ResolvePolicy() : null;
    }

    bool _warnedMissingIdentity;

    // ──────────────────────────────────────────────────────────────────
    //  IRoleContextProvider (includes IEntityIdProvider + IRoleAssetProvider)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the typed role asset for per-role idle dispatch (Integration boundary).
    /// Delegates to <see cref="UnitOrchestrationIdentity.GetRoleAsset"/>.
    /// </summary>
    public RoleAsset GetRoleAsset()
    {
        return _identity != null ? _identity.GetRoleAsset() : null;
    }

    /// <summary>
    /// Returns stable RoleId for RuntimeHost routing.
    /// </summary>
    public RoleId GetRoleId()
    {
        if (_identity == null) return RoleId.None;
        RoleAsset ra = _identity.GetRoleAsset();
        return ra != null ? ra.RoleId : RoleId.None;
    }

    /// <summary>
    /// Returns the stable EntityId from the sibling identity component.
    /// IMPORTANT: Missing identity is fatal — warns once and returns EntityId.None.
    /// </summary>
    public EntityId GetEntityId()
    {
        if (_identity != null) return _identity.GetEntityId();
        if (!_warnedMissingIdentity)
        {
            _warnedMissingIdentity = true;
            Debug.LogWarning("[UnitIdleCommandExecutor2D] Missing UnitOrchestrationIdentity; EntityId is None.", this);
        }
        return EntityId.None;
    }

    // ──────────────────────────────────────────────────────────────────
    //  IFactionAssetProvider / IOrchestrationActor
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

    public bool IsAlive()
    {
        return _unit != null && (_unit.MaxHealth <= 0 || _unit.CurrentHealth > 0);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Bounds provider — resolved from IdleBoundsRegistry by RoleAsset
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to resolve the <see cref="IIdleBoundsProvider"/> from <see cref="IdleBoundsRegistry"/>
    /// using the unit's <see cref="RoleAsset"/>. Uses backoff to support zones that register after units.
    /// Once resolved, no further registry lookups occur.
    /// <para>
    /// RATIONALE: Backoff sequence (0.25s → 0.5s → 1.0s → 2.0s cap) prevents per-call scans
    /// while still supporting late-registering zones within a few seconds.
    /// </para>
    /// </summary>
    void TryResolveProvider()
    {
        // Already resolved
        if (_resolvedProvider != null) return;

        // Backoff: don't retry every call
        if (_resolveAttempts > 0 && Time.time < _nextResolveTime) return;

        _resolveAttempts++;

        RoleAsset role = _identity != null ? _identity.GetRoleAsset() : null;
        if (role == null)
        {
            if (!_warnedMissingRole)
            {
                _warnedMissingRole = true;
                Debug.LogWarning(string.Concat(
                    "[IdleExecutor] ", name,
                    ": missing RoleAsset on identity; using fallback bounds."), this);
            }
            // No point retrying without a role
            _nextResolveTime = float.MaxValue;
            return;
        }

        IIdleBoundsProvider p;
        if (IdleBoundsRegistry.TryGet(role.RoleId, out p))
        {
            _resolvedProvider = p;
            return;
        }

        // Backoff: 0.25 → 0.5 → 1.0 → 2.0 (capped)
        float delay = Mathf.Min(0.25f * Mathf.Pow(2f, _resolveAttempts - 1), 2f);
        _nextResolveTime = Time.time + delay;

        if (!_warnedMissingProvider)
        {
            _warnedMissingProvider = true;
            Debug.LogWarning(string.Concat(
                "[IdleExecutor] ", name,
                ": no IdleBoundsProvider for role '", role.Id,
                "'; using fallback bounds (will retry)."), this);
        }
    }

    /// <summary>
    /// Queries idle bounds via registry-resolved provider, or returns fallback bounds
    /// centered on this unit's position.
    /// </summary>
    public bool TryGetIdleBounds(out Bounds b)
    {
        TryResolveProvider();
        if (_resolvedProvider != null)
            return _resolvedProvider.TryGetIdleBounds(out b);

        // Fallback: bounds centered on this unit's current position
        b = new Bounds(transform.position, new Vector3(fallbackBoundsSize.x, fallbackBoundsSize.y, 1f));
        return true;
    }

    /// <summary>
    /// Queries idle anchor via registry-resolved provider, or returns this unit's position.
    /// </summary>
    public bool TryGetIdleAnchor(out Vector2 a)
    {
        TryResolveProvider();
        if (_resolvedProvider != null)
            return _resolvedProvider.TryGetIdleAnchor(out a);

        // Fallback: unit's own position
        a = (Vector2)transform.position;
        return true;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Waypoint management (PERF: created once, reused)
    // ──────────────────────────────────────────────────────────────────

    Transform FindWaypoint()
    {
        return transform.Find("OrchestrationIdleWaypoint");
    }

    void EnsureWaypoint()
    {
        if (_waypoint != null) return;

        Transform t = FindWaypoint();
        if (t != null)
        {
            _waypoint = t;
            return;
        }

        var go = new GameObject("OrchestrationIdleWaypoint");
        go.transform.SetParent(transform, false);
        _waypoint = go.transform;
    }
}
