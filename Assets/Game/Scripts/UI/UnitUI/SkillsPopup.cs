using System.Collections.Generic;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;

public struct SkillAquireEvent {
    public EventStage Stage { get; private set; }
    public SkillData Data { get; private set; }

    static SkillAquireEvent e;
    public static void Trigger(EventStage stage, SkillData data) {
        e.Stage = stage;
        e.Data = data;
        MMEventManager.TriggerEvent(e);
    }
}

public class SkillsPopup : Popup<UnitData>, MMEventListener<SkillAquireEvent> {
    [Header("Title")]
    [SerializeField] TextMeshProUGUI balanceLabel;

    [Header("List")]
    [SerializeField] Transform itemsRoot;
    [SerializeField] SkillListItem itemPrefab;
    [SerializeField] TextItem delimiterPrefab;

    int _unitLevel;
    int _balance;
    List<SkillListItem> _items = new List<SkillListItem>();

    public bool Setup(UnitData data, int balance) {
        _balance = balance;
        if (balanceLabel != null)
            balanceLabel.SetText(balance.ToShortString());
        return Setup(data);
    }

    public override bool Setup(UnitData value) {
        if (data != null) {
            _items.Clear();
            data = value;
            _unitLevel = data.Level;
            bool unlocked = data.Unlocked;

            if (itemPrefab != null && itemsRoot != null) {
                foreach (Transform child in itemsRoot)
                    Destroy(child.gameObject);

                foreach (UnitSkill skill in data.skills) {
                    if (skill != null && skill.data != null) {
                        bool delimited = false;
                        if (skill.requiredLevel > _unitLevel) {
                            delimited = true;
                            TextItem delimiter = Instantiate(delimiterPrefab, itemsRoot);
                            if (delimiter != null)
                                delimiter.Setup((skill.requiredLevel + 1).ToString());
                        }

                        SkillListItem item = Instantiate(itemPrefab, itemsRoot);
                        if (item != null) {
                            _items.Add(item);
                            item.Setup(skill, unlocked && !delimited);
                            item.SetupPriceColor(_balance);
                        }
                    }
                }
            }

            return true;
        }
        return false;
    }

    public void OnMMEvent(SkillAquireEvent e) {
        if (e.Data != null) {
            SkillListItem item = _items.Find(t => t.Data == e.Data);
            if (item != null)
                item.PlayBuyFeedback();
        }
    }

    void OnEnable() {
        this.MMEventStartListening<SkillAquireEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<SkillAquireEvent>();
    }
}
