/// <summary>
/// Arbitration contract: implemented by domains that own an <see cref="IdleRolePolicyMapAsset"/>.
/// The arbiter pulls the map reference each tick via this interface.
/// <para>
/// IMPORTANT — Implementations must not allocate. Return the serialized field directly.
/// </para>
/// <para>
/// IMPORTANT — Domains must not mutate their <c>rolePolicyMap</c> reference during
/// <see cref="IOrchestrationDomain.Evaluate"/>. The arbiter reads map refs after all
/// Evaluate calls complete.
/// </para>
/// </summary>
public interface IIdleRolePolicyMapSource
{
    IdleRolePolicyMapAsset GetIdleRolePolicyMap();
}
