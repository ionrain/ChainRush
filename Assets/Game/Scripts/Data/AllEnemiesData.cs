using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "New AllEnemiesData", menuName = "Game/AllEnemiesData", order = 15)]
public class AllEnemiesData : SerializedScriptableObject {
    public List<EnemyData> enemies = new List<EnemyData>();
    public Dictionary<EnemyType, LocalizedString> typeNames = new Dictionary<EnemyType, LocalizedString>();

    public List<EnemyData> GetUnlocked() {
        List<EnemyData> result = new List<EnemyData>();
        result.AddRange(enemies.FindAll(t => t.wikiLevel == null || t.wikiLevel.State != LevelState.Locked));
        return result;
    }

    public string GetName(EnemyType enemyType) {
        return typeNames.ContainsKey(enemyType) ? typeNames[enemyType].GetLocalizedString() : string.Empty;
    }
}
