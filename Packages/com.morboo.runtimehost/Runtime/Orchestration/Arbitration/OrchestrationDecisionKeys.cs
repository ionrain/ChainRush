/// <summary>
/// RuntimeHost compatibility wrapper over the canonical orchestration domain enum in Core.
/// Framework decision contracts still use int DomainKey, so RuntimeHost exposes int constants
/// at that boundary while the source-of-truth domain list lives in Core.
/// </summary>
public static class OrchestrationDomainKeys
{
    public const int None = (int)OrchestrationDomainId.None;
    public const int Combat = (int)OrchestrationDomainId.Combat;
    public const int Idle = (int)OrchestrationDomainId.Idle;
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
