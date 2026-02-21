/// <summary>
/// Project-layer compatibility trait keys used by MorbooBridge adapters only.
/// Transitional keys are forbidden in package layers.
/// </summary>
public static class BridgeEntityStateTraitKeys
{
    public const string Kind = "entity.kind";
    public const string Archetype = "entity.archetype";
    public const string UnityInstanceId = "unity.instanceId";

    public const string UnitType = "unit.type";
    public const string UnitClass = "unit.class";
    public const string UnitIsMelee = "unit.isMelee";

    public const string EnemyType = "enemy.type";
}
