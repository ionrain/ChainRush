using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Activities;
using Core.AI;
using Core.AI.Actions;
using Core.AI.Conditions;
using Core.Attributes;
using Core.CapabilityHosts;
using Core.Economy;
using Core.GameRuntime.Installers;
using Core.HostValues;
using Core.Projection;
using Core.Skills;
using Core.Taxonomy;
using Core.World;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEditor;
using UnityEngine;
using FrameworkSkillData = Core.Skills.SkillData;
using FrameworkAttributeData = Core.Attributes.AttributeData;
using FrameworkBrainData = Core.AI.AIBrainData;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        internal static void ApplyEnemyWeapons()
        {
            var economy = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(economy, "assets");
            var installer = LoadRequired<GameplaySkillsInstallerData>(SkillsInstallerPath);
            var skills = GetField<List<FrameworkSkillData>>(installer, "skills");
            double step = GetField<float>(LoadRequired<ActivityData>(ActivityPath).Schedule, "tickDelta");
            var node = WriteCombatTerm("WeaponNode", LoadRequired<TaxonomyTermData>(CombatNodePath));
            var targetKey = WriteCombatTerm("WeaponTarget", LoadRequired<TaxonomyTermData>(CombatTargetPath));
            var ready = WriteCombatTerm("WeaponReady", LoadRequired<TaxonomyTermData>(CombatStateAPath));
            var retry = WriteCombatTerm("WeaponRetry", LoadRequired<TaxonomyTermData>(CombatStateBPath));
            var stopped = WriteCombatTerm("WeaponStopped", LoadRequired<TaxonomyTermData>(DefeatStatePath));
            var attributes = new Dictionary<ElementalAttribute, FrameworkAttributeData>();
            foreach (Element element in Enum.GetValues(typeof(Element)))
                if (element != Element.Any)
                    foreach (var type in new[] { global::Attribute.Power, global::Attribute.Defense })
                        attributes.Add(new ElementalAttribute(element, type),
                            LoadRequired<FrameworkAttributeData>(RunAttributesRoot + "/" + type + element + ".asset"));
            var elements = LoadRequired<ElementsData>("Assets/Game/Resources/ElementsData.asset");
            var sources = new[] { "BugGreenSmall", "BugGreenMedium", "BugPurpleSmall", "BugPurpleMedium" };
            foreach (string name in sources)
            {
                var source = LoadRequired<GameObject>("Assets/Game/Prefabs/Enemies/" + name + ".prefab");
                var weapon = source.GetComponentsInChildren<CharacterHandleWeapon>(true).Single().InitialWeapon;
                var autoAim = weapon == null ? null : weapon.GetComponent<WeaponAutoAim>();
                var autoShoot = weapon == null ? null : weapon.GetComponent<WeaponAutoShoot>();
                if (weapon == null || autoAim == null || autoShoot == null || autoShoot.OnlyAutoShootIfOwnerIsIdle)
                    throw new InvalidOperationException("Enemy requires its authored independent automatic weapon: " + name);
                var host = LoadRequired<CapabilityHostData>(EconomyRoot + "/" + name + ".asset");
                var attack = WriteContentSkill(SkillsRoot + "/" + name + "Weapon.asset", host.Id + ".weapon", definitions, skills);
                var hit = WriteContentSkill(SkillsRoot + "/" + name + "WeaponHit.asset", host.Id + ".weapon-hit", definitions, skills);
                SetField(attack, "startDelay", ContentDuration(weapon.DelayBeforeUse + autoShoot.DelayBeforeShootAfterAcquiringTarget, step));
                SetField(attack, "reloadTime", ContentDuration(weapon.TimeBetweenUses, step));
                SetField(attack, "actionCount", weapon.UseBurstMode ? weapon.BurstLength : 1);
                SetField(attack, "actionInterval", ContentDuration(weapon.BurstTimeBetweenShots, step));
                var range = new SkillTargetDistanceRequirementData();
                SetField(range, "distance", checked((int)RoundContent(autoAim.ScanRadius * 1000)));
                SetField(range, "compareOperation", CompareOperation.LessOrEqual);
                SetField(range, "targetGeometryType", WorldTargetGeometryType.Spatial);
                attack.Requirements.Add(range);
                var spawn = new SkillSpawnCarrierEffectData();
                SetField(spawn, "carriedSkill", hit);
                var lives = LoadRequired<HostValueData>(CarrierLivesPath);
                DamageOnTouch touch;
                if (weapon is MeleeWeapon melee)
                {
                    touch = melee.ExistingDamageArea;
                    var circle = touch == null ? null : touch.GetComponent<CircleCollider2D>();
                    if (circle == null || circle.offset != Vector2.zero)
                        throw new InvalidOperationException("Enemy area weapon requires its centered circular source geometry: " + name);
                    SetField(attack, "startDelay", ContentDuration(weapon.DelayBeforeUse + melee.InitialDelay
                        + autoShoot.DelayBeforeShootAfterAcquiringTarget, step));
                    var area = new SkillCarrierAreaContactData();
                    SetField(area, "radius", checked((int)RoundContent(circle.radius * circle.transform.lossyScale.x * 1000)));
                    SetField(area, "repeatInterval", checked((int)Math.Max(1, ContentDuration(touch.InvincibilityDuration, step))));
                    SetField(area, "maxCandidates", 256);
                    SetField(area, "followOwner", true);
                    SetField(spawn, "areaContact", area);
                    SetField(spawn, "carrier", WriteParticleCarrierVisual(weapon.gameObject, name + "WeaponArea"));
                    spawn.Parameters.Add(CarrierScalar(SkillCarrierScalarParameterType.Lives, 1));
                    spawn.Parameters.Add(CarrierScalar(SkillCarrierScalarParameterType.Lifetime, ContentDuration(melee.ActiveDuration, step)));
                }
                else if (weapon is ProjectileWeapon projectileWeapon)
                {
                    if (projectileWeapon.Spread != Vector3.zero)
                        throw new InvalidOperationException("Enemy weapon spread must be authored explicitly: " + name);
                    var original = projectileWeapon.GetComponent<MMSimpleObjectPooler>()?.GameObjectToPool;
                    var projectile = original == null ? null : original.GetComponent<Projectile>();
                    touch = original == null ? null : original.GetComponent<DamageOnTouch>();
                    if (projectile == null || touch == null) throw new InvalidOperationException("Enemy projectile content is missing: " + name);
                    var circle = original.GetComponent<CircleCollider2D>();
                    if (circle == null || circle.offset != Vector2.zero)
                        throw new InvalidOperationException("Included enemy projectile requires centered circular contact geometry: " + name);
                    var contact = new SkillCarrierAreaContactData();
                    SetField(contact, "radius", Distance(circle.radius * circle.transform.lossyScale.x));
                    SetField(contact, "sweepMovement", true);
                    SetField(contact, "interactionTags", new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(CombatantRolePath) });
                    SetField(contact, "repeatInterval", checked((int)Math.Max(1, ContentDuration(touch.InvincibilityDuration, step))));
                    SetField(spawn, "areaContact", contact);
                    SetField(spawn, "carrier", original.GetComponentsInChildren<SpriteRenderer>(true).Length > 0
                        ? WriteCarrierVisual(original, name + "WeaponProjectile")
                        : WriteParticleCarrierVisual(original, name + "WeaponProjectile"));
                    SetField(spawn, "count", projectileWeapon.ProjectilesPerShot);
                    var velocity = new SkillCarrierTargetLinearVelocityParameterValueData();
                    long speed = RoundContent(projectile.Speed / 10d * 1000 * step);
                    SetField(velocity, "minSpeed", speed);
                    SetField(velocity, "maxSpeed", speed);
                    spawn.Parameters.Add(velocity);
                    spawn.Parameters.Add(CarrierScalar(SkillCarrierScalarParameterType.Lives, 1, lives));
                    spawn.Parameters.Add(CarrierScalar(SkillCarrierScalarParameterType.Lifetime, ContentDuration(projectile.LifeTime, step)));
                    var consume = new SkillHostValueEffectData();
                    ConfigureEffect(consume, EffectRecipient.Carrier, -1);
                    SetField(consume, "hostValue", lives);
                    hit.Effects.Add(consume);
                }
                else throw new InvalidOperationException("Unmapped enemy weapon " + weapon.GetType().FullName);

                foreach (var damage in SourceDamages(touch, elements))
                {
                    var power = attributes[new ElementalAttribute(damage.Element, global::Attribute.Power)];
                    var defense = attributes[new ElementalAttribute(damage.Element, global::Attribute.Defense)];
                    var effect = CreateElementalDamage(damage.Amount, power, defense);
                    var value = LoadRequired<HostValueData>(CarrierDamagePath(damage.Element));
                    var parameter = new SkillCarrierHostValueParameterValueData();
                    SetField(parameter, "value", value);
                    SetField(parameter, "minValue", damage.Amount);
                    SetField(parameter, "maxValue", damage.Amount);
                    SetField(parameter, "ownerMultiplier", new AttributeSelectorData(power));
                    SetField(parameter, "allowMissingMultiplier", true);
                    spawn.Parameters.Add(parameter);
                    var captured = new SkillHostValueFormulaTermData();
                    SetField(captured, "operation", MathOperation.Set);
                    SetField(captured, "parameterTarget", SkillParameterTargetType.Carrier);
                    SetField(captured, "value", value);
                    SetField(captured, "stopWhenNonPositive", true);
                    effect.Formula.Terms[0] = captured;
                    SetField(effect, "value", -1L);
                    hit.Effects.Insert(0, effect);
                }
                attack.Effects.Add(spawn);
                SetDamageProtection(hit, touch.InvincibilityDuration, step);
                WriteCarrierForce(hit, touch);
                var seeds = host.WalletEntries.Single(entry => entry.Wallet == LoadRequired<EconomyWalletData>(UnitWalletPath)).Seed;
                seeds.RemoveAll(seed => seed.Asset == attack);
                seeds.Add(new SeedEntry(attack, 1, EconomyFormType.Stack));
                var brain = LoadRequired<FrameworkBrainData>(AIRoot + "/" + name + "Brain.asset");
                WriteIndependentWeaponNode(brain, attack, node, targetKey, ready, retry, stopped, range.Distance);
                EditorUtility.SetDirty(brain);
                EditorUtility.SetDirty(host);
                EditorUtility.SetDirty(attack);
                EditorUtility.SetDirty(hit);
            }
            EditorUtility.SetDirty(economy);
            EditorUtility.SetDirty(installer);
        }

        static IEnumerable<(Element Element, long Amount)> SourceDamages(DamageOnTouch source, ElementsData elements)
        {
            if (source.MinDamageCaused != source.MaxDamageCaused)
                throw new InvalidOperationException("The source damage range must be represented explicitly.");
            if (source.MinDamageCaused > 0) yield return (Element.Physical, RoundContent(source.MinDamageCaused * CombatValueScale));
            foreach (var damage in source.TypedDamages)
            {
                if (damage.MinDamageCaused != damage.MaxDamageCaused)
                    throw new InvalidOperationException("The source typed damage range must be represented explicitly.");
                yield return (elements.data.Single(pair => pair.Value.damageType == damage.AssociatedDamageType).Key,
                    RoundContent(damage.MinDamageCaused * CombatValueScale));
            }
        }

        static TaxonomyTermData WriteCombatTerm(string name, TaxonomyTermData template)
        {
            var term = ChainRushBoardPlannerAuthoring.WriteContentAsset(TaxonomyRoot + "/" + name + ".asset", template);
            SetField(term, "id", "chainrush.autobattle." + name.ToLowerInvariant());
            SetField(term, "displayName", name);
            var installer = LoadRequired<TaxonomyRuntimeInstallerData>(TaxonomyInstallerPath);
            var data = new SerializedObject(installer);
            var terms = data.FindProperty("terms");
            bool found = false;
            for (int i = 0; i < terms.arraySize; i++) found |= terms.GetArrayElementAtIndex(i).objectReferenceValue == term;
            if (!found) { int index = terms.arraySize++; terms.GetArrayElementAtIndex(index).objectReferenceValue = term; }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(term);
            return term;
        }

        static void WriteIndependentWeaponNode(FrameworkBrainData brain, FrameworkSkillData skill, TaxonomyTermData nodeTag,
            TaxonomyTermData target, TaxonomyTermData ready, TaxonomyTermData retry, TaxonomyTermData stopped, int radius)
        {
            brain.Nodes.RemoveAll(node => node.NodeId == nodeTag);
            brain.Transitions.RemoveAll(transition => transition.ToNodeId == nodeTag);
            var node = new AIBrainNodeData();
            SetField(node, "nodeId", nodeTag);
            SetField(node, "entryState", ready);
            foreach (var stateTag in new[] { ready, retry })
            {
                var state = new AIBrainStateData();
                SetField(state, "tag", stateTag);
                var query = new SelectEntityTargetByQueryAIBrainActionData();
                SetField(query, "targetKey", target);
                SetField(query, "searchRadius", radius);
                SetField(query, "maxCandidates", 256);
                SetField(query, "compatibleSkill", skill);
                SetField(query, "targetGeometryType", WorldTargetGeometryType.Spatial);
                SetField(query, "requiredTargetTags", new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(CombatantRolePath) });
                SetField(query, "blockedTargetStates", new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(DefeatStatePath) });
                state.OnTickActions.Add(query);
                state.OnTickActions.Add(CreateUseSkillAction(skill, target, SkillCompletionPolicyType.OnExecutionComplete));
                var interrupt = new InterruptSkillAIBrainExitActionData();
                SetField(interrupt, "skill", skill);
                state.OnExitActions.Add(interrupt);
                node.States.Add(state);
                var dead = new HostValueConditionAIBrainConditionData();
                var template = brain.Transitions.SelectMany(transition => transition.Conditions)
                    .OfType<HostValueConditionAIBrainConditionData>().First();
                // A fresh managed-reference graph is required for each authored transition.
                UnityEngine.JsonUtility.FromJsonOverwrite(UnityEngine.JsonUtility.ToJson(template), dead);
                brain.Transitions.Insert(0, CreateBrainTransition(stateTag, nodeTag, stopped, dead));
                brain.Transitions.Add(CreateBrainTransition(stateTag, nodeTag, stateTag == ready ? retry : ready,
                    CreateStateResultCondition(AIBrainStateResultMask.Success | AIBrainStateResultMask.Fail | AIBrainStateResultMask.Interrupted), Delay(1)));
            }
            var terminal = new AIBrainStateData();
            SetField(terminal, "tag", stopped);
            node.States.Add(terminal);
            brain.Nodes.Add(node);
        }
    }
}
