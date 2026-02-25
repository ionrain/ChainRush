/// <summary>
/// Command dispatched via <see cref="ICommandBus"/> for a single orchestration receiver.
/// Contains the engine-agnostic <see cref="OrchestrationCommand"/> payload plus receiver identity.
/// <para>
/// IMPORTANT: Integration adapters resolve <see cref="ReceiverEntityId"/> to MonoBehaviour,
/// inject per-role policies/constraints when applicable, and call ApplyCommand.
/// RuntimeHost never calls Apply directly.
/// </para>
/// </summary>
public struct DispatchOrchestrationCommand : ICommand
{
    public EntityId ReceiverEntityId;
    public OrchestrationCommand Payload;
}
