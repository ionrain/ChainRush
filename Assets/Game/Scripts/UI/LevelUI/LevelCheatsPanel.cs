using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.Events;

public class LevelCheatsPanel : MonoBehaviour {
    [SerializeField] UnityEvent OnShowUI;
    [SerializeField] UnityEvent OnHideUI;


    public void CompleteCurrentStage() {
        #if UNITY_EDITOR || CHEATS
        //LevelStageCompleteRequestEvent.Trigger();
        #endif
    }

    public void Win() {
        #if UNITY_EDITOR || CHEATS
        LevelActionEvent.Trigger(EventStage.Start, LevelActionType.Succeed);
        #endif
    }

    public void Lose() {
        #if UNITY_EDITOR || CHEATS
        LevelActionEvent.Trigger(EventStage.Start, LevelActionType.Fail);
        #endif        
    }

    public void AddLevel() {
        #if UNITY_EDITOR || CHEATS
        PartyExpEvent.Trigger(PartyExpEventType.Consume, 0);
        #endif
    }

    public void SpawnUnit() {
        #if UNITY_EDITOR || CHEATS
        PartyUnitEvent.Trigger(EventStage.Process, PartyUnitEventType.Create, null);
        #endif
    }

    public void ToggleUI(bool value) {
        #if UNITY_EDITOR || CHEATS
        if (value) {
            OnShowUI?.Invoke();
        } else {
            OnHideUI?.Invoke();
        }
        #endif
    }
}
