using UnityEngine;

/// <summary>
/// Host-runtime composition seam for domain onboarding.
/// Domain/integration modules can configure the orchestration pipeline from a
/// single touchpoint without editing host runtime entrypoints directly.
/// C04A bootstrap: all hooks are optional; built-in RuntimeHost behavior remains default.
/// </summary>
public abstract class OrchestrationDomainModule : MonoBehaviour
{
    /// <summary>
    /// Optional hook to configure loop-level integration concerns.
    /// </summary>
    public virtual void ConfigureLoop(OrchestrationLoop loop)
    {
    }

    /// <summary>
    /// Optional hook to configure arbiter-side domain onboarding.
    /// </summary>
    public virtual void ConfigureArbiter(OrchestrationArbiter arbiter)
    {
    }

    /// <summary>
    /// Optional hook to register/override router routes.
    /// </summary>
    public virtual void ConfigureRouter(ExecutionRouter router)
    {
    }
}
