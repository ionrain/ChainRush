using System;
using System.Collections.Generic;
using System.Linq;
using Core.CapabilityHosts;
using Core.Economy;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        internal static void ApplyExperienceDrops()
        {
            var sourceScene = EditorSceneManager.OpenPreviewScene("Assets/Game/Scenes/Level.unity");
            try
            {
                var droppers = sourceScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ItemDropper>(true))
                    .Where(dropper => dropper.enabled && dropper.gameObject.activeInHierarchy).ToList();
                var experience = LoadRequired<Core.Economy.Modules.ResourceEconomyModule.ResourceData>(ExperiencePath);
                var wallet = LoadRequired<EconomyWalletData>(UnitWalletPath);
                foreach (string name in new[] { "BugBrownSmall", "BugBrownMedium", "BugGreenSmall", "BugGreenMedium", "BugPurpleSmall", "BugPurpleMedium" })
                {
                    var source = LoadRequired<UnityEngine.GameObject>("Assets/Game/Prefabs/Enemies/" + name + ".prefab")
                        .GetComponent<Enemy>();
                    if (source == null) throw new InvalidOperationException("Missing source enemy drop requester: " + name);
                    long amount = 0;
                    int matching = 0;
                    foreach (var dropper in droppers)
                    {
                        if (dropper.ChannelId != 0 && dropper.ChannelId != source.ChannelId) continue;
                        var entries = GetField<Dictionary<int, DropData>>(dropper, "dropList");
                        if (!entries.TryGetValue(source.Id, out var drop)) continue;
                        if (drop.emptyProbability != 0 || drop.options.Count != 1)
                            throw new InvalidOperationException("Included experience drop requires an explicitly deterministic source entry: " + name);
                        var option = drop.options.Single();
                        var item = option.Key.GetComponent<ExperienceItem>();
                        if (item == null || option.Value <= 0 || (int)item.Data <= 0)
                            throw new InvalidOperationException("Source experience drop has no positive experience payload: " + name);
                        amount = checked(amount + (int)item.Data);
                        matching++;
                    }
                    if (matching != 1) throw new InvalidOperationException("Included enemy must have exactly one active experience drop source: " + name);
                    var host = LoadRequired<CapabilityHostData>(EconomyRoot + "/" + name + ".asset");
                    var seeds = host.WalletEntries.Single(entry => entry.Wallet == wallet).Seed;
                    seeds.RemoveAll(seed => seed.Asset == experience);
                    seeds.Add(new SeedEntry(experience, amount, EconomyFormType.Stack));
                    EditorUtility.SetDirty(host);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(sourceScene); }
        }
    }
}
