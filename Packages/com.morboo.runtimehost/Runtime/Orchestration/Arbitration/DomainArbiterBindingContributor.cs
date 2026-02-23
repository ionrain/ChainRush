using UnityEngine;

/// <summary>
/// Transitional host-runtime seam for domain-provided arbiter bindings.
/// C04A step: collapse multiple domain-specific provider slots into a single
/// cached registration contributor without changing current behavior.
/// </summary>
public interface IDomainArbiterBindingContributor
{
    void ContributeArbiterBindingTargets(ref DomainArbiterBindingTargetContribution contribution);
    void ContributeArbiterBindings(ref DomainArbiterBindingContribution contribution);
}

/// <summary>
/// Generic key for arbiter binding entries contributed by domains.
/// RuntimeHost currently applies these via a local registry (`key -> binding applier`) in
/// <see cref="OrchestrationArbiter"/>.
/// </summary>
public readonly struct DomainArbiterBindingKey
{
    public readonly int Value;

    public DomainArbiterBindingKey(int value)
    {
        Value = value;
    }

    public bool IsNone => Value == 0;

    public override bool Equals(object obj)
    {
        return obj is DomainArbiterBindingKey other && other.Value == Value;
    }

    public override int GetHashCode() => Value;

    public static bool operator ==(DomainArbiterBindingKey a, DomainArbiterBindingKey b) => a.Value == b.Value;
    public static bool operator !=(DomainArbiterBindingKey a, DomainArbiterBindingKey b) => a.Value != b.Value;
}

/// <summary>
/// Transitional single binding entry carried from a domain into arbiter cached refresh path.
/// </summary>
public readonly struct DomainArbiterBindingEntry
{
    public readonly DomainArbiterBindingKey Key;
    public readonly ScriptableObject Asset;

    public DomainArbiterBindingEntry(DomainArbiterBindingKey key, ScriptableObject asset)
    {
        Key = key;
        Asset = asset;
    }
}

/// <summary>
/// Transitional binding-application target seam implemented by <see cref="OrchestrationArbiter"/>.
/// Consumer routing is generic (`TAsset`) and resolved by the target's local registry.
/// TODO(C04A): replace asset-type based routing with fully generic binding consumer descriptors.
/// </summary>
public interface IDomainArbiterBindingApplyTarget
{
    bool TryApplyArbiterBindingConsumer<TAsset>(ScriptableObject asset)
        where TAsset : ScriptableObject;
}

/// <summary>
/// Transitional binding-application delegate cached by <see cref="OrchestrationArbiter"/>
/// and invoked for binding entries resolved from domain contributors.
/// </summary>
public delegate bool DomainArbiterBindingApplyHandler(IDomainArbiterBindingApplyTarget target, ScriptableObject asset);

/// <summary>
/// Transitional key->applier registration entry contributed by domains.
/// </summary>
public readonly struct DomainArbiterBindingTargetEntry
{
    public readonly DomainArbiterBindingKey Key;
    public readonly DomainArbiterBindingApplyHandler Apply;

    public DomainArbiterBindingTargetEntry(
        DomainArbiterBindingKey key,
        DomainArbiterBindingApplyHandler apply)
    {
        Key = key;
        Apply = apply;
    }
}

/// <summary>
/// Transitional fixed registration entry used by generic runtimehost contributor helpers.
/// Owns no domain-specific typing: concrete domains provide key + applier + asset explicitly.
/// </summary>
public readonly struct DomainArbiterBindingRegistration
{
    public readonly DomainArbiterBindingKey Key;
    public readonly DomainArbiterBindingApplyHandler Apply;
    public readonly ScriptableObject Asset;

    public DomainArbiterBindingRegistration(
        DomainArbiterBindingKey key,
        DomainArbiterBindingApplyHandler apply,
        ScriptableObject asset)
    {
        Key = key;
        Apply = apply;
        Asset = asset;
    }
}

/// <summary>
/// Transitional key->applier registration payload used to build arbiter local binding registry
/// from cached domain registrations instead of hardcoded arbiter initialization.
/// </summary>
public struct DomainArbiterBindingTargetContribution
{
    DomainArbiterBindingTargetEntry[] _entries;
    int _count;

    public int Count => _count;

    public void Add(DomainArbiterBindingKey key, DomainArbiterBindingApplyHandler apply)
    {
        if (key.IsNone || apply == null)
            return;

        if (_entries == null)
            _entries = new DomainArbiterBindingTargetEntry[4];
        else if (_count >= _entries.Length)
            System.Array.Resize(ref _entries, _entries.Length * 2);

        _entries[_count++] = new DomainArbiterBindingTargetEntry(key, apply);
    }

    public DomainArbiterBindingTargetEntry GetEntry(int index)
    {
        if ((uint)index >= (uint)_count || _entries == null)
            return default;

        return _entries[index];
    }
}

/// <summary>
/// Transitional contribution payload consumed by <see cref="OrchestrationArbiter"/>
/// during policy-map refresh. Uses slot entries instead of fixed named fields to
/// reduce direct domain-shape coupling in the contributor payload itself.
/// </summary>
public struct DomainArbiterBindingContribution
{
    DomainArbiterBindingEntry[] _entries;
    int _count;

    public int Count => _count;

    public void Add(DomainArbiterBindingKey key, ScriptableObject asset)
    {
        if (key.IsNone || asset == null)
            return;

        if (_entries == null)
            _entries = new DomainArbiterBindingEntry[4];
        else if (_count >= _entries.Length)
            System.Array.Resize(ref _entries, _entries.Length * 2);

        _entries[_count++] = new DomainArbiterBindingEntry(key, asset);
    }

    public DomainArbiterBindingEntry GetEntry(int index)
    {
        if ((uint)index >= (uint)_count || _entries == null)
            return default;

        return _entries[index];
    }
}

/// <summary>
/// Transitional factory helpers for domain arbiter-binding contributors.
/// Keeps legacy StrategyCombat source-shape adaptation out of the base
/// <see cref="DomainOrchestrator"/> registration path.
/// </summary>
public static class DomainArbiterBindingContributors
{
    public static IDomainArbiterBindingContributor CreateFixedContributor(
        params DomainArbiterBindingRegistration[] registrations)
    {
        return FixedArbiterBindingContributor.Create(registrations);
    }
}

/// <summary>
/// Transitional contributor that carries direct policy map references from a domain
/// into the cached <see cref="DomainRegistration"/> so arbiter runtime loop no longer
/// depends on per-tick discovery or domain-specific provider interfaces.
/// </summary>
sealed class FixedArbiterBindingContributor : IDomainArbiterBindingContributor
{
    readonly DomainArbiterBindingRegistration[] _registrations;

    FixedArbiterBindingContributor(DomainArbiterBindingRegistration[] registrations)
    {
        _registrations = registrations;
    }

    public static IDomainArbiterBindingContributor Create(DomainArbiterBindingRegistration[] registrations)
    {
        if (registrations == null || registrations.Length == 0)
            return null;

        int count = 0;
        for (int i = 0; i < registrations.Length; i++)
        {
            if (registrations[i].Asset != null)
                count++;
        }

        if (count == 0)
        {
            return null;
        }

        return new FixedArbiterBindingContributor(registrations);
    }

    public void ContributeArbiterBindings(ref DomainArbiterBindingContribution contribution)
    {
        if (_registrations == null)
            return;

        for (int i = 0; i < _registrations.Length; i++)
            ContributeBinding(ref contribution, _registrations[i]);
    }

    public void ContributeArbiterBindingTargets(ref DomainArbiterBindingTargetContribution contribution)
    {
        if (_registrations == null)
            return;

        for (int i = 0; i < _registrations.Length; i++)
            ContributeTarget(ref contribution, _registrations[i]);
    }

    static void ContributeBinding(
        ref DomainArbiterBindingContribution contribution,
        in DomainArbiterBindingRegistration entry)
    {
        if (entry.Asset == null)
            return;

        contribution.Add(entry.Key, entry.Asset);
    }

    static void ContributeTarget(
        ref DomainArbiterBindingTargetContribution contribution,
        in DomainArbiterBindingRegistration entry)
    {
        if (entry.Asset == null)
            return;

        contribution.Add(entry.Key, entry.Apply);
    }
}
