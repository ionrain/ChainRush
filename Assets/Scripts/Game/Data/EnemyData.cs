using System.Collections;
using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.Localization;

public enum EnemyType { Normal, Chieftain, Boss }

[CreateAssetMenu(fileName = "New EnemyData", menuName = "Game/EnemyData", order = 7)]
public class EnemyData : ScriptableObject, IListItemData {
    public LocalizedString title;
    public LocalizedString description;
    public Sprite icon;
    public EnemyType enemyType;
    public LevelData wikiLevel;

    public string Title => title != null && !title.IsEmpty ? title.GetLocalizedString() : string.Empty;
    public string Description => description != null && !description.IsEmpty ? description.GetLocalizedString() : string.Empty;
    public EnemyType Type => enemyType;
    public Sprite Icon => icon;
}
