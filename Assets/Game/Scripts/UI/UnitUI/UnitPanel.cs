using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using TMPro;
using MoreMountains.Tools;
using Spine.Unity;
using Sirenix.OdinInspector;

public class UnitPanel : SerializedMonoBehaviour, MMEventListener<UnitEvent>, MMEventListener<BalanceResourcesEvent> {

    [Header("Tab Notification")]
    [SerializeField] GameObject tabNotification;
    [SerializeField] AllUnitsData units;
    [SerializeField] UnitType unitType = UnitType.Normal;

    [Header("Avatar")]
    [SerializeField] SkeletonGraphic avatar;
    [SerializeField] Image avatarImage;
    [SerializeField] GameObject lockedIcon;
    [SerializeField] GameObject readyIcon;
    [SerializeField] Color lockedColor = Color.white;

    [Header("Merge Info")]
    [SerializeField] GameObject mergeInfoRoot;
    [SerializeField] LocalizedString mergeLoc;
    [SerializeField] List<TextMeshProUGUI> mergeTitles = new();
    [SerializeField] List<TextMeshProUGUI> mergeLabels = new();

    [Header("Title")]
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI levelLabel;
    [SerializeField] LocalizedString levelLoc;

    [Header("Stats")]
    [SerializeField] ElementsData elements;
    [SerializeField] Dictionary<Attribute, IconTextItem> stats;
    [SerializeField] UnitAttributesPopup attributesPopup;
    [SerializeField] Color defaultStatColor = Color.white;
    [SerializeField] Color enhancedStatColor = Color.white;

    [Header("Buttons")]
    [SerializeField] BuyButton buyButton;

    public int SoftBalance => _soft;

    UnitData _data;
    int _soft;
    int _skillPoints;
    bool _locked;

    void Start() {
        buyButton?.Setup(new Dictionary<ResourceType, int>() { { ResourceType.SoftCurrency, 0 }, { ResourceType.UnitCard, 0 } });   
        if (mergeTitles != null && mergeLoc != null && !mergeLoc.IsEmpty) {
            string mergePattern = mergeLoc.GetLocalizedString();
            for (int i = 0; i < mergeTitles.Count; i++) {
                if (mergeTitles[i] != null)
                    mergeTitles[i].text = string.Format(mergePattern, i + 1);
            }
        }
    }

    public void Upgrade() {
        if (_data != null) {
            _data.SpendCards();
            _data.Upgrade();
            UnitEvent.Trigger(EventStage.End, UnitEventType.LevelUp, _data);
            SpendResourcesEvent.Trigger(EventStage.Start, ResourceTarget.LevelUp,
                new Dictionary<ResourceType, int>() { { ResourceType.SoftCurrency, _data.GetUpgradeSoftPrice() }}, _data.name);
            UpdateUI();
        }
    }

    public void Setup(UnitData data) {
        _data = data;
        if (_data != null) {
            if (titleLabel != null)
                titleLabel.text = _data.Title;
            UpdateUI();
            SetupAvatar();
            CheckBalance();
            
            mergeInfoRoot?.SetActive(_data.Unlocked);
            if (mergeLabels != null) {
                for (int i = 0; i < mergeLabels.Count; i++) {
                    if (mergeLabels[i] != null) {
                        MergeStateData mergeData = _data.GetMergeData((UnitMergeState)(i + 1));
                        if (mergeData != null)
                            mergeLabels[i].text = mergeData.Description;
                    }
                }
            }
        }
    }

    void UpdateLockedUI() {
        _locked = _data.State == UnitState.Locked || _data.State == UnitState.ReadyToBeUnlocked;
        if (avatar != null)
            avatar.color = _locked ? lockedColor : Color.white;
        if (buyButton != null)
            buyButton.Interactive = !_locked;
        SetItemActive(lockedIcon, _data.State == UnitState.Locked);
        SetItemActive(readyIcon, _data.State == UnitState.ReadyToBeUnlocked);
    }

    public void Unlock() {
        if (_data != null) {
            _data.SetState(UnitState.Available);
            UpdateLockedUI();
            UpdateTabNotification();
            UnitEvent.Trigger(EventStage.End, UnitEventType.ChangeState, _data);
        }
    }

    void UpdateUI() {
        UpdateLockedUI();
        SetupLevel();
        SetupStats();
        buyButton?.UpdatePrice(new Dictionary<ResourceType, int>() { { ResourceType.SoftCurrency, _data.GetUpgradeSoftPrice() },
                                                                         { ResourceType.UnitCard, _data.GetUpgradeCardPrice() } });
    }

    public void CheckBalance() {
        BalanceResourcesEvent.Trigger(EventStage.Start, new List<ResourceType>() { ResourceType.SoftCurrency });
    }

    void SetItemActive(GameObject obj, bool active) {
        if (obj != null && obj.activeSelf != active)
            obj.SetActive(active);
    }

    void SetupAvatar() {
        if (avatar != null && _data != null) {
            MergeStateData mergeData = _data.GetMergeData(UnitMergeState.First);
            if (mergeData != null) {
                avatar.skeletonDataAsset = mergeData.spineData;
                if (_data.animations.ContainsKey(AnimationState.Idle)) 
                    avatar.startingAnimation = _data.animations[AnimationState.Idle].name;
                avatar.Initialize(true);
            }
        }
    }

    void SetupLevel() {
        if (levelLabel != null)
            levelLabel.text = string.Format(levelLoc.GetLocalizedString(), _data.Level + 1);
    }

    void SetupStats() {
        if (stats != null && elements != null) {
            ElementData elementData = elements.GetData(_data.element);
            int level = _data.Level;
            Dictionary<Attribute, AttributeUpgradeData> values = _data.attributes;

            foreach (var item in values) {
                if (stats.ContainsKey(item.Key) && stats[item.Key] != null && item.Value != null) {
                    float normalValue = item.Value.GetValue(level);
                    float enhancedValue = _data.GetAttributeValue(item.Key, Element.Any);
                    stats[item.Key].SetText(enhancedValue.ToString());
                    stats[item.Key].SetTextColor(normalValue < enhancedValue ? enhancedStatColor : defaultStatColor);
                }
            }
        }
    }

    public void OnMMEvent(UnitEvent e) { 
        if (e.Data != null && e.Data.type == unitType) {
            if (e.Stage == EventStage.Start && e.Type == UnitEventType.Select)
                Setup(e.Data);
            else if (e.Stage == EventStage.End && (e.Type == UnitEventType.CardBalanceChange || e.Type == UnitEventType.LevelUp))
                CheckBalance();
        }
    }

    public void OnMMEvent(BalanceResourcesEvent e) {
        if (e.Stage == EventStage.End) {
            _soft = e.Balance.GetValueOrDefault(ResourceType.SoftCurrency).Value;
            
            if (_data != null)
                buyButton?.UpdateBalance(new Dictionary<ResourceType, int>() { { ResourceType.SoftCurrency, _soft }, { ResourceType.UnitCard, _data.CardBalance } }); 
            UpdateTabNotification();
        }
    }

    void UpdateTabNotification() {
        if (units != null && tabNotification != null) {
            bool show = false;
            var unitList = units.Get(unitType, UnitListType.All);
            foreach (UnitData unit in unitList) {
                if (unit.State == UnitState.ReadyToBeUnlocked || unit.CanBeUpgraded(_soft) || 
                    unit.HasSkillsToBuy(_skillPoints)) {
                    show = true;
                    break;
                }
            }
            tabNotification.SetActive(show);
        }
    }

    void OnEnable() {
        this.MMEventStartListening<UnitEvent>();
        this.MMEventStartListening<BalanceResourcesEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<UnitEvent>();
        this.MMEventStopListening<BalanceResourcesEvent>();
    }
}
