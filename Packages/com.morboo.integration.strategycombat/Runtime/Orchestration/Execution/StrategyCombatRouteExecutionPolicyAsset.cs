using UnityEngine;

/// <summary>
/// Optional StrategyCombat route-execution policy overrides.
/// Transitional C04A seam toward data/policy-driven route execution.
/// Defaults preserve current behavior.
/// </summary>
[CreateAssetMenu(
    fileName = "StrategyCombatRouteExecutionPolicy",
    menuName = "Morboo/Orchestration/StrategyCombat Route Execution Policy")]
public sealed class StrategyCombatRouteExecutionPolicyAsset : ScriptableObject
{
    [System.Serializable]
    public sealed class CombatRoutePolicySection
    {
        [Tooltip("When Combat route becomes active on mode change, emit Idle hold-all dispatches.")]
        [SerializeField] bool emitIdleHoldOnModeChange = true;

        public bool EmitIdleHoldOnModeChange => emitIdleHoldOnModeChange;
    }

    [System.Serializable]
    public sealed class IdleRoutePolicySection
    {
        [Tooltip("When Idle route becomes active on mode change, emit Combat hold-all dispatches.")]
        [SerializeField] bool emitCombatHoldOnModeChange = true;

        [Tooltip("Optional override for the Combat hold debug label emitted by Idle route on mode change. Empty keeps legacy label.")]
        [SerializeField] string combatHoldDebugLabelOverride;

        [Tooltip("Optional override for the Idle hold debug label used when no role policy match is found. Empty keeps legacy label.")]
        [SerializeField] string noRoleMatchDebugLabelOverride;

        [Tooltip("When true, log warning once if Idle route is active but no idle role policy map is bound.")]
        [SerializeField] bool warnWhenRolePolicyMapMissing = true;

        [Tooltip("When true, log warning once if idle receiver has no RoleId.")]
        [SerializeField] bool warnWhenRoleMissing = true;

        [Tooltip("When true, emit per-policy debug trace logs from Idle route when ctx.DebugLog is enabled.")]
        [SerializeField] bool logPolicyDecisionTraceWhenDebugLog = true;

        [Tooltip("When true, emit no-role-match debug trace logs from Idle route when ctx.DebugLog is enabled.")]
        [SerializeField] bool logNoRoleMatchTraceWhenDebugLog = true;

        public bool EmitCombatHoldOnModeChange => emitCombatHoldOnModeChange;
        public string CombatHoldDebugLabelOverride => combatHoldDebugLabelOverride;
        public string NoRoleMatchDebugLabelOverride => noRoleMatchDebugLabelOverride;
        public bool WarnWhenRolePolicyMapMissing => warnWhenRolePolicyMapMissing;
        public bool WarnWhenRoleMissing => warnWhenRoleMissing;
        public bool LogPolicyDecisionTraceWhenDebugLog => logPolicyDecisionTraceWhenDebugLog;
        public bool LogNoRoleMatchTraceWhenDebugLog => logNoRoleMatchTraceWhenDebugLog;
    }

    [System.Serializable]
    public sealed class NoneRoutePolicySection
    {
        [Tooltip("When None route executes on mode change, emit Combat hold-all dispatches.")]
        [SerializeField] bool emitCombatHoldOnModeChange = true;

        [Tooltip("When None route executes on mode change, emit Idle hold-all dispatches.")]
        [SerializeField] bool emitIdleHoldOnModeChange = true;

        [Tooltip("Optional override for the Combat hold debug label emitted by None route on mode change. Empty keeps legacy label.")]
        [SerializeField] string combatHoldDebugLabelOverride;

        public bool EmitCombatHoldOnModeChange => emitCombatHoldOnModeChange;
        public bool EmitIdleHoldOnModeChange => emitIdleHoldOnModeChange;
        public string CombatHoldDebugLabelOverride => combatHoldDebugLabelOverride;
    }

    [System.Serializable]
    public sealed class UnknownRouteFallbackPolicySection
    {
        [Tooltip("When unknown-route fallback executes on mode change, emit Combat hold-all dispatches.")]
        [SerializeField] bool emitCombatHoldOnModeChange = true;

        [Tooltip("When unknown-route fallback executes on mode change, emit Idle hold-all dispatches.")]
        [SerializeField] bool emitIdleHoldOnModeChange = true;

        [Tooltip("Optional override for the Combat hold debug label emitted by unknown-route fallback on mode change. Empty keeps legacy label.")]
        [SerializeField] string combatHoldDebugLabelOverride;

        public bool EmitCombatHoldOnModeChange => emitCombatHoldOnModeChange;
        public bool EmitIdleHoldOnModeChange => emitIdleHoldOnModeChange;
        public string CombatHoldDebugLabelOverride => combatHoldDebugLabelOverride;
    }

    public StrategyCombatRouteExecutionProfile Profile => new StrategyCombatRouteExecutionProfile(this);

    [Header("Combat Route")]
    [SerializeField] CombatRoutePolicySection combat = new CombatRoutePolicySection();

    [Header("Idle Route")]
    [SerializeField] IdleRoutePolicySection idle = new IdleRoutePolicySection();

    [Header("None Route")]
    [SerializeField] NoneRoutePolicySection noneRoute = new NoneRoutePolicySection();

    [Header("Unknown Route Fallback")]
    [SerializeField] UnknownRouteFallbackPolicySection unknownRouteFallback = new UnknownRouteFallbackPolicySection();

    internal CombatRoutePolicySection Combat => combat ?? (combat = new CombatRoutePolicySection());
    internal IdleRoutePolicySection Idle => idle ?? (idle = new IdleRoutePolicySection());
    internal NoneRoutePolicySection NoneRoute => noneRoute ?? (noneRoute = new NoneRoutePolicySection());
    internal UnknownRouteFallbackPolicySection UnknownRouteFallback
        => unknownRouteFallback ?? (unknownRouteFallback = new UnknownRouteFallbackPolicySection());
}
