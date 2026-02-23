using UnityEngine;

/// <summary>
/// Domain-owned Combat target-set provider (StrategyCombat).
/// Keeps CombatTargetSet ownership/resolution out of RuntimeHost pipeline composition.
/// </summary>
public sealed class CombatTargetProvider : DomainTargetProviderBase
{
    [Tooltip("Optional explicit CombatTargetSet owned by this combat domain.")]
    [SerializeField] CombatTargetSet targetSet;
    bool _warnedMissingTargetSet;

    public override OrchestrationDomainId DomainId => OrchestrationDomainId.Combat;
    public override bool IsConfiguredForOrchestration => targetSet != null;

    public CombatTargetSet ResolveTargetSet()
    {
        if (targetSet != null)
            return targetSet;

        if (!_warnedMissingTargetSet)
        {
            _warnedMissingTargetSet = true;
            Debug.LogError(
                "[CombatTargetProvider] Missing explicit CombatTargetSet. " +
                "Legacy auto-resolve/registry fallback was removed by architecture rule " +
                "(no parallel legacy fallback paths). Assign CombatTargetSet explicitly.",
                this);
        }

        return null;
    }

    /// <summary>
    /// Optional bridge/composition seam for explicit runtime assignment.
    /// </summary>
    public void SetExplicitTargetSet(CombatTargetSet set)
    {
        targetSet = set;
        _warnedMissingTargetSet = false;
    }
}
