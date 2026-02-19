/// <summary>
/// Command dispatched via <see cref="ICommandBus"/> for a single combat receiver.
/// Contains the engine-agnostic <see cref="CombatCommand"/> payload plus receiver identity.
/// <para>
/// IMPORTANT: Integration adapters resolve <see cref="ReceiverEntityId"/> to MonoBehaviour,
/// inject per-role policies/constraints, and call ApplyCombatCommand. RuntimeHost never
/// calls Apply directly.
/// </para>
/// </summary>
public struct DispatchCombatCommand : ICommand
{
    public EntityId ReceiverEntityId;
    public CombatCommand Payload;
    public RoleId ReceiverRoleId;
}
