/// <summary>
/// Per-tick context filled by <see cref="OrchestrationArbiter"/> and passed to
/// <see cref="IOrchestrationDomain.Evaluate"/>. Domains should treat this as read-only.
/// </summary>
public struct OrchestrationArbiterContext
{
    public FactionAsset OrchestratorFaction;
    public FactionRelationTableAsset Relations;
    public Float3 Anchor;
    public float Now;
    public bool DebugLog;
    public IWorldQuery World;
    public IActorCapabilityQuery ActorCapabilities;
}
