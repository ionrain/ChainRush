using UnityEngine;

/// <summary>
/// Idle domain evaluator. Owns the <see cref="IdleRolePolicyMapAsset"/> reference
/// and pushes it to the arbiter via dirty-flag binding. Signals "idle domain active"
/// each tick so the arbiter can dispatch idle commands using the stored map.
/// <para>
/// IMPORTANT — This class does NOT tick itself. It implements
/// <see cref="IOrchestrationDomain"/> and is polled by
/// <see cref="OrchestrationArbiter"/> each tick.
/// </para>
/// <para>
/// IMPORTANT — Does NOT dispatch commands to receivers. Only writes proposals.
/// The arbiter owns all dispatch logic including per-unit command generation.
/// </para>
/// </summary>
public sealed class IdleOrchestratorLite : MonoBehaviour, IOrchestrationDomain
{
    [Header("Per-Role Policies")]
    [Tooltip("Data-driven mapping of RoleAssets to idle policies.")]
    [SerializeField] IdleRolePolicyMapAsset rolePolicyMap;

    [Header("Arbiter Binding")]
    [SerializeField] OrchestrationArbiter arbiter;

    [Header("Debug")]
    [SerializeField] bool debugLog;

    // ──────────────────────────────────────────────────────────────────
    //  Dirty-flag binding state
    // ──────────────────────────────────────────────────────────────────

    IdleRolePolicyMapAsset _lastBoundMap;
    bool _dirtyMap;
    bool _warnedNoArbiter;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _dirtyMap = true;
        _lastBoundMap = null;
    }

    void OnDisable()
    {
        _lastBoundMap = null; // Force rebind on re-enable
    }

#if UNITY_EDITOR
    void OnValidate() { _dirtyMap = true; }
#endif

    // ──────────────────────────────────────────────────────────────────
    //  Arbiter binding — push map only when reference changes
    //  IMPORTANT: Checks ref equality even without dirty flag to catch
    //  runtime script changes (e.g. UnitAISetup reassigning rolePolicyMap).
    // ──────────────────────────────────────────────────────────────────

    void BindIfDirty()
    {
        if (!_dirtyMap && ReferenceEquals(_lastBoundMap, rolePolicyMap)) return;
        _dirtyMap = false;

        if (arbiter == null)
        {
            if (!_warnedNoArbiter)
            {
                _warnedNoArbiter = true;
                Debug.LogWarning("[IdleOrchestratorLite] Missing OrchestrationArbiter reference; " +
                                 "idle role map not bound.", this);
            }
            return;
        }

        _lastBoundMap = rolePolicyMap;
        arbiter.SetIdleRolePolicyMap(rolePolicyMap);

        if (debugLog)
        {
            Debug.Log(string.Concat(
                "[IdleOrchestratorLite] Bound idle role map: ",
                rolePolicyMap != null ? rolePolicyMap.name : "null"), this);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  IOrchestrationDomain
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pushes the role policy map to the arbiter (if dirty) and signals
    /// "idle domain is active" via <see cref="OrchestrationArbiterProposals.SetIdle"/>.
    /// The arbiter resolves policies from its stored map during dispatch.
    /// </summary>
    public void Evaluate(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals)
    {
        BindIfDirty();
        proposals.SetIdle(null); // Signal "idle domain active" — map is on arbiter
    }
}
