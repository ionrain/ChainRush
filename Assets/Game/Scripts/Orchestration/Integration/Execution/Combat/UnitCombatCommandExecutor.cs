using UnityEngine;

/// <summary>
/// Thin adapter that translates <see cref="CombatCommand"/> structs into
/// <see cref="Unit.SetTarget"/> calls, letting the existing AIBrain state
/// machine (Idle ↔ MoveToTarget) handle all movement and attack transitions.
/// <para>
/// IMPORTANT — Ownership contract:
/// • Orchestration decides WHAT target to pursue.
/// • AIBrain / AIDecisions decide HOW to move and WHEN to transition states.
/// This executor never drives movement or attacks directly.
/// </para>
/// <para>
/// Adding this component to a Unit prefab has zero gameplay effect until
/// <see cref="ApplyCombatCommand"/> is called explicitly by an orchestrator.
/// </para>
/// </summary>
[RequireComponent(typeof(Unit))]
public sealed class UnitCombatCommandExecutor : MonoBehaviour, ICombatCommandReceiver, IOrchestrationActor, IRoleAssetProvider
{
    // ──────────────────────────────────────────────────────────────────
    //  Serialized
    // ──────────────────────────────────────────────────────────────────

    [SerializeField] Unit _unit;
    [SerializeField] UnitOrchestrationIdentity _identity;
    [SerializeField] UnitCombatTargetSelector _selector;

    [Header("Waypoint")]
    [Tooltip("When true, Reset/OnValidate will create a child waypoint for editor convenience.")]
    [SerializeField] bool createWaypointOnReset = true;

    [Header("Debug")]
    [SerializeField] bool debugLog;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime
    // ──────────────────────────────────────────────────────────────────

    const string WaypointName = "Orch_Waypoint_Combat";

    Transform _waypoint;

    // ──────────────────────────────────────────────────────────────────
    //  Editor convenience (Reset / OnValidate)
    //  IMPORTANT: object creation happens here, NOT in Awake.
    // ──────────────────────────────────────────────────────────────────

    void Reset()
    {
        _unit = GetComponent<Unit>();
        _identity = GetComponent<UnitOrchestrationIdentity>();
        _selector = GetComponent<UnitCombatTargetSelector>();
        if (createWaypointOnReset)
            FindOrCreateWaypointEditor();
    }

    void OnValidate()
    {
        if (_unit == null)
            _unit = GetComponent<Unit>();
        if (_identity == null)
            _identity = GetComponent<UnitOrchestrationIdentity>();
        if (_selector == null)
            _selector = GetComponent<UnitCombatTargetSelector>();
        if (createWaypointOnReset)
            FindOrCreateWaypointEditor();
    }

    /// <summary>
    /// Editor-only helper. Finds or creates the waypoint child transform.
    /// </summary>
    void FindOrCreateWaypointEditor()
    {
        Transform found = FindWaypointChild();
        if (found != null)
        {
            _waypoint = found;
            return;
        }

        var go = new GameObject(WaypointName);
        go.transform.SetParent(transform, false);
        _waypoint = go.transform;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Runtime init
    // ──────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_unit == null)
            _unit = GetComponent<Unit>();
        if (_identity == null)
            _identity = GetComponent<UnitOrchestrationIdentity>();
        if (_selector == null)
            _selector = GetComponent<UnitCombatTargetSelector>();

        if (_waypoint == null)
            _waypoint = FindWaypointChild();
    }

    void OnEnable() => OrchestrationRegistry.Register((ICombatCommandReceiver)this);
    void OnDisable() => OrchestrationRegistry.Unregister((ICombatCommandReceiver)this);

    // ──────────────────────────────────────────────────────────────────
    //  IRoleAssetProvider
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the typed role asset for per-role combat targeting policy injection.
    /// Delegates to <see cref="UnitOrchestrationIdentity.GetRoleAsset"/>.
    /// </summary>
    public RoleAsset GetRoleAsset()
    {
        return _identity != null ? _identity.GetRoleAsset() : null;
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
    /// IMPORTANT: If MaxHealth &lt;= 0 (health not yet initialized), the entity is
    /// treated as alive to avoid false negatives during early lifecycle.
    /// </summary>
    public bool IsAlive()
    {
        return _unit != null && (_unit.MaxHealth <= 0 || _unit.CurrentHealth > 0);
    }

    // ──────────────────────────────────────────────────────────────────
    //  ICombatCommandReceiver
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a combat command by setting (or clearing) the Unit's AIBrain target.
    /// Called on demand — not per-frame.
    /// </summary>
    public void ApplyCombatCommand(CombatCommand command)
    {
        if (_unit == null)
            return;

        switch (command.Type)
        {
            // None = "no command issued; do not change current target."
            case CombatCommandType.None:
                break;

            case CombatCommandType.Hold:
                ApplyHold();
                break;

            case CombatCommandType.MoveToPoint:
                ApplyMoveToPoint(command);
                break;

            case CombatCommandType.MoveToTarget:
            case CombatCommandType.AttackTarget:
                ApplyTargetOrHold(command);
                break;

            // RATIONALE: KeepDistance, HideBehind, and Assist require domain planner
            // support (range management, cover evaluation, formation logic).
            // The executor only provides basic target plumbing as a conservative fallback.
            case CombatCommandType.KeepDistance:
            case CombatCommandType.HideBehind:
            case CombatCommandType.Assist:
                ApplyFallback(command);
                break;
        }

        if (debugLog)
            LogCommand(command);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Command handlers
    // ──────────────────────────────────────────────────────────────────

    void ApplyHold()
    {
        _unit.SetTarget(null);
    }

    /// <summary>
    /// Positions the cached waypoint at <see cref="CombatCommand.TargetPoint"/>
    /// and sets it as the AIBrain target.
    /// IMPORTANT: TargetPoint is always treated as valid for MoveToPoint commands.
    /// </summary>
    void ApplyMoveToPoint(CombatCommand command)
    {
        EnsureWaypoint();
        _waypoint.position = new Vector3(command.TargetPoint.x, command.TargetPoint.y, 0f);
        _unit.SetTarget(_waypoint);
    }

    /// <summary>
    /// Sets target transform if available; otherwise falls back to Hold (clear target).
    /// Routes through <see cref="_selector"/> when present for per-unit targeting override.
    /// No fallback to TargetPoint — MoveToTarget/AttackTarget require an explicit transform.
    /// </summary>
    void ApplyTargetOrHold(CombatCommand command)
    {
        Transform chosen = _selector != null
            ? _selector.SelectTarget(command)
            : command.TargetTransform;

        if (chosen != null)
            _unit.SetTarget(chosen);
        else
            ApplyHold();
    }

    /// <summary>
    /// Conservative fallback for commands that need planner support (KeepDistance,
    /// HideBehind, Assist). Routes through <see cref="_selector"/> when present.
    /// </summary>
    void ApplyFallback(CombatCommand command)
    {
        Transform chosen = _selector != null
            ? _selector.SelectTarget(command)
            : command.TargetTransform;

        if (chosen != null)
            _unit.SetTarget(chosen);
        else
            ApplyHold();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Waypoint helpers
    //  PERF: waypoint is created at most once at runtime, only when
    //  the first MoveToPoint command is applied and no waypoint exists.
    // ──────────────────────────────────────────────────────────────────

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

    /// <summary>
    /// Finds a child transform named <see cref="WaypointName"/> without allocations.
    /// </summary>
    Transform FindWaypointChild()
    {
        // PERF: manual iteration avoids Transform.Find string allocation on some Unity versions.
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
        Debug.Log($"[UnitCombatCommandExecutor] {command.Type} → {target}" +
                  (string.IsNullOrEmpty(command.DebugLabel) ? "" : $" ({command.DebugLabel})"),
                  this);
    }
}
