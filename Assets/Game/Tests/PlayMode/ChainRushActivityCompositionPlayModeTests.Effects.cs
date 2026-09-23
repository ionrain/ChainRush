using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Activities;
using Core.Activities.Selection;
using Core.Attributes;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Economy;
using Core.Events;
using Core.HostValues;
using Core.Runtime;
using Core.Taxonomy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Tests.PlayMode
{
    public sealed partial class ChainRushActivityCompositionPlayModeTests
    {
        [UnityTest]
        public IEnumerator HealSelection_RestoresThirtyPercentOnce_AndPreservesSurviveHeroExclusion()
        {
            SelectOnlyBoardContent("Heal");
            yield return LaunchPlayableActivities();
            Assert.IsTrue(TryFindRunningActivities(out var battle, out var board));
            var owner = battle.Participants.Single(value => value.TeamIndex == 0).ParticipantEconomyOwner;
            var wallet = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(SharedWalletTagPath);
            IssueTestTurns(owner, wallet, AssetDatabase.LoadAssetAtPath<EconomyAssetData>(BoardTurnTokenPath), 1);
            var water = AssetDatabase.LoadAssetAtPath<CapabilityHostData>("Assets/Game/Activities/Shared/Units/Water/WaterUnit.asset");
            IssueTestTurns(owner, wallet, water, 1);
            EntityId unit = EntityId.Invalid;
            for (int frame = 0; frame < 900 && !TryFindActivityHost(battle.Id, water, out unit); frame++) yield return null;
            Assert.IsTrue(unit.IsValid, "The unit was not deployed through Production.");
            var health = AssetDatabase.LoadAssetAtPath<HostValueData>("Assets/Game/Activities/Autobattle/HostValues/Health.asset");
            var maximum = AssetDatabase.LoadAssetAtPath<AttributeData>("Assets/Game/Activities/Shared/Attributes/Health.asset");
            var cell = AssetDatabase.LoadAssetAtPath<CapabilityHostData>("Assets/Game/Activities/Board/Economy/HealBoardBase.asset");
            var tag = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardCellTagPath);
            yield return AwaitCompletedPopulation(board, tag, cell);
            Assert.IsTrue(TryFindActivityHost(battle.Id, AssetDatabase.LoadAssetAtPath<CapabilityHostData>(PerfumePath), out var hero));
            Assert.IsTrue(CapabilityHostService.TryGetEffectiveAttribute(unit, new AttributeSelectorData(maximum), out long unitMaximum));
            Assert.IsTrue(CapabilityHostService.TryGetHostValue(unit, health, out var unitHealth));
            Assert.IsTrue(CapabilityHostService.TryGetHostValue(hero, health, out var heroHealth));
            Assert.Greater(unitMaximum, 0);
            var mutation = new RuntimeMutationContext(hero, hero, "test:heal-precondition", "heal-selection");
            Assert.IsTrue(CapabilityHostService.TryApplyHostValueDelta(unit, health, unitMaximum / 2 - unitHealth.CurrentValue, mutation));
            Assert.IsTrue(CapabilityHostService.TryApplyHostValueDelta(hero, health, -heroHealth.CurrentValue / 2, mutation));
            var capture = new SelectionEffectReplayCapture(board.Id, health);
            EventBus.Register<SelectionResultEvent>(capture);
            EventBus.Register<HostValueChangedEvent>(capture);
            try
            {
                IssueTestTurns(owner, wallet, AssetDatabase.LoadAssetAtPath<EconomyAssetData>(BoardTurnTokenPath), 2);
                Assert.IsTrue(TryFindBoardHost(board.Id, AssetDatabase.LoadAssetAtPath<CapabilityHostData>(BoardHostPath), out var boardHost));
                yield return AssertBoardMergeSequence(board, tag, cell, boardHost,
                    AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardMergeSelectionPath), 3, new List<string>(),
                    expectedResultNotifications: 2);
                Assert.AreEqual(1, capture.Replayed);
                Assert.AreEqual(unitMaximum * 3 / 10, capture.Increases.Where(value => value.EntityId == unit).Sum(value => value.DeltaRawValue));
                Assert.IsFalse(capture.Increases.Any(value => value.EntityId == hero), "Survive's source GateHero cannot be healed.");
            }
            finally
            {
                EventBus.Unregister<SelectionResultEvent>(capture);
                EventBus.Unregister<HostValueChangedEvent>(capture);
            }
            Assert.IsTrue(ActivityService.Close(battle.Id, ActivityCloseCauseType.Manual));
        }

        [UnityTest]
        public IEnumerator BuffSelection_UpdatesExistingAndFutureUnits_WithoutDuplicateRegistrationBonus(
            [Values("Power", "Defense", "Health", "Speed", "SkillSpeed")] string content)
        {
            SelectOnlyBoardContent(content);
            yield return LaunchPlayableActivities();
            Assert.IsTrue(TryFindRunningActivities(out var battle, out var board));
            var owner = battle.Participants.Single(value => value.TeamIndex == 0).ParticipantEconomyOwner;
            var wallet = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(SharedWalletTagPath);
            IssueTestTurns(owner, wallet, AssetDatabase.LoadAssetAtPath<EconomyAssetData>(BoardTurnTokenPath), 1);
            var water = AssetDatabase.LoadAssetAtPath<CapabilityHostData>("Assets/Game/Activities/Shared/Units/Water/WaterUnit.asset");
            string attributeName = content == "Power" || content == "Defense" ? content + "Physical" : content;
            var attribute = AssetDatabase.LoadAssetAtPath<AttributeData>("Assets/Game/Activities/Shared/Attributes/" + attributeName + ".asset");
            IssueTestTurns(owner, wallet, water, 1);
            EntityId first = EntityId.Invalid;
            for (int frame = 0; frame < 900 && !TryFindActivityHost(battle.Id, water, out first); frame++) yield return null;
            Assert.IsTrue(first.IsValid);
            var cell = AssetDatabase.LoadAssetAtPath<CapabilityHostData>("Assets/Game/Activities/Board/Economy/" + content + "BoardBase.asset");
            var tag = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardCellTagPath);
            yield return AwaitCompletedPopulation(board, tag, cell);
            CapabilityHostService.TryGetEffectiveAttribute(first, new AttributeSelectorData(attribute), out long before);
            Assert.Greater(before, 0, "The buff scenario must exercise a nonzero authored attribute.");
            // A chain of three cells issues ten percentage points.
            long expected = checked(before + before * 10L / 100L);
            var capture = new SelectionEffectReplayCapture(board.Id, null);
            EventBus.Register<SelectionResultEvent>(capture);
            try
            {
                IssueTestTurns(owner, wallet, AssetDatabase.LoadAssetAtPath<EconomyAssetData>(BoardTurnTokenPath), 2);
                Assert.IsTrue(TryFindBoardHost(board.Id, AssetDatabase.LoadAssetAtPath<CapabilityHostData>(BoardHostPath), out var host));
                yield return AssertBoardMergeSequence(board, tag, cell, host,
                    AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardMergeSelectionPath), 3, new List<string>(),
                    expectedResultNotifications: 2);
                long actual = 0;
                for (int frame = 0; frame < 120; frame++)
                {
                    CapabilityHostService.TryGetEffectiveAttribute(first, new AttributeSelectorData(attribute), out actual);
                    if (actual == expected) break;
                    yield return null;
                }
                Assert.AreEqual(expected, actual);
                Assert.AreEqual(1, capture.Replayed);
                Assert.IsTrue(CapabilityHostService.TryGet(first, out var firstHost));
                EventBus.Trigger(new CapabilityHostRegisteredEvent(firstHost));
                IssueTestTurns(owner, wallet, water, 1);
                EntityId second = EntityId.Invalid;
                for (int frame = 0; frame < 900; frame++)
                {
                    second = CapabilityHostService.GetAll().Where(value => value.ActivityId == battle.Id
                        && value.Definition.Matches(water) && value.EntityId != first).Select(value => value.EntityId).FirstOrDefault();
                    if (second.IsValid)
                    {
                        CapabilityHostService.TryGetEffectiveAttribute(second, new AttributeSelectorData(attribute), out actual);
                        if (actual == expected) break;
                    }
                    yield return null;
                }
                Assert.IsTrue(second.IsValid);
                CapabilityHostService.TryGet(second, out var secondHost);
                var unitWallet = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>("Assets/Game/Activities/Autobattle/Economy/UnitWalletTag.asset");
                Assert.AreEqual(expected, actual, "Newly deployed units must inherit the same run buff."
                    + $" first={first} second={second} owner={secondHost.Owner?.StableSimulationKey} participant={owner.StableSimulationKey}"
                    + $" wallet={QueryAmount(secondHost.SelfEconomyOwner, unitWallet, EconomyFormType.Stack, attribute)}"
                    + BuildExecutorDiagnostic(second));
                CapabilityHostService.TryGetEffectiveAttribute(first, new AttributeSelectorData(attribute), out actual);
                Assert.AreEqual(expected, actual, "Repeated registration cannot add the buff again.");
            }
            finally { EventBus.Unregister<SelectionResultEvent>(capture); }
            Assert.IsTrue(ActivityService.Close(battle.Id, ActivityCloseCauseType.Manual));
        }

        sealed class SelectionEffectReplayCapture : IEventListener<SelectionResultEvent>, IEventListener<HostValueChangedEvent>
        {
            readonly ActivityId _board;
            readonly HostValueData _health;
            public int Replayed;
            public readonly List<HostValueChangedEvent> Increases = new List<HostValueChangedEvent>();
            public SelectionEffectReplayCapture(ActivityId board, HostValueData health) { _board = board; _health = health; }
            public void OnEvent(SelectionResultEvent e)
            {
                if (e.ActivityId != _board || e.Type != SelectionResultType.Committed || Replayed != 0) return;
                Replayed++;
                EventBus.Trigger(e);
            }
            public void OnEvent(HostValueChangedEvent e)
            { if (_health != null && _health.Matches(e.Value) && e.DeltaRawValue > 0) Increases.Add(e); }
        }
    }
}
