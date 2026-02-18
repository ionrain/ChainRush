using System;
using UnityEngine;

/// <summary>
/// Typed-only faction snapshot. Null <see cref="Faction"/> means unknown/unconfigured.
/// </summary>
[Serializable]
public struct StateSnapshot
{
    public FactionAsset Faction;
    public EntityId EntityId;
    public Vector2 Position;
    public bool IsAlive;
    public string RoleTag;
    public ParamSet Metrics;

    public static StateSnapshot Create(
        FactionAsset faction,
        EntityId entityId,
        Vector2 position,
        bool isAlive,
        string roleTag = null,
        ParamSet metrics = default)
    {
        return new StateSnapshot
        {
            Faction = faction,
            EntityId = entityId,
            Position = position,
            IsAlive = isAlive,
            RoleTag = roleTag,
            Metrics = metrics
        };
    }

    public bool TryGetMetricFloat(string key, out float value)
    {
        return Metrics.TryGetFloat(key, out value);
    }
}
