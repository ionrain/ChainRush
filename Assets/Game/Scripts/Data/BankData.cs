using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New BankData", menuName = "Game/BankData", order = 21)]
public class BankData : ScriptableObject {
    public List<BankItemData> items = new List<BankItemData>();
}
