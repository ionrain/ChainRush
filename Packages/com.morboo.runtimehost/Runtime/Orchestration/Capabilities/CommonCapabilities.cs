public static class CommonCapabilities
{
    // Movement
    public static readonly CapabilityId Walk = CapabilityId.Of("Move.Walk");
    public static readonly CapabilityId Run = CapabilityId.Of("Move.Run");
    public static readonly CapabilityId Swim = CapabilityId.Of("Move.Swim");
    public static readonly CapabilityId Fly = CapabilityId.Of("Move.Fly");

    // Combat
    public static readonly CapabilityId Melee = CapabilityId.Of("Combat.Melee");
    public static readonly CapabilityId Ranged = CapabilityId.Of("Combat.Ranged");
    public static readonly CapabilityId Block = CapabilityId.Of("Combat.Block");
    public static readonly CapabilityId Dodge = CapabilityId.Of("Combat.Dodge");
    public static readonly CapabilityId StoneSkin = CapabilityId.Of("Combat.StoneSkin");

    // Economy
    public static readonly CapabilityId Sow = CapabilityId.Of("Econ.Sow");
    public static readonly CapabilityId Harvest = CapabilityId.Of("Econ.Harvest");
    public static readonly CapabilityId Herd = CapabilityId.Of("Econ.Herd");
}
