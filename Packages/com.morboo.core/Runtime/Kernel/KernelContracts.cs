using System;
using System.Collections.Generic;

/// <summary>
/// Objective scope model used by kernel objective contracts.
/// </summary>
public enum ObjectiveScope
{
    Meta,
    Campaign,
    Run,
    Encounter,
    Task
}

/// <summary>
/// Stable objective reference (id + scope).
/// </summary>
[Serializable]
public readonly struct ObjectiveRef : IEquatable<ObjectiveRef>
{
    readonly string _id;
    readonly ObjectiveScope _scope;

    public ObjectiveRef(string id, ObjectiveScope scope)
    {
        _id = id ?? string.Empty;
        _scope = scope;
    }

    public string Id => _id;
    public ObjectiveScope Scope => _scope;

    public bool Equals(ObjectiveRef other) => string.Equals(_id, other._id, StringComparison.Ordinal) && _scope == other._scope;
    public override bool Equals(object obj) => obj is ObjectiveRef other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_id, (int)_scope);
    public override string ToString() => $"{_scope}:{_id}";

    public static bool operator ==(ObjectiveRef left, ObjectiveRef right) => left.Equals(right);
    public static bool operator !=(ObjectiveRef left, ObjectiveRef right) => !left.Equals(right);
}

/// <summary>
/// Controls session lifecycle transitions.
/// </summary>
public interface IGameFlowService
{
    bool IsSessionActive { get; }
    bool TryStartSession(string scenarioId);
    bool TryFinishSession(string outcomeId);
}

/// <summary>
/// Owns active scenario selection.
/// </summary>
public interface IScenarioService
{
    string ActiveScenarioId { get; }
    IReadOnlyCollection<string> GetAvailableScenarioIds();
    bool TrySetScenario(string scenarioId);
}

/// <summary>
/// Owns objectives across all scopes.
/// </summary>
public interface IObjectiveService
{
    IReadOnlyCollection<ObjectiveRef> GetActiveObjectives(ObjectiveScope scope);
    bool TryActivateObjective(ObjectiveRef objective);
    bool TryCompleteObjective(ObjectiveRef objective);
    bool TryFailObjective(ObjectiveRef objective);
}

/// <summary>
/// Owns outcome state for the current session.
/// </summary>
public interface IOutcomeService
{
    string CurrentOutcomeId { get; }
    bool TrySetOutcome(string outcomeId);
}

/// <summary>
/// Read-only policy/rules access.
/// </summary>
public interface IRulebookProvider
{
    bool TryGetRuleValue<TValue>(string ruleKey, out TValue value);
}

/// <summary>
/// Session-scoped mutable state store.
/// </summary>
public interface ISessionStateStore
{
    bool TryGet<TState>(string key, out TState value);
    void Set<TState>(string key, TState value);
    bool Remove(string key);
}

/// <summary>
/// Profile-scoped mutable state store.
/// </summary>
public interface IProfileStateStore
{
    bool TryGet<TState>(string key, out TState value);
    void Set<TState>(string key, TState value);
    bool Remove(string key);
}

/// <summary>
/// Save/load orchestration seam for game state.
/// </summary>
public interface ISaveLoadService
{
    void Save(string slotId);
    bool TryLoad(string slotId);
}

/// <summary>
/// Currency/balance ownership seam.
/// </summary>
public interface IEconomyLedger
{
    long GetBalance(string currencyId);
    void Credit(string currencyId, long amount, string reason);
    bool TryDebit(string currencyId, long amount, string reason);
}

/// <summary>
/// Reward dispatch ownership seam.
/// </summary>
public interface IRewardService
{
    void GrantReward(string rewardId);
    void GrantCurrency(string currencyId, long amount, string source);
}
