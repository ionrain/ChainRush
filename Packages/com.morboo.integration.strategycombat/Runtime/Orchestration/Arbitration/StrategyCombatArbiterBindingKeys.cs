/// <summary>
/// StrategyCombat-owned arbiter binding keys contributed by domain orchestrators.
/// RuntimeHost remains generic and only consumes <see cref="DomainArbiterBindingKey"/>
/// via the contributor/registry seam.
/// </summary>
public static class StrategyCombatArbiterBindingKeys
{
    public static readonly DomainArbiterBindingKey IdleRolePolicyMap = new DomainArbiterBindingKey(1);
    public static readonly DomainArbiterBindingKey CombatRolePolicyMap = new DomainArbiterBindingKey(2);
    public static readonly DomainArbiterBindingKey CombatRoleConstraintsMap = new DomainArbiterBindingKey(3);
}
