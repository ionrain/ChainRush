using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ClearBonusPopup : Popup<LevelStageStateData>, MMEventListener<LevelStageStateEvent> {
    [Header("Clear Bonus Popup")]
    [SerializeField] AllUnitsData units;
    [SerializeField] AttributesData attributes;
    [SerializeField] Dictionary<PartyBoosterType, Sprite> boosters = new ();
    [SerializeField] Image icon;

    [Header("Timing")]
    [SerializeField] int iterations = 10;
    [SerializeField] float initialDelay = 0.5f;
    [SerializeField] float optionShowtime = 0.5f;
    [SerializeField] float optionShowSpeedMultiplier = 0.9f;

    [Header("Events")]
    [SerializeField] UnityEvent OnStart;
    [SerializeField] UnityEvent OnShowOption;
    [SerializeField] UnityEvent OnShowBonus;
    

    public override bool Setup(LevelStageStateData value) {
        if (value == null || value.Stage == null || value.Stage.ClearBonus == null) {
            Debug.LogError("ClearBonusPopup Setup: LevelStageStateData or Stage or ClearBonus is NULL.");
            return false;
        }

        if (units == null || attributes == null) {
            Debug.LogError("ClearBonusPopup Setup: AllUnitsData or AttributesData is NULL.");
            return false;
        }

        List<Sprite> options = new();
        options.AddRange(boosters.Values);
        foreach (var attributeData in attributes.data)
            if (attributeData.Value != null)
                options.Add(attributeData.Value.icon);
        List<UnitData> selectedUnits = units.Get(UnitListType.Selected);
        selectedUnits.ForEach(t => options.Add(t.Icon));
        
        Sprite bonusSprite = null;
        StageClearBonus bonus = value.Stage.ClearBonus;
        if (bonus is UnitStageClearBonus unitBonus) {
            if (unitBonus.Data != null)
                bonusSprite = unitBonus.Data.Icon;
            else
                Debug.LogError("ClearBonusPopup Setup: Data is NULL for UnitStageClearBonus.");
        } else if (bonus is BoosterStageClearBonus boosterBonus)
            bonusSprite = boosters.GetValueOrDefault(boosterBonus.Booster);
        
        StartCoroutine(ShowBonus(options, bonusSprite));
        return base.Setup(value);
    }

    IEnumerator ShowBonus(List<Sprite> options, Sprite bonusSprite) {
        yield return new WaitForSecondsRealtime(initialDelay);
        
        OnStart?.Invoke();

        int iteration = 0;
        int optionIndex = 0;
        float showTime = optionShowtime;
        while (iteration < iterations) {
            bool last = iteration == iterations - 1;
            icon.sprite = last ? bonusSprite : options[optionIndex];

            if (!last) {
                OnShowOption?.Invoke();
                yield return new WaitForSecondsRealtime(showTime);
                showTime *= optionShowSpeedMultiplier;
                optionIndex++;
                if (optionIndex >= options.Count)
                    optionIndex = 0;
            }
            iteration++;
        }

        OnShowBonus?.Invoke();
    }

    public void OnMMEvent(LevelStageStateEvent e) {
        if (e.State == LevelStageState.ClearBonus && e.EventStage == EventStage.Process && Setup(e.Data))
            SetVisibility(true);
    }
    
    void OnEnable() {
        this.MMEventStartListening<LevelStageStateEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<LevelStageStateEvent>();
    }
}
