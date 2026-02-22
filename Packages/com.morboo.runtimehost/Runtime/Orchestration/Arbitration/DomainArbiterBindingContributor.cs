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
/// Generic consumer key for arbiter binding application targets.
/// RuntimeHost owns current consumer-slot identities; domains only reference them through
/// the apply-target seam.
/// </summary>
public readonly struct DomainArbiterBindingConsumerKey
{
    public readonly int Value;

    public DomainArbiterBindingConsumerKey(int value)
    {
        Value = value;
    }

    public bool IsNone => Value == 0;

    public override bool Equals(object obj)
    {
        return obj is DomainArbiterBindingConsumerKey other && other.Value == Value;
    }

    public override int GetHashCode() => Value;

    public static bool operator ==(DomainArbiterBindingConsumerKey a, DomainArbiterBindingConsumerKey b) => a.Value == b.Value;
    public static bool operator !=(DomainArbiterBindingConsumerKey a, DomainArbiterBindingConsumerKey b) => a.Value != b.Value;
}

/// <summary>
/// Transitional built-in RuntimeHost consumer-slot keys for current arbiter policy-map fields.
/// TODO(C04A): replace consumer-specific slots with more generic binding consumers.
/// </summary>
public static class RuntimeHostArbiterBindingConsumerKeys
{
    public static readonly DomainArbiterBindingConsumerKey None = new DomainArbiterBindingConsumerKey(0);
    public static readonly DomainArbiterBindingConsumerKey IdleRolePolicyMap = new DomainArbiterBindingConsumerKey(1);
    public static readonly DomainArbiterBindingConsumerKey CombatRolePolicyMap = new DomainArbiterBindingConsumerKey(2);
    public static readonly DomainArbiterBindingConsumerKey CombatRoleConstraintsMap = new DomainArbiterBindingConsumerKey(3);
}

/// <summary>
/// Transitional binding-application target seam implemented by <see cref="OrchestrationArbiter"/>.
/// TODO(C04A): replace built-in consumer keys with more generic binding consumers.
/// </summary>
public interface IDomainArbiterBindingApplyTarget
{
    bool TryApplyArbiterBindingConsumer(DomainArbiterBindingConsumerKey consumerKey, ScriptableObject asset);
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
/// Transitional key->applier registration payload used to build arbiter local binding registry
/// from cached domain registrations instead of hardcoded arbiter initialization.
/// </summary>
public struct DomainArbiterBindingTargetContribution
{
    DomainArbiterBindingTargetEntry _entry0;
    DomainArbiterBindingTargetEntry _entry1;
    DomainArbiterBindingTargetEntry _entry2;
    int _count;

    public int Count => _count;

    public void Add(DomainArbiterBindingKey key, DomainArbiterBindingApplyHandler apply)
    {
        if (key.IsNone || apply == null)
            return;

        var entry = new DomainArbiterBindingTargetEntry(key, apply);
        switch (_count)
        {
            case 0:
                _entry0 = entry;
                _count = 1;
                return;
            case 1:
                _entry1 = entry;
                _count = 2;
                return;
            case 2:
                _entry2 = entry;
                _count = 3;
                return;
            default:
                return;
        }
    }

    public DomainArbiterBindingTargetEntry GetEntry(int index)
    {
        return index switch
        {
            0 => _entry0,
            1 => _entry1,
            2 => _entry2,
            _ => default
        };
    }
}

/// <summary>
/// Transitional contribution payload consumed by <see cref="OrchestrationArbiter"/>
/// during policy-map refresh. Uses slot entries instead of fixed named fields to
/// reduce direct domain-shape coupling in the contributor payload itself.
/// </summary>
public struct DomainArbiterBindingContribution
{
    DomainArbiterBindingEntry _entry0;
    DomainArbiterBindingEntry _entry1;
    DomainArbiterBindingEntry _entry2;
    int _count;

    public int Count => _count;

    public void Add(DomainArbiterBindingKey key, ScriptableObject asset)
    {
        if (key.IsNone || asset == null)
            return;

        var entry = new DomainArbiterBindingEntry(key, asset);
        switch (_count)
        {
            case 0:
                _entry0 = entry;
                _count = 1;
                return;
            case 1:
                _entry1 = entry;
                _count = 2;
                return;
            case 2:
                _entry2 = entry;
                _count = 3;
                return;
            default:
                // Transitional payload currently expects max 3 entries.
                return;
        }
    }

    public DomainArbiterBindingEntry GetEntry(int index)
    {
        return index switch
        {
            0 => _entry0,
            1 => _entry1,
            2 => _entry2,
            _ => default
        };
    }
}

/// <summary>
/// Transitional factory helpers for domain arbiter-binding contributors.
/// Keeps legacy StrategyCombat source-shape adaptation out of the base
/// <see cref="DomainOrchestrator"/> registration path.
/// </summary>
public static class DomainArbiterBindingContributors
{
    public static IDomainArbiterBindingContributor CreatePolicyMapContributor(
        DomainArbiterBindingKey idleRolePolicyMapKey,
        DomainArbiterBindingApplyHandler idleRolePolicyMapApply,
        IdleRolePolicyMapAsset idleRolePolicyMap,
        DomainArbiterBindingKey combatRolePolicyMapKey,
        DomainArbiterBindingApplyHandler combatRolePolicyMapApply,
        CombatRolePolicyMapAsset combatRolePolicyMap,
        DomainArbiterBindingKey combatRoleConstraintsMapKey,
        DomainArbiterBindingApplyHandler combatRoleConstraintsMapApply,
        CombatRoleConstraintsMapAsset combatRoleConstraintsMap)
    {
        return PolicyMapArbiterBindingContributor.Create(
            idleRolePolicyMapKey,
            idleRolePolicyMapApply,
            idleRolePolicyMap,
            combatRolePolicyMapKey,
            combatRolePolicyMapApply,
            combatRolePolicyMap,
            combatRoleConstraintsMapKey,
            combatRoleConstraintsMapApply,
            combatRoleConstraintsMap);
    }
}

/// <summary>
/// Transitional contributor that carries direct policy map references from a domain
/// into the cached <see cref="DomainRegistration"/> so arbiter runtime loop no longer
/// depends on per-tick discovery or domain-specific provider interfaces.
/// </summary>
sealed class PolicyMapArbiterBindingContributor : IDomainArbiterBindingContributor
{
    readonly DomainArbiterBindingKey _idleRolePolicyMapKey;
    readonly DomainArbiterBindingApplyHandler _idleRolePolicyMapApply;
    readonly IdleRolePolicyMapAsset _idleRolePolicyMap;
    readonly DomainArbiterBindingKey _combatRolePolicyMapKey;
    readonly DomainArbiterBindingApplyHandler _combatRolePolicyMapApply;
    readonly CombatRolePolicyMapAsset _combatRolePolicyMap;
    readonly DomainArbiterBindingKey _combatRoleConstraintsMapKey;
    readonly DomainArbiterBindingApplyHandler _combatRoleConstraintsMapApply;
    readonly CombatRoleConstraintsMapAsset _combatRoleConstraintsMap;

    PolicyMapArbiterBindingContributor(
        DomainArbiterBindingKey idleRolePolicyMapKey,
        DomainArbiterBindingApplyHandler idleRolePolicyMapApply,
        IdleRolePolicyMapAsset idleRolePolicyMap,
        DomainArbiterBindingKey combatRolePolicyMapKey,
        DomainArbiterBindingApplyHandler combatRolePolicyMapApply,
        CombatRolePolicyMapAsset combatRolePolicyMap,
        DomainArbiterBindingKey combatRoleConstraintsMapKey,
        DomainArbiterBindingApplyHandler combatRoleConstraintsMapApply,
        CombatRoleConstraintsMapAsset combatRoleConstraintsMap)
    {
        _idleRolePolicyMapKey = idleRolePolicyMapKey;
        _idleRolePolicyMapApply = idleRolePolicyMapApply;
        _idleRolePolicyMap = idleRolePolicyMap;
        _combatRolePolicyMapKey = combatRolePolicyMapKey;
        _combatRolePolicyMapApply = combatRolePolicyMapApply;
        _combatRolePolicyMap = combatRolePolicyMap;
        _combatRoleConstraintsMapKey = combatRoleConstraintsMapKey;
        _combatRoleConstraintsMapApply = combatRoleConstraintsMapApply;
        _combatRoleConstraintsMap = combatRoleConstraintsMap;
    }

    public static IDomainArbiterBindingContributor Create(
        DomainArbiterBindingKey idleRolePolicyMapKey,
        DomainArbiterBindingApplyHandler idleRolePolicyMapApply,
        IdleRolePolicyMapAsset idleRolePolicyMap,
        DomainArbiterBindingKey combatRolePolicyMapKey,
        DomainArbiterBindingApplyHandler combatRolePolicyMapApply,
        CombatRolePolicyMapAsset combatRolePolicyMap,
        DomainArbiterBindingKey combatRoleConstraintsMapKey,
        DomainArbiterBindingApplyHandler combatRoleConstraintsMapApply,
        CombatRoleConstraintsMapAsset combatRoleConstraintsMap)
    {
        if (idleRolePolicyMap == null &&
            combatRolePolicyMap == null &&
            combatRoleConstraintsMap == null)
        {
            return null;
        }

        return new PolicyMapArbiterBindingContributor(
            idleRolePolicyMapKey,
            idleRolePolicyMapApply,
            idleRolePolicyMap,
            combatRolePolicyMapKey,
            combatRolePolicyMapApply,
            combatRolePolicyMap,
            combatRoleConstraintsMapKey,
            combatRoleConstraintsMapApply,
            combatRoleConstraintsMap);
    }

    public void ContributeArbiterBindings(ref DomainArbiterBindingContribution contribution)
    {
        if (_idleRolePolicyMap != null)
            contribution.Add(_idleRolePolicyMapKey, _idleRolePolicyMap);

        if (_combatRolePolicyMap != null)
            contribution.Add(_combatRolePolicyMapKey, _combatRolePolicyMap);

        if (_combatRoleConstraintsMap != null)
            contribution.Add(_combatRoleConstraintsMapKey, _combatRoleConstraintsMap);
    }

    public void ContributeArbiterBindingTargets(ref DomainArbiterBindingTargetContribution contribution)
    {
        if (_idleRolePolicyMap != null)
            contribution.Add(
                _idleRolePolicyMapKey,
                _idleRolePolicyMapApply);

        if (_combatRolePolicyMap != null)
            contribution.Add(
                _combatRolePolicyMapKey,
                _combatRolePolicyMapApply);

        if (_combatRoleConstraintsMap != null)
            contribution.Add(
                _combatRoleConstraintsMapKey,
                _combatRoleConstraintsMapApply);
    }
}
