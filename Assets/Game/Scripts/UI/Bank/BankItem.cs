using UnityEngine;
using TMPro;

public class BankItem : MonoBehaviour {
    public delegate void BankItemEvent(BankItem item);
    public event BankItemEvent OnClick;

    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI amountLabel;
    [SerializeField] TextMeshProUGUI priceLabel;
    [SerializeField] Transform iconRoot;

    BankItemData _data;

    public BankItemData Data => _data;

    public void Setup(BankItemData data) {
        if (data != null) {
            _data = data;

            if (iconRoot != null && _data.prefab != null)
                Instantiate(_data.prefab, iconRoot);

            if (titleLabel != null)
                titleLabel.text = _data.Title;

            if (amountLabel != null && _data.rewards.Count > 0)
                amountLabel.text = _data.rewards[0].Amount.ToShortString();

            if (priceLabel != null)
                priceLabel.text = _data.priceString;
        }
    }

    public void OnClicked() {
        OnClick?.Invoke(this);
    }
}
