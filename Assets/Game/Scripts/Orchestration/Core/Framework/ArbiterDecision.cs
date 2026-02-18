/// <summary>
/// Identifies which domain the arbiter has selected as active.
/// </summary>
public enum ActiveDomainKind
{
    /// <summary>No domain is active. All receivers hold.</summary>
    None = 0,

    /// <summary>Combat domain is active. Dispatch combat command to receivers.</summary>
    Combat = 1,

    /// <summary>Idle domain is active. Compute and dispatch per-unit idle commands.</summary>
    Idle = 2
}

/// <summary>
/// Pure arbitration output produced by <see cref="IArbiter.Arbitrate"/>.
/// Contains no dispatch logic — the router consumes this to drive execution.
/// <para>
/// IMPORTANT — The arbiter only selects which domain is active. Dispatch is
/// the router's responsibility (Phase 2A Commit 4).
/// </para>
/// </summary>
public struct ArbiterDecision
{
    /// <summary>
    /// Which domain the arbiter selected as active this tick.
    /// </summary>
    public ActiveDomainKind ActiveDomain;

    /// <summary>
    /// The combat command to dispatch. Valid only when <see cref="ActiveDomain"/> is
    /// <see cref="ActiveDomainKind.Combat"/>.
    /// </summary>
    public CombatCommand CombatCommand;

    /// <summary>
    /// True when the active domain changed since last tick.
    /// The router uses this to inject cross-domain Hold commands.
    /// IMPORTANT: Router handles cross-domain Hold injection, not the arbiter.
    /// </summary>
    public bool ModeChanged;
}
