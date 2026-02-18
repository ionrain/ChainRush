/// <summary>
/// Marker interface for all orchestration commands.
/// Phase 2B hook — will be implemented by <see cref="CombatCommand"/>,
/// <c>IdleCommand</c>, and future command types.
/// <para>
/// IMPORTANT — Phase 2B MUST: formalize local command routing via ICommandBus
/// when cross-system reactions are needed.
/// </para>
/// </summary>
public interface ICommand
{
}
