using System.Collections.Generic;
using System.Linq;
using ChainRush.Gameplay;
using Core.Activities;
using Core.Attributes;
using Core.CapabilityHosts;
using Core.Economy;
using Core.GameRuntime.Installers;
using Core.HostValues;
using Core.Skills;
using Core.Taxonomy;
using Core.World;
using UnityEditor;
using FrameworkSkillData = Core.Skills.SkillData;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        internal static void ApplyHeroRoute()
        {
            var economy = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(economy, "assets");
            var installer = LoadRequired<GameplaySkillsInstallerData>(SkillsInstallerPath);
            var skills = GetField<List<FrameworkSkillData>>(installer, "skills");
            var activity = LoadRequired<ActivityData>(DistanceActivityPath);
            double step = GetField<float>(activity.Schedule, "tickDelta");
            var skill = WriteContentSkill(SkillsRoot + "/HeroRoute.asset", "chainrush.skill.hero-route", definitions, skills);
            SetField(skill, "targetType", SkillTargetType.Position);
            SetField(skill, "targetTags", new List<TaxonomyTermData>());
            SetField(skill, "requirements", new List<SkillRequirementData>());
            var effect = new SkillMoveToTargetEffectData();
            SetField(effect, "recipient", EffectRecipient.Owner);
            SetField(effect, "value", RoundContent(1000 * step));
            SetField(effect, "multiplier", LoadRequired<Core.Attributes.AttributeData>(RunAttributesRoot + "/Speed.asset"));
            SetField(effect, "targetGeometryType", WorldTargetGeometryType.None);
            SetField(skill, "effects", new List<SkillEffectData> { effect });
            var bindings = new List<ChainRushHeroDefinitionBinding>();
            foreach (string name in new[] { "Perfume", "Tabasco" })
            {
                var host = LoadRequired<CapabilityHostData>(SharedRoot + "/Units/" + name + "/" + name + ".asset");
                if (!host.Capabilities.Any(capability => capability.CapabilityType == CapabilityHostType.MovementOwner))
                    host.Capabilities.Add(CreateCapability(CapabilityHostType.MovementOwner));
                var seeds = host.WalletEntries.Single(entry => entry.Wallet == LoadRequired<EconomyWalletData>(UnitWalletPath)).Seed;
                var movement = LoadRequired<MovementData>(MovementPath);
                seeds.RemoveAll(seed => seed.Asset == skill || seed.Asset == movement);
                seeds.Add(new SeedEntry(movement, 1, EconomyFormType.Stack));
                seeds.Add(new SeedEntry(skill, 1, EconomyFormType.Stack));
                var binding = new ChainRushHeroDefinitionBinding();
                SetField(binding, "contentId", name + "Data");
                SetField(binding, "definition", host);
                bindings.Add(binding);
                EditorUtility.SetDirty(host);
            }
            var feature = ChainRushBoardPlannerAuthoring.WriteContentAsset<ChainRushHeroRouteFeatureData>(
                "Assets/Game/Runtime/Run/HeroRouteFeature.asset", null);
            SetField(feature, "heroes", bindings);
            SetField(feature, "movement", skill);
            SetField(feature, "health", LoadRequired<HostValueData>(HealthPath));
            AddUnique(activity.Teams[0].Features, feature);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(skill);
            EditorUtility.SetDirty(activity);
            EditorUtility.SetDirty(economy);
            EditorUtility.SetDirty(installer);
        }
    }
}
