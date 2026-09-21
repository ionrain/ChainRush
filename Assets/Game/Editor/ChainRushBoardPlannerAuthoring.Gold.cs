using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Economy;
using Core.Economy.Modules.ResourceEconomyModule;
using Core.GameRuntime.Installers;
using Core.Orchestration;
using Core.Production.Authoring;
using Core.Taxonomy;
using UnityEditor;

namespace ChainRush.Editor
{
    public static partial class ChainRushBoardPlannerAuthoring
    {
        internal static void ConfigureGoldSelection()
        {
            var installer = LoadRequired<EconomyDefinitionsInstallerData>(EconomyDefinitionsInstallerPath);
            var definitions = new List<EconomyAssetData>(GetField<List<EconomyAssetData>>(installer, "assets"));
            var recipe = WriteGoldRecipe(definitions);
            var catalog = LoadRequired<ProductionCatalogData>(MergeCatalogPath);
            if (!catalog.Entries.Any(entry => entry.Recipe == recipe))
            {
                var entries = new List<ProductionCatalogEntryData>(catalog.Entries);
                var entry = entries[0];
                SetStructField(ref entry, "recipe", recipe);
                entries.Add(entry);
                SetField(catalog, "entries", entries);
                EditorUtility.SetDirty(catalog);
            }
            SetField(installer, "assets", definitions);
            EditorUtility.SetDirty(installer);
            RemoveGoldConsumeOperator(LoadRequired<OrchestratorAIBrainData>(BrainPath));
        }

        static ProductionRecipeData WriteGoldRecipe(List<EconomyAssetData> definitions)
        {
            var gold = WriteContentAsset(SharedRoot + "/Economy/RunGold.asset",
                LoadRequired<Core.Economy.Modules.ResourceEconomyModule.ResourceData>(ExperiencePath), "chainrush.resource.run-gold", definitions);
            SetField(gold, "allowedOperations", EconomyOperation.Issue | EconomyOperation.Require | EconomyOperation.Consume);
            var recipe = WriteContentAsset(BoardRoot + "/Production/GoldSelectionRecipe.asset",
                LoadRequired<ProductionRecipeData>(MergeRecipe1Path), "chainrush.production.board.gold-selection.recipe", definitions);
            recipe.Inputs.Clear();
            recipe.Inputs.Add(new ProductionInputData(EconomyOperation.Consume,
                LoadRequired<Core.CapabilityHosts.CapabilityHostData>(BoardRoot + "/Economy/GoldBoardBase.asset"),
                EconomyFormType.Token, new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(BoardWalletTagPath) },
                new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(MergeSelectedTagPath) }, new LongFlatProgressionData(1)));
            recipe.Outputs.Clear();
            recipe.Outputs.Add(new ProductionOutputData(gold, EconomyFormType.Stack,
                new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(SharedWalletTagPath) }, new LongFlatProgressionData(10)));
            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        static void RemoveGoldConsumeOperator(OrchestratorAIBrainData brain, List<TaxonomyTermData> authoredTerms = null)
        {
            const string path = OrchestrationTaxonomyRoot + "/GoldConsumeOperator.asset";
            var term = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(path);
            if (term != null) brain.Operators.RemoveAll(item => item.OperatorId == term);
            brain.DecisionGraph.Nodes.RemoveAll(item => item.DecisionId == "board-consume-gold");
            EditorUtility.SetDirty(brain);
            if (term != null)
            {
                authoredTerms?.Remove(term);
                var installer = LoadRequired<TaxonomyRuntimeInstallerData>(TaxonomyInstallerPath);
                var data = new SerializedObject(installer);
                var terms = data.FindProperty("terms");
                for (int i = terms.arraySize - 1; i >= 0; i--)
                    if (terms.GetArrayElementAtIndex(i).objectReferenceValue == term)
                    { terms.GetArrayElementAtIndex(i).objectReferenceValue = null; terms.DeleteArrayElementAtIndex(i); }
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
