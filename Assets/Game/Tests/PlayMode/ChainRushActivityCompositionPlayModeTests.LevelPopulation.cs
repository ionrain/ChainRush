using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using Core;
using Core.Activities;
using Core.Activities.Analytics;
using Core.Production;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Economy;
using Core.Events;
using Core.HostValues;
using Core.Objectives;
using Core.Orchestration;
using Core.Runtime;
using Core.Taxonomy;
using Core.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChainRush.Tests.PlayMode
{
    public sealed partial class ChainRushActivityCompositionPlayModeTests
    {
        static SpaceRegionSnapshot ReadEnemyRegion(ActivityRuntimeSnapshot battle)
        {
            var regions = new System.Collections.Generic.List<SpaceRegionSnapshot>();
            Assert.AreEqual(SpaceRegionQueryResultType.Ready, SpaceRegionService.Collect(
                new SpaceRegionResolvedQuery(battle.Id, default, default, default, default), regions));
            return regions.Single(region => region.RegionId.Value == "chainrush.autobattle.enemies");
        }

        [UnityTest]
        public IEnumerator LevelPopulation_ReplenishesAfterDeaths_ChangesComposition_StopsAtEnd()
        {
            var materializationWarnings = new System.Collections.Generic.List<string>();
            void CaptureWarning(string message, string stack, LogType type)
            {
                if (message.Contains("Materialized marker lease could not be committed")) materializationWarnings.Add(message);
            }
            Application.logMessageReceived += CaptureWarning;
            try
            {
                yield return LaunchPlayableActivities("Distance");
                Assert.IsTrue(TryFindRunningActivities(out var battle, out _));
                var player = battle.Participants.Single(participant => participant.TeamIndex == 0).ParticipantEconomyOwner;
                var enemyOwner = battle.Participants.Single(participant => participant.TeamIndex == 1).ParticipantEconomyOwner;
                var health = AssetDatabase.LoadAssetAtPath<HostValueData>(
                    "Assets/Game/Activities/Autobattle/HostValues/Health.asset");
                var progress = AssetDatabase.LoadAssetAtPath<EconomyAssetData>(
                    "Assets/Game/Activities/Shared/Economy/LevelProgress.asset");
                var wallet = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(SharedWalletTagPath);
                var activity = AssetDatabase.LoadAssetAtPath<ActivityData>(
                    "Assets/Game/Activities/Autobattle/Definition/DistanceActivity.asset");
                var target = activity.Teams[1].Objectives.Single(value => value.Template.name == "Level02ReplenishmentObjective").Template.Root.SuccessConditions
                    .OfType<ObjectiveConditionComposite>().Single().NestedConditions
                    .OfType<ObjectiveConditionMaterializedEntity>().Single().TargetProgression;
                var curve = (LongProgressionData)typeof(ObjectiveLongTargetProgressionData)
                    .GetField("progression", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
                var heroDefinition = AssetDatabase.LoadAssetAtPath<CapabilityHostData>(PerfumePath);
                Assert.IsTrue(TryFindActivityHost(battle.Id, heroDefinition, out var hero));
                Assert.IsTrue(SpatialService.TryGetPose(hero, out var origin));
                // Observe replenishment with controlled deaths; combat has separate integration scenarios.
                var heroSkills = CapabilityHostService.GetSkillSnapshots(hero);
                Assert.IsNotEmpty(heroSkills);
                foreach (var skill in heroSkills)
                    Assert.IsTrue(CapabilityHostService.TrySetSkillEnabled(hero, skill.Id, false));
                var enemies = new System.Collections.Generic.List<CapabilityHostSnapshot>();
                void ReadEnemies()
                {
                    enemies.Clear();
                    enemies.AddRange(CapabilityHostService.GetAll().Where(host => host.ActivityId == battle.Id
                        && host.Owner.StableSimulationKey == enemyOwner.StableSimulationKey
                        && host.Definition.Tags.Any(tag => tag.Id == "chainrush.autobattle.role.enemy-unit")));
                }
                IEnumerator WaitForCount(int expected)
                {
                    float deadline = Time.realtimeSinceStartup + CollectorCycleTimeoutSeconds;
                    do
                    {
                        ReadEnemies();
                        if (enemies.Count == expected) break;
                        yield return null;
                    } while (Time.realtimeSinceStartup < deadline);
                    var diagnostic = new StringBuilder();
                    AppendProcessDiagnostic(diagnostic, battle.DomainId);
                    if (enemies.Count != expected)
                    {
                        foreach (var enemy in enemies)
                        {
                            SpatialService.TryGetPose(enemy.EntityId, out var pose);
                            diagnostic.AppendLine($"Enemy {enemy.EntityId} {enemy.Definition.Id} at {pose.Coordinates}");
                        }
                        var records = (System.Collections.IDictionary)typeof(AgentService)
                            .GetField("PopulationExecutions", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                        foreach (var record in records.Values)
                        {
                            object Read(string name) => record.GetType().GetField(name)?.GetValue(record);
                            if (!Equals(Read("ActivityId"), battle.Id)) continue;
                            diagnostic.AppendLine($"Population state={Read("State")} requested={Read("RequestedCount")} "
                                + $"planned={Read("PlannedCount")} materialized={Read("MaterializedCount")} "
                                + $"closed={Read("AdmissionClosed")} failure={Read("Failure")}");
                            var goal = Read("EntityGoal");
                            diagnostic.AppendLine("Target=" + goal?.GetType().GetProperty("TargetValue")?.GetValue(goal));
                            var readCount = record.GetType().GetMethod("ReadCount");
                            var countArguments = new object[] { 0L, false, null };
                            diagnostic.AppendLine("Count state=" + readCount.Invoke(record, countArguments)
                                + " missing=" + countArguments[0] + " satisfied=" + countArguments[1]
                                + " failure=" + countArguments[2]);
                        }
                    }
                    Assert.AreEqual(expected, enemies.Count, diagnostic.ToString());
                }
                void Defeat(CapabilityHostSnapshot enemy)
                {
                    Assert.IsTrue(HostValueService.TryApplyHostValueDelta(enemy.EntityId, health, -1000000,
                        new RuntimeMutationContext(hero, hero, "level-replenishment-test", enemy.EntityId.ToString())));
                }
                yield return WaitForCount(4);
                var defeated = enemies[0].EntityId;
                Defeat(enemies[0]);
                float deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
                while (CapabilityHostService.Exists(defeated) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsFalse(CapabilityHostService.Exists(defeated), "Defeated enemy did not complete death cleanup.");
                yield return WaitForCount(4);
                Assert.IsFalse(enemies.Any(enemy => enemy.EntityId == defeated));

                foreach (long value in new long[] { 300000, 550000, 800000, 1000000 })
                {
                    Assert.IsTrue(TopologyService.TryResolvePosition(battle.Id,
                        origin.Coordinates + new Vector3(150f * value / 1000000, 0, 0), out var position, out _));
                    Assert.IsTrue(SpatialService.TrySetPose(hero, position, origin.Rotation, out _));
                    deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
                    while (QueryAmount(player, wallet, EconomyFormType.Stack, progress) != value
                        && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.AreEqual(value, QueryAmount(player, wallet, EconomyFormType.Stack, progress));
                    // Let already accepted work settle before checking the next source composition.
                    yield return new WaitForSeconds(.5f);
                    ReadEnemies();
                    var previous = enemies.Select(enemy => enemy.EntityId).ToList();
                    foreach (var enemy in enemies) Defeat(enemy);
                    deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
                    while (previous.Any(CapabilityHostService.Exists) && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.IsFalse(previous.Any(CapabilityHostService.Exists));
                    Assert.IsTrue(curve.TryEvaluate(new ProgressionContext(value), out long count, out string failure), failure);
                    yield return WaitForCount(value == 1000000 ? 0 : (int)count);
                    if (value == 1000000)
                    {
                        yield return new WaitForSeconds(1);
                        ReadEnemies();
                        Assert.IsEmpty(enemies, "Full progress must stop new replenishment assignments.");
                        var fieldTemplate = AssetDatabase.LoadAssetAtPath<ObjectiveTemplateData>(
                            "Assets/Game/Activities/Autobattle/Objectives/EnemyFieldClearedObjective.asset");
                        var fieldObjective = battle.Objectives.Single(value => value.RootNodeId == fieldTemplate.Root.Id);
                        deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
                        ObjectiveState observed = ObjectiveState.None;
                        while (Time.realtimeSinceStartup < deadline)
                        {
                            Assert.IsTrue(ObjectiveService.TryGetNodeState(battle.DomainId, fieldObjective.RuntimeId,
                                fieldTemplate.Root.Id, out observed));
                            if (observed == ObjectiveState.Completed) break;
                            yield return null;
                        }
                        Assert.AreEqual(ObjectiveState.Completed, observed);
                        var participant = battle.Participants.Single(value => value.TeamIndex == 1);
                        var metric = AssetDatabase.LoadAssetAtPath<EntityCountActivityAnalyticsMetricData>(
                            "Assets/Game/Activities/Autobattle/Knowledge/EnemyPopulationMetric.asset");
                        Assert.IsTrue(ActivityAnalyticsService.TryPrepare(battle.DomainId, participant.ParticipantEntityId,
                            metric, ActivityAnalyticsMeasureType.ExistingEntities, out var read, out var readFailure), readFailure);
                        using (read)
                        {
                            var existing = read.Read(ActivityAnalyticsMeasureType.ExistingEntities);
                            var incoming = read.Read(ActivityAnalyticsMeasureType.AcceptedOutputs);
                            Assert.AreEqual(ActivityAnalyticsReadStateType.Ready, existing.State, existing.Reason);
                            Assert.AreEqual(ActivityAnalyticsReadStateType.Ready, incoming.State, incoming.Reason);
                            Assert.AreEqual(0L, existing.Value.Integer);
                            Assert.AreEqual(0L, incoming.Value.Integer);
                        }
                        Assert.IsFalse(ProductionService.GetSnapshots(battle.DomainId).Any(value =>
                            value.Owner?.StableSimulationKey == enemyOwner.StableSimulationKey
                            && (value.QueueCount != 0 || value.ActivePipelineCount != 0)));

                    }
                    else
                    {
                        var expected = value < 500000 ? new[] { "BugBrownMedium", "BugGreenSmall" }
                            : value < 750000 ? new[] { "BugGreenMedium", "BugPurpleSmall" }
                            : new[] { "BugBrownMedium", "BugGreenMedium", "BugPurpleMedium" };
                        var definitions = expected.Select(name => AssetDatabase.LoadAssetAtPath<CapabilityHostData>(
                            "Assets/Game/Activities/Autobattle/Economy/" + name + ".asset")).ToList();
                        Assert.IsTrue(enemies.All(enemy => definitions.Any(definition => definition.Matches(enemy.Definition))),
                            "A replenishment assignment used content outside the current source interval.");
                        foreach (var definition in definitions)
                            Assert.IsTrue(enemies.Any(enemy => definition.Matches(enemy.Definition)), definition.name);
                    }
                }
                Assert.IsEmpty(materializationWarnings, "Every replenishment output must settle its placement lease.");
                Assert.IsTrue(ActivityService.Close(battle.Id, ActivityCloseCauseType.Manual));
                Assert.IsFalse(ActivityAnalyticsService.TryGetSnapshot(battle.DomainId,
                    battle.Participants.Single(value => value.TeamIndex == 1).ParticipantEntityId, out _),
                    "Closed activity must release its Analytics domain.");
            }
            finally
            {
                Application.logMessageReceived -= CaptureWarning;
            }
        }

    }
}
