using System;
using System.Collections.Generic;
using System.IO;
using Core;
using Core.Activities;
using Core.CapabilityHosts;
using Core.Economy;
using Core.GameRuntime.Installers;
using Core.Objectives;
using Core.Orchestration;
using Core.Production.Authoring;
using Core.Projection;
using Core.Taxonomy;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRush.Editor
{
    public static partial class ChainRushBoardPlannerAuthoring
    {
        internal const string BoardContentTagPath = BoardRoot + "/Taxonomy/BoardContent.asset";
        internal const string BoardProducerTagPath = BoardRoot + "/Taxonomy/BoardContentProducer.asset";
        static readonly List<string> ContentNames = new List<string>
        {
            "Water", "Cola", "LightningBolt", "Power", "Defense", "Health", "Speed", "SkillSpeed", "Gold"
        };

        internal static void ApplyPlayableBoardContent(List<CapabilityHostData> waterForms,
            List<CapabilityHostData> colaForms, List<EconomyAssetData> definitions, List<TaxonomyTermData> terms)
        {
            var itemTag = WriteContentTerm(BoardContentTagPath, "chainrush.board.content", 10, terms);
            var producerTag = WriteContentTerm(BoardProducerTagPath, "chainrush.board.content-producer", 11, terms);
            WriteContentTerm(ClearBoardOperatorPath, "chainrush.orchestration.board.clear", 7, terms,
                LoadRequired<TaxonomyFamilyData>(OperatorFamilyPath));
            var template = LoadRequired<CapabilityHostData>(WaterPath);
            var producerTemplate = LoadRequired<CapabilityHostData>(PopulationProducerPath);
            var recipeTemplate = LoadRequired<ProductionRecipeData>(WaterRecipePath);
            var boardWallet = LoadRequired<EconomyWalletData>(BoardWalletPath);
            var walletTag = LoadRequired<TaxonomyTermData>(BoardWalletTagPath);
            var sharedWalletTag = LoadRequired<TaxonomyTermData>(SharedWalletTagPath);
            var selected = LoadRequired<TaxonomyTermData>(MergeSelectedTagPath);
            var brain = LoadRequired<OrchestratorAIBrainData>(BrainPath);
            var activity = LoadRequired<ActivityData>(BoardActivityPath);
            var boardTeam = activity.Teams[0];
            var boardWalletData = boardTeam.Wallets.Find(entry => entry.Wallet == boardWallet);
            var seeds = new List<ActivityWalletSeedEntryData>(boardWalletData.Seed);
            seeds.RemoveAll(entry => entry.Seed.Asset == producerTemplate
                || entry.Seed.Asset is CapabilityHostBaseData host && host.Tags.Contains(producerTag));
            var objectives = new List<ActivityTeamObjectiveData>
            {
                CreateTeamObjective(LoadRequired<ObjectiveTemplateData>(PopulationObjectivePath)),
                CreateTeamObjective(LoadRequired<ObjectiveTemplateData>(SelectionObjectivePath))
            };
            var mergeRecipes = new List<ProductionRecipeData>();
            var mergeTemplate = LoadRequired<ProductionRecipeData>(MergeRecipe1Path);

            for (int index = 0; index < ContentNames.Count; index++)
            {
                string content = ContentNames[index];
                string id = content.ToLowerInvariant();
                bool water = index == 0;
                var cell = WriteContentAsset(water ? WaterPath : BoardRoot + "/Economy/" + content + "BoardBase.asset",
                    template, water ? template.Id : "chainrush.board." + id + ".base", definitions);
                cell.Tags.Remove(LoadRequired<TaxonomyTermData>(WaterTagPath));
                AddUnique(cell.Tags, itemTag, WriteContentTerm(BoardRoot + "/Taxonomy/" + content + "BoardItem.asset",
                    "chainrush.board.item." + id, index, terms));
                Sprite icon = ResolveContentIcon(content);
                SetField(cell, "icon", icon);
                WriteCellProjection(cell, content, icon);

                var recipe = WriteContentAsset(water ? WaterRecipePath : BoardRoot + "/Production/" + content + "BoardBaseRecipe.asset",
                    recipeTemplate, "chainrush.production.board." + id + "-base.recipe", definitions);
                ConfigureCellRecipe(recipe, cell, walletTag);

                string objectivePath = water ? MergeObjectivePath : ObjectivesRoot + "/" + content + "SelectionObjective.asset";
                var objective = WriteContentAsset(objectivePath, LoadRequired<ObjectiveTemplateData>(MergeObjectivePath));
                var root = new ObjectiveNode("board-selected-" + id, null,
                    new List<ObjectiveCondition> { CreateSelectedEconomyCondition(cell, walletTag, selected, 1, CompareOperation.GreaterOrEqual) },
                    new List<ObjectiveCondition> { CreateSelectedEconomyCondition(cell, walletTag, selected, 0, CompareOperation.Equal) });
                SetField(objective, "root", root);
                SetField(objective, "completionPolicyType", ObjectiveCompletionPolicyType.Reset);
                objectives.Add(CreateTeamObjective(objective));
                if (index < 2)
                {
                    var forms = water ? waterForms : colaForms;
                    for (int form = 4; form >= 1; form--)
                    {
                        string path = BoardRoot + "/Production/" + (water ? "BoardMergeRecipe" : "ColaMergeRecipe") + form + ".asset";
                        var merge = WriteContentAsset(path, mergeTemplate, "chainrush.production.board.merge." + id + "." + form, definitions);
                        merge.Inputs.Clear();
                        merge.Inputs.Add(new ProductionInputData(EconomyOperation.Consume, cell, EconomyFormType.Token,
                            new List<TaxonomyTermData> { walletTag }, new List<TaxonomyTermData> { selected }, new LongFlatProgressionData(form)));
                        merge.Outputs.Clear();
                        merge.Outputs.Add(new ProductionOutputData(forms[form - 1], EconomyFormType.Stack,
                            new List<TaxonomyTermData> { sharedWalletTag }, new LongFlatProgressionData(1)));
                        mergeRecipes.Add(merge);
                    }
                }
                else
                {
                    var term = WriteContentTerm(OrchestrationTaxonomyRoot + "/" + content + "ConsumeOperator.asset",
                        "chainrush.orchestration.board.consume." + id, 20 + index, terms,
                        LoadRequired<TaxonomyFamilyData>(OperatorFamilyPath));
                    var operation = new EconomyOperationDecompOpData();
                    SetField(operation, "operatorId", term);
                    SetField(operation, "operation", EconomyOperation.Consume);
                    SetField(operation, "selection", new EconomyEntrySelectionData(cell, EconomyFormType.Token,
                        new List<TaxonomyTermData> { walletTag }, null, null, new List<TaxonomyTermData> { selected }, null));
                    brain.Operators.RemoveAll(item => item.OperatorId == term);
                    brain.Operators.Add(operation);
                    string decisionId = "board-consume-" + id;
                    brain.DecisionGraph.Nodes.RemoveAll(item => item.DecisionId == decisionId);
                    brain.DecisionGraph.Nodes.Insert(0, CreateDecision(decisionId,
                        OrchestrationFactType.EconomyAmount, term, false, OrchestrationDecompositionScopeType.GlobalObjective));
                }
                EditorUtility.SetDirty(objective);
                EditorUtility.SetDirty(cell);
            }
            ConfigurePopulationContentGroups(definitions, seeds);
            ConfigureCatalog(LoadRequired<ProductionCatalogData>(MergeCatalogPath), mergeRecipes.ToArray());
            SetStructField(ref boardWalletData, "seed", seeds);
            int walletIndex = boardTeam.Wallets.FindIndex(entry => entry.Wallet == boardWallet);
            boardTeam.Wallets[walletIndex] = boardWalletData;
            SetStructField(ref boardTeam, "objectives", objectives);
            activity.Teams[0] = boardTeam;
            EditorUtility.SetDirty(activity);
            EditorUtility.SetDirty(brain);

            var selection = LoadRequired<AgentDefinitionData>(SelectionAgentPath);
            SetField(selection, "targetSelectionCriteria", new List<EntityCriterionEntryData>
            {
                Required(CreateCapabilityHostCriterion(null, null, new List<TaxonomyTermData> { itemTag })),
                Required(CreateOwnerCriterion()), Required(CreateAssetCountCriterion()),
                Required(CreateSegmentLengthCriterion(1000, 1000))
            });
            EditorUtility.SetDirty(selection);
            var selectionObjective = LoadRequired<ObjectiveTemplateData>(SelectionObjectivePath);
            selectionObjective.Root.ActivateConditions.RemoveAll(condition => condition is ObjectiveConditionEconomyMetric);
            selectionObjective.Root.ActivateConditions.Add(new ObjectiveConditionEconomyMetric(
                new List<TaxonomyTermData> { walletTag }, EconomyFormType.Token, null, 0, CompareOperation.Equal,
                new List<TaxonomyTermData> { itemTag }, new List<TaxonomyTermData> { selected }));
            EditorUtility.SetDirty(selectionObjective);
            ConfigurePopulationObjectives();
            ValidatePopulationProducerWiring();
        }

        [MenuItem("Tools/ChainRush/Authoring/Apply Population Progression And Content Groups")]
        public static void ApplyPopulationProgressionAndContentGroups()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Population authoring requires Edit Mode.");
            var installer = LoadRequired<EconomyDefinitionsInstallerData>(EconomyDefinitionsInstallerPath);
            var definitions = new List<EconomyAssetData>(GetField<List<EconomyAssetData>>(installer, "assets"));
            var activity = LoadRequired<ActivityData>(BoardActivityPath);
            var team = activity.Teams[0];
            var wallet = LoadRequired<EconomyWalletData>(BoardWalletPath);
            int index = team.Wallets.FindIndex(entry => entry.Wallet == wallet);
            var binding = team.Wallets[index];
            var seeds = new List<ActivityWalletSeedEntryData>(binding.Seed);
            ConfigurePopulationContentGroups(definitions, seeds);
            SetStructField(ref binding, "seed", seeds);
            team.Wallets[index] = binding;
            activity.Teams[0] = team;
            SetField(installer, "assets", definitions);
            var definition = LoadRequired<AgentDefinitionData>(PopulationAgentPath);
            ConfigurePopulationSettings((PopulationAgentData)definition.Agent);
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(activity);
            EditorUtility.SetDirty(installer);
            AssetDatabase.SaveAssets();
            DeletePerAssetPopulationProducers();
            ValidatePopulationProducerWiring();
        }

        static void ConfigurePopulationContentGroups(List<EconomyAssetData> definitions, List<ActivityWalletSeedEntryData> seeds)
        {
            var producerTag = LoadRequired<TaxonomyTermData>(BoardProducerTagPath);
            var producerTemplate = LoadRequired<CapabilityHostData>(PopulationProducerPath);
            var productionTemplate = LoadRequired<ProductionData>(PopulationProductionPath);
            var catalogTemplate = LoadRequired<ProductionCatalogData>(PopulationCatalogPath);
            var wallet = LoadRequired<EconomyWalletData>(BoardWalletPath);
            seeds.RemoveAll(entry => entry.Seed.Asset is CapabilityHostBaseData host && host.Tags.Contains(producerTag));
            foreach (string content in ContentNames)
            {
                if (content == "Water" || content == "Gold") continue;
                definitions.Remove(AssetDatabase.LoadAssetAtPath<CapabilityHostData>(BoardRoot + "/Economy/" + content + "PopulationProducer.asset"));
                definitions.Remove(AssetDatabase.LoadAssetAtPath<ProductionData>(BoardRoot + "/Production/" + content + "PopulationProduction.asset"));
                definitions.Remove(AssetDatabase.LoadAssetAtPath<ProductionCatalogData>(BoardRoot + "/Production/" + content + "PopulationCatalog.asset"));
            }
            var groups = new List<(string Name, List<string> Content)>
            {
                ("Units", new List<string> { "Water", "Cola" }),
                ("Buffs", new List<string> { "Power", "Defense", "Health", "Speed", "SkillSpeed" }),
                ("Skills", new List<string> { "LightningBolt" }),
                ("Gold", new List<string> { "Gold" })
            };
            foreach (var group in groups)
            {
                bool units = group.Name == "Units";
                string id = group.Name.ToLowerInvariant();
                var recipes = new List<ProductionRecipeData>();
                foreach (string content in group.Content)
                    recipes.Add(LoadRequired<ProductionRecipeData>(content == "Water" ? WaterRecipePath
                        : BoardRoot + "/Production/" + content + "BoardBaseRecipe.asset"));
                var catalog = WriteContentAsset(units ? PopulationCatalogPath : BoardRoot + "/Production/" + group.Name + "PopulationCatalog.asset",
                    catalogTemplate, "chainrush.production.board.population." + id + ".catalog", definitions);
                ConfigureCatalog(catalog, recipes.ToArray());
                var production = WriteContentAsset(units ? PopulationProductionPath : BoardRoot + "/Production/" + group.Name + "PopulationProduction.asset",
                    productionTemplate, "chainrush.production.board.population." + id, definitions);
                ConfigureProduction(production, catalog, productionTemplate.MaterializationProviderType);
                var producer = WriteContentAsset(units ? PopulationProducerPath : BoardRoot + "/Economy/" + group.Name + "PopulationProducer.asset",
                    producerTemplate, "chainrush.board.population-producer." + id, definitions);
                AddUnique(producer.Tags, producerTag);
                SetField(producer, "walletEntries", new List<WalletEntry>
                { new WalletEntry(wallet, new List<SeedEntry> { new SeedEntry(production, 1, EconomyFormType.Stack) }) });
                seeds.Add(new ActivityWalletSeedEntryData(new SeedEntry(producer, 1, EconomyFormType.Token),
                    ActivitySeedMaterializationType.NonSpatial, new List<TaxonomyTermData>()));
                EditorUtility.SetDirty(producer);
            }
        }

        internal static void DeletePerAssetPopulationProducers()
        {
            foreach (string content in ContentNames)
            {
                if (content == "Water" || content == "Gold") continue;
                AssetDatabase.DeleteAsset(BoardRoot + "/Economy/" + content + "PopulationProducer.asset");
                AssetDatabase.DeleteAsset(BoardRoot + "/Production/" + content + "PopulationProduction.asset");
                AssetDatabase.DeleteAsset(BoardRoot + "/Production/" + content + "PopulationCatalog.asset");
            }
        }

        static Sprite ResolveContentIcon(string content)
        {
            if (content == "Water" || content == "Cola")
                return LoadRequired<UnitData>("Assets/Game/Resources/Units/" + content + "Data.asset").mergeStates[0].icon;
            if (content == "LightningBolt")
                return LoadRequired<global::SkillData>("Assets/Game/Resources/Skills/SkillLightningBolt.asset").icon;
            if (content == "Gold")
                return LoadRequired<ResourcesData>("Assets/Game/Resources/GameResourcesData.asset").Get(ResourceType.SoftCurrency).icon;
            return LoadRequired<AttributesData>("Assets/Game/Resources/AttributesData.asset")
                .GetData((global::Attribute)Enum.Parse(typeof(global::Attribute), content)).icon;
        }

        static void WriteCellProjection(CapabilityHostData cell, string content, Sprite icon)
        {
            if (icon == null) throw new InvalidOperationException(content + " has no authored icon.");
            string path = BoardRoot + "/Projection/" + content + "BoardBase.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(WaterProjectionPrefabPath);
            try
            {
                root.name = content + "BoardBase";
                var image = root.GetComponent<Image>();
                image.sprite = icon;
                image.preserveAspect = true;
                SetField(root.GetComponent<ProjectionBindingController>(), "poolKey", cell.Id);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                ConfigureAddressable(path, "ChainRush-Activity-Board");
                SetField(cell, "projectionPrefabReference", new ProjectionPrefabReference(AssetDatabase.AssetPathToGUID(path)));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        internal static T WriteContentAsset<T>(string path, T template) where T : ScriptableObject
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new InvalidOperationException("Unexpected asset type at " + path);
                asset = template == null ? ScriptableObject.CreateInstance<T>() : UnityEngine.Object.Instantiate(template);
                AssetDatabase.CreateAsset(asset, path);
            }
            else if (template != null && template != asset)
                EditorUtility.CopySerialized(template, asset);
            asset.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        internal static T WriteContentAsset<T>(string path, T template, string id, List<EconomyAssetData> definitions)
            where T : EconomyAssetData
        {
            T asset = WriteContentAsset(path, template);
            SetField(asset, "id", id);
            if (!definitions.Contains(asset)) definitions.Add(asset);
            return asset;
        }

        static TaxonomyTermData WriteContentTerm(string path, string id, int order, List<TaxonomyTermData> terms,
            TaxonomyFamilyData family = null)
        {
            var term = WriteContentAsset<TaxonomyTermData>(path, null);
            ConfigureTaxonomyTerm(term, id, term.name, family ?? LoadRequired<TaxonomyFamilyData>(BoardItemFamilyPath), order);
            if (!terms.Contains(term)) terms.Add(term);
            return term;
        }
    }
}
