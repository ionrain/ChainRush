using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MoreMountains.Feedbacks;

public class BattlePopup : Popup<AllUnitsData> {
    [Header("Battle Popup")]
    [SerializeField] UnitItem unitPrefab;
    [SerializeField] MMF_Player unitClickSFX;

    [Header("Unit Slots")]
    [SerializeField] Transform slotRoot;
    [SerializeField] GameObject slotPrefab;
    [SerializeField] UnitList availableList;

    [Header("Go Button")]
    [SerializeField] MMF_Player loadingScreenFX;
    [SerializeField] Button goButton;

    List<Transform> _slots = new List<Transform>();
    bool _useEnergy = true;

    public void UseEnergy(bool value) {
        _useEnergy = value;
    }

    void OnEnable() {
        if (availableList != null)
            availableList.OnClick += OnItemClicked;
    }

    void OnDisable() {
        if (availableList != null)
            availableList.OnClick -= OnItemClicked;
    }

    public override bool Setup(AllUnitsData value) {
        if (value != null && unitPrefab != null && availableList != null && slotRoot != null && slotPrefab != null) {
            if (_slots.Count == 0) {
                int slotsCount = value.capacity.GetValueOrDefault(UnitType.Normal);
                for (int i = 0; i < slotsCount; i++) {
                    GameObject slotObject = Instantiate(slotPrefab, slotRoot);
                    Transform t = slotObject.transform.Find("Slot");
                    if (t != null)
                        _slots.Add(t);
                    else
                        Debug.LogErrorFormat("BattlePopup Setup: Cannot find child object woth name Slot in {0}", slotObject.name);
                }
            } else
                _slots.ForEach(t => RemoveChildren(t));


            List<UnitData> selected = data.Get(UnitType.Normal, UnitListType.Selected);
            foreach (UnitData unit in selected) {
                UnitState state = unit.State;
                Transform parent = null;
                parent = _slots.Find(t => t.childCount == 0);

                if (parent != null) {
                    UnitItem item = Instantiate<UnitItem>(unitPrefab, parent);
                    if (item != null) {
                        item.Setup(unit);
                        item.OnClick += OnItemClicked;
                    }
                }
            }

            if (goButton != null)
                goButton.interactable = selected.Count >= 1;

            availableList.Setup();
            return base.Setup(value);
        }
        return false;
    }

    void OnItemClicked(UnitItem item) {
        if (item != null) {
            if (unitClickSFX != null)
                unitClickSFX.PlayFeedbacks();

            if (data.TrySetState(item.Data, item.Data.State == UnitState.Available ? UnitState.Selected : UnitState.Available))
                Setup();
        } else
            Debug.Log("BattlePopup OnItemClicked: HeroItem is NULL");
    }

    void RemoveChildren(Transform root) {
        if (root != null) {
            List<Transform> children = new List<Transform>();
            foreach (Transform child in root)
                children.Add(child);
            root.DetachChildren();
            children.ForEach(child => Destroy(child.gameObject));
            children.Clear();
        }
    }

    public void LoadLevel() {
        if (_useEnergy)
            SpendResourceEvent.Trigger(EventStage.Start, ResourceType.Energy, ResourceTarget.LevelStart, "NormalStart", data.energyPrice);
        loadingScreenFX?.PlayFeedbacks();
    }
}