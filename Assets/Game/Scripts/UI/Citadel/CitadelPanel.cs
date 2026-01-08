using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

public class CitadelPanel: SerializedMonoBehaviour, MMEventListener<BalanceResourcesEvent> {
    [SerializeField] GameObject tabNotification;    
    [SerializeField] CitadelData data;
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextItem delimeterPrefab;
    [SerializeField] LocalizedString levelPattern;
    [SerializeField] LocalizedString citadelPattern;
    [SerializeField] List<CitadelPanelRow> rows = new();
    [SerializeField] Transform rowsRoot;
    [SerializeField] MMF_Player clickFeedback;
    
    [Header("Upgrade Button")]
    [SerializeField] BuyButton buyButton;

    Dictionary<Vector2Int, CitadelPanelItem> _items = new();
    List<TextItem> _delimiters = new();
    Vector2Int _selected = new Vector2Int(-1, -1);
    int _soft;
    int _bolts;
    string _levelPattern = string.Empty;
    string _requirementPattern = string.Empty;

    void Start() {
        if (data == null) {
            Debug.LogError("CitadelPanel: CitadelData is null");
            return;
        }

        buyButton?.Setup(new Dictionary<ResourceType, int>() { { ResourceType.SoftCurrency, 0 }, { ResourceType.Bolt, 0 } });

        if (levelPattern != null && !levelPattern.IsEmpty) {
            _levelPattern = levelPattern.GetLocalizedString();
            if (citadelPattern != null && !citadelPattern.IsEmpty)
                _requirementPattern = string.Format("{0} {1}", citadelPattern.GetLocalizedString(), _levelPattern);            
        }
        
        Setup();
        BalanceResourcesEvent.Trigger(EventStage.Start, new List<ResourceType> { ResourceType.SoftCurrency, ResourceType.Bolt });        
    }

    void Setup() {
        if (rows != null && rows.Count > 0 && rowsRoot != null && delimeterPrefab != null) {
            _items.Clear();
            rowsRoot.DestroyChildren();

            foreach (CitadelPanelRow rowPrefab in rows) {
                int index = rows.IndexOf(rowPrefab);
                bool rowAvailable = data.IsRowAvailable(index);
                if (!rowAvailable) {
                    TextItem delimiter = Instantiate(delimeterPrefab, rowsRoot);
                    delimiter.Setup(string.Format(_requirementPattern, data.GetRequiredCitadelLevel(index)));
                    _delimiters.Add(delimiter);
                }

                if (rowPrefab != null) {
                    CitadelPanelRow row = Instantiate(rowPrefab, rowsRoot);
                    foreach (CitadelPanelItem item in row.Items)
                        if (item != null) {
                            Vector2Int position = item.Position;
                            item.Setup(() => { 
                                Select(item.Position);
                                if (clickFeedback != null)
                                    clickFeedback.PlayFeedbacks();
                            });
                            item.SetAttributeAmount(data.GetValue(position.x, position.y));
                            item.SetActive(rowAvailable);
                            _items.Add(position, item);
                        }
                }
            }

            Select(_selected);
        }
    }

    public void OnMMEvent(BalanceResourcesEvent e) {
        if (e.Stage == EventStage.End) {
            if (e.Balance.ContainsKey(ResourceType.SoftCurrency))
                _soft = e.Balance[ResourceType.SoftCurrency].Value;
            if (e.Balance.ContainsKey(ResourceType.Bolt))
                _bolts = e.Balance[ResourceType.Bolt].Value;

            bool somethingIsUpgradable = false;
            _items.ForEach(t => {
                if (t.Value != null) {
                    bool upgradable = data.IsUpgadable(t.Key.x, t.Key.y, _soft, _bolts);
                    t.Value.SetUpgradeMark(upgradable);
                    if (!somethingIsUpgradable && upgradable)
                        somethingIsUpgradable = true;
                }
            });
            tabNotification?.SetActive(somethingIsUpgradable);

            if (data != null && buyButton != null) {
                buyButton.Interactive = data.IsUpgadable(_selected.x, _selected.y, _soft, _bolts);
                buyButton?.UpdateBalance(new Dictionary<ResourceType, int>() { { ResourceType.SoftCurrency, _soft }, { ResourceType.Bolt, _bolts } });
            }
        }
    }

    void Select(Vector2Int value) {
        _selected = value;
        _items.ForEach(t => {
            if (t.Value != null)
                t.Value.SetSelected(t.Key == _selected);
        });
        UpdatePrice();
        UpdateTitle();
    }

    public void Upgrade() {
        if (data != null) {
            Dictionary<ResourceType, int> price = new() { { ResourceType.SoftCurrency, data.GetSoftPrice(_selected.x, _selected.y) }, 
                                                          { ResourceType.Bolt, data.GetBoltPrice(_selected.x, _selected.y) }};
            data.Upgrade(_selected.x, _selected.y);
            var item = _items[_selected];
            item.Upgrade();
            item.SetAttributeAmount(data.GetValue(_selected.x, _selected.y));

            int count = _delimiters.Count;
            if (count > 0) {
                List<TextItem> toDelete = new();
                for (int i = 0; i < count; i++)
                    if (data.IsRowAvailable(i + 1))
                        toDelete.Add(_delimiters[i]);
                toDelete.ForEach(t => {
                    _delimiters.Remove(t);
                    Destroy(t.gameObject);
                });
            }

            _items.ForEach(t => t.Value.SetActive(data.IsRowAvailable(t.Key.x)));
            UpdatePrice();
            UpdateTitle();
            SpendResourcesEvent.Trigger(EventStage.Start, ResourceTarget.Citadel, price, string.Empty);
        }
    }

    void UpdateTitle() {
        if (data != null)
            titleLabel.SetText(string.Format("{0} ({1})", data.GetTitle(_selected.x, _selected.y), 
                               string.Format(_levelPattern, data.GetLevel(_selected.x, _selected.y) + 1)));
    }

    void UpdatePrice() {
        if (data != null && buyButton != null) {
            buyButton.Interactive = data.IsUpgadable(_selected.x, _selected.y, _soft, _bolts);
            buyButton.UpdatePrice(new Dictionary<ResourceType, int>() { 
                { ResourceType.SoftCurrency, data.GetSoftPrice(_selected.x, _selected.y) }, 
                { ResourceType.Bolt, data.GetBoltPrice(_selected.x, _selected.y) } });
        }        
    }

    void OnEnable() {
        this.MMEventStartListening<BalanceResourcesEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<BalanceResourcesEvent>();
    }
}
