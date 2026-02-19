using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry mapping <see cref="EntityId"/> to <see cref="Transform"/>.
/// IMPORTANT: This is the ONLY place EntityId→Transform resolution occurs.
/// All Integration executors use this to resolve engine-agnostic commands.
/// <para>
/// Populated by identity components (<see cref="UnitOrchestrationIdentity"/>,
/// <see cref="EnemyOrchestrationIdentity"/>) in OnEnable/OnDisable.
/// </para>
/// </summary>
public static class EntityTransformResolver
{
    static readonly Dictionary<EntityId, Transform> _map = new Dictionary<EntityId, Transform>();

    /// <summary>
    /// Registers an EntityId→Transform mapping. Skips EntityId.None.
    /// Called by identity components in OnEnable.
    /// </summary>
    public static void Register(EntityId id, Transform t)
    {
        if (!id.IsNone)
            _map[id] = t;
    }

    /// <summary>
    /// Removes an EntityId mapping. Called by identity components in OnDisable.
    /// </summary>
    public static void Unregister(EntityId id)
    {
        if (!id.IsNone)
            _map.Remove(id);
    }

    /// <summary>
    /// Attempts to resolve an EntityId to a Transform. Returns false if not found.
    /// </summary>
    public static bool TryResolve(EntityId id, out Transform t)
    {
        return _map.TryGetValue(id, out t);
    }

    /// <summary>
    /// Resolves an EntityId to a Transform, or null if not found.
    /// </summary>
    public static Transform Resolve(EntityId id)
    {
        _map.TryGetValue(id, out Transform t);
        return t;
    }
}
