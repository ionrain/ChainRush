using System;
using System.Collections.Generic;
using System.Linq;
using ChainRush.Gameplay;
using Core;
using Core.Activities;
using Core.Attributes;
using Core.CapabilityHosts;
using Core.Economy;
using Core.GameRuntime;
using Core.GameRuntime.Installers;
using Core.HostValues;
using Core.Skills;
using Core.Taxonomy;
using UnityEditor;
using UnityEngine;
using FrameworkAttributeData = Core.Attributes.AttributeData;
using FrameworkSkillData = Core.Skills.SkillData;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        const string RunAttributesRoot = SharedRoot + "/Attributes";
        const int CombatValuePrecision = 3;
        const long CombatValueScale = 1000;
        const string DamageProtectionPath = TaxonomyRoot + "/DamageProtection.asset";

        internal static void ApplyRunAttributes()
        {
            var installer = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(installer, "assets");
            var attributes = WriteRunAttributes(definitions);
            WriteCombatTerm("DamageProtection", LoadRequired<TaxonomyTermData>(DefeatStatePath));
            foreach (Element element in Enum.GetValues(typeof(Element)))
                if (element != Element.Any) WriteCarrierDamageValue(element, definitions);
            var feature = ChainRushBoardPlannerAuthoring.WriteContentAsset<ChainRushRunAttributesFeatureData>(
                "Assets/Game/Runtime/Run/RunAttributesFeature.asset", null);
            var bindings = new List<ChainRushRunCharacterBinding>();
            var health = LoadRequired<HostValueData>(HealthPath);
            SetField(health, "precision", CombatValuePrecision);
            double step = GetField<float>(LoadRequired<ActivityData>(ActivityPath).Schedule, "tickDelta");

            foreach (string name in new[] { "Water", "Cola", "Perfume", "Tabasco" })
            {
                var source = LoadRequired<UnitData>("Assets/Game/Resources/Units/" + name + "Data.asset");
                bool hero = source.type == UnitType.Hero;
                for (int form = 0; form < source.mergeStates.Count; form++)
                {
                    string definitionName = hero ? name : name + "Unit" + (form == 0 ? "" : (form + 1).ToString());
                    var host = LoadRequired<CapabilityHostData>(SharedRoot + "/Units/" + name + "/" + definitionName + ".asset");
                    var binding = new ChainRushRunCharacterBinding { ContentId = source.name, Definition = host };
                    var seeds = GetField<List<WalletEntry>>(host, "walletEntries").Single(
                        entry => entry.Wallet == LoadRequired<EconomyWalletData>(UnitWalletPath)).Seed;
                    seeds.RemoveAll(seed => seed.Asset is FrameworkAttributeData || seed.Asset == health);
                    foreach (var pair in attributes)
                    {
                        float multiplier = source.mergeStates[form].GetAttributeMultiplier(pair.Key.Attribute);
                        binding.Attributes.Add(new ChainRushRunAttributeBinding
                        {
                            Attribute = pair.Key.Attribute, Element = pair.Key.Element,
                            Definition = pair.Value, FormMultiplier = multiplier
                        });
                        double amount = 0;
                        if ((pair.Key.Element == Element.Any || pair.Key.Element == source.element)
                            && source.attributes.TryGetValue(pair.Key.Attribute, out var upgrade) && upgrade != null)
                            amount = upgrade.GetValue(0) * multiplier;
                        long raw = RoundContent(amount * Math.Pow(10, pair.Value.Precision));
                        if (raw > 0) seeds.Add(new SeedEntry(pair.Value, raw, EconomyFormType.Stack));
                        if (pair.Key.Attribute == global::Attribute.Health)
                            seeds.Add(new SeedEntry(health, raw, EconomyFormType.Stack));
                    }
                    bindings.Add(binding);
                    ConfigureCharacterAttributeSkills(source, form, definitionName, attributes, step);
                    binding.Skills.Add(new ChainRushRunSkillBinding
                    {
                        ContentId = source.skills.Single(skill => skill.main).data.name,
                        Definition = LoadRequired<FrameworkSkillData>(SkillsRoot + "/" + definitionName + "Attack.asset")
                    });
                    EditorUtility.SetDirty(host);
                }
            }

            var buffs = new List<ChainRushRunBuffBinding>();
            var sourceBuffs = LoadRequired<BuffsData>("Assets/Game/Resources/BuffsData.asset");
            foreach (var type in new[] { global::Attribute.Power, global::Attribute.Defense,
                global::Attribute.Health, global::Attribute.Speed, global::Attribute.SkillSpeed })
            {
                var source = sourceBuffs.Get(type) ?? throw new InvalidOperationException("Missing source buff " + type);
                var buff = new ChainRushRunBuffBinding
                {
                    Attribute = type,
                    Cell = LoadRequired<CapabilityHostData>(BoardRoot + "/Economy/" + type + "BoardBase.asset")
                };
                foreach (Grade grade in Enum.GetValues(typeof(Grade)))
                    buff.Grades.Add(source.GetValue(grade));
                buffs.Add(buff);
            }
            SetField(feature, "board", LoadRequired<ActivityData>(BoardActivityPath));
            SetField(feature, "health", health);
            SetField(feature, "unitWalletTags", new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(UnitWalletTagPath) });
            SetField(feature, "characters", bindings);
            SetField(feature, "buffs", buffs);
            WriteEnemyAttributeSeeds(attributes, definitions, step);
            foreach (var path in new[] { ActivityPath, DistanceActivityPath })
            {
                var activity = LoadRequired<ActivityData>(path);
                AddUnique(activity.Teams[0].Features, feature);
                EditorUtility.SetDirty(activity);
            }
            ConfigureHealthAttributeCap(attributes[new ElementalAttribute(Element.Any, global::Attribute.Health)]);
            ConfigureSkillSpeedMapping(attributes[new ElementalAttribute(Element.Any, global::Attribute.SkillSpeed)]);
            var economyRuntime = LoadRequired<EconomyRuntimeInstallerData>(EconomyRuntimeInstallerPath);
            var domains = GetField<List<EconomyDomainType>>(economyRuntime, "domains");
            if (!domains.Contains(EconomyDomainType.Attribute)) domains.Add(EconomyDomainType.Attribute);
            EditorUtility.SetDirty(economyRuntime);
            EditorUtility.SetDirty(installer);
            EditorUtility.SetDirty(health);
            EditorUtility.SetDirty(feature);
        }

        static Dictionary<ElementalAttribute, FrameworkAttributeData> WriteRunAttributes(List<EconomyAssetData> definitions)
        {
            var result = new Dictionary<ElementalAttribute, FrameworkAttributeData>();
            foreach (var type in new[] { global::Attribute.Power, global::Attribute.Defense,
                global::Attribute.Health, global::Attribute.Speed, global::Attribute.SkillSpeed })
            {
                foreach (Element element in Enum.GetValues(typeof(Element)))
                {
                    bool elemental = type == global::Attribute.Power || type == global::Attribute.Defense;
                    if (elemental ? element == Element.Any : element != Element.Any) continue;
                    string name = type + (element == Element.Any ? "" : element.ToString());
                    var asset = ChainRushBoardPlannerAuthoring.WriteContentAsset<FrameworkAttributeData>(
                        RunAttributesRoot + "/" + name + ".asset", null,
                        "chainrush.attribute." + name.ToLowerInvariant(), definitions);
                    SetField(asset, "allowedOperations", AllMutableOperations);
                    SetField(asset, "precision", CombatValuePrecision);
                    SetField(asset, "valueType", type == global::Attribute.Health || type == global::Attribute.Defense
                        ? AttributeValueType.Absolute : AttributeValueType.Ratio);
                    SetField(asset, "referenceValue", 1f);
                    result.Add(new ElementalAttribute(element, type), asset);
                }
            }
            return result;
        }

        static void ConfigureCharacterAttributeSkills(UnitData source, int form, string name,
            Dictionary<ElementalAttribute, FrameworkAttributeData> attributes, double step)
        {
            var main = source.skills.Single(skill => skill.main);
            int level = main.syncWithMergeState ? form : 0;
            var sourceAttack = main.data.prefab.GetComponent<AttackSkill>();
            if (sourceAttack == null) throw new InvalidOperationException("Missing source attack " + source.name);
            var attack = LoadRequired<FrameworkSkillData>(SkillsRoot + "/" + name + "Attack.asset");
            SetField(attack, "reloadTime", ContentDuration(main.data.GetParameterValue(
                SkillParameterType.Duration, level, sourceAttack.Weapon.TimeBetweenUses), step));
            long damage = RoundContent(main.data.GetParameterValue(SkillParameterType.Amount, level, 0) * CombatValueScale);
            var damageSkill = sourceAttack.Weapon is MoreMountains.TopDownEngine.MeleeWeapon meleeSource
                ? ConfigurePlayerMeleeAttack(attack, main.data, level, name, meleeSource, damage, step)
                : LoadRequired<FrameworkSkillData>(SkillsRoot + "/" + name + "Hit.asset");
            ConfigureElementalAttack(attack, damageSkill, damage, attributes);
            if (sourceAttack.Weapon is MoreMountains.TopDownEngine.MeleeWeapon melee)
                SetDamageProtection(damageSkill, melee.MeleeDamageAreaMode == MoreMountains.TopDownEngine.MeleeWeapon.MeleeDamageAreaModes.Existing
                    ? melee.ExistingDamageArea.InvincibilityDuration : melee.InvincibilityDuration, step);
            else
            {
                string projectileName = source.name == "ColaData" ? "DaggerProjectile" : "TurretKettleProjectile";
                var touch = LoadRequired<GameObject>("Assets/Game/Prefabs/Skills/" + projectileName + ".prefab")
                    .GetComponent<MoreMountains.TopDownEngine.DamageOnTouch>();
                if (touch == null) throw new InvalidOperationException("Missing source projectile damage: " + source.name);
                SetDamageProtection(damageSkill, touch.InvincibilityDuration, step);
                var spawn = attack.Effects.OfType<SkillSpawnCarrierEffectData>().Single();
                ConfigureProjectileContact(spawn, touch.gameObject, step);
                ConfigurePlayerProjectileParameters(spawn, damageSkill, main.data, level, touch, step);
            }
            EditorUtility.SetDirty(damageSkill);
            if (source.type != UnitType.Hero)
            {
                var approach = LoadRequired<FrameworkSkillData>(SkillsRoot + "/" + name + "Approach.asset");
                SetField(approach.Effects[0], "value", RoundContent(1000 * step));
                SetField(approach.Effects[0], "multiplier", attributes[new ElementalAttribute(Element.Any, global::Attribute.Speed)]);
                EditorUtility.SetDirty(approach);
            }
            EditorUtility.SetDirty(attack);
        }

        static string CarrierDamagePath(Element element) => HostValuesRoot + "/CarrierDamage" + element + ".asset";

        static void ConfigureElementalAttack(FrameworkSkillData attack, FrameworkSkillData damageSkill, long damage,
            Dictionary<ElementalAttribute, FrameworkAttributeData> attributes)
        {
            var health = LoadRequired<HostValueData>(HealthPath);
            int index = damageSkill.Effects.FindIndex(value => value is SkillHostValueEffectData effect && effect.HostValue == health);
            if (index < 0) throw new InvalidOperationException("A combat skill has no damage effect: " + damageSkill.name);
            damageSkill.Effects.RemoveAll(value => value is SkillHostValueEffectData effect && effect.HostValue == health);
            var spawn = attack.Effects.OfType<SkillSpawnCarrierEffectData>().SingleOrDefault();
            if (spawn != null) spawn.Parameters.RemoveAll(value => value is SkillCarrierHostValueParameterValueData);
            foreach (Element element in Enum.GetValues(typeof(Element)))
            {
                if (element == Element.Any) continue;
                var power = attributes[new ElementalAttribute(element, global::Attribute.Power)];
                var defense = attributes[new ElementalAttribute(element, global::Attribute.Defense)];
                var effect = CreateElementalDamage(damage, power, defense);
                if (spawn != null)
                {
                    var capturedDamage = LoadRequired<HostValueData>(CarrierDamagePath(element));
                    var parameter = new SkillCarrierHostValueParameterValueData();
                    SetField(parameter, "value", capturedDamage);
                    SetField(parameter, "minValue", damage);
                    SetField(parameter, "maxValue", damage);
                    SetField(parameter, "ownerMultiplier", new AttributeSelectorData(power));
                    SetField(parameter, "allowMissingMultiplier", true);
                    SetField(parameter, "missingMultiplierValue", 0L);
                    spawn.Parameters.Add(parameter);
                    SetField(effect, "value", -1L);
                    var captured = new SkillHostValueFormulaTermData();
                    SetField(captured, "operation", MathOperation.Set);
                    SetField(captured, "parameterTarget", SkillParameterTargetType.Carrier);
                    SetField(captured, "operandType", SkillHostValueFormulaOperandType.HostValue);
                    SetField(captured, "value", capturedDamage);
                    SetField(captured, "stopWhenNonPositive", true);
                    effect.Formula.Terms[0] = captured;
                }
                damageSkill.Effects.Insert(index++, effect);
            }
        }

        static void WriteCarrierDamageValue(Element element, List<EconomyAssetData> definitions)
        {
            var value = ChainRushBoardPlannerAuthoring.WriteContentAsset<HostValueData>(CarrierDamagePath(element),
                null, "chainrush.host-value.carrier-damage." + element.ToString().ToLowerInvariant(), definitions);
            SetField(value, "precision", CombatValuePrecision);
            SetField(value, "allowedOperations", AllMutableOperations);
            var installer = LoadRequired<GameplayHostValuesInstallerData>(HostValuesInstallerPath);
            AddUnique(GetField<List<HostValueData>>(installer, "values"), value);
            var configs = GetField<List<HostValueDefinitionData>>(installer, "definitions");
            configs.RemoveAll(config => config.Value == value);
            var definition = new HostValueDefinitionData();
            SetField(definition, "value", value);
            configs.Add(definition);
            EditorUtility.SetDirty(installer);
        }

        static void ConfigureHealthAttributeCap(FrameworkAttributeData maximum)
        {
            var installer = LoadRequired<GameplayHostValuesInstallerData>(HostValuesInstallerPath);
            var definition = GetField<List<HostValueDefinitionData>>(installer, "definitions")
                .Single(value => value.Value == LoadRequired<HostValueData>(HealthPath));
            SetField(definition.MaxCap, "capAttribute", new AttributeSelectorData(maximum));
            SetField(definition.MaxCap, "boundryPolicy", HostValueBoundryPolicy.Clamp);
            EditorUtility.SetDirty(installer);
        }

        static void ConfigureSkillSpeedMapping(FrameworkAttributeData speed)
        {
            var timedSkill = WriteCombatTerm("AttributeTimedSkill", LoadRequired<TaxonomyTermData>(CombatNodePath));
            var installer = ChainRushBoardPlannerAuthoring.WriteContentAsset<GameplaySkillPropertyMappingsInstallerData>(
                "Assets/Game/Runtime/Installers/ChainRushSkillPropertyMappingsInstaller.asset", null);
            var mappings = new List<AttributeSkillPropertyMappingData>();
            foreach (SkillPropertyType property in new[] { SkillPropertyType.ActionInterval, SkillPropertyType.ReloadTime })
            {
                var mapping = new AttributeSkillPropertyMappingData();
                SetField(mapping, "property", property);
                SetField(mapping, "attribute", new AttributeSelectorData(speed));
                SetField(mapping, "defaultValue", 1L);
                SetField(mapping, "scaleType", SkillPropertyScaleType.Duration);
                SetField(mapping, "requiredSkillTags", new List<TaxonomyTermData> { timedSkill });
                mappings.Add(mapping);
            }
            SetField(installer, "mappings", mappings);
            var profile = LoadRequired<GameRuntimeProfileData>("Assets/Game/Runtime/Host/ChainRushGameRuntimeProfile.asset");
            AddUnique(GetField<List<GameRuntimeInstallerData>>(profile, "installers"), installer);
            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(installer);
        }

        internal static void ApplySkillTiming()
        {
            var tag = LoadRequired<TaxonomyTermData>(TaxonomyRoot + "/AttributeTimedSkill.asset");
            foreach (string name in new[] { "Water", "Cola", "Perfume", "Tabasco" })
            {
                var source = LoadRequired<UnitData>("Assets/Game/Resources/Units/" + name + "Data.asset");
                int count = source.type == UnitType.Hero ? 1 : source.MergeStatesCount;
                for (int form = 0; form < count; form++)
                {
                    string host = source.type == UnitType.Hero ? name : name + "Unit" + (form == 0 ? "" : (form + 1).ToString());
                    var skill = LoadRequired<FrameworkSkillData>(SkillsRoot + "/" + host + "Attack.asset");
                    AddUnique(skill.Tags, tag); EditorUtility.SetDirty(skill);
                }
            }
            var lightning = LoadRequired<global::SkillData>("Assets/Game/Resources/Skills/SkillLightningBolt.asset");
            for (int level = 0; level < lightning.LevelsCount; level++)
            {
                var skill = LoadRequired<FrameworkSkillData>(SkillsRoot + "/LightningBolt" + (level + 1) + ".asset");
                AddUnique(skill.Tags, tag); EditorUtility.SetDirty(skill);
            }
        }

        static void WriteEnemyAttributeSeeds(Dictionary<ElementalAttribute, FrameworkAttributeData> attributes,
            List<EconomyAssetData> definitions, double step)
        {
            var levels = new[]
            {
                LoadRequired<LevelData>("Assets/Game/Resources/Levels/Location001/Loc001Lvl01Data.asset"),
                LoadRequired<LevelData>("Assets/Game/Resources/Levels/Location001/Loc001Lvl02Data.asset")
            };
            var skillInstaller = LoadRequired<GameplaySkillsInstallerData>(SkillsInstallerPath);
            var skills = GetField<List<FrameworkSkillData>>(skillInstaller, "skills");
            var elements = LoadRequired<ElementsData>("Assets/Game/Resources/ElementsData.asset");
            foreach (var source in levels.SelectMany(level => level.enemyData.enemyProportions.Values)
                .SelectMany(shares => shares.Keys).Distinct())
            {
                var host = LoadRequired<CapabilityHostData>(EconomyRoot + "/" + source.name + ".asset");
                var sourceHealth = source.GetComponent<MoreMountains.TopDownEngine.Health>();
                var movement = source.GetComponentInChildren<MoreMountains.TopDownEngine.CharacterMovement>(true);
                var touch = source.GetComponentInChildren<MoreMountains.TopDownEngine.DamageOnTouch>(true);
                if (sourceHealth == null || movement == null || touch == null)
                    throw new InvalidOperationException("Missing enemy health, movement or contact damage: " + source.name);
                long maximum = RoundContent(sourceHealth.MaximumHealth * CombatValueScale);
                var seeds = GetField<List<WalletEntry>>(host, "walletEntries").Single(
                    entry => entry.Wallet == LoadRequired<EconomyWalletData>(UnitWalletPath)).Seed;
                seeds.RemoveAll(seed => seed.Asset is FrameworkAttributeData || seed.Asset == LoadRequired<HostValueData>(HealthPath));
                seeds.Add(new SeedEntry(LoadRequired<HostValueData>(HealthPath), maximum, EconomyFormType.Stack));
                seeds.Add(new SeedEntry(attributes[new ElementalAttribute(Element.Any, global::Attribute.Health)], maximum, EconomyFormType.Stack));
                seeds.Add(new SeedEntry(attributes[new ElementalAttribute(Element.Any, global::Attribute.Speed)],
                    RoundContent(movement.WalkSpeed * CombatValueScale), EconomyFormType.Stack));
                seeds.Add(new SeedEntry(attributes[new ElementalAttribute(Element.Any, global::Attribute.SkillSpeed)], CombatValueScale, EconomyFormType.Stack));
                foreach (Element element in Enum.GetValues(typeof(Element)))
                    if (element != Element.Any)
                        seeds.Add(new SeedEntry(attributes[new ElementalAttribute(element, global::Attribute.Power)], CombatValueScale, EconomyFormType.Stack));

                var approach = ChainRushBoardPlannerAuthoring.WriteContentAsset(SkillsRoot + "/" + source.name + "Approach.asset",
                    LoadRequired<FrameworkSkillData>(ApproachSkillPath), host.Id + ".approach", definitions);
                SetField(approach.Effects[0], "value", RoundContent(1000 * step));
                SetField(approach.Effects[0], "multiplier", attributes[new ElementalAttribute(Element.Any, global::Attribute.Speed)]);
                var attack = WriteContentSkill(SkillsRoot + "/" + source.name + "Contact.asset", host.Id + ".contact", definitions, skills);
                SetField(attack, "reloadTime", ContentDuration(touch.InvincibilityDuration, step));
                var effects = new List<SkillEffectData>();
                if (touch.MinDamageCaused != touch.MaxDamageCaused)
                    throw new InvalidOperationException("Enemy contact damage requires an explicitly authored random range: " + source.name);
                if (touch.MinDamageCaused > 0)
                    effects.Add(CreateElementalDamage(RoundContent(touch.MinDamageCaused * CombatValueScale),
                        attributes[new ElementalAttribute(Element.Physical, global::Attribute.Power)],
                        attributes[new ElementalAttribute(Element.Physical, global::Attribute.Defense)]));
                foreach (var damage in touch.TypedDamages)
                {
                    if (damage.MinDamageCaused != damage.MaxDamageCaused)
                        throw new InvalidOperationException("Enemy typed contact damage requires an explicitly authored random range: " + source.name);
                    var element = elements.data.Single(pair => pair.Value.damageType == damage.AssociatedDamageType).Key;
                    effects.Add(CreateElementalDamage(RoundContent(damage.MinDamageCaused * CombatValueScale),
                        attributes[new ElementalAttribute(element, global::Attribute.Power)],
                        attributes[new ElementalAttribute(element, global::Attribute.Defense)]));
                }
                SetField(attack, "effects", effects);
                SetDamageProtection(attack, touch.InvincibilityDuration, step);
                var requirement = new SkillTargetDistanceRequirementData();
                SetField(requirement, "distance", 1000);
                SetField(requirement, "compareOperation", CompareOperation.LessOrEqual);
                SetField(requirement, "targetGeometryType", Core.World.WorldTargetGeometryType.Spatial);
                SetField(attack, "requirements", new List<SkillRequirementData> { requirement });
                var brain = ChainRushBoardPlannerAuthoring.WriteContentAsset(AIRoot + "/" + source.name + "Brain.asset",
                    LoadRequired<Core.AI.AIBrainData>(EnemyCombatBrainPath), host.Id + ".brain", definitions);
                foreach (var node in brain.Nodes)
                    foreach (var state in node.States)
                        foreach (var action in state.OnTickActions)
                        {
                            if (action is Core.AI.Actions.UseSkillAIBrainActionData)
                            {
                                var old = GetField<FrameworkSkillData>(action, "skill");
                                if (old == LoadRequired<FrameworkSkillData>(ApproachSkillPath)) SetField(action, "skill", approach);
                                if (old == LoadRequired<FrameworkSkillData>(AttackSkillPath)) SetField(action, "skill", attack);
                            }
                            if (action is Core.AI.Actions.SelectEntityTargetByQueryAIBrainActionData)
                                SetField(action, "compatibleSkill", attack);
                            if (action is SelectActivityTargetAIBrainActionData)
                                SetField(action, "compatibleSkill", attack);
                        }
                seeds.RemoveAll(seed => seed.Asset is Core.AI.AIBrainData || seed.Asset is FrameworkSkillData);
                seeds.Add(new SeedEntry(brain, 1, EconomyFormType.Stack, new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(CombatNodePath) }));
                seeds.Add(new SeedEntry(approach, 1, EconomyFormType.Stack));
                seeds.Add(new SeedEntry(attack, 1, EconomyFormType.Stack));
                AddUnique(skills, approach);
                EditorUtility.SetDirty(brain);
                EditorUtility.SetDirty(approach);
                EditorUtility.SetDirty(attack);
                EditorUtility.SetDirty(host);
            }
            EditorUtility.SetDirty(skillInstaller);
        }

        static SkillHostValueEffectData CreateElementalDamage(long amount, FrameworkAttributeData power, FrameworkAttributeData defense)
        {
            var effect = ContentDamage(amount);
            var source = new SkillHostValueFormulaTermData();
            SetField(source, "operation", MathOperation.Multiply);
            SetField(source, "parameterTarget", SkillParameterTargetType.Owner);
            SetField(source, "operandType", SkillHostValueFormulaOperandType.Attribute);
            SetField(source, "attribute", new AttributeSelectorData(power));
            SetField(source, "allowMissingAttribute", true);
            SetField(source, "missingAttributeValue", 0L);
            SetField(source, "stopWhenNonPositive", true);
            var term = new SkillHostValueFormulaTermData();
            SetField(term, "operation", MathOperation.Substract);
            SetField(term, "parameterTarget", SkillParameterTargetType.Target);
            SetField(term, "operandType", SkillHostValueFormulaOperandType.Attribute);
            SetField(term, "attribute", new AttributeSelectorData(defense));
            SetField(term, "allowMissingAttribute", true);
            SetField(term, "missingAttributeValue", 0L);
            var formula = new SkillHostValueFormulaData();
            formula.Terms.Add(source);
            formula.Terms.Add(term);
            SetField(formula, "minimumMagnitude", CombatValueScale);
            SetField(effect, "formula", formula);
            return effect;
        }

        static void SetDamageProtection(FrameworkSkillData skill, double seconds, double step)
        {
            int duration = checked((int)ContentDuration(seconds, step));
            foreach (var effect in skill.Effects.OfType<SkillHostValueEffectData>())
            {
                if (effect.HostValue != LoadRequired<HostValueData>(HealthPath) || effect.Value >= 0) continue;
                SkillHostValueProtectionData protection = null;
                if (duration > 0)
                {
                    protection = new SkillHostValueProtectionData();
                    SetField(protection, "tag", LoadRequired<TaxonomyTermData>(DamageProtectionPath));
                    SetField(protection, "duration", duration);
                }
                SetField(effect, "protection", protection);
            }
        }
    }
}
