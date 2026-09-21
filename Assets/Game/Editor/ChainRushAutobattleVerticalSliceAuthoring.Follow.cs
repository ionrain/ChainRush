using System.Collections.Generic;
using ChainRush.Gameplay;
using Core.Activities;
using Core.CapabilityHosts;
using UnityEditor;
using UnityEngine;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        internal static void ApplyRunFollow()
        {
            var feature = ChainRushBoardPlannerAuthoring.WriteContentAsset<ChainRushRunFollowFeatureData>(
                "Assets/Game/Runtime/Run/RunFollowFeature.asset", null);
            var heroes = new List<ChainRushHeroDefinitionBinding>();
            foreach (string name in new[] { "Perfume", "Tabasco" })
            {
                var binding = new ChainRushHeroDefinitionBinding();
                SetField(binding, "contentId", name + "Data");
                SetField(binding, "definition", LoadRequired<CapabilityHostData>(SharedRoot + "/Units/" + name + "/" + name + ".asset"));
                heroes.Add(binding);
            }
            SetField(feature, "heroes", heroes);
            SetField(feature, "spatialFollowers", new List<CapabilityHostData> {
                LoadRequired<CapabilityHostData>(PlayerSpawnerPath),
                LoadRequired<CapabilityHostData>(EconomyRoot + "/WaterDeploymentHost.asset"),
                LoadRequired<CapabilityHostData>(EconomyRoot + "/ColaDeploymentHost.asset") });
            foreach (string path in new[] { ActivityPath, DistanceActivityPath })
            {
                var activity = LoadRequired<ActivityData>(path);
                AddUnique(activity.Teams[0].Features, feature);
                EditorUtility.SetDirty(activity);
            }
            var collector = PrefabUtility.LoadPrefabContents(ExperienceCollectorPrefabPath);
            try
            {
                if (collector.GetComponent<ChainRushRunProjectionFollowController>() == null)
                    collector.AddComponent<ChainRushRunProjectionFollowController>();
                PrefabUtility.SaveAsPrefabAsset(collector, ExperienceCollectorPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(collector); }
            EditorUtility.SetDirty(feature);
        }
    }
}
