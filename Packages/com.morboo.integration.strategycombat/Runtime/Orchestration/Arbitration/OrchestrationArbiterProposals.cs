/// <summary>
/// Shared proposal container owned by <see cref="OrchestrationArbiter"/>.
/// A single instance is reused each tick (no per-tick allocations).
/// Domains write proposals via <see cref="SetCombat"/> / <see cref="SetIdle"/>;
/// the arbiter reads them after all domains have evaluated.
/// </summary>
public sealed class OrchestrationArbiterProposals
{
    public bool HasCombat;
    public CombatCommand CombatCommand;
    public int CombatProposalKey;
    public bool ThreatPresent;

    public bool HasIdle;
    public int IdleProposalKey;

    /// <summary>
    /// Resets all proposal state. Called by the arbiter at the start of each tick
    /// before polling domains. Prevents sticky flags from previous ticks.
    /// </summary>
    public void Clear()
    {
        HasCombat = false;
        CombatCommand = default;
        CombatProposalKey = OrchestrationProposalKeys.None;
        ThreatPresent = false;
        HasIdle = false;
        IdleProposalKey = OrchestrationProposalKeys.None;
    }

    /// <summary>
    /// Sets the combat proposal for this tick.
    /// IMPORTANT: Last writer wins — if multiple domains call this in the same tick,
    /// the last call (based on <c>domainOrchestrators</c> array order) takes effect.
    /// </summary>
    public void SetCombat(
        in CombatCommand cmd,
        bool threatPresent,
        int proposalKey = OrchestrationProposalKeys.CombatPrimary)
    {
        HasCombat = true;
        CombatCommand = cmd;
        CombatProposalKey = proposalKey;
        ThreatPresent = threatPresent;
    }

    /// <summary>
    /// Signals that the idle domain is active this tick.
    /// IMPORTANT: Last writer wins — if multiple domains call this in the same tick,
    /// the last call (based on <c>domainOrchestrators</c> array order) takes effect.
    /// </summary>
    public void SetIdle(int proposalKey = OrchestrationProposalKeys.IdleDefault)
    {
        HasIdle = true;
        IdleProposalKey = proposalKey;
    }

    public ArbitrationInput ToArbitrationInput()
    {
        return new ArbitrationInput(HasCombat, HasIdle, ThreatPresent);
    }
}
