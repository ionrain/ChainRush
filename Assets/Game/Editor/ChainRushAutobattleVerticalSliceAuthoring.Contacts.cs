using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.AI;
using Core.AI.Actions;
using Core.AI.Conditions;
using Core.CapabilityHosts;
using Core.Economy;
using Core.GameRuntime.Installers;
using Core.HostValues;
using Core.Projection;
using Core.Skills;
using Core.Taxonomy;
using Core.World;
using MoreMountains.TopDownEngine;
using UnityEditor;
using UnityEngine;
using FrameworkSkillData = Core.Skills.SkillData;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        internal static void ApplyEnemyContacts()
        {
            var economy = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(economy, "assets");
            var installer = LoadRequired<GameplaySkillsInstallerData>(SkillsInstallerPath);
            var skills = GetField<List<FrameworkSkillData>>(installer, "skills");
            var carrier = WriteContactCarrier();
            var nodeTag = WriteCombatTerm("ContactNode", LoadRequired<TaxonomyTermData>(CombatNodePath));
            var active = WriteCombatTerm("ContactActive", LoadRequired<TaxonomyTermData>(CombatStateBPath));
            var stopped = WriteCombatTerm("ContactStopped", LoadRequired<TaxonomyTermData>(DefeatStatePath));
            var health = LoadRequired<HostValueData>(HealthPath);
            double step = GetField<float>(LoadRequired<Core.Activities.ActivityData>(ActivityPath).Schedule, "tickDelta");
            foreach (string name in new[] { "BugBrownSmall", "BugBrownMedium", "BugGreenSmall", "BugGreenMedium", "BugPurpleSmall", "BugPurpleMedium" })
            {
                var source = LoadRequired<GameObject>("Assets/Game/Prefabs/Enemies/" + name + ".prefab");
                var touch = source.GetComponentsInChildren<DamageOnTouch>(true).Single();
                var circle = touch.GetComponent<CircleCollider2D>();
                if (circle == null || circle.offset != Vector2.zero || touch.transform.position != source.transform.position)
                    throw new InvalidOperationException("Enemy contact requires its centered source damage circle: " + name);
                var hit = LoadRequired<FrameworkSkillData>(SkillsRoot + "/" + name + "Contact.asset");
                SetField(hit, "reloadTime", 0L);
                hit.Requirements.Clear();
                var spawn = WriteContentSkill(SkillsRoot + "/" + name + "ContactArea.asset", "chainrush.skill." + name.ToLowerInvariant() + ".contact-area", definitions, skills);
                SetField(spawn, "targetType", SkillTargetType.Self);
                SetField(spawn, "targetTags", new List<TaxonomyTermData>());
                var effect = new SkillSpawnCarrierEffectData();
                SetField(effect, "carrier", carrier); SetField(effect, "carriedSkill", hit);
                var area = new SkillCarrierAreaContactData();
                SetField(area, "followOwner", true);
                SetField(area, "requiredOwnerValue", health); SetField(area, "minimumOwnerValue", 1L);
                SetField(area, "radius", Distance(circle.radius * Mathf.Abs(circle.transform.lossyScale.x)));
                SetField(area, "repeatInterval", checked((int)Math.Max(1, ContentDuration(touch.InvincibilityDuration, step))));
                SetField(area, "targetGeometryType", WorldTargetGeometryType.Interaction);
                SetField(area, "interactionTags", new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(CombatantRolePath) });
                SetField(effect, "areaContact", area);
                effect.Parameters.Add(CarrierScalar(SkillCarrierScalarParameterType.Lives, 1));
                spawn.Effects.Add(effect);
                var host = LoadRequired<CapabilityHostData>(EconomyRoot + "/" + name + ".asset");
                var seeds = host.WalletEntries.Single(entry => entry.Wallet == LoadRequired<EconomyWalletData>(UnitWalletPath)).Seed;
                seeds.RemoveAll(seed => seed.Asset == hit || seed.Asset == spawn);
                seeds.Add(new SeedEntry(spawn, 1, EconomyFormType.Stack));
                var approach = LoadRequired<FrameworkSkillData>(SkillsRoot + "/" + name + "Approach.asset");
                SetField(approach.Effects[0], "targetGeometryType", WorldTargetGeometryType.Interaction);
                SetField(approach.Effects[0], "approachInteractionTags", new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(CombatantRolePath) });
                var brain = LoadRequired<Core.AI.AIBrainData>(AIRoot + "/" + name + "Brain.asset");
                foreach (var state in brain.Nodes.Single(node => node.NodeId == LoadRequired<TaxonomyTermData>(CombatNodePath)).States)
                {
                    state.OnTickActions.RemoveAll(action => action is UseSkillAIBrainActionData && GetField<FrameworkSkillData>(action, "skill") == hit);
                    foreach (var action in state.OnTickActions.OfType<SelectActivityTargetAIBrainActionData>())
                    {
                        SetField(action, "requiredTargetTags", new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(TaxonomyRoot + "/HeroRole.asset") });
                        SetField<FrameworkSkillData>(action, "compatibleSkill", null);
                    }
                    if (state.Tag != LoadRequired<TaxonomyTermData>(DefeatStatePath))
                    {
                        var interrupt = new InterruptSkillAIBrainExitActionData(); SetField(interrupt, "skill", approach);
                        state.OnExitActions.Add(interrupt);
                    }
                }
                brain.Nodes.RemoveAll(node => node.NodeId == nodeTag);
                brain.Transitions.RemoveAll(transition => transition.ToNodeId == nodeTag);
                var node = new AIBrainNodeData(); SetField(node, "nodeId", nodeTag); SetField(node, "entryState", active);
                foreach (var tag in new[] { active, stopped })
                {
                    var state = new AIBrainStateData(); SetField(state, "tag", tag); node.States.Add(state);
                    if (tag == active) state.OnEnterActions.Add(CreateUseSkillAction(spawn, null, SkillCompletionPolicyType.OnExecutionComplete));
                    if (tag == stopped) continue;
                    var dead = new HostValueConditionAIBrainConditionData();
                    SetField(dead, "hostValue", health); SetField(dead, "targetValue", 0L);
                    SetField(dead, "compareOperation", CompareOperation.LessOrEqual);
                    brain.Transitions.Insert(0, CreateBrainTransition(tag, nodeTag, stopped, dead));
                }
                brain.Nodes.Add(node);
                EditorUtility.SetDirty(hit); EditorUtility.SetDirty(spawn); EditorUtility.SetDirty(host);
                EditorUtility.SetDirty(approach); EditorUtility.SetDirty(brain);
            }
            EditorUtility.SetDirty(carrier); EditorUtility.SetDirty(economy); EditorUtility.SetDirty(installer);
        }

        static ProjectionDefinitionData WriteContactCarrier()
        {
            var definition = ChainRushBoardPlannerAuthoring.WriteContentAsset<ProjectionDefinitionData>(ProjectionRoot + "/ContactArea.asset", null);
            var root = new GameObject("ContactArea");
            try
            {
                ConfigureProjectionBinding(root, "chainrush.carrier.contact-area");
                string path = ProjectionRoot + "/ContactArea.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                ConfigureAddressable(path, AddressablesGroup);
                SetField(definition, "projectionPrefabReference", new ProjectionPrefabReference(AssetDatabase.AssetPathToGUID(path)));
                SetField(definition.ProjectionPool, "maxCapacity", 64);
                EditorUtility.SetDirty(definition);
                return definition;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
