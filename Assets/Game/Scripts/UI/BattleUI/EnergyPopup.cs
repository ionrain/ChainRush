using System.Collections.Generic;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;

public class EnergyPopup : Popup<int>, MMEventListener<BankEvent>, MMEventListener<BalanceResourcesEvent> {
    [SerializeField] BuyButton buyButton;
    [SerializeField] TextMeshProUGUI amountLabel;
    [SerializeField] string itemId = "DefaultEnergyPack";
    [SerializeField] int energyAmount = 10;
    [SerializeField] int hardCurrencyPrice = 10;

    protected override void Awake() {
        base.Awake();
        amountLabel?.SetText(energyAmount.ToString());
        buyButton?.Setup(new Dictionary<ResourceType, int> { { ResourceType.HardCurrency, hardCurrencyPrice } }, true);
    }

    public void OnMMEvent(BalanceResourcesEvent e) {
        if (e.Stage == EventStage.End && e.Balance.ContainsKey(ResourceType.HardCurrency))
            buyButton?.UpdateBalance(new Dictionary<ResourceType, int> { { ResourceType.HardCurrency, e.Balance[ResourceType.HardCurrency].Value } });
    }

    public void OnMMEvent(BankEvent e) {
        if (e.Stage == EventStage.Start && e.Type == BankEventType.Request && e.Resource == ResourceType.Energy)
            SetVisibility(true);
    }

    public void BuyEnergy() {
        SpendResourceEvent.Trigger(EventStage.Start, ResourceType.HardCurrency, ResourceTarget.LevelStart, itemId, hardCurrencyPrice);
        EarnResourceEvent.Trigger(EventStage.Start, ResourceType.Energy, ResourceSource.Shop, itemId, energyAmount);
        RewardFlyEvent.Trigger(EventStage.Start, new ResourceReward(ResourceType.Energy, energyAmount), buyButton.transform.position);
        SetVisibility(false);
    }

    void OnEnable() {
        this.MMEventStartListening<BankEvent>();
        this.MMEventStartListening<BalanceResourcesEvent>();  
    }

    void OnDisable() {
        this.MMEventStopListening<BankEvent>();
        this.MMEventStopListening<BalanceResourcesEvent>();
    }
}
