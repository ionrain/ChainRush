using System;
using System.Collections.Generic;
using System.Reflection;
using Core.GameFlow;
using Core.GameRuntime;
using Core.Production;
using Core.UI.GameRuntime;
using Core.UI.Production;
using UnityEditor;
using UnityEngine;

// Explicit authoring command. Install in an Editor folder to execute, then remove the temporary copy.
public static class UISeparationAuthoring
{
    public static void ChainRush()
    {
        InstallUI("Assets/Game/Runtime/Host/ChainRushGameRuntimeProfile.asset",
            "Assets/Game/Runtime/Installers/ChainRushUIRuntimeInstaller.asset");
        const string root = "Assets/Game/FrameworkUI/Data/";
        var flow = Asset<GameFlowTemplateData>(root + "GameFlow/PurchaseFlow.asset");
        var submit = new GameFlowStepData();
        Set(submit, "id", "order");
        Set(submit, "executor", new GameFlowProductionExecutorData());
        var result = new GameFlowStepData();
        Set(result, "id", "result");
        Set(result, "successConditions", new List<GameFlowCondition> { Status(ProductionOrderStatus.Completed) });
        Set(result, "failConditions", new List<GameFlowCondition> {
            Status(ProductionOrderStatus.Cancelled, ProductionOrderStatus.Failed) });
        var sequence = new GameFlowSequenceContainerData();
        Set(sequence, "id", "purchase");
        Set(sequence, "completedPolicy", GameFlowTerminalPolicy.Remove);
        Set(sequence, "failedPolicy", GameFlowTerminalPolicy.Remove);
        Set(sequence, "steps", new List<GameFlowStepData> { submit, result });
        Set(flow, "id", "chainrush.meta.purchase");
        Set(flow, "root", sequence);
        EditorUtility.SetDirty(flow);
        foreach (string character in new List<string> { "Perfume", "Tabasco", "Water", "Cola" })
        {
            var action = Load<ProductionUIActionData>(root + "Production/" + character + "Purchase.asset");
            Set(action, "purchaseFlow", flow);
            EditorUtility.SetDirty(action);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("UI separation authoring complete: ChainRush installer, purchase flow, four actions.");
    }

    public static void Framework()
    {
        InstallUI("Assets/Game/Resources/GameRuntimeHost/GameRuntimeProfileData.asset",
            "Assets/Game/Resources/GameRuntimeHost/Installers/UIRuntimeInstallerData.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("UI separation authoring complete: MorbooFramework installer.");
    }

    static void InstallUI(string profilePath, string installerPath)
    {
        var profile = Load<GameRuntimeProfileData>(profilePath);
        var installer = Asset<UIRuntimeInstallerData>(installerPath);
        var installers = new List<GameRuntimeInstallerData>(profile.Installers);
        if (!installers.Contains(installer)) installers.Add(installer);
        Set(profile, "installers", installers);
        EditorUtility.SetDirty(profile);
    }

    static GameFlowConditionProductionOrder Status(params ProductionOrderStatus[] values)
    {
        var condition = new GameFlowConditionProductionOrder();
        Set(condition, "stepId", "order");
        Set(condition, "statuses", new List<ProductionOrderStatus>(values));
        return condition;
    }

    static T Load<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);

    static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void Set(object instance, string field, object value)
    {
        for (var type = instance.GetType(); type != null; type = type.BaseType)
        {
            var info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (info == null) continue;
            info.SetValue(instance, value);
            return;
        }
        throw new MissingFieldException(instance.GetType().Name, field);
    }
}
