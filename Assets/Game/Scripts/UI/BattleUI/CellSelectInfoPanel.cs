using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Events;

public enum CellSelectInfoMode { OneByOne, Cumulative }

public class CellSelectInfoPanel : MonoBehaviour, MMEventListener<CellUiItemSelectEvent>, MMEventListener<LevelLoadEvent> {
    [SerializeField] CellSelectInfoMode mode;

    [SerializeField] RectTransform panelRoot;
    [SerializeField] IconMultiTextItem itemPrefab;

    [Header("Item Data")]
    [SerializeField] ResourcesData resourcesData;
    [SerializeField] AllUnitsData unitsData;
    [SerializeField] BuffsData buffsData;
    [SerializeField] AttributesData attributesData;
    [SerializeField] BoostersData boostersData;

    [Header("Events")]
    [SerializeField] UnityEvent OnShow;
    [SerializeField] UnityEvent OnHide;

    readonly List<IconMultiTextItem> _items = new();
    readonly Dictionary<Attribute, float> _buffs = new();

    public void OnMMEvent(CellUiItemSelectEvent e) {
        if (mode == CellSelectInfoMode.OneByOne) {
            if (e.Stage == EventStage.Process) {
                OnShow?.Invoke();
                UpdateDisplay(e.Item, e.Count);
            } else if (e.Stage == EventStage.End) {
                if (e.Item != null && e.Item.Type == CellItemType.Buff)
                    AccumulateBuff(e.Item.Id, e.Count);
                OnHide?.Invoke();
            }
        } else {
            if (e.Stage == EventStage.End && e.Item != null && e.Count > 0) {
                OnShow?.Invoke();
                UpdateDisplay(e.Item, e.Count);
                if (e.Item.Type == CellItemType.Buff)
                    AccumulateBuff(e.Item.Id, e.Count);
                OnHide?.Invoke();
            }
        }
    }

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start)
            _buffs.Clear();
    }

    void UpdateDisplay(CellUiItem item, int count) {
        if (item == null) return;

        ClearDisplay();

        switch (item.Type) {
            case CellItemType.Unit:
                DisplayUnits(item.Id, count);
                break;
            case CellItemType.Buff:
                DisplayBuff(item.Id, count);
                break;
            case CellItemType.SoftCurrency:
                DisplayGold(count);
                break;
            case CellItemType.Booster:
                DisplayBooster(item.Id, count);
                break;
        }

        panelRoot.gameObject.SetActive(_items.Count > 0);
    }

    public void ClearDisplay() {
        foreach (var item in _items) {
            if (item != null)
                Destroy(item.gameObject);
        }
        _items.Clear();
    }

    IconMultiTextItem CreateItem(Sprite icon, List<string> texts) {
        IconMultiTextItem item = Instantiate(itemPrefab, panelRoot);
        item.Setup(icon, texts);
        _items.Add(item);
        return item;
    }

    // ==================== UNITS ====================
    void DisplayUnits(string unitId, int count) {
        if (unitsData == null) return;
        UnitData unitData = unitsData.GetByName(unitId);
        if (unitData == null) return;

        string unitName = unitData.ShortName;

        const int maxMergeValue = 4;
        int fullUnits = count / maxMergeValue;
        int remainder = count % maxMergeValue;

        for (int i = 0; i < fullUnits; i++) {
            MergeStateData mergeData = unitData.GetMergeData(UnitMergeState.Fifth);
            Sprite icon = mergeData != null ? mergeData.icon : unitData.Icon;
            CreateItem(icon, new List<string>() { unitName, string.Format("Lv.{0}", maxMergeValue) });
        }

        if (remainder > 0) {
            UnitMergeState mergeState = (UnitMergeState)(remainder - 1);
            MergeStateData mergeData = unitData.GetMergeData(mergeState);
            Sprite icon = mergeData != null ? mergeData.icon : unitData.Icon;
            CreateItem(icon, new List<string>() { unitName, string.Format("Lv.{0}", remainder) });
        }
    }

    // ==================== BUFF ====================
    void DisplayBuff(string buffId, int count) {
        if (!System.Enum.TryParse(buffId, out Attribute attribute)) return;

        Sprite icon = null;
        string title = attribute.ToString();
        if (attributesData != null) {
            AttributeData attrData = attributesData.GetData(attribute);
            if (attrData != null) {
                icon = attrData.icon;
                title = attrData.Title;
            }
        }

        float currentValue = _buffs.GetValueOrDefault(attribute);

        float addValue = 0;
        if (buffsData != null) {
            BuffGradesData buffData = buffsData.Get(attribute);
            if (buffData != null) {
                int gradeIndex = Mathf.Min(count - 1, (int)Grade.Divine);
                Grade grade = (Grade)gradeIndex;
                addValue = buffData.GetValue(grade);
            }
        }

        string addSign = addValue >= 0 ? "+" : "";
        string text = currentValue != 0
            ? $"{currentValue * 100:F0}% ({addSign}{addValue * 100:F0}%)"
            : $"{addSign}{addValue * 100:F0}%";

        CreateItem(icon, new List<string> { title, text });
    }

    void AccumulateBuff(string buffId, int count) {
        if (!System.Enum.TryParse(buffId, out Attribute attribute)) return;
        if (buffsData == null) return;

        BuffGradesData buffData = buffsData.Get(attribute);
        if (buffData == null) return;

        int gradeIndex = Mathf.Min(count - 1, (int)Grade.Divine);
        Grade grade = (Grade)gradeIndex;
        float value = buffData.GetValue(grade);

        _buffs[attribute] = _buffs.GetValueOrDefault(attribute) + value;
    }

    // ==================== GOLD ====================
    void DisplayGold(int count) {
        Sprite icon = null;
        string title = "Gold";
        if (resourcesData != null) {
            ResourceData goldData = resourcesData.Get(ResourceType.SoftCurrency);
            if (goldData != null) {
                icon = goldData.icon;
                title = goldData.Title;
            }
        }
        CreateItem(icon, new List<string>() { title, (count * 10).ToString() });
    }

    // ==================== BOOSTER ====================
    void DisplayBooster(string itemId, int count) {
        if (!System.Enum.TryParse(itemId, out BoosterType booster)) return;

        Sprite icon = null;
        string title = itemId;
        float stat = count;
        if (boostersData != null) {
            BoosterData boosterData = boostersData.Get(booster);
            if (boosterData != null) {
                icon = boosterData.icon;
                title = boosterData.Title;
                stat = boosterData.GetMultiplier(count - 1);
            }
        }
        CreateItem(icon, new List<string>() { title, $"{stat * 100:F0}%" });
    }

    void OnEnable() {
        this.MMEventStartListening<CellUiItemSelectEvent>();
        this.MMEventStartListening<LevelLoadEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<CellUiItemSelectEvent>();
        this.MMEventStopListening<LevelLoadEvent>();
    }
}
