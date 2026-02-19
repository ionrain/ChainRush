using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AmountLevelGoal : LevelGoal {
    [SerializeField] protected int amount;

    public override int Amount => amount;

}
