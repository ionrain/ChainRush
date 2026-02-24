using UnityEngine;

/// <summary>
/// Project-level composition helper for applying a shared route-execution policy.
/// Applies a shared <see cref="DomainRouteExecutionPolicy"/> to scene domain orchestrators
/// before <see cref="OrchestrationLoop"/> builds route registrations.
/// IMPORTANT — Uses generic <see cref="IDomainRouteExecutionPolicyConsumer"/> contract.
/// Genre-specific policy assets inherit <see cref="DomainRouteExecutionPolicy"/>.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class DomainRouteExecutionPolicyBridge : MonoBehaviour
{
    [Header("Route Policy")]
    [Tooltip("Shared route-execution policy applied to configured domains.")]
    [SerializeField] DomainRouteExecutionPolicy routeExecutionPolicy;

    [Header("Source Of Truth")]
    [Tooltip("OrchestrationLoop that owns the ordered domain composition for this scene.")]
    [SerializeField] OrchestrationLoop orchestrationLoop;

    [Header("Behavior")]
    [Tooltip("Apply route policy automatically during Awake (should run before OrchestrationLoop Awake).")]
    [SerializeField] bool applyOnAwake = true;
    [Tooltip("Warn once if loop/configured domains contain null/unsupported entries.")]
    [SerializeField] bool warnOnNullEntries = true;

    bool _warnedNullEntries;
    bool _warnedMissingLoop;

    void Awake()
    {
        if (applyOnAwake)
            ApplyRouteExecutionPolicy();
    }

    [ContextMenu("Apply Route Policy")]
    public void ApplyRouteExecutionPolicy()
    {
        if (orchestrationLoop == null)
        {
            WarnMissingLoopOnce();
            return;
        }

        var configuredDomains = orchestrationLoop.ConfiguredDomainOrchestrators;
        if (configuredDomains == null || configuredDomains.Count == 0)
            return;

        for (int i = 0; i < configuredDomains.Count; i++)
        {
            DomainOrchestrator domain = configuredDomains[i];
            if (domain is UnityEngine.Object uo && uo == null)
                domain = null;

            if (domain == null)
            {
                WarnNullEntryOnce("orchestrationLoop.ConfiguredDomainOrchestrators", i);
                continue;
            }

            if (domain is IDomainRouteExecutionPolicyConsumer policyConsumer)
            {
                policyConsumer.ApplyRouteExecutionPolicy(routeExecutionPolicy);
                continue;
            }
        }
    }

    void WarnNullEntryOnce(string arrayName, int index)
    {
        if (!warnOnNullEntries || _warnedNullEntries)
            return;

        _warnedNullEntries = true;
        Debug.LogWarning(string.Concat(
            "[DomainRouteExecutionPolicyBridge] ",
            arrayName, "[", index.ToString(), "] is null. Skipping."), this);
    }

    void WarnMissingLoopOnce()
    {
        if (_warnedMissingLoop)
            return;

        _warnedMissingLoop = true;
        Debug.LogWarning(
            "[DomainRouteExecutionPolicyBridge] orchestrationLoop is not assigned. " +
            "Route policy was not applied.", this);
    }
}
