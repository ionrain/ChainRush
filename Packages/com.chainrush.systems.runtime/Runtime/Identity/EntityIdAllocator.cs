using System.Threading;

/// <summary>
/// Monotonically-increasing allocator for <see cref="EntityId"/>.
/// Process-lifetime: no Reset() method. Collisions across play sessions are irrelevant.
/// <para>
/// IMPORTANT — Only identity components should call <see cref="Create"/>,
/// and only once in <c>Awake()</c>. The returned EntityId must be stored
/// and reused across enable/disable cycles (pool-safe).
/// </para>
/// </summary>
public static class EntityIdAllocator
{
    static int _counter;

    /// <summary>
    /// Creates a new unique <see cref="EntityId"/>. Thread-safe via Interlocked.
    /// </summary>
    public static EntityId Create()
    {
        int id = Interlocked.Increment(ref _counter);
        return new EntityId(id);
    }
}
