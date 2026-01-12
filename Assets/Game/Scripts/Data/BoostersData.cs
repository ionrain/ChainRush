using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum BoosterType { Heal, Bomb, SlowTime }

public class BoosterData {
    public BoosterType boosterType;
    public Sprite icon;
    public List<float> multipliers;

    public Sprite Icon => icon;
    public BoosterType Type => boosterType;
    public float GetMultiplier(int level) => multipliers != null && level >= 0 && level < multipliers.Count ? multipliers[level] : 1f;
}

[CreateAssetMenu(fileName = "New BoostersData", menuName = "Game/BoostersData", order = 27)]
public class BoostersData : SerializedScriptableObject {
    List<BoosterData> boosters = new();
    
    public BoosterData Get(BoosterType type) {
        return boosters.Find(b => b.boosterType == type);
    }
}
