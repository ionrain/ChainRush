using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class BuffGradesData {
    public Attribute attribute;
    public Dictionary<Grade, float> values = new ();

    public float GetValue(Grade grade) => values.GetValueOrDefault(grade);
}

[CreateAssetMenu(fileName = "New BuffsData", menuName = "Game/BuffsData", order = 26)]
public class BuffsData : SerializedScriptableObject {
    [SerializeField] List<BuffGradesData> buffs = new();

    public BuffGradesData Get(Attribute attribute) {
        return buffs.Find(t => t.attribute == attribute);
    }
}
