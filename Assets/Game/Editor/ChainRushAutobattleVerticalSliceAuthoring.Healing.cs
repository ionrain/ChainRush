using System;
using System.Collections.Generic;
using System.Linq;
using ChainRush.Gameplay;
using Core;
using Core.Activities;
using Core.Attributes;
using Core.CapabilityHosts;
using Core.Economy;
using Core.GameRuntime.Installers;
using Core.HostValues;
using Core.Skills;
using Core.Taxonomy;
using UnityEditor;
using FrameworkSkillData = Core.Skills.SkillData;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        internal static void ApplyHealing()
        {
            var economy = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(economy, "assets");
            var installer = LoadRequired<GameplaySkillsInstallerData>(SkillsInstallerPath);
            var skills = GetField<List<FrameworkSkillData>>(installer, "skills");
            var maximum = LoadRequired<Core.Attributes.AttributeData>(RunAttributesRoot + "/Health.asset");
            var chainSkills = new List<FrameworkSkillData>();
            for (int count = 1; count <= 16; count++)
            {
                var skill = WriteContentSkill(SkillsRoot + "/Heal" + count + ".asset", "chainrush.skill.heal." + count, definitions, skills);
                SetField(skill, "targetType", SkillTargetType.Self);
                SetField(skill, "targetTags", new List<TaxonomyTermData>());
                SetField<TaxonomyTermData>(skill, "executionSlot", null);
                var effect = new SkillHostValueEffectData();
                SetField(effect, "hostValue", LoadRequired<HostValueData>(HealthPath));
                ConfigureEffect(effect, EffectRecipient.Owner, count * 100L);
                var term = new SkillHostValueFormulaTermData();
                SetField(term, "operation", MathOperation.Multiply);
                SetField(term, "operandType", SkillHostValueFormulaOperandType.Attribute);
                SetField(term, "parameterTarget", SkillParameterTargetType.Owner);
                SetField(term, "attribute", new AttributeSelectorData(maximum));
                var formula = new SkillHostValueFormulaData();
                formula.Terms.Add(term);
                SetField(effect, "formula", formula);
                skill.Effects.Add(effect);
                chainSkills.Add(skill);
                EditorUtility.SetDirty(skill);
            }
            var bindings = GetField<List<ChainRushRunCharacterBinding>>(
                LoadRequired<ChainRushRunAttributesFeatureData>("Assets/Game/Runtime/Run/RunAttributesFeature.asset"), "characters");
            foreach (var binding in bindings)
            {
                var seeds = binding.Definition.WalletEntries.Single(entry => entry.Wallet == LoadRequired<EconomyWalletData>(UnitWalletPath)).Seed;
                seeds.RemoveAll(seed => chainSkills.Contains(seed.Asset as FrameworkSkillData));
                foreach (var skill in chainSkills) seeds.Add(new SeedEntry(skill, 1, EconomyFormType.Stack));
                EditorUtility.SetDirty(binding.Definition);
            }
            bool unitsCanHeal = SourceCanBeHealed("Unit");
            foreach (var type in new[] { LevelGoalType.Survive, LevelGoalType.Distance })
            {
                bool heroCanHeal = SourceCanBeHealed(type == LevelGoalType.Survive ? "GateHero" : "NormalHero");
                var recipients = bindings.Where(binding => binding.ContentId == "PerfumeData" || binding.ContentId == "TabascoData"
                    ? heroCanHeal : unitsCanHeal).Select(binding => binding.Definition).ToList();
                var feature = ChainRushBoardPlannerAuthoring.WriteContentAsset<ChainRushHealFeatureData>(
                    "Assets/Game/Runtime/Run/" + type + "HealFeature.asset", null);
                SetField(feature, "board", LoadRequired<ActivityData>(BoardActivityPath));
                SetField(feature, "cell", LoadRequired<CapabilityHostData>(BoardRoot + "/Economy/HealBoardBase.asset"));
                SetField(feature, "health", LoadRequired<HostValueData>(HealthPath));
                SetField(feature, "recipients", recipients);
                SetField(feature, "chainSkills", new List<FrameworkSkillData>(chainSkills));
                var activity = LoadRequired<ActivityData>(type == LevelGoalType.Survive ? ActivityPath : DistanceActivityPath);
                activity.Teams[0].Features.RemoveAll(value => value is ChainRushHealFeatureData);
                AddUnique(activity.Teams[0].Features, feature);
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(activity);
            }
            EditorUtility.SetDirty(economy);
            EditorUtility.SetDirty(installer);
        }

        static bool SourceCanBeHealed(string prefab)
        {
            var source = LoadRequired<UnityEngine.GameObject>("Assets/Game/Prefabs/Units/" + prefab + ".prefab").GetComponent<Unit>();
            if (source == null) throw new InvalidOperationException("Missing source Unit on " + prefab);
            return new SerializedObject(source).FindProperty("canBeHealed").boolValue;
        }
    }
}
