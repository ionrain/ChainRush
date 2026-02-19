using MoreMountains.Tools;
using UnityEngine;

public class InventoryList : ItemList<AllItemsData, InventoryItem>, MMEventListener<ItemEvent> {
    [MMCondition("fillBlanks", true)]
    [SerializeField] protected Transform blankPool;

    [Header("Item List")]
    [SerializeField] bool shouldNotify;
    [SerializeField] GameObject notification;
    [SerializeField] ItemEventType notificationEvent;
    [SerializeField] ItemOwner itemOwner = ItemOwner.Inventory;
    [SerializeField] ItemType itemType = ItemType.All;
    [SerializeField] UnitClass unitClass = UnitClass.Any;
    [SerializeField] ItemEventType addEvent = ItemEventType.Unequip;
    [SerializeField] ItemEventType removeEvent = ItemEventType.Equip;
    [SerializeField] ItemPopup popup;
    [SerializeField] GradesData grades;

    protected virtual void Awake() {
        if (data != null)
            ShowNotification(data.GetByOwner(ItemOwner.Inventory).Count > 0);
    }

    public virtual void SetShouldNotify(bool value) {
        shouldNotify = value;
    }

    public void ShowNotification(bool show) {
        if (shouldNotify && notification != null && notification.activeSelf != show)
            notification.SetActive(show);
    }

    protected override void InstantiateItems() {
        if (grades != null) {
            foreach (ItemData itemData in data.items) {
                if (Suitable(itemData)) {
                    InstatiateItem(itemData);
                    //_pool.Add(item);
                }
            }
        }
        base.InstantiateItems();
    }

    public bool Suitable(ItemData itemData) {
        return itemData != null && itemData.owner == itemOwner && (itemType == ItemType.All || itemData.itemType == itemType)
                    && (unitClass == UnitClass.Any || itemData.unitClass == unitClass);
    }

    public void SetItemType(int value) {
        ItemType result = (ItemType)value;
        //if (itemType != result) {
            itemType = result;
            Setup();
        //}
    }

    public void SetItemSpeciality(UnitClass value) {
        if (unitClass != value) {
            unitClass = value;
            Setup();
        }
    }

    protected virtual void InstatiateItem(ItemData itemData) {
        InventoryItem item = Instantiate(prefab, root);
        item.OnClick += OnItemClick;
        item.Setup(itemData);
        _items.Add(item);
    }

    protected override void OnItemClick(InventoryItem item) {
        if (popup != null && item != null) {
            bool success = popup.Setup(item.Data);
            if (success)
                popup.SetVisibility(true);
        }
    }

    void OnEnable() {
        this.MMEventStartListening<ItemEvent>();
    }

    public void OnMMEvent(ItemEvent e) {
        if (root != null && e.Stage == EventStage.End && e.Value != null) {
            if (addEvent.HasFlag(e.Type ) && Suitable(e.Value)) {
                if (_blanks.Count > 0) {
                    Destroy(_blanks[0].gameObject);
                    _blanks.RemoveAt(0);
                    Transform pool = blankPool != null ? blankPool : transform;
                    _blanks.ForEach(t => {
                        t.gameObject.SetActive(false);
                        t.transform.SetParent(pool);
                    });
                }

                InstatiateItem(e.Value);

                _blanks.ForEach(t => {
                    t.transform.SetParent(root);
                    t.gameObject.SetActive(true);
                });

            } else if (removeEvent.HasFlag(e.Type)) {
                int index = -1;

                for (int i = 0; i < _items.Count; i++) {
                    InventoryItem item = _items[i];
                    if (item.Data == e.Value) {
                        Destroy(item.gameObject);
                        index = i;
                        break;
                    }
                }

                if (index >= 0)
                    _items.RemoveAt(index);

                InstatiateBlanks();
            } else if (notificationEvent.HasFlag(e.Type)) {
                ShowNotification(true);
            }
        }
    }

    void OnDisable() {
        this.MMEventStopListening<ItemEvent>();
    }
}
