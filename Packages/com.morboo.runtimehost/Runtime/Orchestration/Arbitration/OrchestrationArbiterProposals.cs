using System.Collections.Generic;

/// <summary>
/// Shared proposal container owned by <see cref="OrchestrationArbiter"/>.
/// A single instance is reused each tick (no per-tick allocations).
/// Domains write proposals via <see cref="SetCommand"/> / <see cref="MarkDomainPresent"/>;
/// the arbiter reads them after all domains have evaluated.
/// </summary>
public sealed class OrchestrationArbiterProposals
{
    public struct DomainProposalState
    {
        public bool HasCommand;
        public OrchestrationCommand Command;
        public int ProposalKey;
        public bool ThreatPresent;
    }

    readonly Dictionary<OrchestrationDomainId, DomainProposalState> _domainStates =
        new Dictionary<OrchestrationDomainId, DomainProposalState>(4);

    public IReadOnlyDictionary<OrchestrationDomainId, DomainProposalState> DomainStates => _domainStates;

    // Compatibility view for current arbiter hysteresis semantics.
    public bool ThreatPresent
    {
        get
        {
            return TryGetDomainThreatPresent(OrchestrationDomainId.Combat, out bool threatPresent) && threatPresent;
        }
    }

    /// <summary>
    /// Resets all proposal state. Called by the arbiter at the start of each tick
    /// before polling domains. Prevents sticky flags from previous ticks.
    /// </summary>
    public void Clear()
    {
        _domainStates.Clear();
    }

    /// <summary>
    /// Sets a domain proposal with an orchestration command payload.
    /// IMPORTANT: Last writer wins per domain — if multiple domains with the same
    /// <paramref name="domainId"/> write in one tick, the last call takes effect.
    /// </summary>
    public void SetCommand(
        OrchestrationDomainId domainId,
        in OrchestrationCommand cmd,
        bool threatPresent = false,
        int proposalKey = OrchestrationProposalKeys.None)
    {
        if (domainId == OrchestrationDomainId.None)
            return;

        DomainProposalState state;
        _domainStates.TryGetValue(domainId, out state);
        state.HasCommand = true;
        state.Command = cmd;
        state.ProposalKey = NormalizeProposalKey(proposalKey);
        state.ThreatPresent = threatPresent;
        _domainStates[domainId] = state;
    }

    /// <summary>
    /// Signals that a domain is active this tick without necessarily attaching
    /// a command payload (for example idle route-driven per-operator generation).
    /// </summary>
    public void MarkDomainPresent(
        OrchestrationDomainId domainId,
        int proposalKey = OrchestrationProposalKeys.None,
        bool threatPresent = false)
    {
        if (domainId == OrchestrationDomainId.None)
            return;

        DomainProposalState state;
        _domainStates.TryGetValue(domainId, out state);
        state.ProposalKey = NormalizeProposalKey(proposalKey);
        state.ThreatPresent = threatPresent;
        _domainStates[domainId] = state;
    }

    public bool HasDomain(OrchestrationDomainId domainId)
    {
        return domainId != OrchestrationDomainId.None && _domainStates.ContainsKey(domainId);
    }

    public int GetProposalKey(OrchestrationDomainId domainId)
    {
        if (domainId == OrchestrationDomainId.None)
            return OrchestrationProposalKeys.None;

        DomainProposalState state;
        return _domainStates.TryGetValue(domainId, out state)
            ? state.ProposalKey
            : OrchestrationProposalKeys.None;
    }

    public bool TryGetDomainThreatPresent(OrchestrationDomainId domainId, out bool threatPresent)
    {
        if (domainId != OrchestrationDomainId.None &&
            _domainStates.TryGetValue(domainId, out DomainProposalState state))
        {
            threatPresent = state.ThreatPresent;
            return true;
        }

        threatPresent = false;
        return false;
    }

    public bool TryGetCommand(
        OrchestrationDomainId domainId,
        int proposalKey,
        out OrchestrationCommand command)
    {
        if (domainId != OrchestrationDomainId.None &&
            _domainStates.TryGetValue(domainId, out DomainProposalState state) &&
            state.HasCommand &&
            proposalKey == state.ProposalKey)
        {
            command = state.Command;
            return true;
        }

        command = default;
        return false;
    }

    public ArbitrationInput ToArbitrationInput()
    {
        // Compatibility mapping for the legacy two-slot arbitration input contract.
        return new ArbitrationInput(
            HasDomain(OrchestrationDomainId.Combat),
            HasDomain(OrchestrationDomainId.Idle),
            ThreatPresent);
    }

    static int NormalizeProposalKey(int proposalKey)
    {
        return proposalKey != OrchestrationProposalKeys.None
            ? proposalKey
            : OrchestrationProposalKeys.DomainDefault;
    }
}
