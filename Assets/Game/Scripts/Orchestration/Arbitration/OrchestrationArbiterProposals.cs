/// <summary>
/// Shared proposal container owned by <see cref="OrchestrationArbiter"/>.
/// A single instance is reused each tick (no per-tick allocations).
/// Domains write proposals via <see cref="SetCombat"/> / <see cref="SetIdle"/>;
/// the arbiter reads them after all domains have evaluated.
/// <para>
/// IMPORTANT: <see cref="IdlePolicies"/> is a reference to an
/// <see cref="IdleRolePolicySet"/> owned by the idle domain.
/// The arbiter must not store this reference across ticks;
/// <see cref="Clear"/> nulls it each tick.
/// </para>
/// </summary>
public sealed class OrchestrationArbiterProposals
{
    public bool HasCombat;
    public CombatCommand CombatCommand;
    public bool ThreatPresent;

    public bool HasIdle;
    public IdleRolePolicySet IdlePolicies;

    /// <summary>
    /// Resets all proposal state. Called by the arbiter at the start of each tick
    /// before polling domains. Prevents sticky references from previous ticks.
    /// </summary>
    public void Clear()
    {
        HasCombat = false;
        CombatCommand = default;
        ThreatPresent = false;
        HasIdle = false;
        IdlePolicies = null;
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
    /// Sets the idle proposal for this tick.
    /// IMPORTANT: Last writer wins — if multiple domains call this in the same tick,
    /// the last call (based on <c>domainOrchestrators</c> array order) takes effect.
    /// The <paramref name="policySet"/> reference is borrowed for this tick only.
    /// </summary>
    public void SetIdle(IdleRolePolicySet policySet)
    {
        HasIdle = true;
        IdlePolicies = policySet;
    }
}
