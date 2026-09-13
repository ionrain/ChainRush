using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core;
using Core.AI;
using Core.AI.Actions;
using Core.Activities;
using Core.CapabilityHosts;
using Core.Economy;
using Core.Objectives;
using Core.Orchestration;
using Core.Production.Authoring;
using Core.Projection;
using Core.Skills;
using Core.Taxonomy;
using Core.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ChainRush.Tests.EditMode
{
    public sealed class PlayableContentEditModeTests
    {
        const string Root = "Assets/Game/Activities/";
        static readonly List<string> CellNames = new List<string>
        {
            "Water", "Cola", "LightningBolt", "Power", "Defense", "Health", "Speed", "SkillSpeed", "Gold"
        };

        [TestCase("Water")]
        [TestCase("Cola")]
        public void AlliedDefeat_DoesNotAcquireDropFailurePolicy(string unit)
        {
            var brain = Load<AIBrainData>("Autobattle/AI/" + unit + "UnitBrain.asset");
            var defeat = Load<TaxonomyTermData>("Autobattle/AI/Taxonomy/DefeatState.asset");
            Assert.IsFalse(brain.Nodes.SelectMany(node => node.States).Where(state => state.Tag == defeat)
                .SelectMany(state => state.OnExitActions).Any(action => action is RemoveEntityAIBrainExitActionData));
        }

        [Test]
        public void EnemyDefeat_RemovesEntityOnDropFailureWithoutChangingSuccessPath()
        {
            var brain = Load<AIBrainData>("Autobattle/AI/EnemyCombatBrain.asset");
            var defeat = Load<TaxonomyTermData>("Autobattle/AI/Taxonomy/DefeatState.asset");
            var state = brain.Nodes.SelectMany(node => node.States).Single(item => item.Tag == defeat);
            Assert.AreEqual(2, state.OnEnterActions.Count);
            Assert.IsInstanceOf<DropAIBrainActionData>(state.OnEnterActions[0]);
            Assert.IsInstanceOf<RemoveEntityAIBrainActionData>(state.OnEnterActions[1]);
            Assert.AreEqual(1, state.OnExitActions.Count);
            Assert.IsInstanceOf<RemoveEntityAIBrainExitActionData>(state.OnExitActions[0]);
            Assert.AreEqual(AIBrainStateResultMask.Fail, state.OnExitActions[0].TriggerResults);
        }

        [TestCase("Water")]
        [TestCase("Cola")]
        public void SourceUnits_HaveFourAuthoredMergeForms(string unit)
        {
            var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/Game/Resources/Units/" + unit + "Data.asset");
            Assert.NotNull(source);
            var forms = (IList)source.GetType().GetField("mergeStates", BindingFlags.Instance | BindingFlags.Public).GetValue(source);
            Assert.AreEqual(4, forms.Count);
        }

        [TestCase("Water", "WaterBoardBase", "BoardMergeRecipe")]
        [TestCase("Cola", "ColaBoardBase", "ColaMergeRecipe")]
        public void MergeRecipes_ConsumeTheirOwnCellsAndIssueTheExactForm(string unit, string cell, string recipeName)
        {
            var boardCell = AssetDatabase.LoadAssetAtPath<CapabilityHostData>(
                "Assets/Game/Activities/Board/Economy/" + cell + ".asset");
            Assert.NotNull(boardCell);
            for (int form = 1; form <= 4; form++)
            {
                var recipe = AssetDatabase.LoadAssetAtPath<ProductionRecipeData>(
                    "Assets/Game/Activities/Board/Production/" + recipeName + form + ".asset");
                var definition = AssetDatabase.LoadAssetAtPath<CapabilityHostData>(
                    "Assets/Game/Activities/Shared/Units/" + unit + "/" + unit + "Unit" + (form == 1 ? "" : form.ToString()) + ".asset");
                Assert.NotNull(recipe, unit + " form " + form);
                Assert.NotNull(definition);
                Assert.AreEqual(1, recipe.Inputs.Count);
                Assert.AreSame(boardCell, recipe.Inputs[0].Asset);
                Assert.AreEqual(EconomyOperation.Consume, recipe.Inputs[0].Operation);
                Assert.AreEqual(EconomyFormType.Token, recipe.Inputs[0].FormType);
                Assert.AreEqual(1, recipe.Outputs.Count);
                Assert.AreSame(definition, recipe.Outputs[0].Asset);
                Assert.AreEqual(EconomyFormType.Stack, recipe.Outputs[0].FormType);
            }
        }

        [TestCaseSource(nameof(CellNames))]
        public void EveryCell_HasAGroupProducerAndARealTokenOutput(string name)
        {
            var cell = Load<CapabilityHostData>("Board/Economy/" + name + "BoardBase.asset");
            Assert.Contains(Load<TaxonomyTermData>("Board/Taxonomy/BoardContent.asset"), cell.Tags);
            Assert.AreEqual(new Vector3Int(1000, 0, 1000), Read<Vector3Int>(cell, "footprintSize"));
            Assert.IsNotNull(cell.Icon);
            string prefabPath = AssetDatabase.GUIDToAssetPath(cell.ProjectionPrefabReference.AssetGUID);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab);
            Assert.AreSame(cell.Icon, prefab.GetComponent<UnityEngine.UI.Image>().sprite);

            string producerName = name == "Water" || name == "Cola" ? "BoardPopulationProducer"
                : name == "LightningBolt" ? "SkillsPopulationProducer"
                : name == "Gold" ? "GoldPopulationProducer" : "BuffsPopulationProducer";
            var producer = Load<CapabilityHostData>("Board/Economy/" + producerName + ".asset");
            Assert.Contains(Load<TaxonomyTermData>("Board/Taxonomy/BoardContentProducer.asset"), producer.Tags);
            var production = producer.WalletEntries.SelectMany(wallet => wallet.Seed)
                .Select(seed => seed.Asset).OfType<ProductionData>().Single();
            var catalog = production.SupportedCatalogs.Single();
            var entry = catalog.Entries.Single(item => item.Recipe.Outputs.Single().Asset == cell);
            Assert.AreEqual(1, entry.WorkDuration);
            Assert.AreEqual(0, entry.RecoveryDuration);
            Assert.AreSame(cell, entry.Recipe.Outputs.Single().Asset);
            Assert.AreEqual(EconomyFormType.Token, entry.Recipe.Outputs.Single().FormType);
            Assert.IsEmpty(entry.Recipe.Inputs);
            var board = Load<ActivityData>("Board/Definition/BoardActivity.asset");
            Assert.AreEqual(1, board.Teams[0].Wallets.SelectMany(wallet => wallet.Seed)
                .Count(seed => seed.Seed.Asset == producer && seed.Seed.Amount == 1));
        }

        [Test]
        public void Board_HasFourSharedProducerCatalogs_WithAuthoredRecipeOrder()
        {
            var board = Load<ActivityData>("Board/Definition/BoardActivity.asset");
            var tag = Load<TaxonomyTermData>("Board/Taxonomy/BoardContentProducer.asset");
            var producers = board.Teams[0].Wallets.SelectMany(wallet => wallet.Seed)
                .Select(seed => seed.Seed.Asset).OfType<CapabilityHostData>().Where(host => host.Tags.Contains(tag)).ToList();
            Assert.AreEqual(4, producers.Count);
            var catalogs = producers.Select(host => host.WalletEntries.SelectMany(wallet => wallet.Seed)
                .Select(seed => seed.Asset).OfType<ProductionData>().Single().SupportedCatalogs.Single()).ToList();
            CollectionAssert.AreEqual(new[] { 2, 5, 1, 1 }, catalogs.Select(catalog => catalog.Entries.Count));
            CollectionAssert.AreEqual(new[] { "WaterBoardBase", "ColaBoardBase" },
                catalogs[0].Entries.Select(entry => entry.Recipe.Outputs.Single().Asset.name));
        }

        [Test]
        public void StubConsumeOperators_AreExactSelectedTokensAndCannotConsumeUnitCells()
        {
            var brain = Load<OrchestratorAIBrainData>("Board/Orchestration/BoardBrain.asset");
            var selected = Load<TaxonomyTermData>("Board/Taxonomy/BoardMergeSelected.asset");
            var boardWallet = Load<TaxonomyTermData>("Board/Economy/BoardWalletTag.asset");
            var operators = brain.Operators.OfType<EconomyOperationDecompOpData>()
                .Where(operation => Read<EconomyOperation>(operation, "operation") == EconomyOperation.Consume
                    && Read<EconomyEntrySelectionData>(operation, "selection").FormTypes.Contains(EconomyFormType.Token)).ToList();
            Assert.AreEqual(7, operators.Count);
            foreach (string name in CellNames.Skip(2))
            {
                var cell = Load<CapabilityHostData>("Board/Economy/" + name + "BoardBase.asset");
                var operation = operators.Single(item => Read<EconomyEntrySelectionData>(item, "selection").ExactAsset == cell);
                var selection = Read<EconomyEntrySelectionData>(operation, "selection");
                Assert.AreEqual(EconomyOperation.Consume, Read<EconomyOperation>(operation, "operation"));
                CollectionAssert.AreEquivalent(new[] { selected }, selection.RequiredRuntimeTags);
                CollectionAssert.AreEquivalent(new[] { boardWallet }, selection.WalletTags);
                var objective = Load<ObjectiveTemplateData>("Board/Objectives/" + name + "SelectionObjective.asset");
                Assert.AreEqual(ObjectiveCompletionPolicyType.Reset, objective.CompletionPolicyType);
                Assert.AreSame(cell, ((ObjectiveConditionEconomyMetric)objective.Root.SuccessConditions.Single()).Asset);
            }
        }

        [Test]
        public void BoardCleanup_RoutesOnlyUnselectedDemands_AndCoversEveryCellAsset()
        {
            var brain = Load<OrchestratorAIBrainData>("Board/Orchestration/BoardBrain.asset");
            var selected = Load<TaxonomyTermData>("Board/Taxonomy/BoardMergeSelected.asset");
            var wallet = Load<TaxonomyTermData>("Board/Economy/BoardWalletTag.asset");
            var content = Load<TaxonomyTermData>("Board/Taxonomy/BoardContent.asset");
            var operation = brain.Operators.OfType<EconomyOperationDecompOpData>()
                .Single(item => Read<EconomyOperation>(item, "operation") == EconomyOperation.Destroy);
            var selection = Read<EconomyEntrySelectionData>(operation, "selection");
            Assert.IsNull(selection.ExactAsset);
            CollectionAssert.AreEqual(new[] { EconomyFormType.Token }, selection.FormTypes);
            CollectionAssert.AreEqual(new[] { wallet }, selection.WalletTags);
            CollectionAssert.AreEqual(new[] { content }, selection.RequiredAssetTags);
            var root = Load<ObjectiveTemplateData>("Board/Objectives/BoardPopulationObjective.asset").Root;
            var clear = ((ObjectiveConditionTargetNodesState)root.SuccessConditions.Single()).TargetNodes
                .Single(node => node.Id == "chainrush-board-clear");
            CollectionAssert.AreEquivalent(CellNames.Select(name => Load<CapabilityHostData>("Board/Economy/" + name + "BoardBase.asset")),
                clear.SuccessConditions.Cast<ObjectiveConditionEconomyMetric>().Select(metric => metric.Asset));
            foreach (string name in CellNames)
            foreach (bool tagged in new[] { false, true })
            {
                var asset = Load<CapabilityHostData>("Board/Economy/" + name + "BoardBase.asset");
                var fact = new OrchestrationFactQuery<EconomyOrchestrationQueryData>("zero", default,
                    new EconomyOrchestrationQueryData(asset, EconomyFormType.Token, new[] { wallet }, new[] { content },
                        tagged ? new[] { selected } : null, CompareOperation.Equal, 0));
                var result = OrchestrationDecisionGraphEvaluator.Evaluate(brain.DecisionGraph, fact, default, null, null,
                    default, OrchestrationDecompositionScopeType.GlobalObjective);
                Assert.AreEqual(!tagged, result.OperatorIds.Contains(operation.OperatorId));
                Assert.AreEqual(tagged, result.DecisionIds.Contains("board-production-input"));
                if (!tagged)
                    CollectionAssert.AreEqual(new[] { "board-clear" }, result.DecisionIds);
            }
        }

        [Test]
        public void InitialPlayerSeed_HasOnlyPerfumeAndNoFreeUnitStacks()
        {
            var activity = Load<ActivityData>("Autobattle/Definition/AutobattleActivity.asset");
            var hero = Load<CapabilityHostData>("Shared/Units/Perfume/Perfume.asset");
            var combatant = Load<TaxonomyTermData>("Autobattle/Taxonomy/CombatantRole.asset");
            var combatSeeds = activity.Teams[0].Wallets.SelectMany(wallet => wallet.Seed)
                .Where(seed => seed.Seed.Asset.Tags.Contains(combatant)).ToList();
            Assert.AreEqual(1, combatSeeds.Count);
            Assert.AreSame(hero, combatSeeds[0].Seed.Asset);
            Assert.AreEqual(1, combatSeeds[0].Seed.Amount);
            Assert.AreEqual(EconomyFormType.Token, combatSeeds[0].Seed.FormType);
            Assert.AreEqual(ActivitySeedMaterializationType.Spatial, combatSeeds[0].MaterializationType);
            CollectionAssert.AreEqual(new[] { Load<TaxonomyTermData>("Autobattle/Taxonomy/HeroSpawn.asset") }, combatSeeds[0].MaterializationMarkerTags);
            Assert.IsFalse(hero.SupportsCapability(CapabilityHostType.MovementOwner));
            var seedAssets = hero.WalletEntries.SelectMany(wallet => wallet.Seed).Select(seed => seed.Asset).ToList();
            Assert.IsFalse(seedAssets.OfType<MovementData>().Any());
            var brain = seedAssets.OfType<AIBrainData>().Single();
            var actions = brain.Nodes.SelectMany(node => node.States).SelectMany(state => state.OnTickActions).ToList();
            Assert.AreEqual(2, actions.OfType<SelectEntityTargetByQueryAIBrainActionData>().Count());
            Assert.IsTrue(actions.OfType<UseSkillAIBrainActionData>().All(action =>
                Read<Core.Skills.SkillData>(action, "skill").Effects.All(effect => !(effect is SkillMoveToTargetEffectData))));
        }

        [TestCase("Water")]
        [TestCase("Cola")]
        public void AllFourForms_HaveExactStackToTokenDeploymentAndObservableObjectives(string unitName)
        {
            var catalog = Load<ProductionCatalogData>("Autobattle/Production/PlayerProductionCatalog.asset");
            var activity = Load<ActivityData>("Autobattle/Definition/AutobattleActivity.asset");
            for (int form = 1; form <= 4; form++)
            {
                string name = unitName + "Unit" + (form == 1 ? "" : form.ToString());
                var unit = Load<CapabilityHostData>("Shared/Units/" + unitName + "/" + name + ".asset");
                var recipe = catalog.Entries.Select(entry => entry.Recipe).Single(entry => entry.Outputs.Single().Asset == unit);
                Assert.AreSame(unit, recipe.Inputs.Single().Asset);
                Assert.AreEqual(EconomyFormType.Stack, recipe.Inputs.Single().FormType);
                Assert.AreEqual(EconomyFormType.Token, recipe.Outputs.Single().FormType);
                Assert.AreEqual(1, activity.Teams[0].Objectives.Count(objective => objective.Template.Root.ActivateConditions
                    .OfType<ObjectiveConditionEconomyMetric>().Any(condition => condition.Asset == unit)));
                Assert.IsTrue(unit.SupportsCapability(CapabilityHostType.MovementOwner));
            }
        }

        [TestCase("ColaUnit")]
        [TestCase("ColaUnit2")]
        [TestCase("ColaUnit3")]
        [TestCase("ColaUnit4")]
        [TestCase("Perfume")]
        public void RangedAttacks_DamageOnCarrierHitAndConsumeOneCarrierLife(string name)
        {
            var attack = Load<Core.Skills.SkillData>("Autobattle/Skills/" + name + "Attack.asset");
            var spawn = attack.Effects.OfType<SkillSpawnCarrierEffectData>().Single();
            Assert.IsFalse(attack.Effects.OfType<SkillHostValueEffectData>().Any());
            Assert.NotNull(spawn.Carrier);
            Assert.NotNull(spawn.CarriedSkill);
            var lives = spawn.Parameters.OfType<SkillCarrierScalarParameterValueData>()
                .Single(parameter => parameter.ParameterType == SkillCarrierScalarParameterType.Lives);
            Assert.AreEqual(1, lives.MinValue);
            Assert.AreEqual(1, lives.MaxValue);
            var consumeLife = spawn.CarriedSkill.Effects.OfType<SkillHostValueEffectData>()
                .Single(effect => effect.Recipient == EffectRecipient.Carrier);
            Assert.AreSame(lives.CarrierHostValue, consumeLife.HostValue);
            Assert.AreEqual(-1, consumeLife.Value);
            var lifetime = spawn.Parameters.OfType<SkillCarrierScalarParameterValueData>()
                .Single(parameter => parameter.ParameterType == SkillCarrierScalarParameterType.Lifetime);
            Assert.Greater(lifetime.MinValue, 0);
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(Root + path);
            Assert.NotNull(value, path);
            return value;
        }

        static T Read<T>(object value, string name)
        {
            for (var type = value.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) return (T)field.GetValue(value);
            }
            throw new System.MissingFieldException(value.GetType().Name, name);
        }
    }
}
