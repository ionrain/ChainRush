using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Events;

public interface IDropItem {
    public void Setup(int amount = 0);
    public Transform Transform { get; }
    public void Pick();
}

public class DropItem<T> : MonoBehaviour, IDropItem {
    [Header("Drop Item")]
    [SerializeField] protected T data;
    [SerializeField] protected float amount;
    [SerializeField] protected bool fixedAmount;
    [SerializeField] protected UnityEvent OnPick;

    public Transform Transform => transform;

    public T Data => data;

    public virtual void SetupData(T setupData) {
        data = setupData;
    }

    public virtual void Setup(int itemAmount = 0) {
        if (itemAmount > 0 && !fixedAmount)
            amount = itemAmount;
    }

    public virtual void Pick() {
        OnPick?.Invoke();
    }
}
