using System.Collections;
using System.Linq;
using System.Reflection;
using Core.Activities;
using Core.AI;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Economy;
using Core.Events;
using Core.Skills;
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
        [UnityTest]
        public IEnumerator PlayerMeleeAttack_OneActivationHitsEveryHostileInsideTheSourceArea()
        {
            yield return LaunchPlayableActivities();
            Assert.IsTrue(TryFindRunningActivities(out var battle, out _));
            Assert.IsTrue(TryFindActivityHost(battle.Id, AssetDatabase.LoadAssetAtPath<CapabilityHostData>(PerfumePath), out var hero));
            foreach (var skill in CapabilityHostService.GetSkillSnapshots(hero))
                Assert.IsTrue(CapabilityHostService.TrySetSkillEnabled(hero, skill.Id, false));
            var owner = battle.Participants.Single(value => value.TeamIndex == 0).ParticipantEconomyOwner;
            var enemyOwner = battle.Participants.Single(value => value.TeamIndex == 1).ParticipantEconomyOwner;
            var water = AssetDatabase.LoadAssetAtPath<CapabilityHostData>("Assets/Game/Activities/Shared/Units/Water/WaterUnit.asset");
            IssueTestTurns(owner, AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(SharedWalletTagPath), water, 1);
            Core.Entities.EntityId unit = default;
            var enemies = new System.Collections.Generic.List<CapabilityHostSnapshot>();
            float deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            do
            {
                TryFindActivityHost(battle.Id, water, out unit);
                enemies = CapabilityHostService.GetAll().Where(value => value.ActivityId == battle.Id
                    && value.Owner?.StableSimulationKey == enemyOwner.StableSimulationKey
                    && value.Definition.Tags.Any(term => term.Id == "chainrush.autobattle.role.enemy-unit")).Take(2).ToList();
                if (unit.IsValid && enemies.Count == 2) break;
                yield return null;
            } while (Time.realtimeSinceStartup < deadline);
            Assert.IsTrue(unit.IsValid); Assert.AreEqual(2, enemies.Count);
            Assert.IsTrue(AIBrainService.UnregisterOwner(unit));
            Assert.IsTrue(SpatialService.TrySetPosition(unit, new Vector3(5, 0, 5), out _, out _));
            Assert.IsTrue(SpatialService.TryGetPose(unit, out var origin));
            for (int i = 0; i < enemies.Count; i++)
            {
                Assert.IsTrue(AIBrainService.UnregisterOwner(enemies[i].EntityId));
                Assert.IsTrue(TopologyService.TryResolvePosition(battle.Id,
                    origin.Coordinates + new Vector3(i == 0 ? -1.7f : 1.7f, 0, 0), out var position, out _));
                Assert.IsTrue(SpatialService.TrySetPose(enemies[i].EntityId, position, origin.Rotation, out _));
            }
            var capture = new PlayableRuntimeCapture();
            capture.Register();
            try
            {
                var attack = AssetDatabase.LoadAssetAtPath<Core.Skills.SkillData>("Assets/Game/Activities/Autobattle/Skills/WaterUnitAttack.asset");
                Assert.IsTrue(SkillService.TryResolveId(attack, out var id));
                // A unit may already have fired before its brain was unregistered; retain the real reload lifecycle.
                yield return new WaitForSeconds(1.2f);
                Assert.IsTrue(SkillService.TryExecuteAsync(new SkillExecutionRequest(unit, id,
                    SkillTarget.Entities(enemies[0].EntityId))).GetAwaiter().GetResult().Success);
                deadline = Time.realtimeSinceStartup + 1;
                while (Time.realtimeSinceStartup < deadline) yield return null;
                foreach (var enemy in enemies)
                    Assert.IsTrue(capture.Hits.Any(hit => hit.OwnerEntityId == unit && hit.TargetEntityId == enemy.EntityId
                        && capture.Damage.Any(value => value.EntityId == enemy.EntityId
                            && value.MutationContext.SourceEntityId == hit.CarrierEntityId)), "One melee activation must hit each enemy in the source circle: " + enemy.EntityId);
            }
            finally { capture.Unregister(); }
            Assert.IsTrue(ActivityService.Close(battle.Id, ActivityCloseCauseType.Manual));
        }

        [UnityTest]
        public IEnumerator LightningSelection_ProducesARealCarrierHit([Values(1, 6, 16)] int chain)
        {
            SelectOnlyBoardContent("LightningBolt");
            yield return LaunchPlayableActivities();
            Assert.IsTrue(TryFindRunningActivities(out var battle, out var board));
            Assert.IsTrue(TryFindActivityHost(battle.Id,
                AssetDatabase.LoadAssetAtPath<CapabilityHostData>(PerfumePath), out var hero));
            Assert.IsTrue(AIBrainService.UnregisterOwner(hero));
            var owner = battle.Participants.Single(value => value.TeamIndex == 0).ParticipantEconomyOwner;
            var enemyOwner = battle.Participants.Single(value => value.TeamIndex == 1).ParticipantEconomyOwner;
            var wallet = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(SharedWalletTagPath);
            IssueTestTurns(owner, wallet, AssetDatabase.LoadAssetAtPath<EconomyAssetData>(BoardTurnTokenPath), 2);
            var cell = AssetDatabase.LoadAssetAtPath<CapabilityHostData>("Assets/Game/Activities/Board/Economy/LightningBoltBoardBase.asset");
            var tag = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardCellTagPath);
            yield return AwaitCompletedPopulation(board, tag, cell);
            var enemies = new System.Collections.Generic.List<CapabilityHostSnapshot>();
            float deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            do
            {
                enemies = CapabilityHostService.GetAll().Where(value => value.ActivityId == battle.Id
                    && value.Owner?.StableSimulationKey == enemyOwner.StableSimulationKey
                    && value.Definition.Tags.Any(term => term.Id == "chainrush.autobattle.role.enemy-unit")).ToList();
                if (enemies.Count >= 2) break;
                yield return null;
            } while (Time.realtimeSinceStartup < deadline);
            Assert.GreaterOrEqual(enemies.Count, 2);
            Assert.IsTrue(SpatialService.TryGetPose(hero, out var origin));
            for (int i = 0; i < enemies.Count; i++)
            {
                AIBrainService.UnregisterOwner(enemies[i].EntityId);
                Assert.IsTrue(TopologyService.TryResolvePosition(battle.Id,
                    origin.Coordinates + new Vector3(10, 0, i * 3), out var position, out _));
                Assert.IsTrue(SpatialService.TrySetPose(enemies[i].EntityId, position, origin.Rotation, out _));
            }
            var capture = new PlayableRuntimeCapture();
            capture.Register();
            try
            {
                Assert.IsTrue(TryFindBoardHost(board.Id, AssetDatabase.LoadAssetAtPath<CapabilityHostData>(BoardHostPath), out var host));
                yield return AssertBoardMergeSequence(board, tag, cell, host,
                    AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardMergeSelectionPath), chain,
                    new System.Collections.Generic.List<string>());
                deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
                while (!capture.Hits.Any(hit => hit.Carrier.name == "LightningBoltProjectile")
                    && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(capture.Hits.Any(hit => hit.Carrier.name == "LightningBoltProjectile"));
                Assert.IsTrue(capture.Damage.Any(change => enemies.Any(enemy => enemy.EntityId == change.EntityId)));
                Assert.IsNull(capture.Failure);
            }
            finally { capture.Unregister(); }
            Assert.IsTrue(ActivityService.Close(battle.Id, ActivityCloseCauseType.Manual));
        }

        [UnityTest]
        public IEnumerator TabascoLevelVariant_PreservesSourceBodyAndAttackReach([Values("Survive", "Distance")] string level)
        {
            var capture = new PlayableRuntimeCapture();
            float scale = Time.timeScale;
            capture.Register();
            try
            {
                yield return LaunchPlayableActivities(level == "Distance" ? "Level02Tabasco" : "Level01Tabasco");
                Assert.IsTrue(TryFindRunningActivities(out var battle, out _));
                var definition = AssetDatabase.LoadAssetAtPath<CapabilityHostData>(
                    "Assets/Game/Activities/Shared/Units/Tabasco/Tabasco" + (level == "Distance" ? "Distance" : "") + ".asset");
                Assert.IsTrue(TryFindActivityHost(battle.Id, definition, out var hero));
                Assert.IsTrue(InteractionGeometryService.TryGetSnapshot(hero, out _));
                Time.timeScale = 4;
                if (level == "Survive")
                {
                    // SourceGate_StopsAnEnemyOutsideMightyBlowBaseRadius verifies this constraint
                    // with the original controller and colliders. The migration preserves the source radius.
                    var attack = AssetDatabase.LoadAssetAtPath<Core.Skills.SkillData>(
                        "Assets/Game/Activities/Autobattle/Skills/TabascoAttack.asset");
                    var geometry = attack.Effects.OfType<SkillSpawnCarrierEffectData>().Single().InteractionGeometry.Single();
                    Assert.AreEqual(InteractionGeometryPrimitiveType.Sphere, geometry.PrimitiveType);
                    Assert.AreEqual(new Vector3Int(4000, 4000, 4000), geometry.Usage.CellSize);
                    var enemyOwner = battle.Participants.Single(value => value.TeamIndex == 1).ParticipantEconomyOwner;
                    float deadline = Time.realtimeSinceStartup + 2;
                    while (Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.IsTrue(CapabilityHostService.GetAll().Any(value => value.ActivityId == battle.Id
                        && value.Owner?.StableSimulationKey == enemyOwner.StableSimulationKey
                        && value.Definition.Tags.Any(term => term.Id == "chainrush.autobattle.role.enemy-unit")));
                    Assert.IsFalse(capture.Hits.Any(hit => hit.OwnerEntityId == hero),
                        "The base attack must not gain an unauthored range to reach through the source gate.");
                }
                else
                {
                    float deadline = Time.realtimeSinceStartup + CollectorCycleTimeoutSeconds;
                    while (!capture.Hits.Any(hit => hit.OwnerEntityId == hero)
                        && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.IsTrue(capture.Hits.Any(hit => hit.OwnerEntityId == hero
                        && capture.Damage.Any(value => value.EntityId == hit.TargetEntityId
                            && value.MutationContext.SourceEntityId == hit.CarrierEntityId)),
                        "The selected Tabasco hero did not deal area damage.\n" + BuildExecutorDiagnostic(hero));
                }
                Assert.IsNull(capture.Failure);
                var carriers = capture.Carriers.ToList();
                Assert.IsTrue(ActivityService.Close(battle.Id, ActivityCloseCauseType.Manual));
                yield return null;
                Assert.IsFalse(carriers.Any(Core.Entities.EntityService.Exists));
            }
            finally
            {
                Time.timeScale = scale;
                capture.Unregister();
            }
        }
    }
}
