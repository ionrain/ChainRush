using UnityEngine;
using System.Collections.Generic;
using MoreMountains.Tools;

public abstract class ItemList<T, P> : MonoBehaviour
    where P : Object, IListItem {
    public delegate void ItemListClickHandler(P item);
    public event ItemListClickHandler OnClick;

    [SerializeField] protected T data;
    [SerializeField] protected Transform root;
    [SerializeField] protected P prefab;
    [SerializeField] protected bool selectable = true;
    [SerializeField] protected bool selectFirstByDefault = true;

    [Header("Blank Items")]
    [SerializeField] protected bool fillBlanks;
    [MMCondition("fillBlanks", true)]
    [SerializeField] protected int defaultCount;
    [MMCondition("fillBlanks", true)]
    [SerializeField] protected int countPerSection;
    [MMCondition("fillBlanks", true)]
    [SerializeField] protected GameObject blankPrefab;

    protected List<P> _items = new List<P>();
    protected List<GameObject> _blanks = new List<GameObject>();
    

    public virtual void Setup() {
        Setup(default);
    }

    public virtual void Setup(T value) {
        if (value != null)
            data = value;

        if (root != null) {
            Clear();
 
            if (data != null && prefab != null)
                InstantiateItems();
        }
    }

    public void Clear() {
        foreach (Transform child in root) {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        root.DetachChildren();
        _items.Clear();
        _blanks.Clear();
    }

    protected virtual void InstantiateItems() {
        InstatiateBlanks();
    }

    protected virtual void InstatiateBlanks() {
        if (defaultCount > 0 && fillBlanks && blankPrefab != null && root != null) {
            int count = 0;
            int totalCount = _items.Count + _blanks.Count;
            if (totalCount <= defaultCount) {
                count = Mathf.Max(0, defaultCount - totalCount);
            } else if (countPerSection > 0) {
                int remainder = totalCount % countPerSection;
                if (remainder > 0)
                    count = countPerSection - remainder;
            }
                
            for (int i = 0; i < count; i++)
                InstantiateBlank();
        }
    }

    protected virtual void InstantiateBlank() {
        _blanks.Add(Instantiate(blankPrefab, root));
    }

    protected virtual void OnItemClick(P item) {
        if (item != null) {
            OnClick?.Invoke(item);

            if (_items != null && selectable) {
                _items.ForEach(t => t.SetSelected(false));
                item.SetSelected(true);
            }
        }
    }

    public virtual void ClearSelection() {
        if (_items != null && selectable)
            _items.ForEach(t => t.SetSelected(false));
    }
}