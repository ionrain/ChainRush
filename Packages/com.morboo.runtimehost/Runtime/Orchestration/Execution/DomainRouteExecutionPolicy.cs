using UnityEngine;

/// <summary>
/// Generic route-execution policy base for orchestration domains.
/// Strategy-specific implementations (e.g. StrategyCombat per-route policy assets)
/// inherit this abstract base and live in the genre layer.
/// IMPORTANT: RuntimeHost owns the abstract form only; no genre-specific fields/logic here.
/// </summary>
public abstract class DomainRouteExecutionPolicy : ScriptableObject
{
}
