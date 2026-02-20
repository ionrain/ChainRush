using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class Phase2KernelServiceSmokeTests
{
    [Test]
    public void GameFlow_StartFinish_TracksSessionState()
    {
        var service = new InMemoryGameFlowService();

        Assert.That(service.IsSessionActive, Is.False);
        Assert.That(service.TryStartSession("scenario.alpha"), Is.True);
        Assert.That(service.IsSessionActive, Is.True);
        Assert.That(service.TryStartSession("scenario.beta"), Is.False);

        Assert.That(service.TryFinishSession("outcome.win"), Is.True);
        Assert.That(service.IsSessionActive, Is.False);
        Assert.That(service.TryFinishSession("outcome.lose"), Is.False);
    }

    [Test]
    public void ScenarioService_SelectsOnlyAvailableScenario()
    {
        var service = new InMemoryScenarioService(new[] { "scenario.alpha", "scenario.beta" });

        Assert.That(service.TrySetScenario("scenario.alpha"), Is.True);
        Assert.That(service.ActiveScenarioId, Is.EqualTo("scenario.alpha"));
        Assert.That(service.TrySetScenario("scenario.unknown"), Is.False);
    }

    [Test]
    public void ObjectiveService_TracksActiveAndTerminalStates()
    {
        var service = new InMemoryObjectiveService();
        var objective = new ObjectiveRef("obj.a", ObjectiveScope.Encounter);

        Assert.That(service.TryActivateObjective(objective), Is.True);
        IReadOnlyCollection<ObjectiveRef> active = service.GetActiveObjectives(ObjectiveScope.Encounter);
        Assert.That(active, Does.Contain(objective));

        Assert.That(service.TryCompleteObjective(objective), Is.True);
        active = service.GetActiveObjectives(ObjectiveScope.Encounter);
        Assert.That(active.Contains(objective), Is.False);
        Assert.That(service.TryActivateObjective(objective), Is.False);
    }

    [Test]
    public void OutcomeService_SetsCurrentOutcome()
    {
        var service = new InMemoryOutcomeService();

        Assert.That(service.TrySetOutcome("outcome.win"), Is.True);
        Assert.That(service.CurrentOutcomeId, Is.EqualTo("outcome.win"));
        Assert.That(service.TrySetOutcome(""), Is.False);
    }

    [Test]
    public void RulebookProvider_ReturnsTypedRules()
    {
        var provider = new InMemoryRulebookProvider();
        provider.SetRuleValue("unit.max", 5);

        bool found = provider.TryGetRuleValue("unit.max", out int maxUnits);
        Assert.That(found, Is.True);
        Assert.That(maxUnits, Is.EqualTo(5));
        Assert.That(provider.TryGetRuleValue("unit.max", out string _), Is.False);
    }

    [Test]
    public void SaveLoadService_LoadsSavedSlotsOnly()
    {
        var service = new InMemorySaveLoadService();

        Assert.That(service.TryLoad("slot.a"), Is.False);
        service.Save("slot.a");
        Assert.That(service.TryLoad("slot.a"), Is.True);
        Assert.That(service.TryLoad("slot.b"), Is.False);
    }

    [Test]
    public void Ledger_CreditDebit_WorksWithBalanceChecks()
    {
        var ledger = new InMemoryEconomyLedger();

        ledger.Credit("soft", 50, "seed");
        Assert.That(ledger.GetBalance("soft"), Is.EqualTo(50));
        Assert.That(ledger.TryDebit("soft", 20, "buy"), Is.True);
        Assert.That(ledger.GetBalance("soft"), Is.EqualTo(30));
        Assert.That(ledger.TryDebit("soft", 100, "buy"), Is.False);
    }

    [Test]
    public void RewardService_TracksRewardsAndCreditsLedger()
    {
        var ledger = new InMemoryEconomyLedger();
        var service = new InMemoryRewardService(ledger);

        service.GrantReward("reward.daily");
        service.GrantCurrency("soft", 10, "reward.daily");

        Assert.That(service.GrantedRewardIds, Has.Count.EqualTo(1));
        Assert.That(service.GrantedRewardIds[0], Is.EqualTo("reward.daily"));
        Assert.That(ledger.GetBalance("soft"), Is.EqualTo(10));
    }
}
