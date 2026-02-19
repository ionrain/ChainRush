/// <summary>
/// Command dispatch abstraction.
/// </summary>
public interface ICommandBus
{
    void Publish<TCommand>(TCommand command) where TCommand : struct, ICommand;
}
