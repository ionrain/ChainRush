using UnityEngine;

/// <summary>
/// Common orchestration-facing target-provider contract for orchestration domains.
/// Concrete domain providers (e.g. CombatTargetProvider, IdleTargetProvider) may expose
/// typed APIs, but converge on one shared base + interface so domain orchestrators
/// can use one structural pattern.
/// IMPORTANT: RuntimeHost owns the generic form; genre-specific providers live in StrategyCombat.
/// </summary>
public interface IDomainTargetProvider
{
    OrchestrationDomainId DomainId { get; }

    /// <summary>
    /// Minimal generic readiness signal for orchestration wiring/validation.
    /// Domain-specific providers can expose richer typed diagnostics separately.
    /// </summary>
    bool IsConfiguredForOrchestration { get; }
}

/// <summary>
/// Provider capability: resolves the domain-owned candidate carrier (<see cref="DomainTargetSet"/>).
/// Used by domains that publish reusable candidate sets (Combat now, other domains later).
/// </summary>
public interface IDomainTargetSetProvider
{
    bool TryResolveTargetSet(out DomainTargetSet targetSet);
}

/// <summary>
/// Provider capability: resolves the current operator position used for per-operator policy evaluation.
/// Used by Idle today and reusable by future non-combat domains.
/// </summary>
public interface IDomainOperatorPositionProvider
{
    bool TryResolveOperatorPosition(
        EntityId operatorEntityId,
        OrchestrationWorldCache world,
        ExecutionContext ctx,
        out Float3 position);
}

public enum DomainTargetProviderValidationFailure
{
    None = 0,
    MissingProvider = 1,
    DomainMismatch = 2,
    NotConfigured = 3,
    MissingCapability = 4,
}

public struct DomainTargetProviderValidationWarningState
{
    public bool MissingLogged;
    public bool InvalidLogged;
}

public static class DomainTargetProviderValidation
{
    public static DomainTargetProviderValidationFailure Validate(
        IDomainTargetProvider provider,
        OrchestrationDomainId expectedDomainId)
    {
        if (provider == null)
            return DomainTargetProviderValidationFailure.MissingProvider;

        if (provider.DomainId != expectedDomainId)
            return DomainTargetProviderValidationFailure.DomainMismatch;

        if (!provider.IsConfiguredForOrchestration)
            return DomainTargetProviderValidationFailure.NotConfigured;

        return DomainTargetProviderValidationFailure.None;
    }

    public static DomainTargetProviderValidationFailure Validate<TCapability>(
        IDomainTargetProvider provider,
        OrchestrationDomainId expectedDomainId,
        out TCapability capability)
        where TCapability : class
    {
        capability = null;

        DomainTargetProviderValidationFailure baseFailure = Validate(provider, expectedDomainId);
        if (baseFailure != DomainTargetProviderValidationFailure.None)
            return baseFailure;

        capability = provider as TCapability;
        return capability != null
            ? DomainTargetProviderValidationFailure.None
            : DomainTargetProviderValidationFailure.MissingCapability;
    }

    public static void LogFailureOnce(
        ref DomainTargetProviderValidationWarningState warningState,
        DomainTargetProviderValidationFailure failure,
        IDomainTargetProvider provider,
        OrchestrationDomainId expectedDomainId,
        string ownerLabel,
        string missingProviderMessage,
        UnityEngine.Object logContext,
        string requiredCapabilityLabel = null)
    {
        switch (failure)
        {
            case DomainTargetProviderValidationFailure.None:
                return;

            case DomainTargetProviderValidationFailure.MissingProvider:
                if (warningState.MissingLogged)
                    return;

                warningState.MissingLogged = true;
                Debug.LogError(missingProviderMessage, logContext);
                return;

            case DomainTargetProviderValidationFailure.DomainMismatch:
            case DomainTargetProviderValidationFailure.NotConfigured:
            case DomainTargetProviderValidationFailure.MissingCapability:
                if (warningState.InvalidLogged)
                    return;

                warningState.InvalidLogged = true;
                string actualDomain = provider != null ? provider.DomainId.ToString() : "null";
                string configured = (provider != null && provider.IsConfiguredForOrchestration) ? "true" : "false";
                string capabilitySuffix = failure == DomainTargetProviderValidationFailure.MissingCapability
                    ? string.Concat(", capability=", string.IsNullOrEmpty(requiredCapabilityLabel) ? "missing" : requiredCapabilityLabel)
                    : string.Empty;
                Debug.LogError(
                    string.Concat(
                        "[", ownerLabel, "] Invalid target-provider wiring (expected DomainId=",
                        expectedDomainId.ToString(),
                        ", actual=",
                        actualDomain,
                        ", configured=",
                        configured,
                        capabilitySuffix,
                        ")."),
                    logContext);
                return;
        }
    }
}

/// <summary>
/// Shared MonoBehaviour base for domain target providers.
/// IMPORTANT: No <c>Base</c> suffix per C04D naming rule for abstract types in upper layers.
/// </summary>
public abstract class DomainTargetProvider : MonoBehaviour, IDomainTargetProvider
{
    public abstract OrchestrationDomainId DomainId { get; }

    public virtual bool IsConfiguredForOrchestration => true;
}
