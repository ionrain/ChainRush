/// <summary>
/// Runtime-facing structured view over <see cref="StrategyCombatRouteExecutionPolicyAsset"/>.
/// Transitional C04A facade: groups route-execution settings and isolates route executors
/// from the serialized policy asset layout.
/// </summary>
public readonly struct StrategyCombatRouteExecutionProfile
{
    readonly StrategyCombatRouteExecutionPolicyAsset _asset;

    public StrategyCombatRouteExecutionProfile(StrategyCombatRouteExecutionPolicyAsset asset)
    {
        _asset = asset;
    }

    public CombatRouteSettings Combat => new CombatRouteSettings(_asset);
    public IdleRouteSettings Idle => new IdleRouteSettings(_asset);
    public NoneRouteSettings NoneRoute => new NoneRouteSettings(_asset);
    public UnknownRouteFallbackSettings UnknownRouteFallback => new UnknownRouteFallbackSettings(_asset);

    public readonly struct CombatRouteSettings
    {
        readonly StrategyCombatRouteExecutionPolicyAsset _asset;
        public CombatRouteSettings(StrategyCombatRouteExecutionPolicyAsset asset) => _asset = asset;
        public bool EmitIdleHoldOnModeChange => _asset == null || _asset.Combat.EmitIdleHoldOnModeChange;
    }

    public readonly struct IdleRouteSettings
    {
        readonly StrategyCombatRouteExecutionPolicyAsset _asset;
        public IdleRouteSettings(StrategyCombatRouteExecutionPolicyAsset asset) => _asset = asset;

        public bool EmitCombatHoldOnModeChange => _asset == null || _asset.Idle.EmitCombatHoldOnModeChange;
        public string CombatHoldDebugLabelOverride => _asset == null ? null : _asset.Idle.CombatHoldDebugLabelOverride;
        public string NoRoleMatchDebugLabelOverride => _asset == null ? null : _asset.Idle.NoRoleMatchDebugLabelOverride;
        public bool WarnWhenRolePolicyMapMissing => _asset == null || _asset.Idle.WarnWhenRolePolicyMapMissing;
        public bool WarnWhenRoleMissing => _asset == null || _asset.Idle.WarnWhenRoleMissing;
        public bool LogPolicyDecisionTraceWhenDebugLog => _asset == null || _asset.Idle.LogPolicyDecisionTraceWhenDebugLog;
        public bool LogNoRoleMatchTraceWhenDebugLog => _asset == null || _asset.Idle.LogNoRoleMatchTraceWhenDebugLog;
    }

    public readonly struct NoneRouteSettings
    {
        readonly StrategyCombatRouteExecutionPolicyAsset _asset;
        public NoneRouteSettings(StrategyCombatRouteExecutionPolicyAsset asset) => _asset = asset;

        public bool EmitCombatHoldOnModeChange => _asset == null || _asset.NoneRoute.EmitCombatHoldOnModeChange;
        public bool EmitIdleHoldOnModeChange => _asset == null || _asset.NoneRoute.EmitIdleHoldOnModeChange;
        public string CombatHoldDebugLabelOverride => _asset == null ? null : _asset.NoneRoute.CombatHoldDebugLabelOverride;
    }

    public readonly struct UnknownRouteFallbackSettings
    {
        readonly StrategyCombatRouteExecutionPolicyAsset _asset;
        public UnknownRouteFallbackSettings(StrategyCombatRouteExecutionPolicyAsset asset) => _asset = asset;

        public bool EmitCombatHoldOnModeChange => _asset == null || _asset.UnknownRouteFallback.EmitCombatHoldOnModeChange;
        public bool EmitIdleHoldOnModeChange => _asset == null || _asset.UnknownRouteFallback.EmitIdleHoldOnModeChange;
        public string CombatHoldDebugLabelOverride => _asset == null ? null : _asset.UnknownRouteFallback.CombatHoldDebugLabelOverride;
    }
}
