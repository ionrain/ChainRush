using System.Collections.Generic;

/// <summary>
/// Host-level proposal collector used by <see cref="OrchestrationArbiter"/>.
/// Collects proposal metadata entries (framework <see cref="Proposal"/>) and
/// preserves current arbitration semantics while domain producers migrate to
/// unified <see cref="OrchestrationCommand"/> payloads.
/// <para>
/// IMPORTANT: A single instance is reused each tick (no per-tick allocations).
/// </para>
/// </summary>
public sealed class OrchestrationProposalCollector
{
    struct CollectedDomainState
    {
        public int ProposalKey;
        public bool HasCommand;
        public OrchestrationCommand Command;
        public bool ThreatPresent;
    }

    readonly List<Proposal> _entries = new List<Proposal>(8);
    readonly Dictionary<OrchestrationDomainId, CollectedDomainState> _domainStates =
        new Dictionary<OrchestrationDomainId, CollectedDomainState>(4);
    bool _anyThreatPresent;

    public int Count => _entries.Count;
    public Proposal Get(int index) => _entries[index];
    public IReadOnlyList<Proposal> Entries => _entries;

    public bool ThreatPresent => _anyThreatPresent;

    public void Clear()
    {
        _entries.Clear();
        _domainStates.Clear();
        _anyThreatPresent = false;
    }

    /// <summary>
    /// Imports current domain proposal scratch data into the collector without
    /// changing arbitration behavior.
    /// </summary>
    public void Import(OrchestrationArbiterProposals proposals)
    {
        if (proposals == null)
            return;

        foreach (KeyValuePair<OrchestrationDomainId, OrchestrationArbiterProposals.DomainProposalState> kvp in proposals.DomainStates)
        {
            OrchestrationDomainId domainId = kvp.Key;
            OrchestrationArbiterProposals.DomainProposalState src = kvp.Value;

            CollectedDomainState dst = new CollectedDomainState
            {
                ProposalKey = src.ProposalKey,
                HasCommand = src.HasCommand,
                Command = src.Command,
                ThreatPresent = src.ThreatPresent
            };
            _domainStates[domainId] = dst;
            _anyThreatPresent |= src.ThreatPresent;

            _entries.Add(new Proposal(
                (int)domainId,
                src.ProposalKey,
                priority: GetDefaultPriority(domainId),
                score: GetDefaultScore(domainId, src.ThreatPresent)));
        }
    }

    public ArbitrationInput ToArbitrationInput()
    {
        // Compatibility mapping for the legacy two-slot arbitration input contract.
        return new ArbitrationInput(
            HasDomain(OrchestrationDomainId.Combat),
            HasDomain(OrchestrationDomainId.Idle),
            _anyThreatPresent);
    }

    public bool HasDomain(OrchestrationDomainId domainId)
    {
        return domainId != OrchestrationDomainId.None && _domainStates.ContainsKey(domainId);
    }

    public int GetProposalKey(OrchestrationDomainId domainId)
    {
        if (domainId == OrchestrationDomainId.None)
            return OrchestrationProposalKeys.None;

        CollectedDomainState state;
        return _domainStates.TryGetValue(domainId, out state)
            ? state.ProposalKey
            : OrchestrationProposalKeys.None;
    }

    public bool TryGetCommand(OrchestrationDomainId domainId, int proposalKey, out OrchestrationCommand command)
    {
        if (domainId != OrchestrationDomainId.None &&
            _domainStates.TryGetValue(domainId, out CollectedDomainState state) &&
            state.HasCommand &&
            proposalKey == state.ProposalKey)
        {
            command = state.Command;
            return true;
        }

        command = default;
        return false;
    }

    static int GetDefaultPriority(OrchestrationDomainId domainId)
    {
        switch (domainId)
        {
            case OrchestrationDomainId.Combat:
                return 100;
            case OrchestrationDomainId.Idle:
                return 10;
            default:
                return 0;
        }
    }

    static float GetDefaultScore(OrchestrationDomainId domainId, bool threatPresent)
    {
        switch (domainId)
        {
            case OrchestrationDomainId.Combat:
                return threatPresent ? 1f : 0f;
            default:
                return 0f;
        }
    }
}
