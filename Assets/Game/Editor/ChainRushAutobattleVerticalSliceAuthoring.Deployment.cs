using System;
using System.Collections.Generic;
using System.Linq;
using Core.Activities;
using Core.AI;
using Core.CapabilityHosts;
using Core.Economy;
using Core.GameRuntime.Installers;
using Core.Production.Authoring;
using Core.Taxonomy;
using Core.World;
using UnityEditor;
using UnityEngine;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        // Working model: each source speciality owns an ordinary Production host and spatial provider.
        internal static void ApplyRunDeployment()
        {
            var installer = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(installer, "assets");
            var playerCatalog = LoadRequired<ProductionCatalogData>(PlayerCatalogPath);
            var experience = LoadRequired<ProductionRecipeData>(ExperienceRecipePath);
            var timing = playerCatalog.Entries.Single(entry => entry.Recipe == experience);
            var hosts = new List<(CapabilityHostData Host, TaxonomyTermData Anchor)>();
            var space = PrefabUtility.LoadPrefabContents(SpacePrefabPath);
            try
            {
                var heroSocket = space.transform.Find("HeroSpawn");
                if (heroSocket == null) throw new InvalidOperationException("The run requires its explicit hero spawn socket.");
                Vector3Int heroOrigin = GetField<Vector3Int>(heroSocket.GetComponent<SpatialMarkerSocket>(), "topologyCoordinates");
                foreach (string name in new[] { "Water", "Cola" })
                {
                    var providerType = WriteCombatTerm(name + "Deployment", LoadRequired<TaxonomyTermData>(AlliedSpawnPath));
                    var anchor = WriteCombatTerm(name + "DeploymentAnchor", LoadRequired<TaxonomyTermData>(HeroSpawnPath));
                    var pattern = LoadRequired<AIBrainAnchorPatternData>(AIRoot + "/" + name + "AnchorPattern.asset");
                    Vector3Int areaSize = GetField<Vector3Int>(pattern, "roamSize");
                    Vector3Int areaCenter = heroOrigin + GetField<Vector3Int>(pattern, "anchorOffset");
                    Vector3Int origin = new Vector3Int(name == "Water" ? -16000 : -17000, 0, 0);
                    string socketName = name + "DeploymentAnchor";
                    var old = space.transform.Find(socketName);
                    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    CreateMarkerSocket(space.transform, socketName, origin, new List<TaxonomyTermData> { anchor });
                    var catalog = ChainRushBoardPlannerAuthoring.WriteContentAsset<ProductionCatalogData>(
                        ProductionRoot + "/" + name + "DeploymentCatalog.asset", null,
                        "chainrush.production.deploy." + name.ToLowerInvariant() + ".catalog", definitions);
                    SetField(catalog, "allowedOperations", AllMutableOperations);
                    catalog.Entries.Clear();
                    for (int form = 1; form <= 4; form++)
                    {
                        string unitName = name + "Unit" + (form == 1 ? "" : form.ToString());
                        var entry = new ProductionCatalogEntryData();
                        SetStructField(ref entry, "recipe", LoadRequired<ProductionRecipeData>(ProductionRoot + "/" + unitName + "DeploymentRecipe.asset"));
                        SetStructField(ref entry, "workDuration", timing.WorkDuration);
                        SetStructField(ref entry, "recoveryDuration", timing.RecoveryDuration);
                        SetStructField(ref entry, "reservationPolicy", timing.ReservationPolicy);
                        catalog.Entries.Add(entry);
                    }
                    var production = ChainRushBoardPlannerAuthoring.WriteContentAsset<ProductionData>(
                        ProductionRoot + "/" + name + "DeploymentProduction.asset", LoadRequired<ProductionData>(PlayerProductionPath),
                        "chainrush.production.deploy." + name.ToLowerInvariant(), definitions);
                    SetField(production, "supportedCatalogs", new List<ProductionCatalogData> { catalog });
                    SetField(production, "materializationProviderType", providerType);
                    var host = ChainRushBoardPlannerAuthoring.WriteContentAsset<CapabilityHostData>(
                        EconomyRoot + "/" + name + "DeploymentHost.asset", LoadRequired<CapabilityHostData>(PlayerSpawnerPath),
                        "chainrush.deployment." + name.ToLowerInvariant(), definitions);
                    SetField(host, "walletEntries", new List<WalletEntry> {
                        CreateWalletEntry(LoadRequired<EconomyWalletData>(UnitWalletPath),
                            new SeedEntry(production, 1, EconomyFormType.Stack)) });
                    string prefab = ProjectionRoot + "/" + name + "DeploymentHost.prefab";
                    // Marker centers remain inside the source rectangle at the existing one-unit grid resolution.
                    var count = new Vector3Int(areaSize.x / 1000, 1, areaSize.z / 1000);
                    var minimum = areaCenter - new Vector3Int(areaSize.x / 2, 0, areaSize.z / 2)
                        + new Vector3Int(500, 0, 500) - origin;
                    CreateSpawnerPrefab(prefab, name + "DeploymentHost", providerType,
                        LoadRequired<SpatialShapeData>(SpawnAreaShapePath), minimum, count, host.Id, new List<string>());
                    ConfigureAddressable(prefab, AddressablesGroup);
                    SetProjection(host, prefab);
                    hosts.Add((host, anchor));
                    EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(production); EditorUtility.SetDirty(host);
                }
                PrefabUtility.SaveAsPrefabAsset(space, SpacePrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(space); }
            playerCatalog.Entries.Clear();
            playerCatalog.Entries.Add(timing);
            foreach (string path in new[] { ActivityPath, DistanceActivityPath })
            {
                var activity = LoadRequired<ActivityData>(path);
                var team = activity.Teams[0];
                var wallet = team.Wallets.Single(value => value.Wallet == LoadRequired<EconomyWalletData>(SharedWalletPath));
                foreach (var entry in hosts)
                {
                    wallet.Seed.RemoveAll(value => value.Seed.Asset == entry.Host);
                    wallet.Seed.Add(new ActivityWalletSeedEntryData(new SeedEntry(entry.Host, 1, EconomyFormType.Token),
                        ActivitySeedMaterializationType.Spatial, new List<TaxonomyTermData>(), new List<TaxonomyTermData> { entry.Anchor }));
                }
                EditorUtility.SetDirty(activity);
            }
            EditorUtility.SetDirty(playerCatalog); EditorUtility.SetDirty(installer);
        }
    }
}
