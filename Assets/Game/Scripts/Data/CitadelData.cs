using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;


[Serializable]
public class TurretPlaceLevel {
    public int row;
    public int index;
    public int level;
}


[Serializable]
public class CitadelStateData {
    public int level;
    public List<TurretPlaceLevel> turretPlaceLevels = new();
}


[Serializable]
public class TurretData {
    public LocalizedString title;
    public Element element;
    public int baseDamage;
    public int damageUpgradeDelta;

    public int Level { get; set; }
    public int Damage => baseDamage + damageUpgradeDelta * Level;
    public string Title => title != null && !title.IsEmpty ? title.GetLocalizedString() : string.Empty;
}


[CreateAssetMenu(fileName = "New CitadelData", menuName = "Game/CitadelData", order = 23)]
public class CitadelData : GameSettings {
    static int MAX_LEVEL = 100;

    public LocalizedString title;
    public int health = 1000;
    public int healthLevelDelta = 100;
    public int baseSoftPrice = 1000;
    public int coinSoftDelta = 100;
    public int baseBlotPrice = 100;
    public int boltPriceDelta = 10;

    [Header("Turrets")]
    public float turretPriceMultiplier = 0.5f;
    public int citadelLevelRequirementDelta = 10;
    public List<List<TurretData>> turrets = new();

    public int Level { get; private set; }
    public int HP => health + healthLevelDelta * Level;
    public string Title => title != null && !title.IsEmpty ? title.GetLocalizedString() : string.Empty;

    public bool IsUpgadable(int row, int index, int softBalance, int boltsBalance) {
        if (!IsRowAvailable(row))
            return false;

        int level = 0;
        if (row < 0) {
            level = Level;
        } else {
            TurretData turretData = Find(row, index);
            if (turretData != null)
                level = turretData.Level;
        }

        return level < MAX_LEVEL && GetSoftPrice(row, index) <= softBalance && GetBoltPrice(row, index) <= boltsBalance;
    }

    public bool IsRowAvailable(int row) => Level >= GetRequiredCitadelLevel(row);
    public int GetRequiredCitadelLevel(int row) => row * citadelLevelRequirementDelta;
    
    int GetSoftPrice(int level) {
        return Mathf.RoundToInt((baseSoftPrice + coinSoftDelta * Mathf.Pow(level, 2 + level * 0.01f)) / coinSoftDelta) * coinSoftDelta;
    }
    
    public int GetBoltPrice(int level) {
        return (level + 1) * 5 * (1 + Mathf.FloorToInt((float)level / 5));
    }

    public TurretData Find(int row, int index) {
        if (row >= 0 && row < turrets.Count) {
            var list = turrets[row];
            if (list != null && index >= 0 && index < list.Count)
                return list[index];
        }
        return null;
    }

    public int GetSoftPrice(int row, int index) {
        if (row < 0) {
            return GetSoftPrice(Level);
        } else {
            TurretData data = Find(row, index);
            if (data != null)
                return Mathf.FloorToInt((GetRequiredCitadelLevel(row) + 1) * GetSoftPrice(data.Level) * turretPriceMultiplier);
        }
        return 0;
    }

    public int GetValue(int row, int index) {
        if (row < 0) {
            return HP;
        } else {
            TurretData data = Find(row, index);
            if (data != null)
                return data.Damage;
        }
        return -1;
    }

    public int GetLevel(int row, int index) {
        if (row < 0) {
            return Level;
        } else {
            TurretData data = Find(row, index);
            if (data != null)
                return data.Level;
        }
        return -1;
    }

    public string GetTitle(int row, int index) {
        if (row < 0) {
            return Title;
        } else {
            TurretData data = Find(row, index);
            if (data != null)
                return data.Title;
        }
        return string.Empty;
    }

    public int GetBoltPrice(int row, int index) {
        if (row < 0) {
            return GetBoltPrice(Level);
        } else {
            TurretData data = Find(row, index);
            if (data != null)
                return Mathf.FloorToInt((GetRequiredCitadelLevel(row) + 1) * GetBoltPrice(data.Level) * turretPriceMultiplier);
        }
        return 0;
    }

    public void Upgrade(int row, int index) {
        if (row < 0) {
            Level++;
        } else {
            TurretData data = Find(row, index);
            if (data != null)
                data.Level++;
        }
    }

    public override void Reset() {
        Level = 0;
        for (int i = 0; i < turrets.Count; i++) {
            var row = turrets[i];
            if (row != null)
                row.ForEach(data => {
                    if (data != null)
                        data.Level = 0;
                });
        }
    }

    public override void Save(GameData gameData) {
        if (gameData != null) {
            gameData.citadel.level = Level;
            for (int i = 0; i < turrets.Count; i++) {
                var list = turrets[i];
                if (list != null)
                    for (int j = 0; j < list.Count; j++) {
                        var data = list[j];
                        if (data != null)
                            gameData.citadel.turretPlaceLevels.Add(new TurretPlaceLevel() { row = i, index = j, level = data.Level });
                    }
            }
        }
    }

    public override void Load(GameData gameData) {
        if (gameData != null) {
            Level = gameData.citadel.level;
            var list = gameData.citadel.turretPlaceLevels;
            list.ForEach(t => {
                TurretData turretData = Find(t.row, t.index);
                if (turretData != null)
                    turretData.Level = t.level;
            });

        }
    }
}

