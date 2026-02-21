using UnityEngine;

/// <summary>
/// Read-only handle for orchestrators and executors to access an entity's
/// transform, faction, and lifecycle state without going through <see cref="StateSnapshot"/>.
/// <para>
/// IMPORTANT: This is a lightweight bridge — it does not own or drive any
/// gameplay behavior. Implementors (reporters, executors) expose data that
/// already exists on their sibling components.
/// </para>
/// <para>
/// Extends <see cref="IFactionAssetProvider"/> — implementors supply typed
/// faction via <see cref="IFactionAssetProvider.GetFactionAsset"/>.
/// </para>
/// </summary>
public interface IOrchestrationActor : IFactionAssetProvider, IActorRuntimeHandle
{
    Transform GetTransform();
}
