using System;
using System.Collections.Generic;

/// <summary>
/// Minimal mutable entity model for kernel/runtime ownership.
/// Keeps identity, archetype, liveness, tags/traits/capabilities seams.
/// </summary>
[Serializable]
public sealed class EntityState : IEntityStateAccessor
{
    readonly HashSet<string> _tags = new HashSet<string>(StringComparer.Ordinal);
    readonly HashSet<string> _capabilities = new HashSet<string>(StringComparer.Ordinal);
    readonly Dictionary<string, string> _traits = new Dictionary<string, string>(StringComparer.Ordinal);

    public EntityState(EntityId entityId, EntityArchetypeId archetypeId, bool isAlive = true, IEnumerable<string> tags = null)
    {
        EntityId = entityId;
        ArchetypeId = archetypeId;
        IsAlive = isAlive;

        if (tags != null)
        {
            foreach (string tag in tags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                    _tags.Add(tag);
            }
        }
    }

    public EntityId EntityId { get; }
    public EntityArchetypeId ArchetypeId { get; }
    public bool IsAlive { get; private set; }
    public IReadOnlyCollection<string> Tags => _tags;
    public IReadOnlyCollection<string> Capabilities => _capabilities;
    public IReadOnlyDictionary<string, string> Traits => _traits;

    public void SetAlive(bool isAlive)
    {
        IsAlive = isAlive;
    }

    public bool AddTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        return _tags.Add(tag);
    }

    public bool RemoveTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        return _tags.Remove(tag);
    }

    public bool AddCapability(string capabilityId)
    {
        if (string.IsNullOrWhiteSpace(capabilityId))
            return false;

        return _capabilities.Add(capabilityId);
    }

    public bool RemoveCapability(string capabilityId)
    {
        if (string.IsNullOrWhiteSpace(capabilityId))
            return false;

        return _capabilities.Remove(capabilityId);
    }

    public void SetTrait(string traitKey, string traitValue)
    {
        if (string.IsNullOrWhiteSpace(traitKey))
            return;

        _traits[traitKey] = traitValue ?? string.Empty;
    }

    public bool TryGetTrait(string traitKey, out string traitValue)
    {
        if (string.IsNullOrWhiteSpace(traitKey))
        {
            traitValue = string.Empty;
            return false;
        }

        return _traits.TryGetValue(traitKey, out traitValue);
    }
}
