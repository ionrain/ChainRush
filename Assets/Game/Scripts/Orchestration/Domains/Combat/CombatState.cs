using System;
using UnityEngine;

[Serializable]
public struct CombatState
{
    public FactionAsset Faction;
    public int EntityId;
    public Vector2 Position;
    public float Hp01;
    public float PowerScore;
    public float PressureScore;
    public int EngagedEnemyCount;
    public bool IsAlive;
    public string RoleTag;

    public static CombatState Create(
        FactionAsset faction,
        int entityId,
        Vector2 position,
        bool isAlive,
        float hp01 = -1f,
        float powerScore = 1f,
        float pressureScore = 0f,
        int engagedEnemyCount = 0,
        string roleTag = null)
    {
        return new CombatState
        {
            Faction = faction,
            EntityId = entityId,
            Position = position,
            Hp01 = hp01,
            PowerScore = powerScore,
            PressureScore = pressureScore,
            EngagedEnemyCount = engagedEnemyCount,
            IsAlive = isAlive,
            RoleTag = roleTag
        };
    }

    public bool IsHpKnown => Hp01 >= 0f;
    public bool IsUnderPressure => PressureScore > 0.5f;
}
