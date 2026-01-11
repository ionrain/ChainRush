using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;

public class BoosterPanelItemData {
    public BoosterPanelItem item;
    public float delay = 0.4f;
    public int Value { get; set; }
}

public class BoosterPanel : SerializedMonoBehaviour, MMEventListener<PartyBoostEvent>, MMEventListener<TrapEvent>, MMEventListener<LevelLoadEvent> {
    [SerializeField] Transform root;
    [SerializeField] Dictionary<PartyBoosterType, BoosterPanelItemData> data = new();

    public void OnMMEvent(LevelLoadEvent e) {
        /*if (e.Stage == EventStage.Start && e.Data != null)
            foreach (var itemData in data) 
                if (itemData.Value != null && itemData.Value.item != null)
                    itemData.Value.item.gameObject.SetActive(e.Data.ContainsBooster(itemData.Key));*/
    }

    public void OnMMEvent(PartyBoostEvent e) {
        if (e.EventStage == EventStage.Process && e.Source != PartyBoosterSource.Backpack)
            StartCoroutine(UpdatePanelItem(e.Booster, 1));
    }

    IEnumerator UpdatePanelItem(PartyBoosterType booster, int value) {
        BoosterPanelItemData itemData = data.GetValueOrDefault(booster, null);
        if (itemData != null && itemData.item != null) {
            itemData.Value += value;
            yield return new WaitForSeconds(itemData.delay);
            itemData.item.SetText(itemData.Value.ToString());
            itemData.item.PlayFeedbacks();
            yield return new WaitForSeconds(itemData.delay);
            if (value > 0)
                PartyBoostEvent.Trigger(EventStage.End, booster, PartyBoosterSource.None, Vector2.zero);
            else
                TrapEvent.Trigger(EventStage.End, TrapType.Freeze, null);
        }
    }

    public void OnMMEvent(TrapEvent e) {
        if (e.EventStage == EventStage.Process) {
            if (e.Type == TrapType.Freeze)
                StartCoroutine(UpdatePanelItem(PartyBoosterType.Antifreeze, -1));
        }
    }

    void OnEnable() {
        this.MMEventStartListening<LevelLoadEvent>();
        this.MMEventStartListening<PartyBoostEvent>();
        this.MMEventStartListening<TrapEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<LevelLoadEvent>();
        this.MMEventStopListening<PartyBoostEvent>();
        this.MMEventStopListening<TrapEvent>();
    }


}
