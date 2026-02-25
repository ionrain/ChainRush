using UnityEngine;

/// <summary>
/// Domain-owned combat target-set provider (StrategyCombat).
/// Keeps target-set ownership/resolution out of RuntimeHost pipeline composition.
/// </summary>
public sealed class CombatTargetProvider : DomainTargetProvider
{
    [Tooltip("Optional explicit DomainTargetSet owned by this combat domain (CombatTargetSet or another domain-target carrier component).")]
    [SerializeField] DomainTargetSet targetSet;
    bool _warnedMissingTargetSet;

    public override OrchestrationDomainId DomainId => OrchestrationDomainId.Combat;
    public override bool IsConfiguredForOrchestration => targetSet != null;

    public DomainTargetSet ResolveTargetSet()
    {
        if (targetSet != null)
            return targetSet;

        if (!_warnedMissingTargetSet)
        {
            _warnedMissingTargetSet = true;
            Debug.LogError(
                "[CombatTargetProvider] Missing explicit DomainTargetSet. " +
                "Legacy auto-resolve/registry fallback was removed by architecture rule " +
                "(no parallel legacy fallback paths). Assign a target-set carrier explicitly.",
                this);
        }

        return null;
    }

    /// <summary>
    /// Optional bridge/composition seam for explicit runtime assignment.
    /// </summary>
    public void SetExplicitTargetSet(DomainTargetSet set)
    {
        targetSet = set;
        _warnedMissingTargetSet = false;
    }
}
