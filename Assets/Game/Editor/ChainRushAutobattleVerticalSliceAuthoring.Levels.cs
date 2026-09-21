using System;
using System.Collections.Generic;
using ChainRush.Gameplay;
using Core.Activities;
using Core.Economy;
using Core.GameFlow;
using Core.GameFlow.GameRuntime.Installers;
using Core.GameRuntime;
using Core.GameRuntime.Installers;
using UnityEditor;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        const string DistanceActivityPath = AutobattleRoot + "/Definition/DistanceActivity.asset";
        const string DistanceFlowPath = AutobattleRoot + "/GameFlow/DistanceFlow.asset";
        const string LevelStartupPath = "Assets/Game/Runtime/Startup/StartChainRushLevel.asset";
        const string FixedAutobattleStartupPath = "Assets/Game/Runtime/Startup/AddChainRushAutobattleFlow.asset";

        internal static void ApplyLevelFlows()
        {
            var source = LoadRequired<ActivityData>(ActivityPath);
            var economy = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(economy, "assets");
            var distance = ChainRushBoardPlannerAuthoring.WriteContentAsset(DistanceActivityPath, source,
                "chainrush.activity.autobattle.level02", definitions);
            var sourceFlow = LoadRequired<GameFlowTemplateData>(AutobattleFlowPath);
            var distanceFlow = ChainRushBoardPlannerAuthoring.WriteContentAsset(DistanceFlowPath, sourceFlow);
            SetField(distanceFlow, "id", "chainrush.flow.autobattle.level02");
            if (!(distanceFlow.Root is ActivityFlowContainerData root))
                throw new InvalidOperationException("Autobattle requires an Activity flow container.");
            SetField(root, "activity", distance);
            foreach (var step in root.Steps)
            {
                ReplaceFlowActivity(step.SuccessConditions, source, distance);
                ReplaceFlowActivity(step.FailConditions, source, distance);
            }
            var installer = LoadRequired<GameFlowDefinitionsInstallerData>(
                "Assets/Game/Runtime/Installers/ChainRushGameFlowDefinitionsInstaller.asset");
            AddUnique(GetField<List<GameFlowTemplateData>>(installer, "templates"), distanceFlow);
            var start = ChainRushBoardPlannerAuthoring.WriteContentAsset<StartChainRushLevelActionData>(LevelStartupPath, null);
            SetField(start, "levels", new List<ChainRushLevelFlowBinding>
            {
                new ChainRushLevelFlowBinding(LoadRequired<LevelData>("Assets/Game/Resources/Levels/Location001/Loc001Lvl01Data.asset").Id, "PerfumeData", sourceFlow),
                new ChainRushLevelFlowBinding(LoadRequired<LevelData>("Assets/Game/Resources/Levels/Location001/Loc001Lvl02Data.asset").Id, "PerfumeData", distanceFlow)
            });
            SetField(start, "externalKey", "chainrush.flow-runtime.autobattle");
            var startup = LoadRequired<GameStartupPlanData>("Assets/Game/Runtime/Startup/ChainRushGameStartupPlan.asset");
            var serialized = new SerializedObject(startup);
            var actions = serialized.FindProperty("actions");
            bool found = false;
            for (int i = 0; i < actions.arraySize; i++)
            {
                var action = actions.GetArrayElementAtIndex(i);
                if (action.objectReferenceValue == start || AssetDatabase.GetAssetPath(action.objectReferenceValue) == FixedAutobattleStartupPath)
                {
                    if (found) throw new InvalidOperationException("Startup contains multiple level launch actions.");
                    action.objectReferenceValue = start;
                    found = true;
                }
            }
            if (!found) throw new InvalidOperationException("Startup has no Autobattle launch action to author.");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(economy);
            EditorUtility.SetDirty(installer);
            EditorUtility.SetDirty(distanceFlow);
            EditorUtility.SetDirty(start);
            AssetDatabase.SaveAssets();
            if (AssetDatabase.LoadMainAssetAtPath(FixedAutobattleStartupPath) != null)
                AssetDatabase.DeleteAsset(FixedAutobattleStartupPath);
        }

        static void ReplaceFlowActivity(List<GameFlowCondition> conditions, ActivityData source, ActivityData target)
        {
            foreach (var condition in conditions)
            {
                if (condition is GameFlowConditionActivityLifecycle lifecycle && lifecycle.Activity == source)
                    SetField(lifecycle, "activity", target);
                if (condition.NestedConditions.Count > 0)
                    ReplaceFlowActivity(new List<GameFlowCondition>(condition.NestedConditions), source, target);
            }
        }
    }
}
