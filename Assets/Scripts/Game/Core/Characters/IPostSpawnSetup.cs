/// <summary>
/// Interface for components that need explicit setup after spawn.
/// Called by managers after entity initialization is complete (e.g., after Unit.Setup).
/// </summary>
public interface IPostSpawnSetup
{
    void Apply();
}
