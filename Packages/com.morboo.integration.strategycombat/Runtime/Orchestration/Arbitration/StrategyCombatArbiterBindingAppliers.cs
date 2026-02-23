using UnityEngine;

/// <summary>
/// StrategyCombat-owned binding appliers for current policy-map consumer slots.
/// RuntimeHost exposes only the generic apply-target interface/delegate seam.
/// </summary>
public static class StrategyCombatArbiterBindingAppliers
{
    public static readonly DomainArbiterBindingApplyHandler IdleRolePolicyMap = ApplyIdleRolePolicyMap;
    public static readonly DomainArbiterBindingApplyHandler CombatRolePolicyMap = ApplyCombatRolePolicyMap;
    public static readonly DomainArbiterBindingApplyHandler CombatRoleConstraintsMap = ApplyCombatRoleConstraintsMap;

    static bool ApplyIdleRolePolicyMap(IDomainArbiterBindingApplyTarget target, ScriptableObject asset)
    {
        return target != null && target.TryApplyArbiterBindingConsumer<IdleRolePolicyMapAsset>(asset);
    }

    static bool ApplyCombatRolePolicyMap(IDomainArbiterBindingApplyTarget target, ScriptableObject asset)
    {
        return target != null && target.TryApplyArbiterBindingConsumer<CombatRolePolicyMapAsset>(asset);
    }

    static bool ApplyCombatRoleConstraintsMap(IDomainArbiterBindingApplyTarget target, ScriptableObject asset)
    {
        return target != null && target.TryApplyArbiterBindingConsumer<CombatRoleConstraintsMapAsset>(asset);
    }
}
