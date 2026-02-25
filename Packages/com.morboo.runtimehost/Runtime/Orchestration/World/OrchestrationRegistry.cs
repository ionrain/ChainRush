using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight static registry for orchestration reporters and providers.
/// Populated via OnEnable/OnDisable hooks on integration components.
/// IMPORTANT: This is read-only aggregation only — no gameplay ownership, no Update loop.
/// PERF: Linear Contains is acceptable given small N. Safe for pooled objects
/// calling OnEnable multiple times (idempotent).
/// </summary>
public static class OrchestrationRegistry
{
    static readonly List<IStateReporter> _stateReporters = new List<IStateReporter>(256);
    static readonly List<IActorCapabilityProvider> _capabilityProviders = new List<IActorCapabilityProvider>(256);
    static readonly List<IOrchestrationCommandReceiver> _commandReceivers = new List<IOrchestrationCommandReceiver>(128);

    public static IReadOnlyList<IStateReporter> StateReporters => _stateReporters;
    public static IReadOnlyList<IActorCapabilityProvider> CapabilityProviders => _capabilityProviders;
    public static IReadOnlyList<IOrchestrationCommandReceiver> CommandReceivers => _commandReceivers;

    /// <summary>Returns the underlying mutable list for internal query iteration.</summary>
    internal static List<IStateReporter> StateReportersList => _stateReporters;

    public static void Register(IStateReporter reporter)
    {
        if (reporter == null) return;
        if (!_stateReporters.Contains(reporter))
            _stateReporters.Add(reporter);
    }

    public static void Unregister(IStateReporter reporter)
    {
        if (reporter == null) return;
        _stateReporters.Remove(reporter);
    }

    public static void Register(IActorCapabilityProvider provider)
    {
        if (provider == null) return;
        if (!_capabilityProviders.Contains(provider))
            _capabilityProviders.Add(provider);
    }

    public static void Unregister(IActorCapabilityProvider provider)
    {
        if (provider == null) return;
        _capabilityProviders.Remove(provider);
    }

    public static void Register(IOrchestrationCommandReceiver receiver)
    {
        if (receiver == null) return;

        for (int i = _commandReceivers.Count - 1; i >= 0; i--)
        {
            IOrchestrationCommandReceiver existing = _commandReceivers[i];
            if (existing is Object obj && obj == null)
            {
                _commandReceivers.RemoveAt(i);
                continue;
            }
            if (ReferenceEquals(existing, receiver))
                return;
        }

        _commandReceivers.Add(receiver);
    }

    public static void Unregister(IOrchestrationCommandReceiver receiver)
    {
        if (receiver == null) return;

        for (int i = _commandReceivers.Count - 1; i >= 0; i--)
        {
            IOrchestrationCommandReceiver existing = _commandReceivers[i];
            if (ReferenceEquals(existing, receiver))
            {
                _commandReceivers.RemoveAt(i);
                continue;
            }
            if (existing is Object obj && obj == null)
                _commandReceivers.RemoveAt(i);
        }
    }
}
