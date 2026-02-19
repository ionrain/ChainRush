using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "New BankItemData", menuName = "Game/BankItemData", order = 22)]
public class BankItemData : SerializedScriptableObject {
    public LocalizedString title;
    public GameObject prefab;
    public float price;
    public float discount;
    public string priceString;
    public List<Reward> rewards = new List<Reward>();

    public string Title => !title.IsEmpty ? title.GetLocalizedString() : string.Empty;
}
