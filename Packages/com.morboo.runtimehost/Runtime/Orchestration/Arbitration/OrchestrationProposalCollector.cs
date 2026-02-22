using System.Collections.Generic;

/// <summary>
/// Host-level proposal collector used by <see cref="OrchestrationArbiter"/>.
/// Collects proposal metadata entries (framework <see cref="Proposal"/>) and
/// preserves current legacy arbitration semantics via a compatibility adapter.
/// <para>
/// IMPORTANT: A single instance is reused each tick (no per-tick allocations).
/// </para>
/// </summary>
public sealed class OrchestrationProposalCollector
{
    readonly List<Proposal> _entries = new List<Proposal>(8);

    bool _hasCombat;
    CombatCommand _combatCommand;
    int _combatProposalKey;
    bool _threatPresent;

    bool _hasIdle;
    int _idleProposalKey;

    public int Count => _entries.Count;
    public Proposal Get(int index) => _entries[index];
    public IReadOnlyList<Proposal> Entries => _entries;

    public bool HasCombat => _hasCombat;
    public int CombatProposalKey => _combatProposalKey;
    public bool ThreatPresent => _threatPresent;

    public bool HasIdle => _hasIdle;
    public int IdleProposalKey => _idleProposalKey;

    public void Clear()
    {
        _entries.Clear();

        _hasCombat = false;
        _combatCommand = default;
        _combatProposalKey = OrchestrationProposalKeys.None;
        _threatPresent = false;

        _hasIdle = false;
        _idleProposalKey = OrchestrationProposalKeys.None;
    }

    /// <summary>
    /// Compatibility bridge for C03: imports current fixed combat/idle proposal container
    /// into a generic proposal collection without changing behavior.
    /// </summary>
    public void ImportLegacy(OrchestrationArbiterProposals legacy)
    {
        if (legacy == null)
            return;

        if (legacy.HasCombat)
        {
            _hasCombat = true;
            _combatCommand = legacy.CombatCommand;
            _combatProposalKey = legacy.CombatProposalKey;
            _threatPresent = legacy.ThreatPresent;

            _entries.Add(new Proposal(
                (int)OrchestrationDomainId.Combat,
                legacy.CombatProposalKey,
                priority: 100,
                score: legacy.ThreatPresent ? 1f : 0f));
        }

        if (legacy.HasIdle)
        {
            _hasIdle = true;
            _idleProposalKey = legacy.IdleProposalKey;

            _entries.Add(new Proposal(
                (int)OrchestrationDomainId.Idle,
                legacy.IdleProposalKey,
                priority: 10,
                score: 0f));
        }
    }

    public ArbitrationInput ToArbitrationInput()
    {
        return new ArbitrationInput(_hasCombat, _hasIdle, _threatPresent);
    }

    public bool TryGetCombatCommand(int proposalKey, out CombatCommand command)
    {
        if (_hasCombat && proposalKey == _combatProposalKey)
        {
            command = _combatCommand;
            return true;
        }

        command = default;
        return false;
    }
}
