using UnityEngine;

/// <summary>
/// Stores external combat commands (target or point) with a TTL so that
/// TopDownEngine AI nodes (<see cref="AIDecisionHasExternalCommand2D"/>,
/// <see cref="AIActionSetBrainTargetFromExternal2D"/>) can inject orchestrator
/// commands into the existing enemy AIBrain without modifying engine code.
/// <para>
/// IMPORTANT — Ownership contract:
/// • Orchestration decides WHAT target to pursue via <see cref="ApplyCombatCommand"/>.
/// • AIBrain / AIDecisions decide HOW to move and WHEN to transition states.
/// This executor never drives movement or attacks directly.
/// </para>
/// <para>
/// Adding this component to an enemy prefab has zero gameplay effect until
/// <see cref="ApplyCombatCommand"/> is called by an orchestrator AND the AIBrain
/// includes the matching decision/action nodes.
/// </para>
/// </summary>
public sealed class EnemyCombatCommandExecutor : MonoBehaviour, ICombatCommandReceiver, IOrchestrationActor
{
    // ──────────────────────────────────────────────────────────────────
    //  Serialized
    // ──────────────────────────────────────────────────────────────────

    [SerializeField] Enemy _enemy;
    [SerializeField] EnemyOrchestrationIdentity _identity;

    [Header("Command")]
    [Tooltip("How long an external command remains valid before expiring (seconds).")]
    [SerializeField] float externalCommandTtl = 1.5f;

    [Header("Debug")]
    [SerializeField] bool debugLog;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime state
    // ──────────────────────────────────────────────────────────────────

    Transform _externalTarget;
    Vector2 _externalPoint;
    bool _hasExternalPoint;
    float _externalExpiresAt;

    const string WaypointName = "Orch_Waypoint_EnemyCombat";
    Transform _waypoint;

    // ──────────────────────────────────────────────────────────────────
    //  Public API (read by TDE decision/action nodes)
    //  TTL is checked lazily — no Update loop.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// True when an external target transform is set and the command has not expired.
    /// </summary>
    public bool HasExternalTarget => _externalTarget != null && Time.time <= _externalExpiresAt;

    /// <summary>
    /// Returns the external target transform if still valid, otherwise null.
    /// </summary>
    public Transform ExternalTarget => HasExternalTarget ? _externalTarget : null;

    /// <summary>
    /// True when an external point command is set and the command has not expired.
    /// </summary>
    public bool HasExternalPoint => _hasExternalPoint && Time.time <= _externalExpiresAt;

    /// <summary>
    /// The external point position. Only meaningful when <see cref="HasExternalPoint"/> is true.
    /// </summary>
    public Vector2 ExternalPoint => _externalPoint;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void Reset()
    {
        _enemy = GetComponent<Enemy>();
        _identity = GetComponent<EnemyOrchestrationIdentity>();
    }

    void OnValidate()
    {
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();
        if (_identity == null)
            _identity = GetComponent<EnemyOrchestrationIdentity>();
    }

    void Awake()
    {
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();
        if (_identity == null)
            _identity = GetComponent<EnemyOrchestrationIdentity>();
    }

    void OnEnable() => OrchestrationRegistry.Register((ICombatCommandReceiver)this);
    void OnDisable() => OrchestrationRegistry.Unregister((ICombatCommandReceiver)this);

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
    /// Same guard as <see cref="EnemyStateReporter.IsAlive"/>.
    /// </summary>
    public bool IsAlive()
    {
        return _enemy != null
            && (_enemy.Health == null || _enemy.Health.MaximumHealth <= 0 || _enemy.Health.CurrentHealth > 0);
    }

    // ──────────────────────────────────────────────────────────────────
    //  ICombatCommandReceiver
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stores the command's target or point with a TTL.
    /// The actual brain target injection is performed by
    /// <see cref="AIActionSetBrainTargetFromExternal2D"/> during the AIBrain tick.
    /// </summary>
    public void ApplyCombatCommand(CombatCommand command)
    {
        if (_enemy == null)
            return;

        switch (command.Type)
        {
            case CombatCommandType.None:
                break;

            case CombatCommandType.Hold:
                ClearExternal();
                break;

            case CombatCommandType.MoveToPoint:
                SetExternalPoint(command.TargetPoint);
                break;

            case CombatCommandType.MoveToTarget:
            case CombatCommandType.AttackTarget:
                if (command.TargetTransform != null)
                    SetExternalTarget(command.TargetTransform);
                break;

            case CombatCommandType.KeepDistance:
            case CombatCommandType.HideBehind:
            case CombatCommandType.Assist:
                if (command.TargetTransform != null)
                    SetExternalTarget(command.TargetTransform);
                break;
        }

        if (debugLog)
            LogCommand(command);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Command handlers
    // ──────────────────────────────────────────────────────────────────

    void SetExternalTarget(Transform target)
    {
        _externalTarget = target;
        _hasExternalPoint = false;
        _externalExpiresAt = Time.time + externalCommandTtl;
    }

    void SetExternalPoint(Vector2 point)
    {
        _externalPoint = point;
        _hasExternalPoint = true;
        _externalTarget = null;
        _externalExpiresAt = Time.time + externalCommandTtl;
    }

    void ClearExternal()
    {
        _externalTarget = null;
        _hasExternalPoint = false;
        _externalExpiresAt = 0f;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Waypoint (used by AIActionSetBrainTargetFromExternal2D for points)
    //  Created lazily on first use; never destroyed (pooling-friendly).
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a waypoint transform positioned at <see cref="ExternalPoint"/>.
    /// Creates the waypoint child lazily on first call.
    /// </summary>
    public Transform GetOrCreatePointWaypoint()
    {
        EnsureWaypoint();
        _waypoint.position = new Vector3(_externalPoint.x, _externalPoint.y, 0f);
        return _waypoint;
    }

    void EnsureWaypoint()
    {
        if (_waypoint != null)
            return;

        _waypoint = FindWaypointChild();
        if (_waypoint != null)
            return;

        var go = new GameObject(WaypointName);
        go.transform.SetParent(transform, false);
        _waypoint = go.transform;
    }

    Transform FindWaypointChild()
    {
        int count = transform.childCount;
        for (int i = 0; i < count; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name == WaypointName)
                return child;
        }
        return null;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Debug
    // ──────────────────────────────────────────────────────────────────

    void LogCommand(CombatCommand command)
    {
        string target = command.HasTarget
            ? command.TargetTransform.name
            : command.TargetPoint.ToString();
        Debug.Log($"[EnemyCombatCommandExecutor] {command.Type} → {target}" +
                  (string.IsNullOrEmpty(command.DebugLabel) ? "" : $" ({command.DebugLabel})"),
                  this);
    }
}
