using UnityEngine;

/// <summary>
/// Read-only handle for orchestrators and executors to access an entity's
/// transform, faction, and liveness without going through <see cref="StateSnapshot"/>.
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
public interface IOrchestrationActor : IFactionAssetProvider
{
    Transform GetTransform();

    /// <summary>
    /// Quick liveness check. May mirror the logic used by the entity's
    /// <see cref="IStateReporter.ReportState"/> implementation.
    /// </summary>
    bool IsAlive();
}
