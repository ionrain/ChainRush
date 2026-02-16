/// <summary>
/// Arbitration contract: implemented by domains that own a <see cref="CombatRoleConstraintsMapAsset"/>.
/// The arbiter pulls the map reference each tick via this interface.
/// <para>
/// IMPORTANT — Implementations must not allocate. Return the serialized field directly.
/// </para>
/// <para>
/// IMPORTANT — Domains must not mutate their <c>roleConstraintsMap</c> reference during
/// <see cref="IOrchestrationDomain.Evaluate"/>. The arbiter reads map refs after all
/// Evaluate calls complete.
/// </para>
/// </summary>
public interface ICombatRoleConstraintsMapSource
{
    CombatRoleConstraintsMapAsset GetCombatRoleConstraintsMap();
}
