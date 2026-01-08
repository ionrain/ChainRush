using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BuyButton : SerializedMonoBehaviour {
    [SerializeField] ResourcesData resources;
    [SerializeField] Button button;
    [SerializeField] IconTextItem itemPrefab;
    [SerializeField] Transform itemsRoot;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color notEnoughColor = Color.red;
    
    [Header("Events")]
    [SerializeField] UnityEvent OnActive;
    [SerializeField] UnityEvent OnInactive;

    public bool Interactive { get; set; }

    Dictionary<ResourceType, IconTextItem> _resourceItems = new();
    Dictionary<ResourceType, int> _balance = new();
    Dictionary<ResourceType, int> _values = new();


    public void Setup(Dictionary<ResourceType, int> priceValues, bool interactive = false) {
        if (resources != null && itemPrefab != null && itemsRoot != null) {
            itemsRoot.DestroyChildren();
            _resourceItems.Clear();
            _balance.Clear();
            Interactive = interactive;

            foreach (var pair in priceValues) {
                ResourceData data = resources.Get(pair.Key);
                if (data != null) {
                    IconTextItem item = Instantiate(itemPrefab, itemsRoot);
                    item.Setup(data.smallIcon, pair.Value.ToShortString());
                    _resourceItems.Add(pair.Key, item);
                }
                _values[pair.Key] = pair.Value;
            }
        }
    }

    public void UpdatePrice(Dictionary<ResourceType, int> priceValues) {
        foreach (var item in _resourceItems)
            if (priceValues.TryGetValue(item.Key, out var amount)) {
                _values[item.Key] = amount;
                item.Value.SetText(amount.ToShortString());
            }
        UpdateUI();
    }

    public void UpdateBalance(Dictionary<ResourceType, int> balanceValues) {
        balanceValues.ForEach(t => _balance[t.Key] = t.Value);
        UpdateUI();
    }

    void UpdateUI() {
        bool active = true;
        foreach (var pair in _resourceItems) {
            bool enough = _balance.TryGetValue(pair.Key, out var balance) && _values.TryGetValue(pair.Key, out var value) && value <= balance;
            if (!enough)
                active = false;
            pair.Value.SetTextColor(enough ? normalColor : notEnoughColor);
        }

        if (button != null)
            button.interactable = active && Interactive;
        
        if (active && Interactive)
            OnActive.Invoke();
        else
            OnInactive.Invoke();
    }
}
