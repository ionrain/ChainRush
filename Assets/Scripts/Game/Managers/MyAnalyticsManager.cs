using GameAnalyticsSDK;
using MoreMountains.Tools;
using Unity.Services.Analytics;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
/*using Firebase.Extensions;
using Firebase.Analytics;*/
using System.Collections.Generic;
using UnityEngine;

public class MyAnalyticsManager : MMSingleton<MyAnalyticsManager>, MMEventListener<EarnResourceEvent>, MMEventListener<SpendResourceEvent>,
    MMEventListener<EarnResourcesEvent>, MMEventListener<SpendResourcesEvent>, MMEventListener<UnitEvent>, MMEventListener<LevelResultEvent>, 
    MMEventListener<TutorialEvent>, MMEventListener<LevelLoadEvent>/*, MMEventListener<LevelStageStateEvent>*/ {

    const string Unknown = "Unknown"; 
    bool _firebaseOK;

    protected override void Awake() {
        base.Awake();
        Initialize();
    }

    public async void Initialize() {
        var options = new InitializationOptions().SetEnvironmentName("production");
        await UnityServices.InitializeAsync(options);
        AnalyticsService.Instance.StartDataCollection();
        
        GameAnalytics.Initialize();

        /*await Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
            _firebaseOK = task.Result == Firebase.DependencyStatus.Available;
        });*/
    }

    void TrackEarnResource(ResourceType resource, ResourceSource source, string itemId, int amount) {        
        if (itemId.Length == 0)
            itemId = Unknown;
        
        string eventName = "resourceEarn";

        CustomEvent customEvent = new CustomEvent(eventName) {
            { "resource", resource.ToString() }, 
            { "resourceSource", source.ToString() }, 
            { "amount", amount },
            { "itemId", itemId }
        };
        AnalyticsService.Instance.RecordEvent(customEvent);

        GameAnalytics.NewResourceEvent(GAResourceFlowType.Source, resource.ToString(), amount, source.ToString(), itemId);

        /*if (_firebaseOK) {
            Parameter[] parameters = {
                new Parameter("resource", resource.ToString()),
                new Parameter("resourceSource", source.ToString()),
                new Parameter(FirebaseAnalytics.ParameterQuantity, amount),
                new Parameter(FirebaseAnalytics.ParameterItemId, itemId)
            };

            FirebaseAnalytics.LogEvent(eventName, parameters);
        }*/
    }

    void TrackSpendResource(ResourceType resource, ResourceTarget target, string itemId, int amount) {
        if (itemId.Length == 0)
            itemId = Unknown;

        string eventName = "resourceSpend";

        CustomEvent customEvent = new CustomEvent(eventName) {
            { "resource", resource.ToString() }, 
            { "resourceTarget", target.ToString() }, 
            { "amount", amount },
            { "itemId", itemId }
        };
        AnalyticsService.Instance.RecordEvent(customEvent);

        GameAnalytics.NewResourceEvent(GAResourceFlowType.Sink, resource.ToString(), amount, target.ToString(), itemId);

        /*if (_firebaseOK) {
            Parameter[] parameters = {
                new Parameter("resource", resource.ToString()),
                new Parameter("resourceTarget", target.ToString()),
                new Parameter(FirebaseAnalytics.ParameterQuantity, amount),
                new Parameter(FirebaseAnalytics.ParameterItemId, itemId)
            };

            FirebaseAnalytics.LogEvent(eventName, parameters);
        }*/
    }

    public void OnMMEvent(EarnResourceEvent e) {
        if (e.Stage == EventStage.End)
            TrackEarnResource(e.Resource, e.Source, e.ItemId, e.Value);
    }

    public void OnMMEvent(EarnResourcesEvent e) {
        if (e.Stage == EventStage.End)
            foreach (var pair in e.Resources)
                TrackEarnResource(pair.Key, e.Source, e.ItemId, pair.Value);
    }

    public void OnMMEvent(SpendResourcesEvent e) {
        if (e.Stage == EventStage.End)
            foreach (var pair in e.Resources)
                TrackSpendResource(pair.Key, e.Target, e.ItemId, pair.Value);
    }

    public void OnMMEvent(SpendResourceEvent e) {
        if (e.Stage == EventStage.End)
            TrackSpendResource(e.Resource, e.Target, e.ItemId, e.Value);
    }

    public void OnMMEvent(UnitEvent e) {
        if (e.Stage == EventStage.End && e.Type == UnitEventType.LevelUp && e.Data != null) {
            string eventName = "unitUpgrade";
            CustomEvent customEvent = new CustomEvent(eventName) {
                { "unit", e.Data.name }, 
                { "level", e.Data.Level } 
            };
            AnalyticsService.Instance.RecordEvent(customEvent);

            GameAnalytics.NewDesignEvent(string.Format("{0}:{1}", eventName, e.Data.name), e.Data.Level);

            /*if (_firebaseOK) {
                Parameter[] parameters = {
                    new Parameter(FirebaseAnalytics.ParameterCharacter, e.Data.name),
                    new Parameter(FirebaseAnalytics.ParameterLevel, e.Data.Level),
                };

                FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelUp, parameters);
            }*/
        }
    }

    /*public void OnMMEvent(LevelStageStateEvent e) {
        if (e.EventStage == EventStage.Start && e.Data != null) {
            string id = e.Data.Stage.Id;
            string level = e.Data.LevelName;
            int time = e.Data.Time;
            if (e.State == LevelStageState.Start) {
                string eventName = "stageStart";
                CustomEvent customEvent = new CustomEvent(eventName) {
                    { "levelName", level }, 
                    { "stageName", id }
                };
                AnalyticsService.Instance.RecordEvent(customEvent);

                GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, id, level);
                             
            } else if (e.State == LevelStageState.Complete) {
                string eventName = "stageComplete";
                CustomEvent customEvent = new CustomEvent(eventName) {
                    { "levelName", level }, 
                    { "stageName", id },
                    { "time", time },
                };
                AnalyticsService.Instance.RecordEvent(customEvent);

                GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, id, level, time);
            }
        }
    }*/

    public void OnMMEvent(LevelResultEvent e) {
        if (e.Result != LevelResult.None && e.Data != null && e.Data.Valid) {
            LevelData level = e.Data.LevelData;
            string levelName = level.name;
            CustomEvent customEvent = new CustomEvent("levelComplete") {
                { "levelName", levelName }, 
                { "levelResult", e.Result.ToString() },
                { "lastStage", e.Data.LastStage },
                //{ "userScore", e.Data.Score.TotalScore },
                { "time", e.Data.Time }
            };
            AnalyticsService.Instance.RecordEvent(customEvent);

            GAProgressionStatus status = GAProgressionStatus.Undefined;
            if (e.Result == LevelResult.Success)
                status = GAProgressionStatus.Complete;
            else if (e.Result == LevelResult.Failure)
                status = GAProgressionStatus.Fail;

            //GameAnalytics.NewProgressionEvent(status, levelName, e.Data.Score.TotalScore);

            /*if (_firebaseOK) {
                Parameter[] parameters = {
                    new Parameter(FirebaseAnalytics.ParameterLevelName, levelName),
                    new Parameter("levelResult", e.Result.ToString()),
                    new Parameter(FirebaseAnalytics.ParameterCharacter, hero.name),
                    new Parameter(FirebaseAnalytics.ParameterLevel, hero.Level),
                    new Parameter("power", (int)party.GetAttributeValue(Attribute.Power)),
                    new Parameter("defense", (int)hero.GetAttributeValue(Attribute.Defense)),
                    new Parameter("health", (int)hero.GetAttributeValue(Attribute.Health)),
                    new Parameter("speed", (int)hero.GetAttributeValue(Attribute.Speed)),
                    new Parameter("stars", level.Stars),
                    new Parameter("time", e.Data.Time),
                    new Parameter("bestTime", level.Best),
                    new Parameter("bestStars", e.Data.BestStars)
                };

                FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelEnd, parameters);
            }*/

            //TinySauce.OnGameFinished(e.Result == LevelResult.Success, e.Data.Score.TotalScore, levelName);

            /*if (e.Result == LevelResult.Success)
                KWCore.Umbrella.GameCore.ProgressManager.CompleteStage(levelId: levelName, stageNumber: level.Stars, score: e.Data.Time);
            else if (e.Result == LevelResult.Failure)
                KWCore.Umbrella.GameCore.ProgressManager.FailStage(levelId: levelName, stageNumber: level.Stars, score: e.Data.Time);
            else if (e.Result == LevelResult.Quit)
                KWCore.Umbrella.GameCore.ProgressManager.QuitStage(levelId: levelName, stageNumber: level.Stars, score: e.Data.Time);*/

            /*MadPixelAnalytics.AnalyticsManager.CustomEvent("level_finish", new Dictionary<string, object>() {
                { "level_number", e.Data.LevelData.Index + 1 },
                { "level_name", levelName },
                { "level_count", e.Data.LevelData.PlaysCount },
                { "level_diff", 0 },
                { "level_loop", 0 },
                { "level_random", false },
                { "level_type", "normal" },
                { "game_mode", "classic" },
                { "result", e.Result.ToString() },
                { "time", e.Data.Time },
                { "progress", Mathf.Round((((float)e.Data.LastStage + 1) / e.Data.LevelData.StagesCount) * 100) },
                { "continue", 0 },
            }, true);*/
            
        }
    }

    public void OnMMEvent(TutorialEvent e) {
        if (e.Stage == EventStage.End && e.Type == TutorialEventType.Complete) {
            string eventName = "tutorialStepComplete";
            string tutorialStep = "tutorialStep";
            string step = e.Triggered.Step.ToString();

            CustomEvent customEvent = new CustomEvent(eventName) {
                { tutorialStep, step }
            };
            AnalyticsService.Instance.RecordEvent(customEvent);

            GameAnalytics.NewDesignEvent(string.Format("{0}:{1}", eventName, step));

            /*if (_firebaseOK) {
                Parameter[] parameters = {
                    new Parameter(tutorialStep, step),
                };

                FirebaseAnalytics.LogEvent(eventName, parameters);
            }*/

            /*KWAnalytics.CustomEvent kwaleeEvent = new KWAnalytics.CustomEvent(eventName);
            kwaleeEvent.AddField(tutorialStep, step);
            KWCore.Umbrella.Portal.SendCustomEvent(kwaleeEvent);*/

            /*MadPixelAnalytics.AnalyticsManager.CustomEvent("tutorial", new Dictionary<string, object>() {
                { "step_name", string.Format("{0:D2}_{1}", (int)e.Triggered.Step, step) },
            }, true);*/

        }
    }

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start && e.Data != null) {
            string levelName =  e.Data.name;

            CustomEvent customEvent = new CustomEvent("levelStart") { { "levelName", levelName } };
            AnalyticsService.Instance.RecordEvent(customEvent);

            GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, levelName);

            /*if (_firebaseOK) {
                Parameter[] parameters = {
                    new Parameter(FirebaseAnalytics.ParameterLevelName, levelName),
                    new Parameter("bestStars", e.Data.Stars),
                    new Parameter("bestTime", e.Data.Best),
                };

                FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelStart, parameters);
            }*/

            //KWCore.Umbrella.GameCore.ProgressManager.StartStage(levelId: levelName, stageNumber: e.Data.Stars);
            //TinySauce.OnGameStarted(levelName);

            /*MadPixelAnalytics.AnalyticsManager.CustomEvent("level_start", new Dictionary<string, object>() {
                { "level_number", e.Data.Index + 1},
                { "level_name", levelName },
                { "level_count", e.Data.PlaysCount },
                { "level_diff", 0 },
                { "level_loop", 0 },
                { "level_random", false },
                { "level_type", "normal" },
                { "game_mode", "classic" },
            }, true);*/
        }
    }

    void OnEnable() {
        this.MMEventStartListening<EarnResourceEvent>();
        this.MMEventStartListening<SpendResourceEvent>();
        this.MMEventStartListening<EarnResourcesEvent>();
        this.MMEventStartListening<SpendResourcesEvent>();
        this.MMEventStartListening<UnitEvent>();
        this.MMEventStartListening<LevelResultEvent>();
        this.MMEventStartListening<LevelLoadEvent>();
        this.MMEventStartListening<TutorialEvent>();
        //this.MMEventStartListening<LevelStageStateEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<EarnResourceEvent>();
        this.MMEventStopListening<SpendResourceEvent>();
        this.MMEventStopListening<EarnResourcesEvent>();
        this.MMEventStopListening<SpendResourcesEvent>();
        this.MMEventStopListening<UnitEvent>();
        this.MMEventStopListening<LevelResultEvent>();
        this.MMEventStopListening<LevelLoadEvent>();
        this.MMEventStopListening<TutorialEvent>();
        //this.MMEventStopListening<LevelStageStateEvent>();
    }
}
