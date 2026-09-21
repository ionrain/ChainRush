using System;
using System.Collections.Generic;
using ChainRush.Gameplay;
using Core.Activities;
using Core.CapabilityHosts;
using Core.Economy;
using Core.GameFlow;
using Core.GameFlow.GameRuntime.Installers;
using Core.GameRuntime.Installers;
using Core.HostValues;
using Core.Skills;
using Core.World;
using UnityEditor;
using FrameworkSkillData = Core.Skills.SkillData;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        internal static void ApplyHeroContent()
        {
            var economy = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(economy, "assets");
            var installer = LoadRequired<GameplaySkillsInstallerData>(SkillsInstallerPath);
            var skills = GetField<List<FrameworkSkillData>>(installer, "skills");
            double step = GetField<float>(LoadRequired<ActivityData>(ActivityPath).Schedule, "tickDelta");
            WriteUnitForms("Tabasco", 1, LoadRequired<InteractionGeometryData>(CombatGeometryPath),
                LoadRequired<HostValueData>(CarrierLivesPath), step, definitions, skills);
            EditorUtility.SetDirty(economy);
            EditorUtility.SetDirty(installer);
        }

        // Finite authored launch variants keep Activity seeds immutable and use its existing materialization lifecycle.
        internal static void ApplyHeroLevelFlows()
        {
            var economy = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(economy, "assets");
            var installer = LoadRequired<GameFlowDefinitionsInstallerData>(
                "Assets/Game/Runtime/Installers/ChainRushGameFlowDefinitionsInstaller.asset");
            var templates = GetField<List<GameFlowTemplateData>>(installer, "templates");
            var perfume = LoadRequired<CapabilityHostData>(PerfumePath);
            var tabasco = LoadRequired<CapabilityHostData>(SharedRoot + "/Units/Tabasco/Tabasco.asset");
            var bindings = new List<ChainRushLevelFlowBinding>();
            for (int i = 1; i <= 2; i++)
            {
                string levelId = LoadRequired<LevelData>("Assets/Game/Resources/Levels/Location001/Loc001Lvl0" + i + "Data.asset").Id;
                var sourceActivity = LoadRequired<ActivityData>(i == 1 ? ActivityPath : DistanceActivityPath);
                var sourceFlow = LoadRequired<GameFlowTemplateData>(i == 1 ? AutobattleFlowPath : DistanceFlowPath);
                bindings.Add(new ChainRushLevelFlowBinding(levelId, "PerfumeData", sourceFlow));
                var activity = ChainRushBoardPlannerAuthoring.WriteContentAsset(
                    AutobattleRoot + "/Definition/" + levelId + "TabascoActivity.asset", sourceActivity,
                    sourceActivity.Id + ".tabasco", definitions);
                int replacements = 0;
                foreach (var wallet in activity.Teams[0].Wallets)
                    for (int entry = 0; entry < wallet.Seed.Count; entry++)
                    {
                        var seed = wallet.Seed[entry];
                        var sourceHero = i == 1 ? perfume : LoadRequired<CapabilityHostData>(SharedRoot + "/Units/Perfume/PerfumeDistance.asset");
                        if (seed.Seed.Asset != sourceHero) continue;
                        wallet.Seed[entry] = new ActivityWalletSeedEntryData(
                            new SeedEntry(i == 1 ? tabasco : LoadRequired<CapabilityHostData>(SharedRoot + "/Units/Tabasco/TabascoDistance.asset"),
                                1, EconomyFormType.Token), seed.MaterializationType,
                            new List<Core.Taxonomy.TaxonomyTermData>(seed.ProjectionTargetTags),
                            new List<Core.Taxonomy.TaxonomyTermData>(seed.MaterializationMarkerTags));
                        replacements++;
                    }
                if (replacements != 1) throw new InvalidOperationException("An authored run must contain exactly one hero seed.");
                var flow = ChainRushBoardPlannerAuthoring.WriteContentAsset(
                    AutobattleRoot + "/GameFlow/" + levelId + "TabascoFlow.asset", sourceFlow);
                SetField(flow, "id", sourceFlow.Id + ".tabasco");
                var root = flow.Root as ActivityFlowContainerData
                    ?? throw new InvalidOperationException("Hero launch requires an Activity flow.");
                SetField(root, "activity", activity);
                foreach (var step in root.Steps)
                {
                    ReplaceFlowActivity(step.SuccessConditions, sourceActivity, activity);
                    ReplaceFlowActivity(step.FailConditions, sourceActivity, activity);
                }
                AddUnique(templates, flow);
                bindings.Add(new ChainRushLevelFlowBinding(levelId, "TabascoData", flow));
                EditorUtility.SetDirty(activity);
                EditorUtility.SetDirty(flow);
            }
            var start = LoadRequired<StartChainRushLevelActionData>(LevelStartupPath);
            SetField(start, "levels", bindings);
            EditorUtility.SetDirty(start);
            EditorUtility.SetDirty(economy);
            EditorUtility.SetDirty(installer);
        }
    }
}
