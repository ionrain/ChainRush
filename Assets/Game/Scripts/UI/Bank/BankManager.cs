using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public enum BankEventType { Request }

public struct BankEvent {
    public EventStage Stage;
    public BankEventType Type;
    public ResourceType Resource;

    static BankEvent e;
    public static void Trigger(EventStage stage, BankEventType type, ResourceType resource) {
        e.Stage = stage;
        e.Type = type;
        e.Resource = resource;
        MMEventManager.TriggerEvent(e);
    }
}

public class BankManager : MonoBehaviour {
    [SerializeField] BankData data;
    [SerializeField] BankItem prefab;
    [SerializeField] Transform itemsRoot;

    public void Setup() {
        if (data != null && prefab != null && itemsRoot != null) {
            foreach (Transform child in itemsRoot)
                Destroy(child.gameObject);

            foreach (BankItemData itemData in data.items) {
                BankItem item = Instantiate<BankItem>(prefab, itemsRoot);
                if (item != null) {
                    item.Setup(itemData);
                    item.OnClick += OnClicked;
                }
            }
        }
    }

    void OnClicked(BankItem item) {

    }

    void OnEnable() {
        Setup();
    }
}
