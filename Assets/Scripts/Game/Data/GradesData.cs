using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Sirenix.OdinInspector;

public enum Grade { Common, Uncommon, Rare, Epic, Legendary, Mythical, Divine }

[System.Serializable]
public class GradeData {
    public LocalizedString title;
    public Color color = Color.white;
    public Color backColor = Color.white;
    public Color borderColor = Color.white;
    public Color outlineColor = Color.white;
    
    public string Title => title != null && !title.IsEmpty ? title.GetLocalizedString() : string.Empty;
}

[CreateAssetMenu(fileName = "New GradesData", menuName = "Game/GradesData", order = 13)]
public class GradesData : SerializedScriptableObject {
    [SerializeField] Dictionary<Grade, GradeData> grades = new Dictionary<Grade, GradeData>();

    public GradeData GetData(Grade grade) => grades.ContainsKey(grade) ? grades[grade] : null;
}
