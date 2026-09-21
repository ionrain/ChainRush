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
        const string Root = "Assets/Game/Runtime/Run/";

        [TestCase("Survive", "Level01", 300)]
        [TestCase("Distance", "Level02", 150)]
        public void AuthoredSelection_CapturesTheSelectedLevelAndRoster(string name, string levelId, int amount)
        {
            ScriptableObject selection = Load(name);
            object snapshot = Capture(selection);
            Assert.AreEqual(levelId, Read(snapshot, "LevelId"));
            Assert.AreEqual(name, Read(snapshot, "GoalType").ToString());
            Assert.AreEqual(amount, Read(snapshot, "GoalAmount"));
            Assert.AreEqual(4, Read(snapshot, "BoardWidth"));
            Assert.AreEqual(4, Read(snapshot, "BoardHeight"));
            Assert.AreEqual(12345, Read(snapshot, "Seed"));
            Assert.AreEqual("PerfumeData", Read(Read(snapshot, "Hero"), "ContentId"));
            var units = (IList)Read(snapshot, "Units");
            Assert.AreEqual(2, units.Count);
            Assert.AreEqual("WaterData", Read(units[0], "ContentId"));
            Assert.AreEqual("ColaData", Read(units[1], "ContentId"));
            Assert.Throws<NotSupportedException>(() => units.Clear());
            Assert.Throws<NotSupportedException>(() => ((IList)Read(units[0], "AcquiredSkills")).Clear());
        }

        [Test]
        public void CapturedInput_IsUnaffectedByLaterSelectionChanges()
        {
            ScriptableObject selection = UnityEngine.Object.Instantiate(Load("Survive"));
            try
            {
                object snapshot = Capture(selection);
                var data = new SerializedObject(selection);
                data.FindProperty("hero").FindPropertyRelative("level").intValue = 9;
                data.FindProperty("units").arraySize = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(0, Read(Read(snapshot, "Hero"), "Level"));
                Assert.AreEqual(2, ((IList)Read(snapshot, "Units")).Count);
                Assert.AreEqual(9, Read(Read(Capture(selection), "Hero"), "Level"));
                var attributes = (IDictionary)Read(Read(snapshot, "Hero"), "Attributes");
                Assert.Greater(attributes.Count, 0);
                Assert.Throws<NotSupportedException>(() => attributes.Clear());
            }
            finally { UnityEngine.Object.DestroyImmediate(selection); }
        }

        [Test]
        public void IncompleteLevel_IsRejectedInsteadOfReceivingInferredDefaults()
        {
            ScriptableObject selection = UnityEngine.Object.Instantiate(Load("Survive"));
            try
            {
                var data = new SerializedObject(selection);
                data.FindProperty("level").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/Game/Resources/Levels/Location001/Loc001Lvl03Data.asset");
                data.ApplyModifiedPropertiesWithoutUndo();
                var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() => Capture(selection));
                Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
            }
            finally { UnityEngine.Object.DestroyImmediate(selection); }
        }

        [Test]
        public void IntegrationStartup_CapturesInputBeforeGameFlows()
        {
            var plan = AssetDatabase.LoadAssetAtPath<GameStartupPlanData>(
                "Assets/Game/Runtime/Startup/ChainRushGameStartupPlan.asset");
            Assert.AreEqual(3, plan.Actions.Length);
            Assert.AreEqual("CaptureChainRushRunActionData", plan.Actions[0].GetType().Name);
            Assert.AreEqual(GameStartupPhase.PreWorld, plan.Actions[0].Phase);
            Assert.AreEqual(GameStartupPhase.PostWorld, plan.Actions[1].Phase);
            Assert.AreEqual(GameStartupPhase.PostWorld, plan.Actions[2].Phase);
        }

        [TestCase("Level01", "AutobattleActivity", "Loc001Lvl01Data")]
        [TestCase("Level02", "DistanceActivity", "Loc001Lvl02Data")]
        public void LevelPopulation_UsesSourceFloorCountsAndProgressCompositions(string level, string activityName, string sourceName)
        {
            const string battle = "Assets/Game/Activities/Autobattle/";
            var activity = AssetDatabase.LoadAssetAtPath<ActivityData>(battle + "Definition/" + activityName + ".asset");
            var objective = activity.Teams[1].Objectives.Single().Template;
            Assert.AreEqual(ObjectiveCompletionPolicyType.ResetOnConditions, objective.CompletionPolicyType);
            var activation = objective.Root.ActivateConditions.OfType<ObjectiveConditionMaterializedEntity>().Single();
            var target = objective.Root.SuccessConditions.OfType<ObjectiveConditionMaterializedEntity>().Single();
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
        [TestCase("Level01TabascoActivity", false)]
        [TestCase("Level02TabascoActivity", true)]
        public void AuthoredHeroVariants_BindOneFeaturePerTypeAndTheSeededHero(string name, bool distance)
        {
            var activity = AssetDatabase.LoadAssetAtPath<ActivityData>(
                "Assets/Game/Activities/Autobattle/Definition/" + name + ".asset");
            var features = activity.Teams[0].Features;
            var hero = activity.Teams[0].Wallets.SelectMany(wallet => wallet.Seed)
                .Select(entry => entry.Seed.Asset).OfType<Core.CapabilityHosts.CapabilityHostData>()
                .Single(value => value.Tags.Any(tag => tag.Id == "chainrush.autobattle.herorole"));
            var failures = new System.Collections.Generic.List<string>();
            if (features.Count != features.Select(feature => feature.GetType()).Distinct().Count())
                failures.Add("Activity feature types must be unique.");
            foreach (var feature in features)
            {
                var data = new SerializedObject(feature);
                var heroes = data.FindProperty("heroes");
                if (heroes == null) continue;
                var definitions = Enumerable.Range(0, heroes.arraySize).Select(index =>
                    heroes.GetArrayElementAtIndex(index).FindPropertyRelative("definition").objectReferenceValue);
                if (!definitions.Contains(hero)) failures.Add(feature.name + " does not bind its activity's hero.");
            }
            if (distance)
            {
                var heal = features.SingleOrDefault(feature => feature.name == "DistanceHealFeature");
                if (heal == null) failures.Add("Distance has no healing feature.");
                if (heal != null)
                {
                    var recipients = new SerializedObject(heal).FindProperty("recipients");
                    if (!Enumerable.Range(0, recipients.arraySize)
                        .Select(index => recipients.GetArrayElementAtIndex(index).objectReferenceValue).Contains(hero))
                        failures.Add("The Distance hero must be an authored healing recipient.");
                }
            }
            Assert.IsEmpty(failures);
        }

        static ScriptableObject Load(string name)
        {
            var selection = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Root + name + "RunSelection.asset");
            Assert.NotNull(selection);
            return selection;
        }

        static object Capture(ScriptableObject selection) => selection.GetType().GetMethod("Capture").Invoke(selection, new object[] { 12345 });
        static object Read(object value, string property) => value.GetType().GetProperty(property).GetValue(value);
    }
}
