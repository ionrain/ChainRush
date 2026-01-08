using System.Collections.Generic;
using DG.Tweening;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StagesProgressPanel : MonoBehaviour, MMEventListener<LevelStageStateEvent>, MMEventListener<LevelLoadEvent> {
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] StagesProgressItem itemPrefab;
    [SerializeField] HorizontalLayoutGroup itemsRoot;
    [SerializeField] float transitionTime = 0.5f;

    float _transitionDistance;
    float _itemWidth;
    List<StagesProgressItem> _items = new();

    void Awake() {
        if (itemPrefab != null)
            _itemWidth = itemPrefab.GetComponent<RectTransform>().sizeDelta.x;
        if (itemsRoot != null)
            _transitionDistance = itemsRoot.spacing + _itemWidth;
    }

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start && e.Data != null && itemsRoot != null && itemPrefab != null) {
            RectTransform itemsRootTransform = itemsRoot.transform as RectTransform;
            itemsRootTransform.DestroyChildren();
            _items.Clear();

            RectTransform itemsRootParent = itemsRootTransform.parent as RectTransform;
            RectTransform itemPrefabTranform = itemPrefab.GetComponent<RectTransform>();
            if (itemsRootParent == null || itemPrefabTranform == null) {
                Debug.LogError("StagesProgressPanel: itemsRootParent or itemPrefabTranform is not set correctly.");
                return;
            }

            itemsRootTransform.anchoredPosition += new Vector2((itemsRootParent.rect.width - itemPrefabTranform.sizeDelta.x) * 0.5f, 0);
            var stages = e.Data.stages;
            if (stages.Count > 0) {
                stages.ForEach(stage => {
                    var item = Instantiate(itemPrefab, itemsRootTransform);
                    item.Setup(stage);
                    _items.Add(item);
                });
                _items[0].Activate();
            }
        }
    }    

    public void OnMMEvent(LevelStageStateEvent e) {
        if (e.EventStage == EventStage.Start && e.State == LevelStageState.Start && e.IsValid) {
            if (titleLabel != null)
                titleLabel.SetText(string.Format("{0}/{1}", e.Data.StageIndex + 1, e.Data.TotalStages));
            if (e.Data.StageIndex > 0 && e.Data.StageIndex < _items.Count) {
                _items[e.Data.StageIndex - 1].Deactivate();
                _items[e.Data.StageIndex].Activate();
                Transform itemsRootTransform = itemsRoot.transform;
                itemsRootTransform.DOLocalMoveX(itemsRootTransform.localPosition.x - _transitionDistance, transitionTime, true).onComplete += () => {

                };
            }
        }
    }

    void OnEnable() {
        this.MMEventStartListening<LevelLoadEvent>();
        this.MMEventStartListening<LevelStageStateEvent>();
    }
    
    void OnDisable() {
        this.MMEventStopListening<LevelStageStateEvent>();
        this.MMEventStopListening<LevelLoadEvent>();
    }
}
