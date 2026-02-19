/// <summary>
/// Immutable per-tick timing metadata passed to tick subscribers.
/// </summary>
public readonly struct TickContext
{
    public readonly float Now;
    public readonly float DeltaTime;
    public readonly int TickIndex;

    public TickContext(float now, float deltaTime, int tickIndex)
    {
        Now = now;
        DeltaTime = deltaTime;
        TickIndex = tickIndex;
    }
}
