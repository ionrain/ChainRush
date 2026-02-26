using UnityEngine;

/// <summary>
/// Thin adapter that translates <see cref="OrchestrationCommand"/> structs into
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
/// <see cref="IOrchestrationCommandReceiver.ApplyCommand"/> is called explicitly by an orchestrator.
/// </para>
/// <para>
/// IMPORTANT — Movement constraints: When <see cref="IConstrainedCombatReceiver.SetRuntimeContext"/>
/// injects a non-null <see cref="CombatMoveConstraintsAsset"/>, movement targets are clamped
/// to a leash radius around the world anchor. Anti-thrash state machine prevents ping-pong.
/// When constraints are null, all paths delegate to original unconstrained methods with no side effects.
/// </para>
/// </summary>
[RequireComponent(typeof(Unit))]
public sealed class UnitCombatCommandExecutor : MonoBehaviour, IOrchestrationCommandReceiver, IConstrainedCombatReceiver, IOrchestrationActor, IRoleAssetProvider, IEntityIdProvider
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
    //  Runtime — Constraints (tick-scoped, injected by arbiter)
    // ──────────────────────────────────────────────────────────────────

    CombatMoveConstraintsAsset _constraints;
    IWorldQuery _world;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Constrained movement state (persistent across ticks)
    //  IMPORTANT: Only consulted when constrainedActive is true.
    //  Unconstrained paths never read these fields.
    // ──────────────────────────────────────────────────────────────────

    enum ConstrainedMoveMode { None, Hold, Waypoint }
    ConstrainedMoveMode _constrainedMode;
    float _modeEnteredTime;
    float _lastRepathTime;
    Vector3 _lastClampedTarget;

    // ──────────────────────────────────────────────────────────────────
    //  Constants — Spread sampling
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Quantization grid for boundary point hashing.
    /// 4 = 0.25 world-unit cells. Controls stability vs responsiveness of spread positions.
    /// </summary>
    const float BOUNDARY_QUANT = 4f;
    const int SAMPLE_SEED_PRIME = 0x45D9F3B;
    const float EPSILON_SQR = 0.01f;

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

    void OnEnable() => OrchestrationRegistry.Register((IOrchestrationCommandReceiver)this);
    void OnDisable() => OrchestrationRegistry.Unregister((IOrchestrationCommandReceiver)this);

    bool _warnedMissingIdentity;

    // ──────────────────────────────────────────────────────────────────
    //  IEntityIdProvider
    // ──────────────────────────────────────────────────────────────────

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
            Debug.LogWarning("[UnitCombatCommandExecutor2D] Missing UnitOrchestrationIdentity; EntityId is None.", this);
        }
        return EntityId.None;
    }

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

    // ──────────────────────────────────────────────────────────────────
    //  IConstrainedCombatReceiver
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Injects runtime constraints and world cache for this tick.
    /// IMPORTANT: Called by arbiter BEFORE <see cref="IOrchestrationCommandReceiver.ApplyCommand"/> each tick.
    /// Null constraints = unconstrained movement (default behavior).
    /// </summary>
    public void SetRuntimeContext(CombatMoveConstraintsAsset constraints, IWorldQuery world)
    {
        // Detect constrained → unconstrained transition: reset mode so re-entry starts fresh
        if (_constraints != null && constraints == null)
            _constrainedMode = ConstrainedMoveMode.None;

        _constraints = constraints;
        _world = world;
    }

    // ──────────────────────────────────────────────────────────────────
    //  IOrchestrationCommandReceiver
    // ──────────────────────────────────────────────────────────────────

    public void ApplyCommand(OrchestrationCommand command)
    {
        if (_unit == null)
            return;

        switch (command.Type)
        {
            case OrchestrationCommandType.None:
                return;

            case OrchestrationCommandType.Cancel:
                ApplyHold();
                break;

            case OrchestrationCommandType.Engage:
                switch (command.Target.Kind)
                {
                    case OrchestrationTargetKind.Entity:
                        ApplyTargetOrHoldConstrained(command.Target.TargetEntityId);
                        break;

                    case OrchestrationTargetKind.Point:
                        if (OrchestrationTargetPointRegistry.TryGetPointTarget(command.Target.TargetEntityId, out Float3 point))
                            ApplyMoveToPointConstrained(point);
                        else
                            ApplyHold();
                        break;

                    case OrchestrationTargetKind.Area:
                    case OrchestrationTargetKind.Route:
                    case OrchestrationTargetKind.None:
                    default:
                        ApplyHold();
                        break;
                }
                break;

            default:
                return;
        }

        if (debugLog)
            LogCommand(command);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Command handlers — unconstrained (original)
    // ──────────────────────────────────────────────────────────────────

    void ApplyHold()
    {
        _unit.SetTarget(null);
    }

    /// <summary>
    /// Positions the cached waypoint at the given point
    /// and sets it as the AIBrain target.
    /// </summary>
    void ApplyMoveToPoint(Float3 targetPoint)
    {
        EnsureWaypoint();
        _waypoint.position = targetPoint.ToVector3();
        _unit.SetTarget(_waypoint);
    }

    /// <summary>
    /// Sets target transform if available; otherwise falls back to Hold (clear target).
    /// Routes through <see cref="_selector"/> when present for per-unit targeting override.
    /// No fallback to point — entity Engage requires an explicit transform.
    /// </summary>
    void ApplyTargetOrHold(EntityId targetEntityId)
    {
        // IMPORTANT: EntityId→Transform resolution at Integration boundary
        Transform chosen = _selector != null
            ? _selector.SelectTarget(targetEntityId)
            : (!targetEntityId.IsNone ? EntityTransformResolver.Resolve(targetEntityId) : null);

        if (chosen != null)
            _unit.SetTarget(chosen);
        else
            ApplyHold();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Command handlers — constrained
    //  IMPORTANT: When constrainedActive is false, these delegate to
    //  the original unconstrained methods with no side effects.
    // ──────────────────────────────────────────────────────────────────

    void ApplyMoveToPointConstrained(Float3 targetPoint)
    {
        bool constrainedActive = _constraints != null && _constraints.leashRadius > 0f && _world != null;
        if (!constrainedActive)
        {
            ApplyMoveToPoint(targetPoint);

            return;
        }

        Vector3 point = targetPoint.ToVector3();
        Vector2 clamped = ClampToLeash(point, out bool wasClamped);

        if (!wasClamped)
        {
            ApplyMoveToPoint(targetPoint);
            return;
        }

        // Apply anti-thrash gating, then set constrained waypoint
        ApplyConstrainedWaypoint(clamped);
    }

    void ApplyTargetOrHoldConstrained(EntityId targetEntityId)
    {
        bool constrainedActive = _constraints != null && _constraints.leashRadius > 0f && _world != null;
        if (!constrainedActive)
        {
            ApplyTargetOrHold(targetEntityId);
            return;
        }

        // Resolve target via selector (policy already injected by arbiter)
        // IMPORTANT: EntityId→Transform resolution at Integration boundary
        Transform chosen = _selector != null
            ? _selector.SelectTarget(targetEntityId)
            : (!targetEntityId.IsNone ? EntityTransformResolver.Resolve(targetEntityId) : null);

        if (chosen == null)
        {
            ApplyHold();
            return;
        }

        // Check if target is inside leash
        Vector2 targetPos = (Vector2)chosen.position;
        Vector2 clamped = ClampToLeash(targetPos, out bool wasClamped);

        if (!wasClamped)
        {
            // Target is inside leash — set directly, no constraints apply
            _unit.SetTarget(chosen);
            return;
        }

        // Target is outside leash — apply OutOfBoundsMode strategy
        float now = _world.Now;

        switch (_constraints.outOfBoundsMode)
        {
            case OutOfBoundsMode.Hold:
                EnterConstrainedHold(now);
                break;

            case OutOfBoundsMode.ClosestAllowedPoint:
                ApplyConstrainedWaypoint(clamped);
                break;

            case OutOfBoundsMode.SpreadNearBoundary:
                Vector3 spread = ComputeSpreadPoint(clamped, _world.Anchor.ToVector3());
                ApplyConstrainedWaypoint(spread);
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Constrained state machine
    //  RATIONALE: Explicit mode tracking prevents ping-pong between
    //  Hold and Waypoint. Mode transitions are gated by min-time timers.
    // ──────────────────────────────────────────────────────────────────

    void EnterConstrainedHold(float now)
    {
        // If already in Hold, check epsilon — target hasn't changed
        if (_constrainedMode == ConstrainedMoveMode.Hold)
            return;

        // If in Waypoint, check waypointMinTime before switching
        if (_constrainedMode == ConstrainedMoveMode.Waypoint &&
            now - _modeEnteredTime < _constraints.waypointMinTime)
            return;

        _constrainedMode = ConstrainedMoveMode.Hold;
        _modeEnteredTime = now;
        ApplyHold();
    }

    void ApplyConstrainedWaypoint(Vector3 point)
    {
        float now = _world.Now;

        // Epsilon check: target hasn't meaningfully moved — skip
        if ((_lastClampedTarget - point).sqrMagnitude < EPSILON_SQR &&
            _constrainedMode != ConstrainedMoveMode.None)
            return;

        // If in Hold, check holdMinTime before switching to Waypoint
        if (_constrainedMode == ConstrainedMoveMode.Hold &&
            now - _modeEnteredTime < _constraints.holdMinTime)
            return;

        // If already in Waypoint, check waypointMinTime
        if (_constrainedMode == ConstrainedMoveMode.Waypoint &&
            now - _modeEnteredTime < _constraints.waypointMinTime)
        {
            // Target changed beyond epsilon but min time hasn't elapsed — check repath
            if (now - _lastRepathTime < _constraints.minRepathInterval)
                return;
        }

        // Enter/stay in Waypoint mode
        if (_constrainedMode != ConstrainedMoveMode.Waypoint)
        {
            _constrainedMode = ConstrainedMoveMode.Waypoint;
            _modeEnteredTime = now;
        }

        _lastRepathTime = now;
        _lastClampedTarget = point;

        EnsureWaypoint();
        _waypoint.position = new Vector3(point.x, point.y, 0f);
        _unit.SetTarget(_waypoint);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Constraint helpers — geometry
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Clamps target to leash circle around <c>_world.Anchor</c>.
    /// Pure geometry, no crowd logic.
    /// </summary>
    Vector3 ClampToLeash(Vector3 target, out bool clamped)
    {
        Vector3 anchor = _world.Anchor.ToVector3();
        Vector3 toTarget = target - anchor;
        float distSqr = toTarget.sqrMagnitude;
        float leashSqr = _constraints.leashRadius * _constraints.leashRadius;

        if (distSqr <= leashSqr)
        {
            clamped = false;
            return target;
        }

        clamped = true;
        float dist = Mathf.Sqrt(distSqr);
        Vector3 dir = toTarget / dist;
        return anchor + dir * _constraints.leashRadius;
    }

    /// <summary>
    /// Samples radial offsets around <paramref name="boundaryPoint"/> and picks the position
    /// with lowest crowd penalty + anchor distance. Only "outside leash" is hard-rejected;
    /// personal space is a soft penalty so a position is always found.
    /// <para>
    /// PERF: Uses <see cref="CrowdScoringUtility2D"/> with <see cref="ICrowdQuery"/>.
    /// Self-skip via <see cref="EntityId"/>. No allocations, no <c>UnityEngine.Random</c>.
    /// </para>
    /// </summary>
    Vector3 ComputeSpreadPoint(Vector3 boundaryPoint, Vector3 anchor)
    {
        int sampleCount = _constraints.samples > 0 ? _constraints.samples : 8;
        float personalSpace = _constraints.personalSpace;
        float maxOffset = _constraints.maxSpreadOffset;
        float crowdRadiusSqr = _constraints.crowdCheckRadius * _constraints.crowdCheckRadius;
        float personalSpaceSqr = personalSpace * personalSpace;
        float leashSqr = _constraints.leashRadius * _constraints.leashRadius;
        float anchorWeight = _constraints.anchorDistanceWeight;

        // Deterministic seed: entitySeed ^ quantized boundary position
        // RATIONALE: Stable while boundary stays in the same quantization cell, changes when
        // boundary moves significantly. No float time in seed prevents drift.
        EntityId selfId = GetEntityId();
        int entitySeed = selfId.ToStableInt();
        int qx = Mathf.RoundToInt(boundaryPoint.x * BOUNDARY_QUANT);
        int qy = Mathf.RoundToInt(boundaryPoint.y * BOUNDARY_QUANT);
        int baseSeed = entitySeed ^ CrowdScoringUtility.Hash01ToInt(qx ^ (qy * 0x6C62272E));

        Vector2 bestPoint = boundaryPoint;
        float bestScore = float.MaxValue;

        for (int i = 0; i < sampleCount; i++)
        {
            int sampleSeed = baseSeed ^ (i * SAMPLE_SEED_PRIME);
            float angle = CrowdScoringUtility.Hash01(sampleSeed) * 360f;
            float dist = personalSpace + CrowdScoringUtility.Hash01(sampleSeed ^ 0x1B873) * (maxOffset - personalSpace);

            float rad = angle * Mathf.Deg2Rad;
            Vector3 candidate = boundaryPoint + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * dist;

            // Hard reject: outside leash
            if ((candidate - anchor).sqrMagnitude > leashSqr)
                continue;

            // Score: crowd penalty via IWorldQuery (soft — never rejects, self-skip via EntityId)
            float score = CrowdScoringUtility.ScoreCrowdPenalty(
                _world, selfId, candidate.ToFloat3(),
                personalSpaceSqr, crowdRadiusSqr);

            // Score: anchor distance preference
            float distToAnchor = (candidate - anchor).magnitude;
            float anchorScore = _constraints.leashRadius > 0f
                ? distToAnchor / _constraints.leashRadius
                : 0f;
            score += anchorScore * anchorWeight;

            if (score < bestScore)
            {
                bestScore = score;
                bestPoint = candidate;
            }
        }

        return bestPoint;
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

    void LogCommand(OrchestrationCommand command)
    {
        string target = "None";
        if (command.Type == OrchestrationCommandType.Cancel)
        {
            target = "Cancel";
        }
        else if (command.Type == OrchestrationCommandType.Engage)
        {
            switch (command.Target.Kind)
            {
                case OrchestrationTargetKind.Entity:
                    Transform resolved = EntityTransformResolver.Resolve(command.Target.TargetEntityId);
                    target = resolved != null ? resolved.name : command.Target.TargetEntityId.ToStableInt().ToString();
                    break;
                case OrchestrationTargetKind.Point:
                    if (OrchestrationTargetPointRegistry.TryGetPointTarget(command.Target.TargetEntityId, out Float3 point))
                        target = point.ToString();
                    else
                        target = command.Target.TargetEntityId.ToStableInt().ToString();
                    break;
                default:
                    target = command.Target.Kind.ToString();
                    break;
            }
        }

        Debug.Log($"[UnitCombatCommandExecutor2D] {command.Type}/{command.Target.Kind} → {target}" +
                  (string.IsNullOrEmpty(command.DebugLabel) ? "" : $" ({command.DebugLabel})"),
                  this);
    }
}
