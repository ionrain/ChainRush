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
    public bool ThreatPresent;

    public bool HasIdle;

    /// <summary>
    /// Resets all proposal state. Called by the arbiter at the start of each tick
    /// before polling domains. Prevents sticky flags from previous ticks.
    /// </summary>
    public void Clear()
    {
        HasCombat = false;
        CombatCommand = default;
        ThreatPresent = false;
        HasIdle = false;
    }

    /// <summary>
    /// Sets the combat proposal for this tick.
    /// IMPORTANT: Last writer wins — if multiple domains call this in the same tick,
    /// the last call (based on <c>domainOrchestrators</c> array order) takes effect.
    /// </summary>
    public void SetCombat(in CombatCommand cmd, bool threatPresent)
    {
        HasCombat = true;
        CombatCommand = cmd;
        ThreatPresent = threatPresent;
    }

    /// <summary>
    /// Signals that the idle domain is active this tick.
    /// IMPORTANT: Last writer wins — if multiple domains call this in the same tick,
    /// the last call (based on <c>domainOrchestrators</c> array order) takes effect.
    /// </summary>
    public void SetIdle()
    {
        HasIdle = true;
    }
}
