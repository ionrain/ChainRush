using Core.UI.GameFlow;
using Core.UI.Activities;
// Explicit editor authoring command. Execute through Unity Pipeline run_script;
// this file is outside Assets and is never part of the game runtime.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core;
using Core.Activities;
using Core.Activities.Analytics;
using Core.Attributes;
using Core.CapabilityHosts;
using Core.Economy;
using Core.Economy.Authoring;
using Core.Economy.Modules.ResourceEconomyModule;
using Core.GameRuntime.Installers;
using Core.GameRuntime;
using Core.GameFlow;
using Core.GameFlow.GameRuntime;
using Core.UI.Flow;
using Core.Players;
using Core.Objectives;
using Core.Orchestration;
using Core.Production;
using Core.Production.Authoring;
using Core.UI.Production;
using Core.Taxonomy;
using Core.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using FrameworkResourceData = Core.Economy.Modules.ResourceEconomyModule.ResourceData;

public static class ChainRushMetaUIAuthoring
{
    const string Root = "Assets/Game/FrameworkUI";
    const string Data = Root + "/Data";
    const string Shared = "Assets/Game/Activities/Shared";
    const string Auto = "Assets/Game/Activities/Autobattle";
    const EconomyOperation MetaResourceOperations = EconomyOperation.Require | EconomyOperation.Issue | EconomyOperation.Consume;
    const EconomyOperation MetaProductionOperations = EconomyOperation.Require | EconomyOperation.Issue;
    const EconomyOperation MetaHostOperations = EconomyOperation.Require | EconomyOperation.Issue | EconomyOperation.Destroy;

    public static string SetInitialGameplaySeed()
    {
        ConfigureInitialGameplaySeed();
        AssetDatabase.SaveAssets();
        return "Both levels seed the player's ActivityWallet with one SpawnArea and one initial BoardTurnToken.";
    }

    static void ConfigureInitialGameplaySeed()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Initial gameplay seed authoring requires Edit Mode.");

        var activityWallet = Load<EconomyWalletData>(Shared + "/Economy/ActivityWallet.asset");
        var shape = Load<EconomyAssetData>(Auto + "/Space/Shapes/SpawnArea.asset");
        var turnToken = Load<EconomyAssetData>(Shared + "/Economy/BoardTurnToken.asset");
        foreach (string name in new[] { "AutobattleActivity", "DistanceActivity" })
        {
            var activity = Load<ActivityData>(Auto + "/Definition/" + name + ".asset");
            var teams = activity.Teams.ToList();
            if (teams.Count != 2)
                throw new InvalidOperationException("Expected player and bot teams in " + activity.name);
            var wallets = teams[0].Wallets.ToList();
            if (wallets.Count(w => w.Wallet == activityWallet) != 1)
                throw new InvalidOperationException("Expected one player ActivityWallet in " + activity.name);
            int index = wallets.FindIndex(w => w.Wallet == activityWallet);
            var seed = wallets[index].Seed.Where(s => s.Seed.Asset != shape && s.Seed.Asset != turnToken).ToList();
            seed.Add(new ActivityWalletSeedEntryData(new SeedEntry(shape, 1, EconomyFormType.Stack),
                ActivitySeedMaterializationType.None, new List<TaxonomyTermData>()));
            seed.Add(new ActivityWalletSeedEntryData(new SeedEntry(turnToken, 1, EconomyFormType.Stack),
                ActivitySeedMaterializationType.None, new List<TaxonomyTermData>()));
            object wallet = wallets[index];
            Set(wallet, "seed", seed);
            wallets[index] = (ActivityTeamWalletData)wallet;
            object team = teams[0];
            Set(team, "wallets", wallets);
            teams[0] = (ActivityTeamData)team;
            Set(activity, "teams", teams);
        }
    }

    public static string SetProgressForBothParticipants()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Progress authoring requires Edit Mode.");

        var progress = Load<EconomyAssetData>(Shared + "/Economy/LevelProgress.asset");
        var walletTag = Load<TaxonomyTermData>(Shared + "/Economy/ActivityWalletTag.asset");
        var elapsed = Load<ActivityAnalyticsMetricData>(Auto + "/Knowledge/ElapsedSimulationMetric.asset");
        var displacement = Load<ActivityAnalyticsMetricData>(Auto + "/Knowledge/HeroDisplacementMetric.asset");
        var resourceMetric = Load<ActivityAnalyticsMetricData>(Auto + "/Knowledge/LevelProgressMetric.asset");
        var analytics = Load<ActivityAnalyticsConfigData>(Auto + "/Knowledge/EnemyAnalytics.asset");
        var activities = new[] { "AutobattleActivity", "DistanceActivity" };
        var objectives = new[] { "SurviveProgressObjective", "DistanceProgressObjective" };

        for (int i = 0; i < activities.Length; i++)
        {
            var activity = Load<ActivityData>(Auto + "/Definition/" + activities[i] + ".asset");
            var objective = Load<ObjectiveTemplateData>(Auto + "/Objectives/" + objectives[i] + ".asset");
            var teams = activity.Teams.ToList();
            if (teams.Count != 2 || !teams[0].Objectives.Any(o => o.Template == objective))
                throw new InvalidOperationException("Expected two teams and the player's progress Objective in " + activity.name);
            object team = teams[1];
            var entries = teams[1].Objectives.Where(o => o.Template != objective).ToList();
            object entry = new ActivityTeamObjectiveData();
            Set(entry, "template", objective);
            entries.Insert(0, (ActivityTeamObjectiveData)entry);
            Set(team, "objectives", entries);
            teams[1] = (ActivityTeamData)team;
            Set(activity, "teams", teams);

            string level = i == 0 ? "Level01" : "Level02";
            var populationObjective = Load<ObjectiveTemplateData>(Auto + "/Objectives/" + level + "ReplenishmentObjective.asset");
            BindProgressConditionsToParticipant(populationObjective.Root.ActivateConditions, progress);
            BindProgressConditionsToParticipant(populationObjective.Root.SuccessConditions, progress);
            BindProgressConditionsToParticipant(populationObjective.Root.FailConditions, progress);
            BindProgressConditionsToParticipant(populationObjective.ResetConditions, progress);
            EditorUtility.SetDirty(populationObjective);

            var definition = Load<AgentDefinitionData>(Auto + "/Agents/" + level + "PopulationAgent.asset");
            foreach (var condition in definition.ApplicabilityConditions.OfType<AgentEconomyApplicabilityConditionData>())
                if (condition.Selection.ExactAsset == progress)
                    Set(condition, "owner", new EconomyOperationOwnerBindingData());
            var population = (PopulationAgentData)definition.Agent;
            if (population.Progress.Resource != progress)
                throw new InvalidOperationException("Unexpected Population progress resource in " + definition.name);
            Set(population.Progress, "owner", new EconomyOperationOwnerBindingData());
            EditorUtility.SetDirty(definition);

            var brain = Load<OrchestratorAIBrainData>(Auto + "/Orchestration/" + level + "EnemyBrain.asset");
            SetProgressOperation(brain, "IssueProgressOperator", EconomyOperation.Issue, progress, walletTag);
            SetProgressOperation(brain, "ConsumeProgressOperator", EconomyOperation.Consume, progress, walletTag);
            EditorUtility.SetDirty(brain);
        }

        Set(displacement, "scopeType", ActivityAnalyticsAggregationScopeType.Activity);
        Set(resourceMetric, "scopeType", ActivityAnalyticsAggregationScopeType.Participant);
        if (!analytics.Metrics.Contains(elapsed)) analytics.Metrics.Add(elapsed);
        if (!analytics.Metrics.Contains(displacement)) analytics.Metrics.Add(displacement);
        EditorUtility.SetDirty(analytics);
        AssetDatabase.SaveAssets();
        return "Both teams have their level's progress Objective; enemy progress consumers use ContextOwner. Enemy brains reconcile LevelProgress only for Equal requirements. Analytics observes the common hero and each participant's own progress balance.";
    }

    static void BindProgressConditionsToParticipant(IEnumerable<ObjectiveCondition> conditions, EconomyAssetData progress)
    {
        foreach (var condition in conditions)
        {
            if (condition is ObjectiveConditionEconomyMetric economy && economy.Asset == progress)
                Set(economy, "observedOwner", new EconomyOperationOwnerBindingData());
            if (condition is IObjectiveProgressiveLongCondition numeric && numeric.TargetProgression != null
                && Get<EconomyAssetData>(numeric.TargetProgression, "resource") == progress)
                Set(numeric.TargetProgression, "owner", new EconomyOperationOwnerBindingData());
            if (condition.NestedConditions != null)
                BindProgressConditionsToParticipant(condition.NestedConditions, progress);
        }
    }

    static void SetProgressOperation(OrchestratorAIBrainData brain, string name, EconomyOperation operation,
        EconomyAssetData progress, TaxonomyTermData walletTag)
    {
        var operatorId = Load<TaxonomyTermData>(Auto + "/Orchestration/Taxonomy/" + name + ".asset");
        var op = new EconomyOperationDecompOpData();
        Set(op, "operatorId", operatorId);
        Set(op, "operation", operation);
        Set(op, "selection", Selection(progress, walletTag));
        brain.Operators.RemoveAll(o => o.OperatorId == operatorId);
        brain.Operators.Add(op);

        var fact = new FactTypeDecisionConditionData();
        Set(fact, "factType", OrchestrationFactType.EconomyAmount);
        var compare = new CompareOperationDecisionConditionData();
        Set(compare, "compareOperations", new List<CompareOperation> { CompareOperation.Equal });
        var decision = new OrchestrationDecisionData();
        Set(decision, "decisionId", name);
        Set(decision, "operatorId", operatorId);
        Set(decision, "conditions", new List<OrchestrationDecisionConditionData> { fact, compare });
        brain.DecisionGraph.Nodes.RemoveAll(n => n is OrchestrationDecisionData d && d.OperatorId == operatorId);
        brain.DecisionGraph.Nodes.Add(decision);
    }

    public static string SetMetaTabContentActive()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Meta tab authoring requires Edit Mode.");

        string path = Root + "/Scenes/FrameworkMain.unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
        bool openedHere = !scene.isLoaded;
        if (openedHere)
            scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            var main = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform.Find("MainPanel/Content");
            var contents = new List<GameObject>();
            foreach (string panel in new[] { "HeroUI", "UnitUI", "BattleUI" })
            {
                var content = main.Find(panel + "/Content");
                if (content == null)
                    throw new InvalidOperationException("Missing tab content: " + panel);
                contents.Add(content.gameObject);
            }
            foreach (var content in contents)
                content.SetActive(true);
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save FrameworkMain tab content.");
        }
        finally
        {
            if (openedHere)
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        }
        return "Enabled the three inner Content objects; tab roots remain controlled by TabSelectButton.";
    }

    public static string SetMetaEconomyOperations()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Meta economy authoring requires Edit Mode.");

        var operations = new Dictionary<string, EconomyOperation>
        {
            { "Resources/Coins.asset", MetaResourceOperations },
            { "Resources/PerfumeCard.asset", MetaResourceOperations },
            { "Resources/TabascoCard.asset", MetaResourceOperations },
            { "Resources/WaterCard.asset", MetaResourceOperations },
            { "Resources/ColaCard.asset", MetaResourceOperations },
            { "Resources/Level.asset", MetaResourceOperations },
            { "Production/HeroProduction.asset", MetaProductionOperations },
            { "Production/MetaProduction.asset", MetaProductionOperations },
            { "Production/HeroProductionHost.asset", MetaHostOperations },
            { "Production/MetaProductionHost.asset", MetaHostOperations },
        };
        var assets = operations.Keys.ToDictionary(path => path, path => Load<EconomyAssetData>(Data + "/" + path));
        foreach (var pair in assets)
            if (pair.Value.AllowedOperations != EconomyOperation.None)
                throw new InvalidOperationException("Expected unset operations in " + pair.Key);
        foreach (var pair in assets)
        {
            Set(pair.Value, "allowedOperations", operations[pair.Key]);
            AssetDatabase.SaveAssetIfDirty(pair.Value);
        }
        return "Authored explicit Economy operation permissions for 6 resources, 2 productions and 2 hosts.";
    }

    public static string SeparateEnemyElementPowerWallets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Enemy seed authoring requires Edit Mode.");

        var power = Load<Core.Attributes.AttributeData>(Shared + "/Attributes/Power.asset");
        var unitWallet = Load<EconomyWalletData>(Auto + "/Economy/UnitWallet.asset");
        var physical = Load<TaxonomyTermData>(Shared + "/Taxonomy/PhysicalElement.asset");
        var elementNames = new List<string> { "Fire", "Water", "Earth", "Air", "Dark", "Light", "Poison", "Chaos" };
        var elements = elementNames.Select(name => Load<TaxonomyTermData>(Shared + "/Taxonomy/" + name + "Element.asset")).ToList();
        var enemyNames = new List<string> { "BugBrownSmall", "BugBrownMedium", "BugGreenSmall", "BugGreenMedium", "BugPurpleSmall", "BugPurpleMedium" };
        var enemies = enemyNames.Select(name => Load<CapabilityHostData>(Auto + "/Economy/" + name + ".asset")).ToList();

        // Validate the exact source data before creating or changing any asset.
        foreach (var enemy in enemies)
        {
            var source = enemy.WalletEntries.Single(entry => entry.Wallet == unitWallet);
            var powers = source.Seed.Where(seed => seed.Asset == power).ToList();
            if (powers.Count != elements.Count + 1 || powers.Any(seed => seed.FormType != EconomyFormType.Stack))
                throw new InvalidOperationException("Unexpected Power seeds in " + enemy.name);
            foreach (var element in elements.Concat(Terms(physical)))
                if (powers.Count(seed => seed.RuntimeTags.Count == 1 && seed.RuntimeTags[0] == element) != 1)
                    throw new InvalidOperationException("Expected one Power seed for " + element.name + " in " + enemy.name);
        }
        foreach (var name in elementNames)
            if (AssetDatabase.LoadMainAssetAtPath(Auto + "/Economy/" + name + "PowerWallet.asset") != null)
                throw new InvalidOperationException("Power wallet already authored: " + name);

        var wallets = new List<EconomyWalletData>();
        for (int index = 0; index < elements.Count; index++)
        {
            var wallet = Asset<EconomyWalletData>(Auto + "/Economy/" + elementNames[index] + "PowerWallet.asset");
            Set(wallet, "id", "chainrush.wallet.autobattle.power." + elementNames[index].ToLowerInvariant());
            Set(wallet, "tags", Terms(elements[index]));
            wallets.Add(wallet);
            AssetDatabase.SaveAssetIfDirty(wallet);
        }
        foreach (var enemy in enemies)
        {
            var source = enemy.WalletEntries.Single(entry => entry.Wallet == unitWallet);
            var retained = source.Seed.Where(seed => seed.Asset != power || seed.RuntimeTags.Contains(physical));
            var entries = enemy.WalletEntries.Select(entry => entry.Wallet == unitWallet
                ? new WalletEntry(unitWallet, retained) : entry).ToList();
            for (int index = 0; index < elements.Count; index++)
                entries.Add(new WalletEntry(wallets[index], source.Seed.Where(seed => seed.Asset == power && seed.RuntimeTags.Contains(elements[index]))));
            Set(enemy, "walletEntries", entries);
            AssetDatabase.SaveAssetIfDirty(enemy);
        }
        return "Authored 8 element Power wallets and separated seeds in 6 enemy definitions; amounts, forms and runtime tags preserved.";
    }

    public static string CreateMetaNavigation()
    {
        var available = Load<TaxonomyTermData>(Data + "/Taxonomy/Available.asset");
        var selected = Load<TaxonomyTermData>(Data + "/Taxonomy/Selected.asset");
        var levelRole = Tag("PlayableLevel", Load<TaxonomyFamilyData>(Data + "/Taxonomy/MetaRoleFamily.asset"));
        var levelWalletTag = Tag("LevelCatalog", Load<TaxonomyFamilyData>(Data + "/Taxonomy/MetaWalletFamily.asset"));
        var levelWallet = Wallet("LevelCatalog", levelWalletTag);
        var levels = new[] { Load<ActivityData>(Auto + "/Definition/AutobattleActivity.asset"), Load<ActivityData>(Auto + "/Definition/DistanceActivity.asset") };
        foreach (var level in levels)
            Set(level, "tags", level.Tags.Concat(Terms(levelRole)).Distinct().ToList());
        var player = Load<PlayerData>(Data + "/FrameworkProfile.asset");
        var wallets = Get<List<WalletEntry>>(player, "walletEntries");
        wallets.Add(new WalletEntry(levelWallet, new[] {
            new SeedEntry(levels[0], 1, EconomyFormType.Stack, Terms(available, selected)),
            new SeedEntry(levels[1], 1, EconomyFormType.Stack, Terms(available)) }));
        Set(player, "walletEntries", wallets);
        var eventFamily = Family("MetaFlow");
        var openMain = Tag("OpenMain", eventFamily); var startLevel = Tag("StartLevel", eventFamily);
        var mainScene = SceneData("FrameworkMain"); var loadingScene = SceneData("FrameworkLoading");
        var gameScene = SceneData("FrameworkGameplay");
        var meta = Asset<GameFlowTemplateData>(Data + "/GameFlow/MetaFlow.asset");
        var metaActivity = Load<ActivityData>(Data + "/Activity/MetaActivity.asset");
        var metaRoot = new ActivityFlowContainerData();
        Set(metaRoot, "id", "meta"); Set(metaRoot, "activity", metaActivity);
        Set(metaRoot, "activateConditions", new List<GameFlowCondition> { Signal(openMain) });
        Set(metaRoot, "completedPolicy", GameFlowTerminalPolicy.Restart);
        Set(metaRoot, "steps", new List<GameFlowStepData> {
            Step("resolve-meta-participant", new GameFlowResolveParticipantsExecutorData()),
            Step("launch-meta", new GameFlowLaunchActivityExecutorData()),
            Step("await-meta-ready", null, ActivityReady(metaActivity)),
            Step("open-main", ShowScene("main", mainScene)),
            Step("await-start", null, Signal(startLevel)),
            // The replacement scene is loaded before releasing the previous presentation.
            Step("await-loading", null, SceneActive(loadingScene)) });
        Set(meta, "id", "chainrush.meta.flow"); Set(meta, "root", metaRoot);
        var flows = new List<GameFlowTemplateData> { meta };
        for (int index = 0; index < levels.Length; index++)
        {
            string name = index == 0 ? "Survive" : "Distance";
            var flow = Asset<GameFlowTemplateData>(Data + "/GameFlow/" + name + "Flow.asset");
            var root = new ActivityFlowContainerData(); Set(root, "id", name.ToLowerInvariant()); Set(root, "activity", levels[index]);
            var selectedLevel = new GameFlowConditionEconomyMetric();
            Set(selectedLevel, "walletTags", Terms(levelWalletTag)); Set(selectedLevel, "asset", levels[index]);
            Set(selectedLevel, "formType", EconomyFormType.Stack); Set(selectedLevel, "requiredRuntimeTags", Terms(available, selected));
            Set(selectedLevel, "targetValue", 1L);
            Set(root, "activateConditions", new List<GameFlowCondition> { Signal(startLevel), selectedLevel });
            Set(root, "failedPolicy", GameFlowTerminalPolicy.Restart);
            var ready = Step("await-autobattle-ready", null, ActivityReady(levels[index]));
            Set(ready, "failConditions", new List<GameFlowCondition> { ActivityFailure(levels[index]) });
            var board = new GameFlowPublishChildActivationExecutorData();
            Set(board, "taxonomy", Terms(Load<TaxonomyTermData>(Shared + "/Taxonomy/BoardActivationTerm.asset")));
            Set(root, "steps", new List<GameFlowStepData> {
                Step("open-loading", ShowScene("loading", loadingScene)),
                Step("await-loading", null, SceneActive(loadingScene)),
                Step("resolve-participants", new GameFlowResolveParticipantsExecutorData()),
                Step("launch-autobattle", new GameFlowLaunchActivityExecutorData()), ready,
                Step("open-gameplay", ShowScene("gameplay", gameScene)),
                Step("await-gameplay", null, SceneActive(gameScene)),
                Step("publish-board-activation", board) });
            Set(flow, "id", "chainrush.meta." + name.ToLowerInvariant()); Set(flow, "root", root); flows.Add(flow);
        }
        var startup = Asset<GameStartupPlanData>(Data + "/GameFlow/MetaStartupPlan.asset");
        var actions = new List<GameStartupActionData> {
            Load<GameStartupActionData>("Assets/Game/Runtime/Startup/AddChainRushBoardFlow.asset") };
        foreach (var flow in flows)
        {
            var action = Asset<AddGameFlowRuntimeActionData>(Data + "/GameFlow/Add" + flow.name + ".asset");
            Set(action, "template", flow); Set(action, "externalKey", flow.Id); actions.Add(action);
        }
        Set(startup, "actions", actions.ToArray());
        var installer = Load<Core.GameFlow.GameRuntime.Installers.GameFlowDefinitionsInstallerData>("Assets/Game/Runtime/Installers/ChainRushGameFlowDefinitionsInstaller.asset");
        Set(installer, "templates", Get<List<GameFlowTemplateData>>(installer, "templates").Concat(flows).Distinct().ToList());
        RegisterDefinitions(); AssetDatabase.SaveAssets();
        return "Authored two Selected levels, meta/gameplay flows and MetaStartupPlan. No runtime execution.";
    }

    static UI.SceneManagement.GameSceneData SceneData(string name)
    { var value = Asset<UI.SceneManagement.GameSceneData>(Data + "/Scenes/" + name + ".asset"); Set(value, "id", name); return value; }
    static GameFlowCondition Signal(TaxonomyTermData term)
    { var value = new GameFlowConditionTaxonomy(); Set(value, "taxonomy", Terms(term)); return value; }
    static GameFlowCondition SceneActive(UI.SceneManagement.GameSceneData scene)
    { var value = new GameFlowConditionScene(); Set(value, "scene", scene); Set(value, "state", UILifecycleState.Active); return value; }
    static GameFlowCondition ActivityReady(ActivityData activity)
    {
        var value = new GameFlowConditionActivityLifecycle(); Set(value, "activity", activity);
        Set(value, "stage", Core.Activities.Events.ActivityLifecycleStage.Ready);
        Set(value, "result", Core.Activities.Events.ActivityLifecycleResult.Success); return value;
    }
    static GameFlowCondition ActivityFailure(ActivityData activity)
    {
        var value = new GameFlowConditionActivityLifecycle(); Set(value, "activity", activity);
        Set(value, "stage", Core.Activities.Events.ActivityLifecycleStage.Ready);
        Set(value, "result", Core.Activities.Events.ActivityLifecycleResult.Fail); return value;
    }
    static GameFlowStepData Step(string id, GameFlowExecutorData executor, params GameFlowCondition[] conditions)
    { var value = new GameFlowStepData(); Set(value, "id", id); Set(value, "executor", executor); Set(value, "successConditions", conditions.ToList()); return value; }
    static GameFlowPresentationExecutorData ShowScene(string key, UI.SceneManagement.GameSceneData scene)
    {
        var ui = new UIScenePresentationData(); Set(ui, "scene", scene);
        var presentation = new GameFlowUIPresentationData(); Set(presentation, "presentation", ui);
        var executor = new GameFlowPresentationExecutorData(); Set(executor, "presentationKey", key); Set(executor, "presentation", presentation); return executor;
    }

    public static string ConnectFrameworkBattleUI()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root + "/Scenes/FrameworkMain.unity");
        var old = Object.FindFirstObjectByType<BattlePanel>(FindObjectsInactive.Include);
        var panel = old.gameObject;
        var markerRoot = Get<Transform>(old, "markerRoot");
        var progressRoot = Get<RectTransform>(old, "progressRoot");
        progressRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, markerRoot.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().spacing);
        var location = Load<LocationData>("Assets/Game/Resources/Locations/Location001Data.asset");
        var back = (GameObject)PrefabUtility.InstantiatePrefab(location.backPrefab, Get<Transform>(old, "backRoot"));
        var icon = (GameObject)PrefabUtility.InstantiatePrefab(location.iconPrefab.gameObject, Get<Transform>(old, "iconRoot"));
        PrefabUtility.UnpackPrefabInstance(icon, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(icon.GetComponent<LocationIcon>());
        var title = Get<TMPro.TextMeshProUGUI>(old, "locationLabel");
        var localize = title.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>()
            ?? title.gameObject.AddComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
        localize.StringReference = location.title;
        localize.OnUpdateString = new UnityEngine.Localization.Events.UnityEventString();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(localize.OnUpdateString, title.SetText);
        Get<GameObject>(old, "lockedLocation").SetActive(false); Get<GameObject>(old, "buttonsRoot").SetActive(true);
        Get<UnityEngine.UI.Button>(old, "nextButton").gameObject.SetActive(false);
        Get<UnityEngine.UI.Button>(old, "previousButton").gameObject.SetActive(false);
        var oldBar = Get<Progressbar>(old, "progressbar");
        var barFill = Get<UnityEngine.UI.Image>(oldBar, "progressFill");
        barFill.gameObject.SetActive(false); Object.DestroyImmediate(oldBar);

        var selected = Load<TaxonomyTermData>(Data + "/Taxonomy/Selected.asset");
        var available = Load<TaxonomyTermData>(Data + "/Taxonomy/Available.asset");
        var role = Load<TaxonomyTermData>(Data + "/Taxonomy/PlayableLevel.asset");
        var catalog = Load<TaxonomyTermData>(Data + "/Taxonomy/LevelCatalog.asset");
        string itemPath = Root + "/Prefabs/UI/LocationProgress/LevelMarkerUnlocked.prefab";
        Folder(System.IO.Path.GetDirectoryName(itemPath));
        if (!AssetDatabase.CopyAsset("Assets/Game/Prefabs/UI/LocationProgress/LevelMarkerUnlocked.prefab", itemPath))
            throw new InvalidOperationException("Could not copy level marker.");
        var itemRoot = PrefabUtility.LoadPrefabContents(itemPath);
        try
        {
            var normal = itemRoot.transform.GetChild(0).gameObject;
            var active = Object.Instantiate(Load<GameObject>("Assets/Game/Prefabs/UI/LocationProgress/LevelMarkerActive.prefab"), itemRoot.transform, false);
            active.name = "Selected"; active.SetActive(false);
            var item = itemRoot.AddComponent<Core.UI.EconomyListViewItem>();
            var selectedEvent = new UnityEngine.Events.UnityEvent();
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(selectedEvent, active.SetActive, true);
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(selectedEvent, normal.SetActive, false);
            Get<Dictionary<List<TaxonomyTermData>, UnityEngine.Events.UnityEvent>>(item, "tagEvents").Add(Terms(selected), selectedEvent);
            itemRoot.GetComponent<UnityEngine.UI.Button>().onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            PrefabUtility.SaveAsPrefabAsset(itemRoot, itemPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(itemRoot); }
        var source = panel.AddComponent<Core.UI.EconomyListUISource>();
        Set(source, "walletTags", Terms(catalog)); Set(source, "formType", EconomyFormType.Stack);
        Set(source, "requiredAssetTags", Terms(role)); Set(source, "requiredRuntimeTags", Terms(available));
        var select = panel.AddComponent<Core.UI.EconomyRuntimeTagUIAction>();
        Set(select, "mode", Core.UI.EconomyRuntimeTagUIActionMode.Add); Set(select, "runtimeTag", selected);
        Set(select, "minCount", 1); Set(select, "maxCount", 1); Set(select, "policy", Core.UI.EconomyRuntimeTagUIActionPolicy.Substitute);
        Set(select, "allowedGroup", new EconomyEntrySelectionData(null, EconomyFormType.Stack, Terms(catalog), Terms(role), null, Terms(available), null));
        var list = panel.AddComponent<Core.UI.EconomyListView>(); Set(list, "root", markerRoot); Set(list, "selectDefault", false);
        Get<Dictionary<List<TaxonomyTermData>, Core.UI.EconomyListViewItem>>(list, "prefabRoutes").Add(Terms(role), Load<GameObject>(itemPath).GetComponent<Core.UI.EconomyListViewItem>());
        var loaded = Get<Core.UI.EconomyListUISourceEvent>(source, "onSuccess");
        UnityEditor.Events.UnityEventTools.AddPersistentListener(loaded, select.Setup);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(loaded, list.Setup);
        var changed = Get<Core.UI.EconomyItemUIListEvent>(list, "onSelectionChanged");
        UnityEditor.Events.UnityEventTools.AddPersistentListener(changed, select.SetupSelection);
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(changed, select.Apply);
        var load = panel.AddComponent<Core.Events.UnityEventTrigger>(); Set(load, "mode", Core.Events.TriggerMode.OnEnable); Set(load, "triggerOnce", false);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(Get<UnityEngine.Events.UnityEvent>(load, "onTrigger"), source.Load);
        var start = Get<GameObject>(old, "playButton");
        var startButton = start.GetComponent<UnityEngine.UI.Button>(); startButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        var startSignal = AddSignal(start, "StartLevel");
        UnityEditor.Events.UnityEventTools.AddPersistentListener(startButton.onClick, startSignal.TriggerNow);
        foreach (var energy in start.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("Energy") || t.name == "Price").ToList())
            Object.DestroyImmediate(energy.gameObject);
        Object.DestroyImmediate(old);
        foreach (var go in scene.GetRootGameObjects().Where(g => g.name == "SceneLoader" || g.name == "GameScene" || g.name == "FX").ToList()) Object.DestroyImmediate(go);
        var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas");
        foreach (var loading in canvas.GetComponentsInChildren<Transform>(true).Where(t => t.name == "LoadingScreen").ToList()) Object.DestroyImmediate(loading.gameObject);
        var controller = canvas.AddComponent<UI.SceneManagement.GameScene>(); Set(controller, "scene", SceneData("FrameworkMain"));
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return "Connected two level markers and free start; replaced copied Main scene routing. Originals preserved.";
    }

    static GameFlowEventTrigger AddSignal(GameObject owner, string name)
    {
        var signal = owner.AddComponent<GameFlowEventTrigger>(); Set(signal, "taxonomy", Terms(Load<TaxonomyTermData>(Data + "/Taxonomy/" + name + ".asset")));
        Set(signal, "triggerOnce", false); return signal;
    }

    public static string ConnectFrameworkSceneEntry()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root + "/Scenes/FrameworkStart.unity");
        foreach (var root in scene.GetRootGameObjects().Where(g => g.name == "SceneLoader" || g.name == "GameScene").ToList()) Object.DestroyImmediate(root);
        var settings = scene.GetRootGameObjects().Single(g => g.name == "GameSettings");
        foreach (var component in settings.GetComponents<MonoBehaviour>().Where(c => c is GameManager || c is GameResourcesManager
            || c is LocalNotificationsManager || c is MyAnalyticsManager || c is RewardManager || c is SocialManager).ToList()) Object.DestroyImmediate(component);
        settings.name = "PresentationSettings";
        var runtime = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/Game/Runtime/Host/ChainRushGameRuntimeHost.prefab"));
        runtime.name = "FrameworkRuntime";
        var host = runtime.GetComponent<GameRuntimeHost>(); Set(host, "startupPlan", Load<GameStartupPlanData>(Data + "/GameFlow/MetaStartupPlan.asset"));
        Set(host, "owner", Load<PlayerData>(Data + "/FrameworkProfile.asset"));
        var open = AddSignal(runtime, "OpenMain");
        Set(host, "onInitialized", new UnityEngine.Events.UnityEvent());
        UnityEditor.Events.UnityEventTools.AddPersistentListener(Get<UnityEngine.Events.UnityEvent>(host, "onInitialized"), open.TriggerNow);
        PrefabUtility.RecordPrefabInstancePropertyModifications(host);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

        scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root + "/Scenes/FrameworkLoading.unity");
        foreach (var root in scene.GetRootGameObjects().Where(g => g.name == "SceneLoader" || g.name == "GameScene").ToList()) Object.DestroyImmediate(root);
        var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas");
        var loading = canvas.AddComponent<ActivityLoadingPresentationController>(); Set(loading, "launchStepId", "launch-autobattle");
        var oldBar = canvas.GetComponentInChildren<Progressbar>(true);
        var fill = Get<UnityEngine.UI.Image>(oldBar, "progressFill");
        var text = Get<TMPro.TextMeshProUGUI>(oldBar, "valueText");
        var bar = oldBar.gameObject.AddComponent<MoreMountains.Tools.MMProgressBar>();
        bar.ForegroundBar = fill.transform; bar.PercentageTextMeshPro = text;
        bar.TextFormat = "0"; bar.TextSuffix = "%"; bar.TextValueMultiplier = 100;
        bar.FillMode = MoreMountains.Tools.MMProgressBar.FillModes.FillAmount;
        bar.LerpForegroundBar = false; bar.BumpScaleOnChange = false; bar.ChangeColorWhenBumping = false;
        bar.SetInitialFillValueOnStart = false;
        fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 0;
        text.text = "0%";
        UnityEditor.Events.UnityEventTools.AddPersistentListener(Get<UnityEngine.Events.UnityEvent<float>>(loading, "onProgress"), bar.SetBar01);
        var returnMain = AddSignal(canvas, "OpenMain");
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(Get<UnityEngine.Events.UnityEvent<string>>(loading, "onFailed"), returnMain.TriggerNow);
        Object.DestroyImmediate(oldBar);
        ReplaceSafeAreas(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

        string gameplayPath = Root + "/Scenes/Integration/FrameworkGameplay.unity";
        Folder(System.IO.Path.GetDirectoryName(gameplayPath));
        if (!AssetDatabase.CopyAsset("Assets/Game/Scenes/Integration/ChainRushFrameworkIntegration.unity", gameplayPath))
            throw new InvalidOperationException("Could not copy integration presentation scene.");
        scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(gameplayPath);
        foreach (var previousHost in Object.FindObjectsByType<GameRuntimeHost>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(previousHost.gameObject);
        var controller = new GameObject("FrameworkGameplay").AddComponent<UI.SceneManagement.GameScene>(); Set(controller, "scene", SceneData("FrameworkGameplay"));
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        var scenes = EditorBuildSettings.scenes.ToList(); scenes.Add(new EditorBuildSettingsScene(gameplayPath, true)); EditorBuildSettings.scenes = scenes.ToArray();
        foreach (string name in new[] { "Survive", "Distance" })
        {
            var flow = Load<GameFlowTemplateData>(Data + "/GameFlow/" + name + "Flow.asset");
            var root = (ActivityFlowContainerData)flow.Root;
            Set(root.Steps.Single(s => s.Id == "await-autobattle-ready"), "failConditions", new List<GameFlowCondition> { ActivityFailure(root.Activity) });
            EditorUtility.SetDirty(flow);
        }
        scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root + "/Scenes/FrameworkMain.unity");
        ReplaceSafeAreas(scene); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Connected FrameworkStart, Activity Loading and the gameplay presentation; preserved old startup and originals. No Play mode or tests.";
    }

    static void ReplaceSafeAreas(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        foreach (var old in root.GetComponentsInChildren<global::SafeArea>(true))
        {
            var replacement = old.gameObject.AddComponent<Core.UI.SafeArea.SafeArea>();
            foreach (string field in new[] { "applySafeArea", "useTransformValueIfZero", "logging" }) Set(replacement, field, Get<bool>(old, field));
            Set(replacement, "applyTo", (Core.UI.SafeArea.SafeAreaBorder)(int)Get<global::SafeAreaBorder>(old, "applyTo"));
            Set(replacement, "OnRefresh", Get<UnityEngine.Events.UnityEvent<RectOffset>>(old, "OnRefresh"));
            Object.DestroyImmediate(old);
        }
    }

    public static string RemoveExcludedMainUI()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root + "/Scenes/FrameworkMain.unity");
        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "DailyRewardBanner" || t.name == "LeaderboardButton").ToList())
                if (item != null) Object.DestroyImmediate(item.gameObject);
            foreach (var old in root.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c is UnitActionButton || c is SpineTextItem || c is SceneLoader).ToList())
                Object.DestroyImmediate(old);
            // No remaining listener targets these original background size helpers in the copied panels.
            foreach (var safe in root.GetComponentsInChildren<Core.UI.SafeArea.SafeArea>(true))
            {
                var callbacks = Get<UnityEngine.Events.UnityEvent<RectOffset>>(safe, "OnRefresh");
                if (callbacks == null) continue;
                for (int i = callbacks.GetPersistentEventCount() - 1; i >= 0; i--)
                    if (callbacks.GetPersistentTarget(i) == null) UnityEditor.Events.UnityEventTools.RemovePersistentListener(callbacks, i);
                EditorUtility.SetDirty(safe);
            }
            foreach (var old in root.GetComponentsInChildren<SafeAreaSizeAdjuster>(true)) Object.DestroyImmediate(old);
        }
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        return "Removed excluded daily/social UI and disconnected original panel helpers from copied Main.";
    }

    public static string RemoveSupersededGameplayAssets()
    {
        var board = Load<ActivityData>("Assets/Game/Activities/Board/Definition/BoardActivity.asset");
        var teams = board.Teams.ToList();
        for (int i = 0; i < teams.Count; i++)
        {
            object team = teams[i]; Set(team, "wallets", teams[i].Wallets.Where(w => w.Wallet.name != "LoadoutWallet").ToList());
            teams[i] = (ActivityTeamData)team;
        }
        Set(board, "teams", teams);
        var delete = new List<string> {
            "Assets/Game/Runtime/Players/ChainRushLocalPlayer.asset",
            Shared + "/Economy/LoadoutWallet.asset", Shared + "/Taxonomy/LoadoutWalletTag.asset",
            Auto + "/Space/NormalHeroBody.asset", Auto + "/Space/TabascoBody.asset" };
        foreach (string name in new[] { "Level01Perfume", "Level01Tabasco", "Level02Tabasco" })
        {
            delete.Add("Assets/Game/Runtime/Startup/" + name + "StartupPlan.asset");
            delete.Add("Assets/Game/Runtime/Startup/Start" + name + ".asset");
        }
        var removed = new HashSet<Object>(delete.Select(Load<Object>));
        var economy = Load<EconomyDefinitionsInstallerData>("Assets/Game/Runtime/Installers/ChainRushEconomyDefinitionsInstaller.asset");
        Set(economy, "assets", Get<List<EconomyAssetData>>(economy, "assets").Where(a => !removed.Contains(a)).ToList());
        Set(economy, "wallets", Get<List<EconomyWalletData>>(economy, "wallets").Where(a => !removed.Contains(a)).ToList());
        var taxonomy = Load<TaxonomyRuntimeInstallerData>("Assets/Game/Runtime/Installers/ChainRushTaxonomyRuntimeInstaller.asset");
        Set(taxonomy, "terms", Get<TaxonomyTermData[]>(taxonomy, "terms").Where(a => !removed.Contains(a)).ToArray());
        AssetDatabase.SaveAssets();
        foreach (string path in delete) if (!AssetDatabase.DeleteAsset(path)) throw new InvalidOperationException("Could not delete " + path);
        RenameAuthoredAsset("Assets/Game/Runtime/Startup/Level02PerfumeStartupPlan.asset", "DistanceStartupPlan");
        RenameAuthoredAsset("Assets/Game/Runtime/Startup/StartLevel02Perfume.asset", "StartDistanceLevel");
        AssetDatabase.SaveAssets();
        return "Removed replaced Loadout and hero-specific startup copies. Kept the common hero body and two standalone level startup configurations.";
    }

    public static string FinishCommonUIPrefabs()
    {
        string rewardPath = Root + "/Prefabs/UI/RewardPopup.prefab";
        var reward = PrefabUtility.LoadPrefabContents(rewardPath);
        try { ReplaceSafeAreas(reward.scene); PrefabUtility.SaveAsPrefabAsset(reward, rewardPath); }
        finally { PrefabUtility.UnloadPrefabContents(reward); }
        string unitPath = Root + "/Prefabs/UI/UnitItems/UnitItem.prefab";
        var unit = PrefabUtility.LoadPrefabContents(unitPath);
        try
        {
            foreach (var progress in unit.GetComponentsInChildren<Progressbar>(true)) Object.DestroyImmediate(progress.gameObject);
            PrefabUtility.SaveAsPrefabAsset(unit, unitPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(unit); }
        string unusedCounter = Root + "/Prefabs/UI/ResourceBalanceItem.prefab";
        var consumers = AssetDatabase.FindAssets("t:Prefab t:Scene", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p != unusedCounter && AssetDatabase.GetDependencies(p, false).Contains(unusedCounter)).ToList();
        if (consumers.Count != 0) throw new InvalidOperationException("Counter still referenced: " + string.Join(", ", consumers));
        AssetDatabase.DeleteAsset(unusedCounter);
        AssetDatabase.SaveAssets();
        return "Finished RewardPopup SafeArea binding; removed inactive card-progress and deferred counter copy.";
    }

    public static string FinishMainBindings()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root + "/Scenes/FrameworkMain.unity");
        var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas");
        var main = canvas.transform.Find("MainPanel").gameObject;
        var input = main.GetComponent<CanvasGroup>();
        if (input == null) input = main.AddComponent<CanvasGroup>();
        foreach (var button in main.GetComponentsInChildren<Core.UI.PurchaseButton>(true))
        {
            BindBoolPropertyEvent(button, "onPurchaseStarted", input, "set_interactable", false);
            BindBoolPropertyEvent(button, "onPurchaseEnded", input, "set_interactable", true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
        }
        foreach (var safe in main.GetComponentsInChildren<Core.UI.SafeArea.SafeArea>(true))
        {
            var callbacks = Get<UnityEngine.Events.UnityEvent<RectOffset>>(safe, "OnRefresh");
            for (int i = callbacks.GetPersistentEventCount() - 1; i >= 0; i--)
                if (callbacks.GetPersistentTarget(i) == null)
                    UnityEditor.Events.UnityEventTools.RemovePersistentListener(callbacks, i);
            EditorUtility.SetDirty(safe);
            PrefabUtility.RecordPrefabInstancePropertyModifications(safe);
        }
        // These controls address excluded daily/idle screens or locations beyond Location001.
        foreach (var button in main.GetComponentsInChildren<UnityEngine.UI.Button>(true).ToList())
        {
            if (button.name == "DailyRewardsButton" || button.name == "Patrol"
                || ((button.name == "ButtonPrevious" || button.name == "ButtonNext")
                    && button.transform.IsChildOf(main.transform.Find("Content/BattleUI"))))
                Object.DestroyImmediate(button.gameObject);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        return "Bound both purchase events to the Main input group and removed callbacks of excluded UI.";
    }

    public static string SetRewardPopupFonts()
    {
        string path = Root + "/Prefabs/UI/RewardPopup.prefab";
        var popup = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var labels = popup.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            var title = labels.Single(label => label.name == "TitleLabel");
            var tap = labels.Single(label => label.name == "TapAnywhareLabel");
            title.font = Load<TMPro.TMP_FontAsset>("Assets/Game/Fonts/Cairo_Line_Blue SDF.asset");
            title.fontSharedMaterial = title.font.material;
            tap.font = Load<TMPro.TMP_FontAsset>("Assets/Game/Fonts/Cairo SDF.asset");
            tap.fontSharedMaterial = tap.font.material;
            PrefabUtility.SaveAsPrefabAsset(popup, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(popup); }
        return "Assigned the approved existing Cairo fonts to the two copied RewardPopup labels.";
    }

    public static string RenameFrameworkScenes()
    {
        foreach (string name in new[] { "Start", "Main", "Loading" })
        {
            string destination = Root + "/Scenes/Framework" + name + ".unity";
            string error = AssetDatabase.MoveAsset(Root + "/Scenes/" + name + ".unity", destination);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        var scenes = EditorBuildSettings.scenes.ToList();
        foreach (string name in new[] { "Start", "Main", "Loading" })
            scenes.Add(new EditorBuildSettingsScene(Root + "/Scenes/Framework" + name + ".unity", true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        return "Renamed the three new scene copies; appended them to Build Settings without changing the original startup order.";
    }

    public static string UnifyGameplayDefinitions()
    {
        foreach (string name in new[] { "Perfume", "Tabasco" })
        {
            string targetPath = Auto + "/Projection/" + name + ".prefab";
            var target = PrefabUtility.LoadPrefabContents(targetPath);
            var source = Load<GameObject>(Auto + "/Projection/" + name + "Distance.prefab");
            try
            {
                foreach (string region in new[] { "WaterProductionZone", "ColaProductionZone", "EnemyProductionVolume" })
                {
                    if (target.transform.Find(region) != null) throw new InvalidOperationException("Region already copied: " + region);
                    var child = Object.Instantiate(source.transform.Find(region).gameObject, target.transform, false); child.name = region;
                }
                PrefabUtility.SaveAsPrefabAsset(target, targetPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(target); }
        }
        RenameAuthoredAsset(Auto + "/Knowledge/PerfumeDisplacementMetric.asset", "HeroDisplacementMetric");
        RenameAuthoredAsset(Auto + "/Knowledge/PerfumeDistancePlayerAnalytics.asset", "DistancePlayerAnalytics");
        RenameAuthoredAsset(Auto + "/Objectives/PerfumeDistanceProgressObjective.asset", "DistanceProgressObjective");
        var metric = Load<Core.Activities.Analytics.EntityMovementActivityAnalyticsMetricData>(Auto + "/Knowledge/HeroDisplacementMetric.asset");
        Set(metric, "exactAsset", null);
        object filter = new Core.Activities.Analytics.ActivityAnalyticsTagFilterData();
        Set(filter, "includedTags", Terms(Load<CapabilityHostData>(Shared + "/Units/Perfume/Perfume.asset").Tags.Single(t => t.name == "HeroRole")));
        Set(metric, "objectTags", filter);
        var objective = Load<ObjectiveTemplateData>(Auto + "/Objectives/DistanceProgressObjective.asset");
        Set(objective.Root, "_id", "chainrush-progress-distance"); EditorUtility.SetDirty(objective);
        return ConnectUnifiedGameplayReferences();
    }

    public static string ConnectUnifiedGameplayReferences()
    {
        var map = new Dictionary<Object, Object>();
        var remove = new List<string>();
        Action<string, string> replace = (oldPath, newPath) =>
        {
            map.Add(Load<Object>(oldPath), Load<Object>(newPath)); remove.Add(oldPath);
        };
        foreach (string name in new[] { "Perfume", "Tabasco" })
        {
            replace(Shared + "/Units/" + name + "/" + name + "Distance.asset", Shared + "/Units/" + name + "/" + name + ".asset");
            replace(Shared + "/Taxonomy/" + name + "DistanceDefinition.asset", Shared + "/Taxonomy/" + name + "Definition.asset");
            remove.Add(Auto + "/Projection/" + name + "Distance.prefab");
        }
        replace(Auto + "/Knowledge/TabascoDisplacementMetric.asset", Auto + "/Knowledge/HeroDisplacementMetric.asset");
        replace(Auto + "/Knowledge/TabascoDistancePlayerAnalytics.asset", Auto + "/Knowledge/DistancePlayerAnalytics.asset");
        replace(Auto + "/Objectives/TabascoDistanceProgressObjective.asset", Auto + "/Objectives/DistanceProgressObjective.asset");
        replace(Auto + "/Definition/Level01TabascoActivity.asset", Auto + "/Definition/AutobattleActivity.asset");
        replace(Auto + "/Definition/Level02TabascoActivity.asset", Auto + "/Definition/DistanceActivity.asset");
        replace(Auto + "/GameFlow/Level01TabascoFlow.asset", Auto + "/GameFlow/AutobattleFlow.asset");
        replace(Auto + "/GameFlow/Level02TabascoFlow.asset", Auto + "/GameFlow/DistanceFlow.asset");
        map.Add(Load<PlayerData>("Assets/Game/Runtime/Players/ChainRushLocalPlayer.asset"), Load<PlayerData>(Data + "/FrameworkProfile.asset"));
        var paths = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Game/Activities", "Assets/Game/Runtime", Data })
            .Select(AssetDatabase.GUIDToAssetPath).Distinct().Where(p => !remove.Contains(p)).ToList();
        foreach (string path in paths) ReplaceAuthoredReferences(Load<Object>(path), map);
        var economy = Load<EconomyDefinitionsInstallerData>("Assets/Game/Runtime/Installers/ChainRushEconomyDefinitionsInstaller.asset");
        Set(economy, "assets", Get<List<EconomyAssetData>>(economy, "assets").Distinct().ToList());
        var taxonomy = Load<TaxonomyRuntimeInstallerData>("Assets/Game/Runtime/Installers/ChainRushTaxonomyRuntimeInstaller.asset");
        Set(taxonomy, "terms", Get<TaxonomyTermData[]>(taxonomy, "terms").Distinct().ToArray());
        var flows = Load<Core.GameFlow.GameRuntime.Installers.GameFlowDefinitionsInstallerData>("Assets/Game/Runtime/Installers/ChainRushGameFlowDefinitionsInstaller.asset");
        Set(flows, "templates", Get<List<Core.GameFlow.GameFlowTemplateData>>(flows, "templates").Distinct().ToList());
        var runtimePath = "Assets/Game/Runtime/Host/ChainRushGameRuntimeHost.prefab";
        var runtime = PrefabUtility.LoadPrefabContents(runtimePath);
        try
        {
            foreach (var component in runtime.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null) ReplaceAuthoredReferences(component, map);
            PrefabUtility.SaveAsPrefabAsset(runtime, runtimePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(runtime); }
        foreach (string path in new[] { Auto + "/Definition/AutobattleActivity.asset", Auto + "/Definition/DistanceActivity.asset" })
        {
            var activity = Load<ActivityData>(path); var teams = activity.Teams.ToList(); object team = teams[0];
            var rules = teams[0].EntryRules.ToList();
            foreach (var comparison in new[] { CompareOperation.GreaterOrEqual, CompareOperation.LessOrEqual })
            {
                var rule = new ActivityEntryRule(); Set(rule, "walletTags", Terms(Load<TaxonomyTermData>(Data + "/Taxonomy/CharacterCatalog.asset")));
                Set(rule, "assetTags", Terms(Load<TaxonomyTermData>(Data + "/Taxonomy/Unit.asset")));
                Set(rule, "runtimeTags", Terms(Load<TaxonomyTermData>(Data + "/Taxonomy/Selected.asset"), Load<TaxonomyTermData>(Data + "/Taxonomy/Available.asset")));
                Set(rule, "compareOperation", comparison); Set(rule, "amount", comparison == CompareOperation.GreaterOrEqual ? 1L : 4L);
                rules.Add(rule);
            }
            Set(team, "entryRules", rules); teams[0] = (ActivityTeamData)team; Set(activity, "teams", teams);
        }
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/Integration/ChainRushFrameworkIntegration.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            foreach (var go in scene.GetRootGameObjects().Where(g => g.name == "Level01TabascoActivityViewport" || g.name == "Level02TabascoActivityViewport")) Object.DestroyImmediate(go);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        AssetDatabase.SaveAssets();
        foreach (string path in remove) if (!AssetDatabase.DeleteAsset(path)) throw new InvalidOperationException("Could not delete " + path);
        AssetDatabase.SaveAssets();
        return "Unified hero definitions and projection providers, Distance analytics/objective and two Activities; rebound FrameworkProfile and entry rules. Startup/fixture cleanup remains.";
    }

    static void RenameAuthoredAsset(string path, string name)
    {
        string error = AssetDatabase.RenameAsset(path, name);
        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        var asset = Load<Object>(System.IO.Path.GetDirectoryName(path).Replace('\\', '/') + "/" + name + ".asset");
        asset.name = name; EditorUtility.SetDirty(asset);
    }

    static void ReplaceAuthoredReferences(Object asset, Dictionary<Object, Object> replacements)
    {
        var serialized = new SerializedObject(asset); var property = serialized.GetIterator(); bool changed = false;
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null) continue;
            if (!replacements.TryGetValue(property.objectReferenceValue, out var replacement)) continue;
            property.objectReferenceValue = replacement; changed = true;
        }
        if (!changed) return;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (asset is Sirenix.OdinInspector.SerializedScriptableObject || asset is Sirenix.OdinInspector.SerializedMonoBehaviour)
            ((ISerializationCallbackReceiver)asset).OnAfterDeserialize();
        EditorUtility.SetDirty(asset);
    }

    public static string CreateUICopies()
    {
        var paths = new List<string> {
            "Scenes/Start.unity", "Scenes/Main.unity", "Scenes/Loading.unity",
            "Prefabs/UI/BuyButton.prefab", "Prefabs/UI/BuyButtonPriceItem.prefab",
            "Prefabs/UI/ResourceBalanceItem.prefab", "Prefabs/UI/UnitItems/UnitItem.prefab",
            "Prefabs/UI/RewardResourceVerticaltem.prefab", "Prefabs/UI/RewardResourceVerticaltemVFX.prefab",
            "Prefabs/UI/RewardUnitVerticaltem.prefab", "Prefabs/UI/RewardUnitVerticaltemVFX.prefab",
            "Prefabs/UI/RewardUnitCardVerticaltem.prefab", "Prefabs/UI/RewardUnitCardVerticaltemVFX.prefab"
        };
        foreach (var relative in paths)
        {
            string source = "Assets/Game/" + relative;
            string target = Root + "/" + relative;
            if (AssetDatabase.LoadMainAssetAtPath(source) == null) throw new InvalidOperationException("Missing original UI: " + source);
            if (AssetDatabase.LoadMainAssetAtPath(target) != null)
                throw new InvalidOperationException("UI copy already exists; do not overwrite ongoing wiring: " + target);
        }
        foreach (var relative in paths)
        {
            string target = Root + "/" + relative;
            Folder(System.IO.Path.GetDirectoryName(target).Replace('\\', '/'));
            if (!AssetDatabase.CopyAsset("Assets/Game/" + relative, target))
                throw new InvalidOperationException("Unity could not copy " + relative);
        }
        AssetDatabase.SaveAssets();
        return "Created 3 scene and 10 prefab copies under FrameworkUI. Originals unchanged; the copies still require framework component and reference wiring.";
    }

    public static string RegeneratePackageUIIdentities()
    {
        const string package = "Packages/com.morboo.framework/";
        var paths = new[] { "Scripts/Core/UI/Tabs/TabManager.cs", "Scripts/Core/UI/Tabs/TabSelectButton.cs",
            "Scripts/Core/UI/Unlocker/Unlocker.cs", "Scripts/Core/UI/Unlocker/UnlockCondition.cs" };
        var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(package + paths[0]);
        if (info == null || info.source != UnityEditor.PackageManager.PackageSource.Local)
            throw new InvalidOperationException("Expected the writable local Framework package.");
        var identities = paths.ToDictionary(p => p, p => AssetDatabase.AssetPathToGUID(package + p));
        foreach (var path in paths)
            if (string.IsNullOrEmpty(identities[path])) throw new InvalidOperationException("Unimported script: " + path);
        EditorApplication.LockReloadAssemblies();
        try
        {
            foreach (var path in paths)
            {
                string original = package + path;
                string copy = original.Replace(".cs", ".NewIdentity.cs");
                if (AssetDatabase.LoadMainAssetAtPath(copy) != null)
                    throw new InvalidOperationException("Identity copy already exists: " + copy);
                // CopyAsset owns GUID generation. Local packages do not support MoveAsset.
                if (!AssetDatabase.CopyAsset(original, copy))
                    throw new InvalidOperationException("Unity refused to create a new script identity: " + path);
                AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport);
                if (!AssetDatabase.DeleteAsset(original))
                {
                    AssetDatabase.DeleteAsset(copy);
                    throw new InvalidOperationException("Unity refused to remove the old script identity: " + path);
                }
                if (!AssetDatabase.CopyAsset(copy, original))
                    throw new InvalidOperationException("Unity could not restore the script from " + copy);
                if (!AssetDatabase.DeleteAsset(copy))
                    throw new InvalidOperationException("Unity could not delete the temporary identity copy: " + copy);
            }
        }
        finally
        {
            EditorApplication.UnlockReloadAssemblies();
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return string.Join("\n", paths.Select(p => p + ": " + identities[p] + " -> " + AssetDatabase.AssetPathToGUID(package + p)));
    }

    public static string CreateMetaContent()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Author content outside Play mode.");
        var definitionFamily = Load<TaxonomyFamilyData>(Shared + "/Taxonomy/CharacterDefinitionFamily.asset");
        var sourceSeeds = AssetDatabase.FindAssets("t:ActivityData", new[] { Auto + "/Definition" })
            .Select(id => Load<ActivityData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(a => a.Teams).SelectMany(t => t.Wallets)
            .Where(w => w.Wallet.name == "DevelopmentWallet")
            .SelectMany(w => w.Seed).Select(s => s.Seed).ToList();
        if (sourceSeeds.Count == 0) throw new InvalidOperationException("Capture authored base values before removing Activity development seeds.");

        var stateFamily = Family("MetaState");
        var walletFamily = Family("MetaWallet");
        var roleFamily = Family("MetaRole");
        var entitled = Tag("Entitled", stateFamily);
        var available = Tag("Available", stateFamily);
        var selected = Tag("Selected", stateFamily);
        var unitRole = Tag("Unit", roleFamily);
        var producerRole = Tag("MetaProducer", roleFamily);
        var catalogTag = Tag("CharacterCatalog", walletFamily);
        var currencyTag = Tag("Currency", walletFamily);
        var cardsTag = Tag("Cards", walletFamily);
        var levelTag = Tag("Level", walletFamily);
        var contentTag = Tag("BoardContent", walletFamily);
        var developmentTag = Load<TaxonomyTermData>(Shared + "/Taxonomy/DevelopmentWalletTag.asset");
        var catalogWallet = Wallet("CharacterCatalog", catalogTag);
        var currencyWallet = Wallet("Currency", currencyTag);
        var cardsWallet = Wallet("Cards", cardsTag);
        var levelWallet = Wallet("Level", levelTag);
        var contentWallet = Wallet("BoardContent", contentTag);
        var level = Economy<FrameworkResourceData>("Level", "Resources");
        Set(level, "allowedOperations", MetaResourceOperations);
        var coins = Economy<FrameworkResourceData>("Coins", "Resources");
        Set(coins, "allowedOperations", MetaResourceOperations);
        Set(coins, "icon", Load<EconomyAssetData>(Shared + "/Economy/RunGold.asset").Icon);

        var catalogueSeeds = new List<SeedEntry>();
        var cardSeeds = new List<SeedEntry>();
        var upgradeEntries = new List<ProductionCatalogEntryData>();
        foreach (string character in new[] { "Perfume", "Tabasco", "Water", "Cola" })
        {
            var original = Load<UnitData>("Assets/Game/Resources/Units/" + character + "Data.asset");
            string baseName = character == "Water" || character == "Cola" ? character + "Unit" : character;
            var baseHost = Load<CapabilityHostData>(Shared + "/Units/" + character + "/" + baseName + ".asset");
            var cards = Economy<FrameworkResourceData>(character + "Card", "Resources");
            Set(cards, "allowedOperations", MetaResourceOperations);
            Set(cards, "icon", original.icon);
            CopyPresentation(original, cards);
            cardSeeds.Add(new SeedEntry(cards, 5, EconomyFormType.Stack));
            var stateTags = character == "Tabasco" ? new List<TaxonomyTermData> { entitled }
                : new List<TaxonomyTermData> { entitled, available, selected };
            catalogueSeeds.Add(new SeedEntry(baseHost, 1, EconomyFormType.Stack, stateTags));
            var permanentWallets = new List<WalletEntry>();
            var outputs = new List<ProductionOutputData>
            {
                new ProductionOutputData(level, EconomyFormType.Stack, Terms(levelTag), new LongFlatProgressionData(1))
            };
            int forms = character == "Water" || character == "Cola" ? 4 : 1;
            for (int form = 0; form < forms; form++)
            {
                string hostName = baseName + (form == 0 ? "" : (form + 1).ToString());
                var host = Load<CapabilityHostData>(Shared + "/Units/" + character + "/" + hostName + ".asset");
                var definitionTag = host.Tags.Single(t => t.Family == definitionFamily);
                var taggedBase = sourceSeeds.Where(s => s.RuntimeTags.Contains(definitionTag))
                    .GroupBy(s => s.Asset.Id + ":" + string.Join(",", s.RuntimeTags.Select(t => t.Id).OrderBy(v => v)))
                    .Select(g => {
                        if (g.Select(s => s.Amount).Distinct().Count() != 1)
                            throw new InvalidOperationException("Conflicting authored bases for " + hostName + "/" + g.Key);
                        return new SeedEntry(g.First());
                    }).ToList();
                if (taggedBase.Count == 0) throw new InvalidOperationException("No authored base for " + hostName);
                var baseWallet = Wallet(hostName + "Base");
                var permanentAddressTag = Tag(hostName + "Development", walletFamily);
                var permanentWallet = Wallet(hostName + "Development", developmentTag, permanentAddressTag);
                permanentWallets.Add(new WalletEntry(permanentWallet));
                var localBase = taggedBase.Select(s => new SeedEntry(s.Asset, s.Amount, s.FormType,
                    s.RuntimeTags.Where(t => t.Family != definitionFamily))).ToList();
                var wallets = host.WalletEntries.Where(w => !AssetDatabase.GetAssetPath(w.Wallet).StartsWith(Data + "/Wallets/", StringComparison.Ordinal)).ToList();
                wallets.Add(new WalletEntry(baseWallet, localBase));
                Set(host, "walletEntries", wallets);
                var sources = host.AttributeSources.Where(s => s.Id != "participant-development").ToList();
                var owner = new EconomyOperationOwnerBindingData();
                Set(owner, "entryPath", new List<EconomyEntrySelectionData> { Selection(baseHost, catalogTag) });
                var development = new CapabilityHostAttributeSourceData();
                Set(development, "id", "participant-development");
                Set(development, "owner", owner);
                Set(development, "walletTags", Terms(developmentTag));
                Set(development, "hostSelectorFamilies", new List<TaxonomyFamilyData> { definitionFamily });
                sources.Add(development);
                Set(host, "attributeSources", sources);
                if (forms > 1 && !host.Tags.Contains(unitRole))
                    Set(host, "tags", host.Tags.Concat(Terms(unitRole)).ToList());
                if (form == 0) CopyPresentation(original, host);
                foreach (var seed in taggedBase)
                {
                    if (!Enum.TryParse(seed.Asset.name, out global::Attribute sourceAttribute)
                        || !original.attributes.TryGetValue(sourceAttribute, out var growth))
                        throw new InvalidOperationException("No source progression for " + seed.Asset.name);
                    double sourceBase = growth.GetValue(0);
                    if (sourceBase <= 0) throw new InvalidOperationException("A positive source base is required for " + seed.Asset.name);
                    var deltas = new List<long>(99);
                    for (int n = 0; n < 99; n++)
                    {
                        long before = checked((long)Math.Round(growth.GetValue(n) / sourceBase * seed.Amount, MidpointRounding.AwayFromZero));
                        long after = checked((long)Math.Round(growth.GetValue(n + 1) / sourceBase * seed.Amount, MidpointRounding.AwayFromZero));
                        if (after < before) throw new InvalidOperationException("Negative development delta requires an explicit content decision.");
                        deltas.Add(after - before);
                    }
                    outputs.Add(new ProductionOutputData(seed.Asset, EconomyFormType.Stack, Terms(permanentAddressTag),
                        new LongListProgressionData(deltas, 0, 1), seed.RuntimeTags));
                }
            }
            var baseEntries = baseHost.WalletEntries.ToList();
            baseEntries.AddRange(permanentWallets);
            baseEntries.Add(new WalletEntry(levelWallet));
            var boardSeed = new List<SeedEntry>();
            string board = character == "Perfume" ? "LightningBolt" : forms > 1 ? character : null;
            if (board != null) boardSeed.Add(new SeedEntry(Load<EconomyAssetData>("Assets/Game/Activities/Board/Economy/" + board + "BoardBase.asset"), 1, EconomyFormType.Stack));
            baseEntries.Add(new WalletEntry(contentWallet, boardSeed));
            Set(baseHost, "walletEntries", baseEntries);
            var recipe = Economy<ProductionRecipeData>(character + "Upgrade", "Production");
            Set(recipe, "progressionSource", ProductionProgressionSourceType.RecipientResource);
            Set(recipe, "progressionResource", Selection(level, levelTag));
            Set(recipe, "restrictProgression", true);
            Set(recipe, "progressionInterval", new ProgressInterval(0, 99, true));
            var coinPrices = Enumerable.Range(0, 99).Select(n => (long)Mathf.RoundToInt((1000 + 100 * Mathf.Pow(n, 2 + n * .01f)) / 100) * 100).ToList();
            var cardPrices = Enumerable.Range(0, 99).Select(n => (long)(n + 1) * 5 * (1 + n / 5)).ToList();
            Set(recipe, "inputs", new List<ProductionInputData> {
                new ProductionInputData(EconomyOperation.Require, baseHost, EconomyFormType.Stack, Terms(catalogTag), Terms(available), new LongFlatProgressionData(1)),
                new ProductionInputData(EconomyOperation.Consume, coins, EconomyFormType.Stack, Terms(currencyTag), null, new LongListProgressionData(coinPrices, 0, 1)),
                new ProductionInputData(EconomyOperation.Consume, cards, EconomyFormType.Stack, Terms(cardsTag), null, new LongListProgressionData(cardPrices, 0, 1)) });
            Set(recipe, "outputs", outputs);
            object entry = new ProductionCatalogEntryData();
            Set(entry, "recipe", recipe); Set(entry, "workDuration", 1); Set(entry, "recoveryDuration", 0);
            Set(entry, "reservationPolicy", ProductionReservationPolicy.OnEnqueue);
            upgradeEntries.Add((ProductionCatalogEntryData)entry);
            var action = Asset<ProductionUIActionData>(Data + "/Production/" + character + "Purchase.asset");
            Set(action, "recipe", recipe); Set(action, "producerTags", Terms(producerRole));
            Set(action, "recipientSelection", Selection(baseHost, catalogTag, available));
        }
        var catalog = Economy<ProductionCatalogData>("Upgrades", "Production");
        Set(catalog, "entries", upgradeEntries); Set(catalog, "cooldownEnabled", false);
        var production = Economy<ProductionData>("MetaProduction", "Production");
        Set(production, "allowedOperations", MetaProductionOperations);
        Set(production, "tags", Terms(producerRole));
        Set(production, "supportedCatalogs", new List<ProductionCatalogData> { catalog });
        Set(production, "maxQueuedOrders", 1); Set(production, "maxParallelPipelines", 1);
        var productionHost = Economy<CapabilityHostData>("MetaProductionHost", "Production");
        Set(productionHost, "allowedOperations", MetaHostOperations);
        Set(productionHost, "tags", Terms(producerRole));
        var capability = new CapabilityEntry(); Set(capability, "capabilityType", CapabilityHostType.ProductionOwner);
        Set(productionHost, "capabilities", new List<CapabilityEntry> { capability });
        Set(productionHost, "walletEntries", new List<WalletEntry> { new WalletEntry(Wallet("MetaProduction"),
            new[] { new SeedEntry(production, 1, EconomyFormType.Stack) }) });
        var player = Asset<PlayerData>(Data + "/FrameworkProfile.asset");
        Set(player, "id", "FrameworkProfile");
        Set(player, "walletEntries", new List<WalletEntry> {
            new WalletEntry(catalogWallet, catalogueSeeds), new WalletEntry(currencyWallet, new[] { new SeedEntry(coins, 4000, EconomyFormType.Stack) }),
            new WalletEntry(cardsWallet, cardSeeds) });
        RegisterDefinitions();
        AssetDatabase.SaveAssets();
        return "Authored FrameworkProfile, four upgrade recipes/actions, permanent wallets and character bases. Activity and UI wiring remain separate authoring steps.";
    }

    public static string ConnectRosterAndHeroProduction()
    {
        var roleFamily = Load<TaxonomyFamilyData>(Data + "/Taxonomy/MetaRoleFamily.asset");
        var catalogTag = Load<TaxonomyTermData>(Data + "/Taxonomy/CharacterCatalog.asset");
        var available = Load<TaxonomyTermData>(Data + "/Taxonomy/Available.asset");
        var selected = Load<TaxonomyTermData>(Data + "/Taxonomy/Selected.asset");
        var unitRole = Load<TaxonomyTermData>(Data + "/Taxonomy/Unit.asset");
        var contentTag = Load<TaxonomyTermData>(Data + "/Taxonomy/BoardContent.asset");
        var perfume = Load<CapabilityHostData>(Shared + "/Units/Perfume/Perfume.asset");
        var heroRole = perfume.Tags.Single(t => t.name == "HeroRole");
        var selectedState = new List<TaxonomyTermData> { available, selected };
        var board = Load<AgentDefinitionData>("Assets/Game/Activities/Board/Agents/BoardPopulationAgent.asset");
        foreach (var release in ((PopulationAgentData)board.Agent).Releases)
        foreach (var content in release.Content)
        {
            if (!(content.Source is PopulationCatalogContentSourceData source)) continue;
            var outputNames = source.Catalog.Entries.SelectMany(e => e.Recipe.Outputs).Select(o => o.Asset.name).ToList();
            TaxonomyTermData role = outputNames.Contains("WaterBoardBase") || outputNames.Contains("ColaBoardBase") ? unitRole
                : outputNames.Contains("LightningBoltBoardBase") ? heroRole : null;
            if (role == null) continue;
            Set(content, "requireParticipantContent", true);
            Set(content, "participantContentSource", PopulationParticipantContentSourceType.EntryWallets);
            Set(content, "participantEntries", new EconomyEntrySelectionData(null, EconomyFormType.Stack,
                Terms(catalogTag), Terms(role), null, new List<TaxonomyTermData>(selectedState), null));
            Set(content, "entryWalletContent", Selection(null, contentTag));
        }
        EditorUtility.SetDirty(board);

        var outputWalletTag = Load<EconomyWalletData>(Shared + "/Economy/ActivityWallet.asset").Tags;
        var catalog = Economy<ProductionCatalogData>("HeroDeployment", "Production");
        var entries = new List<ProductionCatalogEntryData>();
        foreach (string name in new[] { "Perfume", "Tabasco" })
        {
            var host = Load<CapabilityHostData>(Shared + "/Units/" + name + "/" + name + ".asset");
            var recipe = Economy<ProductionRecipeData>(name + "Deployment", "Production");
            Set(recipe, "inputs", new List<ProductionInputData> { new ProductionInputData(EconomyOperation.Require,
                host, EconomyFormType.Stack, Terms(catalogTag), new List<TaxonomyTermData>(selectedState), new LongFlatProgressionData(1)) });
            Set(recipe, "outputs", new List<ProductionOutputData> { new ProductionOutputData(host,
                EconomyFormType.Token, new List<TaxonomyTermData>(outputWalletTag), new LongFlatProgressionData(1)) });
            object entry = new ProductionCatalogEntryData();
            Set(entry, "recipe", recipe); Set(entry, "workDuration", 1); Set(entry, "recoveryDuration", 0);
            Set(entry, "reservationPolicy", ProductionReservationPolicy.OnEnqueue);
            entries.Add((ProductionCatalogEntryData)entry);
        }
        Set(catalog, "entries", entries);
        var markerTag = Tag("HeroDeploymentMarkers", roleFamily);
        var production = Economy<ProductionData>("HeroProduction", "Production");
        Set(production, "allowedOperations", MetaProductionOperations);
        Set(production, "supportedCatalogs", new List<ProductionCatalogData> { catalog });
        Set(production, "maxQueuedOrders", 1); Set(production, "maxParallelPipelines", 1);
        Set(production, "materializationMarkerProviderType", MaterializationMarkerProviderType.External);
        Set(production, "materializationMarkerProvider", markerTag);
        var producer = Economy<CapabilityHostData>("HeroProductionHost", "Production");
        Set(producer, "allowedOperations", MetaHostOperations);
        var capability = new CapabilityEntry(); Set(capability, "capabilityType", CapabilityHostType.ProductionOwner);
        Set(producer, "capabilities", new List<CapabilityEntry> { capability });
        Set(producer, "walletEntries", new List<WalletEntry> { new WalletEntry(Wallet("HeroProduction"),
            new[] { new SeedEntry(production, 1, EconomyFormType.Stack) }) });

        string agentPath = Data + "/Production/HeroPopulationAgent.asset";
        if (AssetDatabase.LoadAssetAtPath<AgentDefinitionData>(agentPath) == null
            && !AssetDatabase.CopyAsset(Auto + "/Agents/Level01PopulationAgent.asset", agentPath))
            throw new InvalidOperationException("Could not create Hero Population asset.");
        var agent = Load<AgentDefinitionData>(agentPath);
        Set(agent, "agentId", "chainrush.hero.population");
        Set(agent, "applicabilityConditions", new List<AgentApplicabilityConditionData>());
        Set(agent, "matchConditions", new List<ObjectiveCondition> { HeroCondition(heroRole) });
        Set(agent, "executorSelectionCriteria", ProducerCriteria(producer));
        Set(agent, "targetSelectionCriteria", ProducerCriteria(producer));
        var population = (PopulationAgentData)agent.Agent;
        var shape = population.Releases[0].Shapes[0].Shape;
        Set(population, "progress", new PopulationProgressData());
        Set(population, "volume", new LongFlatProgressionData(1));
        Set(population, "completionPolicy", PopulationCompletionPolicyType.RequireFullVolume);
        var heroContent = new PopulationContentRuleData(new PopulationCatalogContentSourceData(catalog), 1f);
        Set(heroContent, "requireParticipantContent", true);
        Set(heroContent, "participantContentSource", PopulationParticipantContentSourceType.Entries);
        Set(heroContent, "participantEntries", new EconomyEntrySelectionData(null, EconomyFormType.Stack,
            Terms(catalogTag), Terms(heroRole), null, new List<TaxonomyTermData>(selectedState), null));
        var usage = new SpatialShapeUsageData(SpatialShapeFillType.Inside, Vector3Int.zero, Vector3Int.one,
            Vector3Int.zero, new Vector3Int(1000, 1, 1000), Vector3Int.zero);
        Set(population, "releases", new List<PopulationReleaseData> { new PopulationReleaseData(
            new ProgressInterval(0, 0, false), new SpaceRegionQueryData(SpaceRegionScopeType.Activity,
                Terms(markerTag), null, null, null),
            new[] { new PopulationShapeRuleData(shape, new[] { usage }, 1, new IntRange(0, 1)) },
            new[] { heroContent }) });
        EditorUtility.SetDirty(agent);
        var objective = Asset<ObjectiveTemplateData>(Data + "/Production/HeroDeploymentObjective.asset");
        Set(objective, "root", new ObjectiveNode("initial-hero", successConditions: new[] { HeroCondition(heroRole) }));
        Set(objective, "completionPolicyType", ObjectiveCompletionPolicyType.Terminal);
        Set(objective, "resetConditions", new List<ObjectiveCondition>());
        var brain = Load<OrchestratorAIBrainData>(Auto + "/Orchestration/PlayerBrain.asset");
        var operatorFamily = Load<TaxonomyFamilyData>(Auto + "/Orchestration/Taxonomy/AutobattleOperatorFamily.asset");
        var operatorTag = Tag("HeroPopulationOperator", operatorFamily);
        brain.Operators.RemoveAll(op => op.OperatorId == operatorTag);
        var op = new AgentDecompOpData(); Set(op, "operatorId", operatorTag); Set(op, "agentDefinition", agent);
        brain.Operators.Add(op);
        var decision = new OrchestrationDecisionData();
        Set(decision, "decisionId", "initial-hero-population"); Set(decision, "operatorId", operatorTag);
        var factType = new FactTypeDecisionConditionData(); Set(factType, "factType", OrchestrationFactType.MaterializedEntity);
        var scope = new ScopeDecisionConditionData(); Set(scope, "scopeType", OrchestrationDecompositionScopeType.GlobalObjective);
        Set(decision, "conditions", new List<OrchestrationDecisionConditionData> { factType, scope, new AgentMatchDecisionConditionData() });
        brain.DecisionGraph.Nodes.RemoveAll(n => n != null && n.DecisionId == "initial-hero-population");
        brain.DecisionGraph.Nodes.Insert(0, decision);
        EditorUtility.SetDirty(brain);

        foreach (string name in new[] { "AutobattleActivity", "DistanceActivity" })
        {
            var activity = Load<ActivityData>(Auto + "/Definition/" + name + ".asset");
            var teams = activity.Teams.ToList();
            object team = teams[0];
            var wallets = teams[0].Wallets.Where(w => w.Wallet.name != "DevelopmentWallet" && w.Wallet.name != "LoadoutWallet").ToList();
            for (int i = 0; i < wallets.Count; i++)
            {
                object wallet = wallets[i];
                var seed = wallets[i].Seed.Where(s => !(s.Seed.Asset is CapabilityHostData h && h.Tags.Contains(heroRole))
                    && s.Seed.Asset != producer).ToList();
                if (wallets[i].Wallet.name == "ActivityWallet")
                    seed.Add(new ActivityWalletSeedEntryData(new SeedEntry(producer, 1, EconomyFormType.Token),
                        ActivitySeedMaterializationType.NonSpatial, new List<TaxonomyTermData>()));
                Set(wallet, "seed", seed); wallets[i] = (ActivityTeamWalletData)wallet;
            }
            Set(team, "wallets", wallets);
            var objectives = teams[0].Objectives.Where(o => o.Template != objective).ToList();
            object entry = new ActivityTeamObjectiveData(); Set(entry, "template", objective);
            objectives.Insert(0, (ActivityTeamObjectiveData)entry); Set(team, "objectives", objectives);
            var rules = teams[0].EntryRules.ToList();
            rules.RemoveAll(r => r.WalletTags.Contains(catalogTag));
            var selectedHero = new ActivityEntryRule(); Set(selectedHero, "walletTags", Terms(catalogTag));
            Set(selectedHero, "assetTags", Terms(heroRole)); Set(selectedHero, "runtimeTags", new List<TaxonomyTermData>(selectedState));
            Set(selectedHero, "amount", 1L); Set(selectedHero, "compareOperation", CompareOperation.Equal);
            rules.Add(selectedHero); Set(team, "entryRules", rules);
            teams[0] = (ActivityTeamData)team; Set(activity, "teams", teams);
            var space = (ActivityPrefabSpaceData)activity.Space;
            string prefabPath = AssetDatabase.GUIDToAssetPath(space.PrefabReference.AssetGUID);
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var socket = prefab.GetComponentsInChildren<SpatialMarkerSocket>(true).Single(s => s.name == "HeroSpawn");
                var provider = socket.GetComponent<SpatialShapeProviderController>() ?? socket.gameObject.AddComponent<SpatialShapeProviderController>();
                Set(provider, "providerId", "initial-hero-production"); Set(provider, "providerType", markerTag);
                Set(provider, "shape", shape); Set(provider, "regionTags", Terms(markerTag));
                Set(provider, "markerTags", Terms(markerTag)); Set(provider, "publishMarkers", true);
                Set(provider, "usage", new SpatialShapeUsageData(SpatialShapeFillType.Inside, socket.TopologyCoordinates,
                    Vector3Int.one, Vector3Int.zero, new Vector3Int(1000, 1, 1000), Vector3Int.zero));
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        ConfigureInitialGameplaySeed();
        RegisterDefinitions(); AssetDatabase.SaveAssets();
        return "Connected Selected roster to Board and initial hero Population/Production in both levels; removed fixed hero and temporary development/loadout seeds from their player teams.";
    }

    public static string ConnectCommonUIPrefabs()
    {
        foreach (string name in new[] { "BuyButtonPriceItem", "RewardResourceVerticaltem", "RewardResourceVerticaltemVFX",
            "RewardUnitCardVerticaltem", "RewardUnitCardVerticaltemVFX", "RewardUnitVerticaltem", "RewardUnitVerticaltemVFX" })
        {
            string path = Root + "/Prefabs/UI/" + name + ".prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(prefab))
                    PrefabUtility.UnpackPrefabInstance(prefab, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var old in prefab.GetComponentsInChildren<IconTextItem>(true)) Object.DestroyImmediate(old);
                var item = prefab.GetComponent<Core.UI.EconomyListViewItem>() ?? prefab.AddComponent<Core.UI.EconomyListViewItem>();
                Set(item, "select", false);
                Set(item, "icon", prefab.transform.Find("Icon").GetComponent<UnityEngine.UI.Image>());
                Set(item, "amountText", prefab.transform.Find("Text").GetComponent<TMPro.TextMeshProUGUI>());
                // The unit reward's localized caption is presentation text, not an amount binding.
                if (name.StartsWith("RewardUnitVertical")) Set(item, "amountText", null);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        string purchasePath = Root + "/Prefabs/UI/BuyButton.prefab";
        var purchase = PrefabUtility.LoadPrefabContents(purchasePath);
        try
        {
            var old = purchase.GetComponent<BuyButton>();
            var active = old == null ? new UnityEngine.Events.UnityEvent() : Get<UnityEngine.Events.UnityEvent>(old, "OnActive");
            var inactive = old == null ? new UnityEngine.Events.UnityEvent() : Get<UnityEngine.Events.UnityEvent>(old, "OnInactive");
            if (old != null) Object.DestroyImmediate(old);
            var button = purchase.GetComponent<Core.UI.PurchaseButton>() ?? purchase.AddComponent<Core.UI.PurchaseButton>();
            var prices = purchase.GetComponent<Core.UI.EconomyListView>() ?? purchase.AddComponent<Core.UI.EconomyListView>();
            Set(prices, "root", purchase.transform.Find("PriceRoot")); Set(prices, "selectable", false); Set(prices, "selectDefault", false);
            Set(prices, "itemPrefab", Load<GameObject>(Root + "/Prefabs/UI/BuyButtonPriceItem.prefab").GetComponent<Core.UI.EconomyListViewItem>());
            Set(button, "button", purchase.GetComponent<UnityEngine.UI.Button>()); Set(button, "prices", prices);
            Set(button, "onActive", active); Set(button, "onInactive", inactive);
            purchase.GetComponent<UnityEngine.UI.Button>().onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            PrefabUtility.SaveAsPrefabAsset(purchase, purchasePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(purchase); }
        string itemPath = Root + "/Prefabs/UI/UnitItems/UnitItem.prefab";
        var card = PrefabUtility.LoadPrefabContents(itemPath);
        try
        {
            foreach (var old in card.GetComponents<UnitItem>()) Object.DestroyImmediate(old);
            var view = card.GetComponent<Core.UI.EconomyListViewItem>() ?? card.AddComponent<Core.UI.EconomyListViewItem>();
            Set(view, "icon", card.transform.Find("Shadow/Border/Front/Back/Icon").GetComponent<UnityEngine.UI.Image>());
            card.GetComponent<UnityEngine.UI.Button>().onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            foreach (string child in new[] { "LabelBack", "Progressbar", "Notification", "CardIconShadow" })
                card.transform.Find(child).gameObject.SetActive(false);
            var locked = card.transform.Find("Locked").gameObject;
            var ready = card.transform.Find("Ready").gameObject;
            var selected = card.transform.Find("Selected").gameObject;
            locked.SetActive(true); ready.SetActive(false); selected.SetActive(false);
            var tags = Get<Dictionary<List<TaxonomyTermData>, UnityEngine.Events.UnityEvent>>(view, "tagEvents"); tags.Clear();
            var entitledEvent = new UnityEngine.Events.UnityEvent();
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(entitledEvent, ready.SetActive, true);
            tags.Add(Terms(Load<TaxonomyTermData>(Data + "/Taxonomy/Entitled.asset")), entitledEvent);
            var availableEvent = new UnityEngine.Events.UnityEvent();
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(availableEvent, ready.SetActive, false);
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(availableEvent, locked.SetActive, false);
            tags.Add(Terms(Load<TaxonomyTermData>(Data + "/Taxonomy/Available.asset")), availableEvent);
            var selectedEvent = new UnityEngine.Events.UnityEvent();
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(selectedEvent, selected.SetActive, true);
            tags.Add(Terms(Load<TaxonomyTermData>(Data + "/Taxonomy/Selected.asset")), selectedEvent);
            var highlight = card.transform.Find("Shadow/Border/Front/Highlight").gameObject;
            highlight.SetActive(false);
            var onSelect = new UnityEngine.Events.UnityEvent(); var onDeselect = new UnityEngine.Events.UnityEvent();
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(onSelect, highlight.SetActive, true);
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(onDeselect, highlight.SetActive, false);
            Set(view, "onSelected", onSelect); Set(view, "onDeselected", onDeselect);
            PrefabUtility.SaveAsPrefabAsset(card, itemPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(card); }
        Set(Load<ProductionData>(Data + "/Production/MetaProduction.asset"), "tags",
            Terms(Load<TaxonomyTermData>(Data + "/Taxonomy/MetaProducer.asset")));
        AssetDatabase.SaveAssets();
        return "Bound copied purchase, amount/reward rows and selectable card to generic Framework components. Original prefabs unchanged.";
    }

    public static string ConnectMetaPanels()
    {
        string path = Root + "/Scenes/Main.unity";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas");
            var main = canvas.transform.Find("MainPanel").gameObject;
            var input = main.GetComponent<CanvasGroup>();
            if (input == null) input = main.AddComponent<CanvasGroup>();
            foreach (var oldPanel in main.GetComponentsInChildren<UnitPanel>(true))
                ConnectCharacterPanel(oldPanel, input);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        CreateMetaActivity();
        AssetDatabase.SaveAssets();
        return "Connected Hero/Unit selection, numeric fields and Source-driven purchase controls in the new Main; created MetaActivity. Scene startup/tabs/level flow wiring remains.";
    }

    static void ConnectCharacterPanel(UnitPanel oldPanel, CanvasGroup input)
    {
        var panel = oldPanel.gameObject; bool hero = panel.name == "HeroUI";
        var player = Load<PlayerData>(Data + "/FrameworkProfile.asset");
        var catalog = Load<TaxonomyTermData>(Data + "/Taxonomy/CharacterCatalog.asset");
        var available = Load<TaxonomyTermData>(Data + "/Taxonomy/Available.asset");
        var selected = Load<TaxonomyTermData>(Data + "/Taxonomy/Selected.asset");
        var entitled = Load<TaxonomyTermData>(Data + "/Taxonomy/Entitled.asset");
        var role = hero ? Load<CapabilityHostData>(Shared + "/Units/Perfume/Perfume.asset").Tags.Single(t => t.name == "HeroRole")
            : Load<TaxonomyTermData>(Data + "/Taxonomy/Unit.asset");
        var title = Get<TMPro.TextMeshProUGUI>(oldPanel, "titleLabel");
        var level = Get<TMPro.TextMeshProUGUI>(oldPanel, "levelLabel");
        var locked = Get<GameObject>(oldPanel, "lockedIcon");
        var ready = Get<GameObject>(oldPanel, "readyIcon");
        var oldBuy = Get<BuyButton>(oldPanel, "buyButton");
        var oldRect = (RectTransform)oldBuy.transform;
        var purchase = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Root + "/Prefabs/UI/BuyButton.prefab"), oldBuy.transform.parent);
        var rect = (RectTransform)purchase.transform;
        rect.anchorMin = oldRect.anchorMin; rect.anchorMax = oldRect.anchorMax; rect.pivot = oldRect.pivot;
        rect.sizeDelta = oldRect.sizeDelta; rect.anchoredPosition3D = oldRect.anchoredPosition3D;
        rect.localScale = oldRect.localScale; rect.localRotation = oldRect.localRotation;
        purchase.name = "PurchaseButton"; purchase.transform.SetSiblingIndex(oldBuy.transform.GetSiblingIndex());
        Object.DestroyImmediate(oldBuy.gameObject);
        var button = purchase.GetComponent<Core.UI.PurchaseButton>();
        var action = panel.AddComponent<Core.UI.ProductionUIAction>(); Set(action, "purchaseButton", button);
        var source = panel.AddComponent<Core.UI.ProductionUISource>();
        Set(source, "actions", (hero ? new[] { "Perfume", "Tabasco" } : new[] { "Water", "Cola" })
            .Select(n => Load<ProductionUIActionData>(Data + "/Production/" + n + "Purchase.asset")).ToList());
        UnityEditor.Events.UnityEventTools.AddPersistentListener(Get<UnityEngine.Events.UnityEvent<Core.UI.PurchaseUIData>>(source, "onDataChanged"), button.Setup);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(Get<UnityEngine.Events.UnityEvent<Core.UI.ProductionUISource>>(source, "onUpdated"), action.Setup);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(purchase.GetComponent<UnityEngine.UI.Button>().onClick, action.Purchase);
        // Persistent UnityEvents require an actual Unity target method, not a captured lambda.
        BindBoolPropertyEvent(button, "onPurchaseStarted", input, "set_interactable", false);
        BindBoolPropertyEvent(button, "onPurchaseEnded", input, "set_interactable", true);

        var profile = panel.AddComponent<Core.UI.EconomyListViewItemProfile>(); Set(profile, "titleText", title);
        var numeric = panel.AddComponent<Core.UI.EconomyValueUISource>();
        var fields = new List<Core.UI.EconomyValueUIField>();
        var levelQuery = Selection(Load<FrameworkResourceData>(Data + "/Resources/Level.asset"), Load<TaxonomyTermData>(Data + "/Taxonomy/Level.asset"));
        fields.Add(NumericField(level, levelQuery, 1));
        var stats = Get<System.Collections.IDictionary>(oldPanel, "stats");
        var attributeNames = new Dictionary<string, string> { { "Power", "Power" }, { "Defense", "Defense" },
            { "Health", "Health" }, { "Speed", "Speed" }, { "SkillSpeed", "SkillSpeed" } };
        foreach (System.Collections.DictionaryEntry stat in stats)
        {
            if (!attributeNames.TryGetValue(stat.Key.ToString(), out string attributeName)) continue;
            var statItem = (IconTextItem)stat.Value;
            if (statItem == null) continue;
            var selector = new AttributeSelectorData(Load<Core.Attributes.AttributeData>(Shared + "/Attributes/" + attributeName + ".asset"));
            if (attributeName == "Power" || attributeName == "Defense")
            {
                var seed = ((IEconomyWalletEntrySource)Load<CapabilityHostData>(Shared + "/Units/Perfume/Perfume.asset"))
                    .WalletEntries.SelectMany(w => w.Seed).Single(s => s.Asset == selector.Attribute);
                Set(selector, "qualifiers", new List<TaxonomyTermData>(seed.RuntimeTags));
            }
            var query = new EconomyEntrySelectionData(selector.Attribute, EconomyFormType.Stack, null, null, null, selector.Qualifiers, null);
            fields.Add(NumericField(Get<TMPro.TextMeshProUGUI>(statItem, "label"), query, 0));
            Object.DestroyImmediate(statItem);
        }
        Set(numeric, "fields", fields);
        Set(numeric, "attributeSourceIds", new List<string> { "participant-development" });
        var select = panel.AddComponent<Core.UI.EconomyRuntimeTagUIAction>();
        Set(select, "mode", Core.UI.EconomyRuntimeTagUIActionMode.Add); Set(select, "runtimeTag", selected);
        Set(select, "minCount", 1); Set(select, "maxCount", hero ? 1 : 4);
        Set(select, "policy", hero ? Core.UI.EconomyRuntimeTagUIActionPolicy.Substitute : Core.UI.EconomyRuntimeTagUIActionPolicy.Reject);
        Set(select, "allowedGroup", new EconomyEntrySelectionData(null, EconomyFormType.Stack, Terms(catalog), Terms(role), null, Terms(available), null));
        var deselect = hero ? null : panel.AddComponent<Core.UI.EconomyRuntimeTagUIAction>();
        if (deselect != null)
        {
            Set(deselect, "mode", Core.UI.EconomyRuntimeTagUIActionMode.Remove); Set(deselect, "runtimeTag", selected);
            Set(deselect, "minCount", 1); Set(deselect, "maxCount", 4);
            Set(deselect, "allowedGroup", new EconomyEntrySelectionData(null, EconomyFormType.Stack, Terms(catalog), Terms(role), null, Terms(available), null));
        }
        var unlock = panel.AddComponent<Core.UI.EconomyRuntimeTagUIAction>();
        Set(unlock, "mode", Core.UI.EconomyRuntimeTagUIActionMode.Add); Set(unlock, "runtimeTag", available);
        Set(unlock, "maxCount", int.MaxValue);
        Set(unlock, "allowedGroup", new EconomyEntrySelectionData(null, EconomyFormType.Stack, Terms(catalog), Terms(role), null, Terms(entitled), Terms(available)));
        var controls = panel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        var selectButton = controls.Single(b => b.name == "SelectButton");
        selectButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(selectButton.onClick, select.Apply);
        var deselectButton = hero ? null : controls.Single(b => b.name == "DeselectButton");
        if (deselectButton != null)
        {
            deselectButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(deselectButton.onClick, deselect.Apply);
        }
        var unlockButton = ready.GetComponent<UnityEngine.UI.Button>();
        unlockButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(unlockButton.onClick, unlock.Apply);
        var profileTags = Get<Dictionary<List<TaxonomyTermData>, UnityEngine.Events.UnityEvent>>(profile, "tagEvents");
        var entitledEvent = new UnityEngine.Events.UnityEvent(); UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(entitledEvent, ready.SetActive, true);
        profileTags.Add(Terms(entitled), entitledEvent);
        var availableEvent = new UnityEngine.Events.UnityEvent();
        UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(availableEvent, ready.SetActive, false);
        UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(availableEvent, locked.SetActive, false);
        UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(availableEvent, selectButton.gameObject.SetActive, true);
        profileTags.Add(Terms(available), availableEvent);
        var selectedEvent = new UnityEngine.Events.UnityEvent();
        UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(selectedEvent, selectButton.gameObject.SetActive, false);
        if (deselectButton != null) UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(selectedEvent, deselectButton.gameObject.SetActive, true);
        profileTags.Add(Terms(selected), selectedEvent);

        var lists = new List<Core.UI.EconomyListView>();
        var roots = new List<Transform>();
        if (hero)
        {
            var hidden = new GameObject("HeroSelectionItems", typeof(RectTransform), typeof(CanvasGroup));
            hidden.transform.SetParent(panel.transform, false);
            var group = hidden.GetComponent<CanvasGroup>(); group.alpha = 0; group.interactable = group.blocksRaycasts = false;
            roots.Add(hidden.transform);
        }
        else
        {
            roots.Add(panel.GetComponentsInChildren<Transform>(true).Single(t => t.name == "AvailableRoot"));
            RemoveUnitGroups(panel, roots[0]);
        }
        for (int index = 0; index < roots.Count; index++)
        {
            var sourceObject = new GameObject(hero ? "HeroCatalog" : "UnitCatalog");
            sourceObject.transform.SetParent(panel.transform, false);
            var entries = sourceObject.AddComponent<Core.UI.EconomyListUISource>();
            Set(entries, "walletTags", Terms(catalog));
            Set(entries, "formType", EconomyFormType.Stack); Set(entries, "requiredAssetTags", Terms(role));
            Set(entries, "warnWhenLoadProducesNoItems", false);
            var list = sourceObject.AddComponent<Core.UI.EconomyListView>(); Set(list, "root", roots[index]);
            Set(list, "selectDefault", index == 0); Set(list, "warnWhenNoItemsInstantiated", false);
            Get<Dictionary<List<TaxonomyTermData>, Core.UI.EconomyListViewItem>>(list, "prefabRoutes").Add(Terms(role),
                Load<GameObject>(Root + "/Prefabs/UI/UnitItems/UnitItem.prefab").GetComponent<Core.UI.EconomyListViewItem>());
            var loaded = Get<Core.UI.EconomyListUISourceEvent>(entries, "onSuccess");
            UnityEditor.Events.UnityEventTools.AddPersistentListener(loaded, select.Setup);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(loaded, unlock.Setup);
            if (deselect != null) UnityEditor.Events.UnityEventTools.AddPersistentListener(loaded, deselect.Setup);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(loaded, list.Setup);
            var changed = Get<Core.UI.EconomyItemUIListEvent>(list, "onSelectionChanged");
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(changed, locked.SetActive, true);
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(changed, ready.SetActive, false);
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(changed, selectButton.gameObject.SetActive, false);
            if (deselectButton != null) UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(changed, deselectButton.gameObject.SetActive, false);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(changed, profile.Setup);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(changed, numeric.SetupSelection);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(changed, source.SetupSelection);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(changed, select.SetupSelection);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(changed, unlock.SetupSelection);
            if (deselect != null) UnityEditor.Events.UnityEventTools.AddPersistentListener(changed, deselect.SetupSelection);
            var load = sourceObject.AddComponent<Core.Events.UnityEventTrigger>(); Set(load, "mode", Core.Events.TriggerMode.OnEnable);
            Set(load, "triggerOnce", false);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(Get<UnityEngine.Events.UnityEvent>(load, "onTrigger"), entries.Load);
            lists.Add(list);
        }
        if (hero)
        {
            var previous = controls.Single(b => b.name == "ButtonPrevious"); var next = controls.Single(b => b.name == "ButtonNext");
            previous.onClick = new UnityEngine.UI.Button.ButtonClickedEvent(); next.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(previous.onClick, lists[0].Previous);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(next.onClick, lists[0].Next);
        }
        foreach (var spine in panel.GetComponentsInChildren<Spine.Unity.SkeletonGraphic>(true)) spine.gameObject.SetActive(false);
        foreach (var item in panel.GetComponentsInChildren<Transform>(true).Where(t => t.name == "MergeInfo").ToList()) item.gameObject.SetActive(false);
        foreach (var old in panel.GetComponents<MonoBehaviour>().Where(c => c != null && c.GetType().Assembly == typeof(UnitPanel).Assembly).ToList())
            Object.DestroyImmediate(old);
    }

    public static string ConnectSingleUnitList()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root + "/Scenes/Main.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            var panel = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform.Find("MainPanel/Content/UnitUI").gameObject;
            var catalog = panel.transform.Find("SelectedUnitCatalog").gameObject;
            var entries = catalog.GetComponent<Core.UI.EconomyListUISource>();
            Set(entries, "requiredRuntimeTags", new List<TaxonomyTermData>());
            Set(entries, "excludedRuntimeTags", new List<TaxonomyTermData>());
            var root = panel.GetComponentsInChildren<Transform>(true).Single(t => t.name == "AvailableRoot");
            Set(catalog.GetComponent<Core.UI.EconomyListView>(), "root", root);
            Object.DestroyImmediate(panel.transform.Find("AvailableUnitCatalog").gameObject);
            catalog.name = "UnitCatalog";
            RemoveUnitGroups(panel, root);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        return "New Main uses one Unit catalog, one list and one viewed entry; Selected remains an item tag.";
    }

    public static string ConnectMetaTabs()
    {
        string animationRoot = Root + "/Animations/UI/Tabs";
        Folder(animationRoot);
        string controllerPath = animationRoot + "/MetaTab.controller";
        if (AssetDatabase.LoadMainAssetAtPath(controllerPath) != null)
            throw new InvalidOperationException("Tab animation has already been authored.");
        var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        foreach (bool selected in new[] { false, true })
        {
            var clip = new AnimationClip { name = selected ? "Selected" : "Normal" };
            var color = selected ? new Color(.45882353f, .4117647f, .5568628f, 1f) : new Color(.25714037f, .22745098f, .3254902f, 1f);
            clip.SetCurve("", typeof(RectTransform), "m_SizeDelta.x", AnimationCurve.Constant(0f, .1f, selected ? 260f : 200f));
            foreach (var axis in new[] { "x", "y", "z" })
                clip.SetCurve("Icon", typeof(Transform), "m_LocalScale." + axis, AnimationCurve.EaseInOut(0f, selected ? .7f : 1f, .1f, selected ? 1f : .7f));
            var channels = new[] { "r", "g", "b", "a" };
            for (int i = 0; i < channels.Length; i++)
                clip.SetCurve("", typeof(UnityEngine.UI.Image), "m_Color." + channels[i], AnimationCurve.Constant(0f, .1f, color[i]));
            AssetDatabase.CreateAsset(clip, animationRoot + "/" + clip.name + ".anim");
            var state = controller.layers[0].stateMachine.AddState(clip.name); state.motion = clip;
            if (!selected) controller.layers[0].stateMachine.defaultState = state;
        }
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root + "/Scenes/Main.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform;
            var main = canvas.Find("MainPanel"); var bottom = main.Find("Bottom").gameObject;
            UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(bottom);
            var oldManager = bottom.GetComponent<TabManager>(); if (oldManager != null) Object.DestroyImmediate(oldManager);
            var manager = bottom.AddComponent<Core.UI.TabManager>(); Set(manager, "tabsRoot", bottom);
            foreach (string tabName in new[] { "Hero", "Units", "Battle" })
            {
                var tab = bottom.transform.Find(tabName).gameObject;
                foreach (var child in tab.transform.Cast<Transform>().Where(t => t.name != "Icon").ToList()) Object.DestroyImmediate(child.gameObject);
                UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(tab);
                var old = tab.GetComponent<TabSelectButton>(); if (old != null) Object.DestroyImmediate(old);
                tab.GetComponent<UnityEngine.UI.Button>().onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                var button = tab.AddComponent<Core.UI.TabSelectButton>(); Set(button, "defaultTab", tabName == "Battle");
                var animator = tab.AddComponent<Animator>(); animator.runtimeAnimatorController = controller;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                var target = main.Find("Content/" + (tabName == "Units" ? "UnitUI" : tabName + "UI")).gameObject;
                target.transform.Find("Content").gameObject.SetActive(true);
                var show = new UnityEngine.Events.UnityEvent(); var hide = new UnityEngine.Events.UnityEvent();
                UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(show, target.SetActive, true);
                UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(hide, target.SetActive, false);
                UnityEditor.Events.UnityEventTools.AddStringPersistentListener(show, animator.Play, "Selected");
                UnityEditor.Events.UnityEventTools.AddStringPersistentListener(hide, animator.Play, "Normal");
                Set(button, "selectEvent", show); Set(button, "deselectEvent", hide);
                target.SetActive(tabName == "Battle");
            }
            foreach (string name in new[] { "Recipes", "Bank" }) Object.DestroyImmediate(bottom.transform.Find(name).gameObject);
            foreach (string name in new[] { "ShopUI", "BankUI" }) Object.DestroyImmediate(main.Find("Content/" + name).gameObject);
            Object.DestroyImmediate(main.Find("ResourcesPanel").gameObject);
            foreach (string name in new[] { "TutorialManager", "SettingsButton", "SettingsPopup", "CheatsPopup", "RatePopup", "EnergyPopup", "IdlePopup", "DailyRewardsPopup", "RewardFlyManager", "TabUnlockPopup" })
                Object.DestroyImmediate(canvas.Find(name).gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        AssetDatabase.SaveAssets();
        return "Connected package tabs Hero/Unit/Battle, authored original visual values and removed excluded entry points from the new Main.";
    }

    static void RemoveUnitGroups(GameObject panel, Transform items)
    {
        Object.DestroyImmediate(panel.GetComponentsInChildren<Transform>(true).Single(t => t.name == "SelectedRoot").gameObject);
        foreach (var caption in items.parent.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)
            .Where(t => t.transform.parent == items.parent).ToList())
            Object.DestroyImmediate(caption.gameObject);
        items.name = "UnitItems";
    }

    public static string ConnectRewardPopup()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root + "/Scenes/Main.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas");
            var old = canvas.GetComponentsInChildren<RewardPopup>(true).Single();
            var popupObject = old.gameObject;
            var itemsRoot = Get<Transform>(old, "itemsRoot"); var unitsRoot = Get<Transform>(old, "unitRewardRoot");
            var position = Get<RectTransform>(old, "positionContainer");
            var animator = Get<Animator>(old, "animator");
            var sound = Object.Instantiate(((Component)old.OnShow.GetPersistentTarget(0)).gameObject, popupObject.transform)
                .GetComponent<MoreMountains.Feedbacks.MMF_Player>();
            sound.gameObject.name = "PopupToggle"; sound.StopFeedbacksOnDisable = true;
            float initialDelay = Get<float>(old, "initialDelay"), spawnCooldown = Get<float>(old, "spawnCooldown"), showDelay = Get<float>(old, "showDelay");
            var closeTrigger = popupObject.GetComponent<DelayedUnityEventTrigger>();
            float closeDelay = Get<float>(closeTrigger, "delay");
            Object.DestroyImmediate(closeTrigger); Object.DestroyImmediate(old);
            var popup = popupObject.AddComponent<Core.UI.RewardPopup>();
            Set(popup, "animator", animator); Set(popup, "positionContainer", position); Set(popup, "showDelay", showDelay);
            Set(popup, "initialDelay", initialDelay); Set(popup, "spawnCooldown", spawnCooldown); Set(popup, "requiresManualCloseRelease", true);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(Get<UnityEngine.Events.UnityEvent>(popup, "onShow"), sound.PlayFeedbacks);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(Get<UnityEngine.Events.UnityEvent>(popup, "onHide"), sound.PlayFeedbacks);
            var autoClose = popupObject.AddComponent<MoreMountains.Feedbacks.MMF_Player>();
            autoClose.StopFeedbacksOnDisable = true;
            var closeEvent = (MoreMountains.Feedbacks.MMF_Events)autoClose.AddFeedback(typeof(MoreMountains.Feedbacks.MMF_Events));
            closeEvent.PlayEvents = new UnityEngine.Events.UnityEvent();
            closeEvent.Timing.InitialDelay = closeDelay;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(closeEvent.PlayEvents, popup.Close);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(Get<UnityEngine.Events.UnityEvent>(popup, "onRewardsShown"), autoClose.PlayFeedbacks);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(Get<UnityEngine.Events.UnityEvent>(popup, "onShow"), autoClose.StopFeedbacks);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(Get<UnityEngine.Events.UnityEvent>(popup, "onHide"), autoClose.StopFeedbacks);
            var back = popupObject.transform.Find("Back").GetComponent<UnityEngine.UI.Button>();
            back.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(back.onClick, popup.Close);

            var cardsTag = Tag("PersonalCard", Load<TaxonomyFamilyData>(Data + "/Taxonomy/MetaRoleFamily.asset"));
            foreach (string character in new[] { "Perfume", "Tabasco", "Water", "Cola" })
                Set(Load<FrameworkResourceData>(Data + "/Resources/" + character + "Card.asset"), "tags", Terms(cardsTag));
            var normal = itemsRoot.gameObject.AddComponent<Core.UI.EconomyListView>();
            var units = unitsRoot.gameObject.AddComponent<Core.UI.EconomyListView>();
            foreach (var list in new[] { normal, units })
            { Set(list, "root", list.transform); Set(list, "selectable", false); Set(list, "selectDefault", false); }
            Set(normal, "itemPrefab", Load<GameObject>(Root + "/Prefabs/UI/RewardResourceVerticaltemVFX.prefab").GetComponent<Core.UI.EconomyListViewItem>());
            Get<Dictionary<List<TaxonomyTermData>, Core.UI.EconomyListViewItem>>(normal, "prefabRoutes").Add(Terms(cardsTag),
                Load<GameObject>(Root + "/Prefabs/UI/RewardUnitCardVerticaltemVFX.prefab").GetComponent<Core.UI.EconomyListViewItem>());
            Set(units, "itemPrefab", Load<GameObject>(Root + "/Prefabs/UI/RewardUnitVerticaltemVFX.prefab").GetComponent<Core.UI.EconomyListViewItem>());
            var serialized = new SerializedObject(popup); var routes = serialized.FindProperty("itemRoutes"); routes.arraySize = 3;
            var roleTags = new List<TaxonomyTermData> { null, Load<TaxonomyTermData>(Data + "/Taxonomy/Unit.asset"),
                Load<CapabilityHostData>(Shared + "/Units/Perfume/Perfume.asset").Tags.Single(t => t.name == "HeroRole") };
            for (int i = 0; i < roleTags.Count; i++)
            {
                var route = routes.GetArrayElementAtIndex(i); var tags = route.FindPropertyRelative("requiredAssetTags");
                tags.arraySize = i == 0 ? 0 : 1; if (i != 0) tags.GetArrayElementAtIndex(0).objectReferenceValue = roleTags[i];
                route.FindPropertyRelative("list").objectReferenceValue = i == 0 ? normal : units;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            const string originalAnimation = "Assets/Game/Animations/UI/FullscreenPopup";
            string newAnimation = Root + "/Animations/UI/FullscreenPopup"; Folder(newAnimation);
            string controllerPath = newAnimation + "/FullscreenPopup.overrideController";
            if (!AssetDatabase.CopyAsset(originalAnimation + "/FullscreenPopup.overrideController", controllerPath))
                throw new InvalidOperationException("Could not create Reward popup animation copy.");
            var controller = Load<AnimatorOverrideController>(controllerPath);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(); controller.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
            {
                var source = overrides[i].Value; string path = newAnimation + "/" + source.name + ".anim";
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path)) throw new InvalidOperationException("Could not copy " + source.name);
                var clip = Load<AnimationClip>(path);
                var events = new List<AnimationEvent>();
                if (source.name.EndsWith(" Show") || source.name.EndsWith(" Hide"))
                    events.Add(new AnimationEvent { time = clip.length, functionName = source.name.EndsWith(" Show") ? "OnShowAnimationCompleted" : "OnHideAnimationCompleted" });
                AnimationUtility.SetAnimationEvents(clip, events.ToArray());
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
            }
            controller.ApplyOverrides(overrides); animator.runtimeAnimatorController = controller; animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            var popupCanvas = popupObject.AddComponent<Canvas>(); popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay; popupCanvas.sortingOrder = 100;
            EditorUtility.CopySerialized(canvas.GetComponent<UnityEngine.UI.CanvasScaler>(), popupObject.AddComponent<UnityEngine.UI.CanvasScaler>());
            popupObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            Folder(Root + "/Prefabs/UI");
            PrefabUtility.SaveAsPrefabAsset(popupObject, Root + "/Prefabs/UI/RewardPopup.prefab");
            Object.DestroyImmediate(popupObject);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        foreach (string name in new[] { "RewardResourceVerticaltemVFX", "RewardUnitCardVerticaltemVFX", "RewardUnitVerticaltemVFX" })
        {
            string path = Root + "/Prefabs/UI/" + name + ".prefab"; var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var feedback = prefab.GetComponent<MoreMountains.Feedbacks.MMFeedbacks>();
                if (feedback != null) UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    Get<UnityEngine.Events.UnityEvent>(prefab.GetComponent<Core.UI.EconomyListViewItem>(), "onShow"), feedback.PlayFeedbacks);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        RegisterDefinitions(); AssetDatabase.SaveAssets();
        return "Authored Framework RewardPopup prefab, item routes, appearance feedback, animation completion events and existing one-second close delay.";
    }

    static Core.UI.EconomyValueUIField NumericField(TMPro.TextMeshProUGUI text, EconomyEntrySelectionData query, long offset)
    {
        var field = text.gameObject.AddComponent<Core.UI.EconomyValueUIField>();
        Set(field, "query", query); Set(field, "displayOffset", offset);
        var callback = (UnityEngine.Events.UnityAction<string>)Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction<string>), text,
            typeof(TMPro.TMP_Text).GetProperty("text").GetSetMethod());
        UnityEditor.Events.UnityEventTools.AddPersistentListener(Get<UnityEngine.Events.UnityEvent<string>>(field, "onValueChanged"), callback);
        return field;
    }

    static void BindBoolPropertyEvent(object component, string field, Object target, string method, bool value)
    {
        Set(component, field, new UnityEngine.Events.UnityEvent());
        var serialized = new SerializedObject((Object)component);
        var calls = serialized.FindProperty(field + ".m_PersistentCalls.m_Calls");
        calls.arraySize = 1;
        var call = calls.GetArrayElementAtIndex(0);
        call.FindPropertyRelative("m_Target").objectReferenceValue = target;
        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = target.GetType().AssemblyQualifiedName;
        call.FindPropertyRelative("m_MethodName").stringValue = method;
        call.FindPropertyRelative("m_Mode").enumValueIndex = (int)UnityEngine.Events.PersistentListenerMode.Bool;
        call.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue = value;
        call.FindPropertyRelative("m_CallState").enumValueIndex = (int)UnityEngine.Events.UnityEventCallState.RuntimeOnly;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateMetaActivity()
    {
        var definition = Economy<ActivityData>("MetaActivity", "Activity");
        var gameplay = Load<ActivityData>(Auto + "/Definition/AutobattleActivity.asset");
        var type = Asset<ActivityTypeData>(Data + "/Activity/MetaActivityType.asset");
        Set(type, "id", "chainrush.meta"); Set(type, "title", "Meta");
        Set(definition, "activityType", type); Set(definition, "schedule", gameplay.Schedule);
        Set(definition, "simulation", gameplay.Simulation); Set(definition, "topology", gameplay.Topology);
        Set(definition, "result", new ActivityResultSettingsData(ActivityEndMode.Manual, 1, -1));
        var wallet = Wallet("MetaActivity");
        object mount = new ActivityTeamWalletData(); Set(mount, "wallet", wallet);
        Set(mount, "seed", new List<ActivityWalletSeedEntryData> { new ActivityWalletSeedEntryData(
            new SeedEntry(Load<CapabilityHostData>(Data + "/Production/MetaProductionHost.asset"), 1, EconomyFormType.Token),
            ActivitySeedMaterializationType.NonSpatial, new List<TaxonomyTermData>()) });
        object team = new ActivityTeamData(); Set(team, "slotCount", 1);
        Set(team, "wallets", new List<ActivityTeamWalletData> { (ActivityTeamWalletData)mount });
        Set(definition, "teams", new List<ActivityTeamData> { (ActivityTeamData)team });
        var installer = Load<Core.Activities.GameRuntime.Installers.ActivityRuntimeInstallerData>("Assets/Game/Runtime/Installers/ChainRushActivityRuntimeInstaller.asset");
        var activities = Get<List<ActivityData>>(installer, "activities");
        if (!activities.Contains(definition)) activities.Add(definition);
        Set(installer, "activities", activities);
        RegisterDefinitions();
    }

    static ObjectiveConditionMaterializedEntity HeroCondition(TaxonomyTermData role)
        => new ObjectiveConditionMaterializedEntity(default, null, EconomyFormType.Token, Terms(role), null,
            1, CompareOperation.GreaterOrEqual, default);
    static List<EntityCriterionEntryData> ProducerCriteria(CapabilityHostData host)
    {
        var criterion = new CapabilityHostCriterionData(); Set(criterion, "definition", host);
        return new List<EntityCriterionEntryData> { new EntityCriterionEntryData(CriterionRequirementType.Required, criterion),
            new EntityCriterionEntryData(CriterionRequirementType.Required, new OwnerCriterionData()) };
    }

    static void RegisterDefinitions()
    {
        var economy = Load<EconomyDefinitionsInstallerData>("Assets/Game/Runtime/Installers/ChainRushEconomyDefinitionsInstaller.asset");
        var assets = Get<List<EconomyAssetData>>(economy, "assets");
        var wallets = Get<List<EconomyWalletData>>(economy, "wallets");
        foreach (string id in AssetDatabase.FindAssets("t:ScriptableObject", new[] { Data }))
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(id));
            if (asset is EconomyAssetData value && !assets.Contains(value)) assets.Add(value);
            if (asset is EconomyWalletData wallet && !wallets.Contains(wallet)) wallets.Add(wallet);
        }
        Set(economy, "assets", assets); Set(economy, "wallets", wallets);
        var taxonomy = Load<TaxonomyRuntimeInstallerData>("Assets/Game/Runtime/Installers/ChainRushTaxonomyRuntimeInstaller.asset");
        var families = Get<TaxonomyFamilyData[]>(taxonomy, "families").ToList();
        var terms = Get<TaxonomyTermData[]>(taxonomy, "terms").ToList();
        foreach (string id in AssetDatabase.FindAssets("t:TaxonomyFamilyData", new[] { Data }))
        { var value = Load<TaxonomyFamilyData>(AssetDatabase.GUIDToAssetPath(id)); if (!families.Contains(value)) families.Add(value); }
        foreach (string id in AssetDatabase.FindAssets("t:TaxonomyTermData", new[] { Data }))
        { var value = Load<TaxonomyTermData>(AssetDatabase.GUIDToAssetPath(id)); if (!terms.Contains(value)) terms.Add(value); }
        // These two pre-existing installer fields still use arrays; do not refactor their contract here.
        Set(taxonomy, "families", families.ToArray()); Set(taxonomy, "terms", terms.ToArray());
    }

    static void CopyPresentation(UnitData source, EconomyAssetData target)
    { Set(target, "title", source.title); Set(target, "description", source.description); Set(target, "icon", source.icon); }
    static EconomyEntrySelectionData Selection(EconomyAssetData asset, TaxonomyTermData wallet, params TaxonomyTermData[] state)
        => new EconomyEntrySelectionData(asset, EconomyFormType.Stack, Terms(wallet), null, null, state.ToList(), null);
    static List<TaxonomyTermData> Terms(params TaxonomyTermData[] terms) => terms.ToList();
    static T Economy<T>(string name, string directory) where T : EconomyAssetData
    { var value = Asset<T>(Data + "/" + directory + "/" + name + ".asset"); Set(value, "id", "chainrush.meta." + name.ToLowerInvariant()); return value; }
    static TaxonomyFamilyData Family(string name)
    { var value = Asset<TaxonomyFamilyData>(Data + "/Taxonomy/" + name + "Family.asset"); Set(value, "id", "chainrush.meta." + name.ToLowerInvariant()); Set(value, "displayName", name); return value; }
    static TaxonomyTermData Tag(string name, TaxonomyFamilyData family)
    { var value = Asset<TaxonomyTermData>(Data + "/Taxonomy/" + name + ".asset"); Set(value, "id", "chainrush.meta." + name.ToLowerInvariant()); Set(value, "displayName", name); Set(value, "family", family); return value; }
    static EconomyWalletData Wallet(string name, params TaxonomyTermData[] tags)
    { var value = Asset<EconomyWalletData>(Data + "/Wallets/" + name + ".asset"); Set(value, "id", "chainrush.meta.wallet." + name.ToLowerInvariant()); Set(value, "tags", tags.ToList()); return value; }
    static T Load<T>(string path) where T : Object
        => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing " + path);
    static T Asset<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        Folder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        var value = ScriptableObject.CreateInstance<T>();
        value.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(value, path);
        return value;
    }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
    static FieldInfo Field(object value, string name)
    {
        for (var type = value.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); if (field != null) return field; }
        throw new MissingFieldException(value.GetType().FullName, name);
    }
    static T Get<T>(object value, string name) => (T)Field(value, name).GetValue(value);
    static void Set(object value, string name, object content)
    { Field(value, name).SetValue(value, content); if (value is Object asset) EditorUtility.SetDirty(asset); }
}
