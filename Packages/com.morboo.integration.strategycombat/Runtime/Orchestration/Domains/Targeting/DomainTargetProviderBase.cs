using UnityEngine;

/// <summary>
/// Common orchestration-facing target-provider contract for StrategyCombat domains.
/// C04C bootstrap: concrete domain providers may expose typed APIs, but should converge
/// on one shared base + interface so domain orchestrators can use one structural pattern.
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

public enum DomainTargetProviderValidationFailure
{
    None = 0,
    MissingProvider = 1,
    DomainMismatch = 2,
    NotConfigured = 3,
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

    public static void LogFailureOnce(
        ref DomainTargetProviderValidationWarningState warningState,
        DomainTargetProviderValidationFailure failure,
        IDomainTargetProvider provider,
        OrchestrationDomainId expectedDomainId,
        string ownerLabel,
        string missingProviderMessage,
        UnityEngine.Object logContext)
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
                if (warningState.InvalidLogged)
                    return;

                warningState.InvalidLogged = true;
                string actualDomain = provider != null ? provider.DomainId.ToString() : "null";
                string configured = (provider != null && provider.IsConfiguredForOrchestration) ? "true" : "false";
                Debug.LogError(
                    string.Concat(
                        "[", ownerLabel, "] Invalid target-provider wiring (expected DomainId=",
                        expectedDomainId.ToString(),
                        ", actual=",
                        actualDomain,
                        ", configured=",
                        configured,
                        ")."),
                    logContext);
                return;
        }
    }
}

/// <summary>
/// Shared MonoBehaviour base for domain target providers in StrategyCombat.
/// </summary>
public abstract class DomainTargetProviderBase : MonoBehaviour, IDomainTargetProvider
{
    public abstract OrchestrationDomainId DomainId { get; }

    public virtual bool IsConfiguredForOrchestration => true;
}
