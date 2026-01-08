using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelResultPopup : RewardPopup, MMEventListener<LevelResultEvent>, MMEventListener<BalanceResourcesEvent> {
    [Header("Level Reward Popup")]
    [SerializeField] protected LevelResult showCondition = LevelResult.None;
    [SerializeField] protected AllUnitsData units;
    [SerializeField] protected Button replayButton;
    [SerializeField] protected ConfirmationPopup confirmationPopup;
    [SerializeField] protected SceneLoader sceneLoader;

    [Header("Score")]
    [SerializeField] float scoreCountDelay;
    [SerializeField] int scoreCountSteps;
    [SerializeField] float scoreCountDuration;
    [SerializeField] protected TextMeshProUGUI cellsOpenScoreLabel;
    [SerializeField] protected TextMeshProUGUI enemiesKillScoreLabel;
    [SerializeField] protected TextMeshProUGUI boostersOpenScoreLabel;
    [SerializeField] protected TextMeshProUGUI trapsRevealScoreLabel;
    [SerializeField] protected TextMeshProUGUI trapsTriggerScoreLabel;
    [SerializeField] protected TextMeshProUGUI unitsLostScoreLabel;
    [SerializeField] protected TextMeshProUGUI totalScoreLabel;
    [SerializeField] protected Progressbar scoreProgressbar;     

    [Header("Score Bonus")]    
    [SerializeField] protected Dictionary<ScoreBonusMultiplier, MMF_Player> scoreBonusFeedbacks = new();

    int _totalScore;
    int _maxScore;
    bool _useEnergy = true;

    protected override void Awake() {
        base.Awake();
        if (scoreProgressbar != null) {
            scoreProgressbar.Setup();
            scoreProgressbar.SetTotal(1);
            scoreProgressbar.SetValue(0);
        }
    }

    public void UseEnergy(bool value) {
        _useEnergy = value;
    }

    void RequestEnergyBalance() {
        if (_useEnergy)
            BalanceResourcesEvent.Trigger(EventStage.Start, new List<ResourceType> { ResourceType.Energy });
    }

    public override bool Setup(IRewardList value) {
        if (base.Setup(value) && value is LevelResultData levelResultData && levelResultData.Valid) {
            ScoreData score = levelResultData.Score;
            cellsOpenScoreLabel?.SetText(score.CellScore.ToString());
            enemiesKillScoreLabel?.SetText(score.EnemyScore.ToString());
            boostersOpenScoreLabel?.SetText(score.BoosterScore.ToString());
            trapsTriggerScoreLabel?.SetText(score.TrapTriggerScore.ToString());
            trapsRevealScoreLabel?.SetText(score.TrapRevealScore.ToString());
            unitsLostScoreLabel?.SetText(score.UnitScore.ToString());

            _maxScore = score.MaxScore;

            if (scoreCountSteps <= 1 || scoreCountDuration == 0)
                totalScoreLabel?.SetText(score.TotalScore.ToString());
            else
                _totalScore = score.TotalScore;



            return true;
        }
        return false;
    }

    protected override IEnumerator Shown(float delay) {
        yield return new WaitForSecondsRealtime(delay);
        if (scoreCountDelay > 0)
            yield return new WaitForSecondsRealtime(scoreCountDelay);
        if (scoreCountDuration > 0 && scoreCountSteps > 1) {
            float interval = scoreCountDuration / scoreCountSteps;
            int delta = _totalScore / scoreCountSteps;
            List<float> keys = new List<float>(ScoreData.ScoreMultipliers.Keys);
            if (delta >= 1) {
                OnShown?.Invoke();
                int score = 0;
                for (int i = 0; i < scoreCountSteps; i++) {
                    totalScoreLabel?.SetText(score.ToString());
                    score += delta;
                    float progress = score / (float)_maxScore;
                    for (int j = keys.Count - 1; j >= 0; j--) {
                        float key = keys[j];
                        if (progress >= key && scoreBonusFeedbacks.TryGetValue(ScoreData.ScoreMultipliers[key], out MMF_Player feedback)) {
                            feedback.PlayFeedbacks();
                            break;
                        }
                    }
                    if (scoreProgressbar != null)
                        scoreProgressbar.SetValue(progress);
                    yield return new WaitForSecondsRealtime(interval);
                }
            }
            totalScoreLabel?.SetText(_totalScore.ToString());
            if (scoreProgressbar != null)
                scoreProgressbar.SetValue(Mathf.Min(_totalScore / (float)_maxScore, 1));
        }
    }

    protected void LoadScene(SceneName sceneName, LevelActionType action) {
        if (confirmationPopup != null && sceneLoader != null)
            if (confirmationPopup.Setup(() => LoadSceneAction(sceneName, action)))
                confirmationPopup.SetVisibility(true);
    }

    protected void LoadSceneAction(SceneName sceneName, LevelActionType action) {
        SetTimescale(1f);
        SetVisibility(false);
        LevelActionEvent.Trigger(EventStage.Start, action);
        if (action == LevelActionType.Reload && _useEnergy)
            SpendResourceEvent.Trigger(EventStage.Start, ResourceType.Energy, ResourceTarget.LevelStart, "LevelReplay", units.energyPrice);
        sceneLoader.LoadScene(sceneName);
    }

    public virtual void Home(bool showConfirmation = true) {
        if (showConfirmation)
            LoadScene(SceneName.Main, LevelActionType.Quit);
        else
            LoadSceneAction(SceneName.Main, LevelActionType.Quit);
    }

    public virtual void Restart() {
        LoadScene(SceneName.Level, LevelActionType.Reload);
    }

    public virtual void OnMMEvent(LevelResultEvent e) {
        if (showCondition == e.Result && Setup(e.Data)) {
            RequestEnergyBalance();
            SetVisibility(true);
        }
    }

    public void OnMMEvent(BalanceResourcesEvent e) {
        if (e.Stage == EventStage.End && e.Balance.ContainsKey(ResourceType.Energy) && units != null && replayButton != null && _useEnergy)
            replayButton.interactable = e.Balance.GetValueOrDefault(ResourceType.Energy).Value >= units.energyPrice;
    }  

    protected override void OnEnable() {
        this.MMEventStartListening<LevelResultEvent>();
        this.MMEventStartListening<BalanceResourcesEvent>();
    }

    protected override void OnDisable() {
        this.MMEventStopListening<LevelResultEvent>();
        this.MMEventStopListening<BalanceResourcesEvent>();
    }

}
