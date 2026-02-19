/// <summary>
/// Command dispatched via <see cref="ICommandBus"/> for a single idle receiver.
/// Contains the engine-agnostic <see cref="IdleCommand"/> payload plus receiver identity.
/// <para>
/// IMPORTANT: Integration adapters resolve <see cref="ReceiverEntityId"/> to MonoBehaviour,
/// handle per-unit selector overrides, and call ApplyIdleCommand. RuntimeHost never
/// calls Apply directly.
/// </para>
/// </summary>
public struct DispatchIdleCommand : ICommand
{
    public EntityId ReceiverEntityId;
    public IdleCommand Payload;
    public RoleId ReceiverRoleId;
}
