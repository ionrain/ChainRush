using System;
using System.Collections.Generic;

/// <summary>
/// In-memory implementation of <see cref="IGameFlowService"/>.
/// </summary>
public sealed class InMemoryGameFlowService : IGameFlowService
{
    public bool IsSessionActive { get; private set; }

    public string LastStartedScenarioId { get; private set; } = string.Empty;
    public string LastFinishedOutcomeId { get; private set; } = string.Empty;

    public bool TryStartSession(string scenarioId)
    {
        if (IsSessionActive || string.IsNullOrWhiteSpace(scenarioId))
            return false;

        LastStartedScenarioId = scenarioId;
        IsSessionActive = true;
        return true;
    }

    public bool TryFinishSession(string outcomeId)
    {
        if (!IsSessionActive || string.IsNullOrWhiteSpace(outcomeId))
            return false;

        LastFinishedOutcomeId = outcomeId;
        IsSessionActive = false;
        return true;
    }
}

/// <summary>
/// In-memory implementation of <see cref="IScenarioService"/>.
/// </summary>
public sealed class InMemoryScenarioService : IScenarioService
{
    readonly HashSet<string> _available = new HashSet<string>(StringComparer.Ordinal);

    public string ActiveScenarioId { get; private set; } = string.Empty;

    public InMemoryScenarioService()
    {
    }

    public InMemoryScenarioService(IEnumerable<string> scenarioIds)
    {
        SetAvailableScenarioIds(scenarioIds);
    }

    public IReadOnlyCollection<string> GetAvailableScenarioIds()
    {
        return new List<string>(_available);
    }

    public bool TrySetScenario(string scenarioId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
            return false;
        if (!_available.Contains(scenarioId))
            return false;

        ActiveScenarioId = scenarioId;
        return true;
    }

    public void SetAvailableScenarioIds(IEnumerable<string> scenarioIds)
    {
        _available.Clear();
        if (scenarioIds == null)
            return;

        foreach (string scenarioId in scenarioIds)
        {
            if (!string.IsNullOrWhiteSpace(scenarioId))
                _available.Add(scenarioId);
        }
    }
}

/// <summary>
/// In-memory implementation of <see cref="IObjectiveService"/>.
/// </summary>
public sealed class InMemoryObjectiveService : IObjectiveService
{
    readonly HashSet<ObjectiveRef> _active = new HashSet<ObjectiveRef>();
    readonly HashSet<ObjectiveRef> _completed = new HashSet<ObjectiveRef>();
    readonly HashSet<ObjectiveRef> _failed = new HashSet<ObjectiveRef>();

    public IReadOnlyCollection<ObjectiveRef> GetActiveObjectives(ObjectiveScope scope)
    {
        var results = new List<ObjectiveRef>();
        foreach (ObjectiveRef objective in _active)
        {
            if (objective.Scope == scope)
                results.Add(objective);
        }

        return results;
    }

    public bool TryActivateObjective(ObjectiveRef objective)
    {
        if (string.IsNullOrWhiteSpace(objective.Id))
            return false;
        if (_completed.Contains(objective) || _failed.Contains(objective))
            return false;

        return _active.Add(objective);
    }

    public bool TryCompleteObjective(ObjectiveRef objective)
    {
        if (!_active.Remove(objective))
            return false;

        _completed.Add(objective);
        return true;
    }

    public bool TryFailObjective(ObjectiveRef objective)
    {
        if (!_active.Remove(objective))
            return false;

        _failed.Add(objective);
        return true;
    }
}

/// <summary>
/// In-memory implementation of <see cref="IOutcomeService"/>.
/// </summary>
public sealed class InMemoryOutcomeService : IOutcomeService
{
    public string CurrentOutcomeId { get; private set; } = string.Empty;

    public bool TrySetOutcome(string outcomeId)
    {
        if (string.IsNullOrWhiteSpace(outcomeId))
            return false;

        CurrentOutcomeId = outcomeId;
        return true;
    }
}

/// <summary>
/// In-memory implementation of <see cref="IRulebookProvider"/>.
/// </summary>
public sealed class InMemoryRulebookProvider : IRulebookProvider
{
    readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);

    public bool TryGetRuleValue<TValue>(string ruleKey, out TValue value)
    {
        if (_values.TryGetValue(ruleKey, out object raw) && raw is TValue typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public void SetRuleValue<TValue>(string ruleKey, TValue value)
    {
        _values[ruleKey] = value;
    }
}

/// <summary>
/// In-memory implementation of <see cref="ISaveLoadService"/>.
/// </summary>
public sealed class InMemorySaveLoadService : ISaveLoadService
{
    readonly HashSet<string> _savedSlots = new HashSet<string>(StringComparer.Ordinal);

    public string LastSavedSlotId { get; private set; } = string.Empty;
    public string LastLoadedSlotId { get; private set; } = string.Empty;

    public void Save(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return;

        _savedSlots.Add(slotId);
        LastSavedSlotId = slotId;
    }

    public bool TryLoad(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return false;
        if (!_savedSlots.Contains(slotId))
            return false;

        LastLoadedSlotId = slotId;
        return true;
    }
}

/// <summary>
/// In-memory implementation of <see cref="IEconomyLedger"/>.
/// </summary>
public sealed class InMemoryEconomyLedger : IEconomyLedger
{
    readonly Dictionary<string, long> _balances = new Dictionary<string, long>(StringComparer.Ordinal);

    public long GetBalance(string currencyId)
    {
        return _balances.TryGetValue(currencyId, out long value) ? value : 0L;
    }

    public void Credit(string currencyId, long amount, string reason)
    {
        if (string.IsNullOrWhiteSpace(currencyId) || amount <= 0L)
            return;

        long balance = GetBalance(currencyId);
        _balances[currencyId] = balance + amount;
    }

    public bool TryDebit(string currencyId, long amount, string reason)
    {
        if (string.IsNullOrWhiteSpace(currencyId) || amount <= 0L)
            return false;

        long balance = GetBalance(currencyId);
        if (balance < amount)
            return false;

        _balances[currencyId] = balance - amount;
        return true;
    }
}

/// <summary>
/// In-memory implementation of <see cref="IRewardService"/>.
/// </summary>
public sealed class InMemoryRewardService : IRewardService
{
    readonly IEconomyLedger _ledger;
    readonly List<string> _grantedRewardIds = new List<string>();

    public InMemoryRewardService(IEconomyLedger ledger = null)
    {
        _ledger = ledger;
    }

    public IReadOnlyList<string> GrantedRewardIds => _grantedRewardIds;

    public void GrantReward(string rewardId)
    {
        if (string.IsNullOrWhiteSpace(rewardId))
            return;

        _grantedRewardIds.Add(rewardId);
    }

    public void GrantCurrency(string currencyId, long amount, string source)
    {
        _ledger?.Credit(currencyId, amount, source);
    }
}
