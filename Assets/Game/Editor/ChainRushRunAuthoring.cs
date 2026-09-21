using System;
using System.Linq;
using ChainRush.Gameplay;
using Core.Activities;
using Core.CapabilityHosts;
using Core.Economy;
using Core.Economy.Modules.ResourceEconomyModule;
using Core.GameRuntime;
using Core.GameRuntime.Installers;
using Core.Orchestration;
using Core.Production.Authoring;
using UnityEditor;
using UnityEngine;

namespace ChainRush.Editor
{
    public static class ChainRushRunAuthoring
    {
        const string Root = "Assets/Game/Runtime/Run";

        [MenuItem("Tools/ChainRush/Authoring/Apply Run Inputs")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run authoring requires Edit Mode.");

            ChainRushRunSelectionData survive = WriteSelection("Survive", "Loc001Lvl01Data");
            WriteSelection("Distance", "Loc001Lvl02Data");
            var action = ChainRushBoardPlannerAuthoring.WriteContentAsset<CaptureChainRushRunActionData>(
                "Assets/Game/Runtime/Startup/CaptureChainRushRun.asset", null);
            var actionData = new SerializedObject(action);
            actionData.FindProperty("selection").objectReferenceValue = survive;
            actionData.ApplyModifiedPropertiesWithoutUndo();

            var plan = AssetDatabase.LoadAssetAtPath<GameStartupPlanData>(
                "Assets/Game/Runtime/Startup/ChainRushGameStartupPlan.asset");
            if (plan == null) throw new InvalidOperationException("Integration startup plan is missing.");
            var data = new SerializedObject(plan);
            SerializedProperty actions = data.FindProperty("actions");
            var existing = plan.Actions.Where(value => value != action).ToList();
            if (existing.Any(value => value is CaptureChainRushRunActionData))
                throw new InvalidOperationException("Startup plan already uses another run capture action.");
            actions.arraySize = existing.Count + 1;
            actions.GetArrayElementAtIndex(0).objectReferenceValue = action;
            for (int i = 0; i < existing.Count; i++)
                actions.GetArrayElementAtIndex(i + 1).objectReferenceValue = existing[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyHeroContent();
            WriteProgressFeature();
            ChainRushBoardPlannerAuthoring.ApplyRunBoardContent();
            var population = Require<Core.Orchestration.AgentDefinitionData>(
                "Assets/Game/Activities/Board/Agents/BoardPopulationAgent.asset");
            var populationData = new SerializedObject(population);
            populationData.FindProperty("agent").FindPropertyRelative("completionPolicy").enumValueIndex =
                (int)Core.Orchestration.PopulationCompletionPolicyType.RequireFullVolume;
            populationData.ApplyModifiedPropertiesWithoutUndo();
            WriteBoardContent((PopulationAgentData)population.Agent, population);
            ChainRushBoardPlannerAuthoring.ApplyRunShapes();
            ChainRushBoardPlannerAuthoring.ConfigureGoldSelection();
            var drop = Require<CapabilityHostData>("Assets/Game/Activities/Autobattle/Economy/ExperienceDrop.asset");
            var dropData = new SerializedObject(drop);
            dropData.FindProperty("projectionPool").FindPropertyRelative("maxCapacity").intValue = 64;
            dropData.ApplyModifiedPropertiesWithoutUndo();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyLevelFlows();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyLevelPopulation();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyRunAttributes();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyEnemyWeapons();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyExperienceDrops();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyHeroSkills();
            ChainRushAutobattleVerticalSliceAuthoring.ApplySkillTiming();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyHeroRoute();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyMovementForces();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyAlliedAI();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyRunDeployment();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyRunFollow();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyEnemyContacts();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyRunGeometry();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyHealing();
            ChainRushAutobattleVerticalSliceAuthoring.ApplyHeroLevelFlows();
            AssetDatabase.SaveAssets();
            ChainRushGameplayContentRegistry.Export();
            Debug.Log("Run input working model authored for Survive 300 and Distance 150. Integration startup captures Survive before either game flow starts.");
        }

        static void WriteBoardContent(PopulationAgentData population, AgentDefinitionData definition)
        {
            const string board = "Assets/Game/Activities/Board/";
            var eligibility = WriteBoardRosterEligibility();
            var progress = Require<Core.Economy.Modules.ResourceEconomyModule.ResourceData>(
                "Assets/Game/Activities/Shared/Economy/LevelProgress.asset");
            var wallet = Require<Core.Taxonomy.TaxonomyTermData>(
                "Assets/Game/Activities/Shared/Economy/ActivityWalletTag.asset");
            var sources = new[]
            {
                (Name: "Unit", Catalog: "Board", Share: 0.175f, Cooldown: 240L),
                (Name: "Buff", Catalog: "Buffs", Share: 0.175f, Cooldown: 150L),
                (Name: "HeroSkill", Catalog: "Skills", Share: 0.175f, Cooldown: 180L),
                (Name: "Heal", Catalog: "Boosters", Share: 0.175f, Cooldown: 450L),
                (Name: "Gold", Catalog: "Gold", Share: 0.3f, Cooldown: 0L)
            };
            var content = new System.Collections.Generic.List<PopulationContentRuleData>();
            foreach (var source in sources)
            {
                // Included source levels use constant 8/5/6/15 second cooldowns. Board simulation is 30 Hz.
                var availability = ChainRushBoardPlannerAuthoring.WriteContentAsset<PopulationContentAvailabilityData>(
                    board + "Agents/" + source.Name + "ContentAvailability.asset", null);
                var data = new SerializedObject(availability);
                var input = data.FindProperty("progress");
                input.FindPropertyRelative("resource").objectReferenceValue = progress;
                input.FindPropertyRelative("scaleType").enumValueIndex = (int)PopulationProgressScaleType.Relative;
                input.FindPropertyRelative("relativeBase").longValue = 1000000;
                var tags = input.FindPropertyRelative("walletTags");
                tags.arraySize = 1;
                tags.GetArrayElementAtIndex(0).objectReferenceValue = wallet;
                data.FindProperty("cooldown").managedReferenceValue = new Core.LongFlatProgressionData(source.Cooldown);
                data.FindProperty("initialElapsed").longValue = source.Cooldown;
                data.ApplyModifiedPropertiesWithoutUndo();
                content.Add(new PopulationContentRuleData(new PopulationCatalogContentSourceData(
                    Require<ProductionCatalogData>(board + "Production/" + source.Catalog + "PopulationCatalog.asset"),
                    PopulationCatalogSelectionType.DeterministicRandom,
                    source.Name == "Unit" || source.Name == "HeroSkill" ? eligibility : null), source.Share, availability));
            }
            var definitionData = new SerializedObject(definition);
            var releases = definitionData.FindProperty("agent").FindPropertyRelative("releases");
            if (releases.arraySize != 1) throw new InvalidOperationException("Board run authoring expects its explicit continuous release.");
            var rules = releases.GetArrayElementAtIndex(0).FindPropertyRelative("content");
            rules.arraySize = content.Count;
            for (int i = 0; i < content.Count; i++)
            {
                var rule = rules.GetArrayElementAtIndex(i);
                rule.FindPropertyRelative("source").managedReferenceValue = content[i].Source;
                rule.FindPropertyRelative("share").floatValue = content[i].Share;
                rule.FindPropertyRelative("availability").objectReferenceValue = content[i].Availability;
            }
            definitionData.ApplyModifiedPropertiesWithoutUndo();
            foreach (var rule in population.Releases[0].Content)
                if (!rule.TryValidate(out string failure)) throw new InvalidOperationException(failure);
        }

        static ChainRushBoardRosterEligibilityData WriteBoardRosterEligibility()
        {
            var eligibility = ChainRushBoardPlannerAuthoring.WriteContentAsset<ChainRushBoardRosterEligibilityData>(
                Root + "/BoardRosterEligibility.asset", null);
            var data = new SerializedObject(eligibility);
            var bindings = data.FindProperty("bindings");
            var content = new[]
            {
                (Name: "Water", Id: "WaterData", Type: ChainRushBoardRosterContentType.Unit),
                (Name: "Cola", Id: "ColaData", Type: ChainRushBoardRosterContentType.Unit),
                (Name: "LightningBolt", Id: "SkillLightningBolt", Type: ChainRushBoardRosterContentType.HeroSkill)
            };
            bindings.arraySize = content.Length;
            for (int i = 0; i < content.Length; i++)
            {
                var binding = bindings.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("Output").objectReferenceValue = Require<CapabilityHostData>(
                    "Assets/Game/Activities/Board/Economy/" + content[i].Name + "BoardBase.asset");
                binding.FindPropertyRelative("ContentId").stringValue = content[i].Id;
                binding.FindPropertyRelative("Type").enumValueIndex = (int)content[i].Type;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            if (!eligibility.TryValidate(out string failure)) throw new InvalidOperationException(failure);
            return eligibility;
        }

        static ChainRushRunSelectionData WriteSelection(string name, string level)
        {
            var selection = ChainRushBoardPlannerAuthoring.WriteContentAsset<ChainRushRunSelectionData>(
                Root + "/" + name + "RunSelection.asset", null);
            var data = new SerializedObject(selection);
            data.FindProperty("level").objectReferenceValue = Require<LevelData>(
                "Assets/Game/Resources/Levels/Location001/" + level + ".asset");
            WriteCharacter(data.FindProperty("hero"), "Perfume");
            SerializedProperty units = data.FindProperty("units");
            units.arraySize = 2;
            WriteCharacter(units.GetArrayElementAtIndex(0), "Water");
            WriteCharacter(units.GetArrayElementAtIndex(1), "Cola");
            data.ApplyModifiedPropertiesWithoutUndo();
            selection.Capture(12345);
            return selection;
        }

        static void WriteProgressFeature()
        {
            const string shared = "Assets/Game/Activities/Shared/Economy/";
            var progress = ChainRushBoardPlannerAuthoring.WriteContentAsset(
                shared + "LevelProgress.asset", Require<Core.Economy.Modules.ResourceEconomyModule.ResourceData>(shared + "Experience.asset"));
            var resourceData = new SerializedObject(progress);
            resourceData.FindProperty("id").stringValue = "chainrush.resource.level-progress";
            resourceData.FindProperty("allowedOperations").intValue = (int)(EconomyOperation.Require | EconomyOperation.Issue | EconomyOperation.Consume);
            resourceData.ApplyModifiedPropertiesWithoutUndo();
            var installer = Require<EconomyDefinitionsInstallerData>(
                "Assets/Game/Runtime/Installers/ChainRushEconomyDefinitionsInstaller.asset");
            var installerData = new SerializedObject(installer);
            AddReference(installerData.FindProperty("assets"), progress);
            installerData.ApplyModifiedPropertiesWithoutUndo();

            var feature = ChainRushBoardPlannerAuthoring.WriteContentAsset<ChainRushRunProgressFeatureData>(
                Root + "/RunProgressFeature.asset", null);
            var featureData = new SerializedObject(feature);
            featureData.FindProperty("progress").objectReferenceValue = progress;
            featureData.FindProperty("progressResolution").longValue = 1000000;
            featureData.FindProperty("distanceScale").floatValue = 1;
            SerializedProperty wallets = featureData.FindProperty("walletTags");
            wallets.arraySize = 1;
            wallets.GetArrayElementAtIndex(0).objectReferenceValue = Require<Core.Taxonomy.TaxonomyTermData>(shared + "ActivityWalletTag.asset");
            SerializedProperty heroes = featureData.FindProperty("heroes");
            heroes.arraySize = 2;
            var heroNames = new[] { "Perfume", "Tabasco" };
            for (int i = 0; i < heroNames.Length; i++)
            {
                heroes.GetArrayElementAtIndex(i).FindPropertyRelative("contentId").stringValue = heroNames[i] + "Data";
                heroes.GetArrayElementAtIndex(i).FindPropertyRelative("definition").objectReferenceValue = Require<CapabilityHostData>(
                    "Assets/Game/Activities/Shared/Units/" + heroNames[i] + "/" + heroNames[i] + ".asset");
            }
            featureData.ApplyModifiedPropertiesWithoutUndo();
            var activity = Require<ActivityData>("Assets/Game/Activities/Autobattle/Definition/AutobattleActivity.asset");
            var activityData = new SerializedObject(activity);
            AddReference(activityData.FindProperty("teams").GetArrayElementAtIndex(0).FindPropertyRelative("features"), feature);
            activityData.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddReference(SerializedProperty list, UnityEngine.Object value)
        {
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == value) return;
            int index = list.arraySize;
            list.arraySize++;
            list.GetArrayElementAtIndex(index).objectReferenceValue = value;
        }

        static void WriteCharacter(SerializedProperty target, string name)
        {
            UnitData definition = Require<UnitData>("Assets/Game/Resources/Units/" + name + "Data.asset");
            target.FindPropertyRelative("definition").objectReferenceValue = definition;
            target.FindPropertyRelative("level").intValue = 0;
            target.FindPropertyRelative("equipment").arraySize = 0;
            var skills = definition.skills.Where(value => value != null && value.defaultSkill).ToList();
            SerializedProperty acquired = target.FindPropertyRelative("acquiredSkills");
            acquired.arraySize = skills.Count;
            for (int i = 0; i < skills.Count; i++)
                acquired.GetArrayElementAtIndex(i).objectReferenceValue = skills[i].data;
        }

        static T Require<T>(string path) where T : UnityEngine.Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            return value != null ? value : throw new InvalidOperationException("Missing run source: " + path);
        }
    }
}
