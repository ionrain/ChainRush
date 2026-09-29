using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Core;
using Core.Activities;
using Core.Activities.Analytics;
using Core.Objectives;
using Core.Orchestration;
using Core.GameRuntime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ChainRush.Tests.EditMode
{
    public sealed class ChainRushRunInputEditModeTests
    {
        [TestCase("Level01", "AutobattleActivity", "Loc001Lvl01Data")]
        [TestCase("Level02", "DistanceActivity", "Loc001Lvl02Data")]
        public void LevelPopulation_UsesSourceFloorCountsAndProgressCompositions(string level, string activityName, string sourceName)
        {
            const string battle = "Assets/Game/Activities/Autobattle/";
            var activity = AssetDatabase.LoadAssetAtPath<ActivityData>(battle + "Definition/" + activityName + ".asset");
            var objective = activity.Teams[1].Objectives.Single(value => value.Template.name == level + "ReplenishmentObjective").Template;
            Assert.AreEqual(ObjectiveCompletionPolicyType.ResetOnConditions, objective.CompletionPolicyType);
            var activation = objective.Root.ActivateConditions.OfType<ObjectiveConditionMaterializedEntity>().Single();
            var target = objective.Root.SuccessConditions.OfType<ObjectiveConditionComposite>().Single()
                .Conditions.OfType<ObjectiveConditionMaterializedEntity>().Single();
            Assert.AreEqual(CompareOperation.Less, activation.CompareOperation);
            Assert.AreEqual(CompareOperation.GreaterOrEqual, target.CompareOperation);
            Assert.AreNotSame(activation.TargetProgression, target.TargetProgression);
            var reset = objective.ResetConditions.OfType<ObjectiveConditionMaterializedEntity>().Single();
            Assert.AreEqual(CompareOperation.Less, reset.CompareOperation);
            Assert.AreNotSame(activation, reset);
            Assert.AreNotSame(activation.TargetProgression, reset.TargetProgression);
            var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/Game/Resources/Levels/Location001/" + sourceName + ".asset");
            object enemySource = source.GetType().GetField("enemyData").GetValue(source);
            var curve = (AnimationCurve)enemySource.GetType().GetField("enemyCountCurve").GetValue(enemySource);
            int maximum = (int)enemySource.GetType().GetField("maxFillCount").GetValue(enemySource);
            int simultaneous = (int)enemySource.GetType().GetField("maxSimulteneousCount").GetValue(enemySource);
            var progression = (LongProgressionData)typeof(ObjectiveLongTargetProgressionData)
                .GetField("progression", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target.TargetProgression);
            for (long value = 0; value <= 1000000; value++)
            {
                long expected = Math.Min(simultaneous, (long)(curve.Evaluate((float)value / 1000000) * maximum));
                if (!progression.TryEvaluate(new ProgressionContext(value), out long actual, out string failure) || actual != expected)
                    Assert.Fail($"Source count mismatch at {value}: expected {expected}, actual {actual}. {failure}");
            }
            var orchestration = activity.Teams[1].Features.OfType<ActivityOrchestrationConfigData>().Single();
            Assert.AreEqual(1, activity.Teams[1].Features.OfType<ActivityAnalyticsConfigData>().Count());
            var agent = orchestration.OrchestratorBrain.Operators.OfType<AgentDecompOpData>().Single().AgentDefinition;
            Assert.AreEqual("chainrush.autobattle.population." + level.ToLowerInvariant(), agent.AgentId);
            var population = (PopulationAgentData)agent.Agent;
            Assert.IsInstanceOf<SpatialPopulationDistributionAlgorithmData>(population.Distribution);
            Assert.AreEqual(simultaneous, ((LongFlatProgressionData)population.Volume).Value);
            Assert.AreEqual(PopulationCompletionPolicyType.AllowPartialVolume, population.CompletionPolicy);
            CollectionAssert.AreEqual(new[] { 0d, .25, .5, .75 }, population.Releases.Select(release => release.Interval.Minimum));
            Assert.AreEqual(1, population.Releases.Last().Interval.Maximum);
            foreach (var release in population.Releases) Assert.AreEqual(1f, release.Content.Sum(rule => rule.Share), .0001f);
            var stop = agent.ApplicabilityConditions.OfType<AgentEconomyApplicabilityConditionData>().Single();
            Assert.AreEqual(CompareOperation.Less, stop.CompareOperation);
            Assert.AreEqual(1000000, stop.Amount);
        }

        [TestCase("AutobattleActivity", false)]
        [TestCase("DistanceActivity", true)]
        public void AuthoredLevels_DeploySelectedHeroAndUseModeProgress(string name, bool distance)
        {
            var activity = AssetDatabase.LoadAssetAtPath<ActivityData>(
                "Assets/Game/Activities/Autobattle/Definition/" + name + ".asset");
            var features = activity.Teams[0].Features;
            var seededHosts = activity.Teams[0].Wallets.SelectMany(wallet => wallet.Seed)
                .Select(entry => entry.Seed.Asset).OfType<Core.CapabilityHosts.CapabilityHostData>()
                .ToList();
            Assert.IsFalse(seededHosts.Any(value => value.Tags.Any(tag => tag.Id == "chainrush.autobattle.herorole")));
            Assert.AreEqual(1, seededHosts.Count(value => value.name == "HeroProductionHost"));
            var deployment = activity.Teams[0].Objectives.Single(value => value.Template.name == "HeroDeploymentObjective").Template;
            Assert.AreEqual(ObjectiveCompletionPolicyType.Terminal, deployment.CompletionPolicyType);
            var failures = new System.Collections.Generic.List<string>();
            if (features.Count != features.Select(feature => feature.GetType()).Distinct().Count())
                failures.Add("Activity feature types must be unique.");
            var analytics = features.OfType<ActivityAnalyticsConfigData>().Single();
            var metric = analytics.Metrics.Single();
            if (distance)
            {
                Assert.IsInstanceOf<EntityMovementActivityAnalyticsMetricData>(metric);
                var movement = (EntityMovementActivityAnalyticsMetricData)metric;
                Assert.IsNull(movement.ExactAsset);
                Assert.IsTrue(movement.ObjectTags.IncludedTags.Any(tag => tag.Id == "chainrush.autobattle.herorole"));
                Assert.AreEqual(EntityMovementCalculationType.DisplacementAlongAxis,
                    ((EntityMovementActivityAnalyticsMetricData)metric).CalculationType);
            }
            else Assert.IsInstanceOf<ElapsedSimulationActivityAnalyticsMetricData>(metric);
            var progress = activity.Teams[0].Objectives.Select(value => value.Template)
                .Single(value => value.name.EndsWith("ProgressObjective"));
            Assert.AreEqual(ObjectiveCompletionPolicyType.Reset, progress.CompletionPolicyType);
            foreach (var condition in progress.Root.ActivateConditions.Concat(progress.Root.SuccessConditions)
                .OfType<ObjectiveConditionEconomyMetric>())
            {
                Assert.AreEqual(ObjectiveProgressSourceType.Analytics, condition.TargetProgression.SourceType);
                Assert.AreSame(metric, condition.TargetProgression.Metric);
            }
            Assert.IsEmpty(failures);
        }

    }
}
