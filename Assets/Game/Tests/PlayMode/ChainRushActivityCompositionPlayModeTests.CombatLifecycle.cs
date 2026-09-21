using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Activities;
using Core.AI;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Economy;
using Core.Events;
using Core.HostValues;
using Core.Skills;
using Core.Taxonomy;
using Core.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Tests.PlayMode
{
    public sealed partial class ChainRushActivityCompositionPlayModeTests
    {
        [UnityTest]
        public IEnumerator EnemyContact_CreatesOnePersistentAreaPerLivingOwner()
        {
            var capture = new ContactLifetimeCapture();
            EventBus.Register<SkillCarrierLifecycleEvent>(capture);
            try
            {
                yield return LaunchPlayableActivities();
                var health = AssetDatabase.LoadAssetAtPath<HostValueData>("Assets/Game/Activities/Autobattle/HostValues/Health.asset");
                float deadline = UnityEngine.Time.realtimeSinceStartup + StartupTimeoutSeconds;
                while (capture.Spawned.Count == 0 && UnityEngine.Time.realtimeSinceStartup < deadline) yield return null;
                for (int frame = 0; frame < 120; frame++)
                {
                    foreach (var pair in capture.Spawned)
                    {
                        if (!CapabilityHostService.TryGetHostValue(pair.Key, health, out var current) || current.CurrentValue <= 0) continue;
                        AIBrainService.TryGetState(pair.Key, out var brain);
                        string states = brain == null ? "no brain" : string.Join(";", brain.Nodes.Select(node =>
                            node.NodeId.name + ":" + node.CurrentState.name + "/completed=" + node.CurrentStateIsCompleted));
                        Assert.AreEqual(1, pair.Value, "A living owner spawned duplicate contact areas: " + pair.Key + "; " + states);
                    }
                    yield return null;
                }
                Assert.Greater(capture.Spawned.Count, 0);
                Assert.IsTrue(TryFindRunningActivities(out var battle, out _));
                Assert.IsTrue(ActivityService.Close(battle.Id, ActivityCloseCauseType.Manual));
                yield return null;
                Assert.IsFalse(capture.Entities.Any(Core.Entities.EntityService.Exists));
            }
            finally { EventBus.Unregister<SkillCarrierLifecycleEvent>(capture); }
        }

        [UnityTest]
        public IEnumerator HeroBody_HasAuthoredLevelGeometryRegisteredForCombat()
        {
            yield return LaunchPlayableActivities();
            Assert.IsTrue(TryFindRunningActivities(out var battle, out _));
            var definition = AssetDatabase.LoadAssetAtPath<CapabilityHostData>(PerfumePath);
            EntityId hero = EntityId.Invalid;
            for (int frame = 0; frame < 300 && !TryFindActivityHost(battle.Id, definition, out hero); frame++) yield return null;
            Assert.IsTrue(CapabilityHostService.TryGet(hero, out var host));
            var body = AssetDatabase.LoadAssetAtPath<InteractionGeometryData>("Assets/Game/Activities/Autobattle/Space/GateHeroBody.asset");
            var wallet = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>("Assets/Game/Activities/Autobattle/Economy/UnitWalletTag.asset");
            Assert.NotNull(wallet);
            for (int frame = 0; frame < 10; frame++) yield return null;
            Assert.AreEqual(1, QueryAmount(host.SelfEconomyOwner, wallet, EconomyFormType.Stack, body),
                "The host must contain the selected level's body.");
            Assert.IsTrue(InteractionGeometryService.TryGetSnapshot(hero, out _),
                "The issued body must be registered by the CapabilityHost interaction owner.");
        }

        sealed class ContactLifetimeCapture : IEventListener<SkillCarrierLifecycleEvent>
        {
            public readonly Dictionary<EntityId, int> Spawned = new Dictionary<EntityId, int>();
            public readonly HashSet<EntityId> Entities = new HashSet<EntityId>();
            public void OnEvent(SkillCarrierLifecycleEvent e)
            {
                if (e.EventType != SkillCarrierLifecycleEventType.Spawned || e.Carrier == null || e.Carrier.name != "ContactArea") return;
                Spawned.TryGetValue(e.OwnerEntityId, out int count);
                Spawned[e.OwnerEntityId] = count + 1;
                Entities.Add(e.CarrierEntityId);
            }
        }
    }
}
