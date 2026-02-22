public enum ControlLevel
{
    IntentOnly,
    Guided,
    Direct
}

public enum DomainId
{
    Generic,
    Combat,
    Economy
}

/// <summary>
/// Canonical orchestration domain identifiers shared across RuntimeHost/Integration/Bridge.
/// Kept in Core so RuntimeHost is not the owner of the domain list definition.
/// NOTE: Framework decision contracts still use int DomainKey for compatibility, so
/// RuntimeHost/Bridge cast this enum at the Framework boundary.
/// </summary>
public enum OrchestrationDomainId
{
    None = 0,
    Combat = 1,
    Idle = 2
}
