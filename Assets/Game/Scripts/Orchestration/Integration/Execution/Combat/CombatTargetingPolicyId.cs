/// <summary>
/// Well-known targeting policy identifiers carried in <see cref="CombatCommand.DebugLabel"/>
/// using the format <c>"Policy={id}"</c>.
/// <para>
/// RATIONALE: CombatCommand has a fixed 8-field layout with no generic param channel.
/// Policy tags are embedded in DebugLabel as a lightweight forward-compat mechanism
/// until a dedicated param field is added.
/// </para>
/// </summary>
public static class CombatTargetingPolicyId
{
    /// <summary>Use the orchestrator-chosen primary target as-is.</summary>
    public const string PrimaryTarget = "PrimaryTarget";

    /// <summary>Hold position — no target.</summary>
    public const string Hold = "Hold";

    /// <summary>Pick the closest hostile from the Top-K candidate set to this unit's position.</summary>
    public const string NearestToSelf = "NearestToSelf";

    // Future policies (not yet implemented):
    // public const string WeakestHp01InRange = "WeakestHp01InRange";
    // public const string TauntOverride = "TauntOverride";
    // public const string NearestToAnchor = "NearestToAnchor";
}
