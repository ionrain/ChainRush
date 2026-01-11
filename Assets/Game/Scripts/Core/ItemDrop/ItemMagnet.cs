using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

public struct ItemMagnetEvent {
    public EventStage EventStage { get; private set; }
    public int Id { get; private set; }

    static ItemMagnetEvent e;
    public static void Trigger(EventStage eventStage, int id) {
        e.EventStage = eventStage;
        e.Id = id;
        MMEventManager.TriggerEvent(e);
    }
}

public class ItemMagnet : MonoBehaviour, MMEventListener<ItemDropEvent> {
    [SerializeField] int id;
    [SerializeField] float delay = 2;
    [SerializeField] float maxTime = 3;
    [SerializeField] Vector2 time = Vector2.one;
    [SerializeField] Vector2 offset = Vector2.zero;
    [SerializeField] MMF_Player feedback;

    List<IDropItem> _items = new List<IDropItem>();
    //LevelStageState _stageState = LevelStageState.Start;
    
    public void OnMMEvent(ItemDropEvent e) {
        if (e.Item != null) {
            IDropItem dropItem = e.Item.GetComponent<IDropItem>();
            /*f (dropItem != null) {
                if (_stageState == LevelStageState.Start)
                    StartCoroutine(AttractOneItem(dropItem));
                else if (_stageState == LevelStageState.Battle)
                    _items.Add(dropItem);
            }*/
        }
    }

    /*public void OnMMEvent(LevelStageStateEvent e) {
        if (e.EventStage == EventStage.Start)
            _stageState = e.State;

        if (e.EventStage == EventStage.End && e.State == LevelStageState.Complete)
            AttractAllItems();
    }*/

    public void AttractAllItems() {
        StartCoroutine(AttractCo());
    }

    IEnumerator AttractOneItem(IDropItem item) {
        yield return new WaitForSeconds(delay);
        AttractItem(item, 1.5f);
    }

    IEnumerator AttractCo() {
        ItemMagnetEvent.Trigger(EventStage.Start, id);
        yield return new WaitForSeconds(delay);

        float interval = Time.fixedDeltaTime;
        int countPerTick = Mathf.Max(1, Mathf.CeilToInt(_items.Count * interval / maxTime));
        while (_items.Count > 0) {
            for (int i = 0; i < countPerTick; i++) {
                if (_items.Count > 0) {
                    AttractItem(_items[0]);
                    _items.RemoveAt(0);
                }
            }
            yield return new WaitForSeconds(interval);
        }
        ItemMagnetEvent.Trigger(EventStage.End, id);
    }

    void AttractItem(IDropItem item, float timeMultiplier = 1f) {
        Vector3 randomOffset = new Vector3(Random.Range(-offset.x, offset.x), Random.Range(-offset.y, offset.y), 0) * 0.5f;
        item.Transform.DOMove(transform.position + randomOffset, Random.Range(time.x, time.y) * timeMultiplier).OnComplete(() => { 
            item.Pick(); 
            feedback?.PlayFeedbacks();
        });        
    }

    void OnEnable() {
        this.MMEventStartListening<ItemDropEvent>();
        //this.MMEventStartListening<LevelStageStateEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<ItemDropEvent>();
        //this.MMEventStopListening<LevelStageStateEvent>();
    }
}
