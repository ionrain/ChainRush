using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;

public enum BoosterType { Heal, Bomb, SlowTime }

public class BoosterData {
    public BoosterType boosterType;
    public Sprite icon;
    public LocalizedString title;
    public List<float> multipliers;

    public Sprite Icon => icon;
    public BoosterType Type => boosterType;
    public string Title => title != null && !title.IsEmpty ? title.GetLocalizedString() : string.Empty;
    public float GetMultiplier(int level) {
        level = Mathf.Clamp(level, 0, multipliers.Count - 1);
        return multipliers != null && level >= 0 && level < multipliers.Count ? multipliers[level] : 1f;
    }
}

[CreateAssetMenu(fileName = "New BoostersData", menuName = "Game/BoostersData", order = 27)]
public class BoostersData : SerializedScriptableObject {
    public List<BoosterData> boosters = new();

    public BoosterData Get(BoosterType type) {
        return boosters.Find(b => b.boosterType == type);
    }

    public BoosterData GetRandom() {
        return boosters.Count > 0 ? boosters[Random.Range(0, boosters.Count)] : null;
    }
}
