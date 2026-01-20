using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public class CellSelectInfoPanel : MonoBehaviour, MMEventListener<CellUiItemSelectEvent>, MMEventListener<LevelLoadEvent> {

    [SerializeField] RectTransform panelRoot;
    [SerializeField] IconTextItem itemPrefab;

    [Header("Item Data")]
    [SerializeField] ResourcesData resourcesData;
    [SerializeField] AllUnitsData unitsData;
    [SerializeField] BuffsData buffsData;
    [SerializeField] AttributesData attributesData;
    [SerializeField] BoostersData boostersData;

    readonly List<IconTextItem> _items = new();
    readonly Dictionary<Attribute, float> _buffs = new();

    public void OnMMEvent(CellUiItemSelectEvent e) {
        if (e.Stage == EventStage.Process) {
            panelRoot.gameObject.SetActive(true);
            UpdateDisplay(e.Item, e.Count);
        } else if (e.Stage == EventStage.End) {
            if (e.Item != null && e.Item.Type == CellItemType.Buff)
                AccumulateBuff(e.Item.Id, e.Count);
            ClearDisplay();
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
                DisplayBooster(item.Icon, count);
                break;
        }

        panelRoot.gameObject.SetActive(_items.Count > 0);
    }

    void ClearDisplay() {
        foreach (var item in _items) {
            if (item != null)
                Destroy(item.gameObject);
        }
        _items.Clear();
        panelRoot.gameObject.SetActive(false);
    }

    IconTextItem CreateItem(Sprite icon, string text) {
        IconTextItem item = Instantiate(itemPrefab, panelRoot);
        item.Setup(icon, text);
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
            string text = string.Format("{0} Lv.{1}", unitName, maxMergeValue);
            CreateItem(icon, text);
        }

        if (remainder > 0) {
            UnitMergeState mergeState = (UnitMergeState)(remainder - 1);
            MergeStateData mergeData = unitData.GetMergeData(mergeState);
            Sprite icon = mergeData != null ? mergeData.icon : unitData.Icon;
            string text = string.Format("{0} Lv.{1}", unitName, remainder);
            CreateItem(icon, text);
        }
    }

    // ==================== BUFF ====================
    void DisplayBuff(string buffId, int count) {
        if (!System.Enum.TryParse(buffId, out Attribute attribute)) return;

        Sprite icon = null;
        if (attributesData != null) {
            AttributeData attrData = attributesData.GetData(attribute);
            if (attrData != null)
                icon = attrData.icon;
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

        CreateItem(icon, text);
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
        if (resourcesData != null) {
            ResourceData goldData = resourcesData.Get(ResourceType.SoftCurrency);
            if (goldData != null)
                icon = goldData.icon;
        }
        CreateItem(icon, (count * 10).ToString());
    }

    // ==================== BOOSTER ====================
    void DisplayBooster(Sprite icon, int count) {
        CreateItem(icon, string.Format("Lv.{0}", count));
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
