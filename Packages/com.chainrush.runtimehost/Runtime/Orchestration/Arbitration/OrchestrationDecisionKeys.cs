/// <summary>
/// RuntimeHost domain key constants used by payload-agnostic framework decisions.
/// </summary>
public static class OrchestrationDomainKeys
{
    public const int None = 0;
    public const int Combat = 1;
    public const int Idle = 2;
}

/// <summary>
/// RuntimeHost proposal key constants.
/// </summary>
public static class OrchestrationProposalKeys
{
    public const int None = 0;
    public const int CombatPrimary = 1;
    public const int IdleDefault = 1;
}
