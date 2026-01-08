using UnityEngine;
using UnityEngine.Events;

public class ResourceDropItem : MonoBehaviour, IDropItem {
    [SerializeField] ResourceType resource;
    [SerializeField] int amount;
    [SerializeField] UnityEvent OnPick;

    public Transform Transform => transform;

    public void Pick() {
        EarnResourceEvent.Trigger(EventStage.Process, resource, ResourceSource.Gameplay, name, amount);
        OnPick?.Invoke();
    }
}
