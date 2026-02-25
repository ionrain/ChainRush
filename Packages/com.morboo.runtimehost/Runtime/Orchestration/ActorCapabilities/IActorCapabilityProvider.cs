/// <summary>
/// Reports per-actor capabilities to the orchestration layer.
/// Implemented by integration components (UnitActorCapabilityProvider, EnemyActorCapabilityProvider).
/// IMPORTANT: Replaces the old string-based ICapabilityProvider.
/// </summary>
public interface IActorCapabilityProvider
{
    ActorCapabilitySnapshot ReportCapabilities();
}
