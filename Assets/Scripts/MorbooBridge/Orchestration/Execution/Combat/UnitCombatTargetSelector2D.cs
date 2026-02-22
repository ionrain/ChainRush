using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Per-unit targeting hook that allows different selection criteria per unit
/// via typed <see cref="CombatTargetingPolicyAsset"/> ScriptableObjects.
/// <para>
/// IMPORTANT — Without a policy asset assigned (both <see cref="overridePolicy"/> and
/// <see cref="defaultPolicy"/> null), this component is a passthrough: it resolves
/// the orchestrator-chosen primary target via <see cref="EntityTransformResolver"/>.
/// This component is optional; if absent, the executor resolves EntityId→Transform directly.
/// </para>
/// <para>
/// IMPORTANT — Policies operate on EntityId + Float2 + IWorldQueryBase (no Transform).
/// This component resolves the returned EntityId → Transform at the Integration boundary.
/// </para>
/// <para>
/// Variant B binding: If <see cref="autoResolveTargetSet"/> is true and no
/// <see cref="targetSet"/> is assigned, resolves from <see cref="OrchestrationRegistry"/>
/// by faction. Supports HOT REBIND: if the set was not available at OnEnable
/// (e.g. unit spawned before set), resolves on first <see cref="SelectTarget"/> call.
/// </para>
/// </summary>
public sealed class UnitCombatTargetSelector2D : MonoBehaviour, ICombatTargetPolicySelector
{
    // ──────────────────────────────────────────────────────────────────
    //  Serialized
    // ──────────────────────────────────────────────────────────────────

    [SerializeField] Unit _unit;
    [SerializeField] UnitOrchestrationIdentity _identity;

    [Header("Target Set")]
    [Tooltip("Optional shared Top-K hostile candidates. If null and autoResolveTargetSet " +
             "is true, resolved from OrchestrationRegistry by faction.")]
    [SerializeField] CombatTargetSet targetSet;
    [Tooltip("When true, resolve targetSet from OrchestrationRegistry if not assigned.")]
    [SerializeField] bool autoResolveTargetSet = true;

    [Header("Typed Policy")]
    [Tooltip("Per-unit targeting policy override. Takes priority over runtime default and defaultPolicy.")]
    [FormerlySerializedAs("policy")]
    [SerializeField] CombatTargetingPolicyAsset overridePolicy;

    [Tooltip("Scene/prefab default policy. If both policy and defaultPolicy are null, " +
             "resolves command.TargetEntityId via EntityTransformResolver (PrimaryTarget equivalent).")]
    [SerializeField] CombatTargetingPolicyAsset defaultPolicy;

    [Header("World Query")]
    [Tooltip("Optional IWorldQuery provider for targeting policies. If null, policies " +
             "that need world data will fall back to primary target.")]
    [SerializeField] MonoBehaviour worldQueryProvider;

    [SerializeField] bool debugLog;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// One-shot guard. Set true after the first resolve attempt so we don't
    /// call TryGetCombatTargetSet every SelectTarget call.
    /// Reset in OnEnable for pooling safety.
    /// </summary>
    bool _triedResolveTargetSet;

    /// <summary>
    /// Runtime default policy injected by arbiter per-role dispatch.
    /// Cleared in OnEnable for pooling safety.
    /// </summary>
    CombatTargetingPolicyAsset _runtimeDefaultPolicy;

    /// <summary>
    /// Cached IWorldQueryBase from worldQueryProvider or arbiter injection.
    /// </summary>
    IWorldQueryBase _worldQuery;

    // ──────────────────────────────────────────────────────────────────
    //  Editor convenience
    // ──────────────────────────────────────────────────────────────────

    void Reset()
    {
        _unit = GetComponent<Unit>();
        _identity = GetComponent<UnitOrchestrationIdentity>();
    }

    void OnValidate()
    {
        if (_unit == null)
            _unit = GetComponent<Unit>();
        if (_identity == null)
            _identity = GetComponent<UnitOrchestrationIdentity>();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        // Reset for pooling: old refs may be stale after scene change / re-pool.
        _triedResolveTargetSet = false;
        _runtimeDefaultPolicy = null;
        _worldQuery = null;
        TryResolveTargetSet();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the per-unit override policy at runtime (e.g. from <see cref="UnitAISetup"/>).
    /// Highest priority in resolution: overridePolicy > runtimeDefault > defaultPolicy.
    /// </summary>
    public void SetOverridePolicy(CombatTargetingPolicyAsset p) { overridePolicy = p; }

    /// <summary>
    /// Sets the runtime default policy injected by the arbiter per-role dispatch.
    /// Does not override a per-unit inspector policy; only fills the gap between
    /// per-unit override and scene default.
    /// </summary>
    public void SetRuntimeDefaultPolicy(CombatTargetingPolicyAsset p)
    {
        _runtimeDefaultPolicy = p;
    }

    /// <summary>
    /// Injects a world query reference for targeting policies.
    /// Called by the arbiter/adapter each tick or on setup.
    /// </summary>
    public void SetWorldQuery(IWorldQueryBase world)
    {
        _worldQuery = world;
    }

    /// <summary>
    /// Resolves the active policy using strict precedence:
    /// overridePolicy → runtime default (arbiter-injected) → defaultPolicy → null (passthrough).
    /// </summary>
    public CombatTargetingPolicyAsset ResolvePolicy()
    {
        if (overridePolicy != null) return overridePolicy;
        if (_runtimeDefaultPolicy != null) return _runtimeDefaultPolicy;
        return defaultPolicy;
    }

    /// <summary>
    /// Selects the target transform this unit should pursue for the given command.
    /// IMPORTANT: Policies return EntityId. This method resolves EntityId → Transform
    /// via <see cref="EntityTransformResolver"/> at the Integration boundary.
    /// Returns null to indicate "no target" (executor will fall back to Hold).
    /// </summary>
    public Transform SelectTarget(in CombatCommand command)
    {
        if (command.IsNone || command.Type == CombatCommandType.Hold)
            return null;

        // HOT REBIND: if CombatTargetSet registered after our OnEnable, try once.
        if (targetSet == null && autoResolveTargetSet && !_triedResolveTargetSet)
            TryResolveTargetSet();

        CombatTargetingPolicyAsset active = ResolvePolicy();

        if (active != null)
        {
            // Build policy inputs at Integration boundary
            EntityId selfId = _identity != null ? _identity.GetEntityId() : EntityId.None;
            Float2 selfPos = ((Vector2)transform.position).ToFloat2();

            // Resolve world query: injected > inspector provider
            IWorldQueryBase world = _worldQuery;
            if (world == null && worldQueryProvider != null)
                world = worldQueryProvider as IWorldQueryBase;

            string debugInfo;
            EntityId resultId = active.ChooseTarget(
                selfId, selfPos,
                command.TargetEntityId,
                targetSet,
                Time.time,
                world,
                out debugInfo);

            // Resolve EntityId → Transform at Integration boundary
            Transform result = !resultId.IsNone
                ? EntityTransformResolver.Resolve(resultId)
                : null;

            if (debugLog)
            {
                string targetName = result != null ? result.name : "null";
                Debug.Log($"[UnitCombatTargetSelector2D] Policy={active.Id} Info={debugInfo} Target={targetName}", this);
            }

            return result;
        }

        // No policy assigned: passthrough — resolve EntityId → Transform directly.
        return command.HasEntityTarget
            ? EntityTransformResolver.Resolve(command.TargetEntityId)
            : null;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Auto-resolve (Variant B)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// One-shot attempt to resolve <see cref="targetSet"/> from
    /// <see cref="OrchestrationRegistry"/> by this unit's typed <see cref="FactionAsset"/>.
    /// If the identity has no faction asset assigned, leaves targetSet null (no fallback).
    /// Sets <see cref="_triedResolveTargetSet"/> so subsequent calls are no-ops.
    /// </summary>
    void TryResolveTargetSet()
    {
        _triedResolveTargetSet = true;

        if (targetSet != null) return;
        if (!autoResolveTargetSet) return;

        if (_identity != null)
        {
            FactionAsset fa = _identity.GetFactionAsset();
            if (fa != null)
                OrchestrationRegistry.TryGetCombatTargetSet(fa, out targetSet);
        }
    }
}
