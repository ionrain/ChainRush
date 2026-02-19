using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using System.Linq;
using Sirenix.Utilities;

public enum EnemyFillCompleteAction { Stop, Continue, Repeat }
public enum WaveSelectionMethod { First, Sequential, Random }
public enum SpawnShape { None, FillBounds, Point, Line, InsideCircle, OnCircle, OnBox, InsideBox }

public class EnemyWaveGenerationData {
    public List<Dictionary<GameObject, float>> shares = new List<Dictionary<GameObject, float>>();
    public WaveSelectionMethod shareSelection;
    public bool notify;
    public int amount;
    public int wavesCount = 1;
    public float waveInterval = 0.1f;
    public SpawnShape spawnShape;
    public Vector2 spawnerSize;
    public Vector2 spawnerDistance;
    public Dictionary<Attribute, float> multipliers = new Dictionary<Attribute, float>();

    int _useCount;

    public void Reset() {
        _useCount = 0;
    }

    public int IncreaseUseCount() {
        if (shares != null) {
            _useCount++;
            if (_useCount >= shares.Count)
                _useCount = 0;
            return _useCount;
        }
        return 0;
    }
}

[CreateAssetMenu(fileName = "New EnemyGenerationData", menuName = "Game/EnemyGenerationData", order = 5)]
public class EnemyGenerationData : SerializedScriptableObject {
    [Header("Fill options")]
    public int maxSimulteneousCount = 100;
    public int maxFillCount = 100;
    public AnimationCurve enemyCountCurve = AnimationCurve.Constant(0, 1, 1);
    public Dictionary<float, Dictionary<GameObject, float>> enemyProportions = new Dictionary<float, Dictionary<GameObject, float>>();

    [Header("Waves")]
    public Dictionary<float, EnemyWaveGenerationData> waves = new Dictionary<float, EnemyWaveGenerationData>();

    [Header("Triggers")]
    public Dictionary<string, EnemyWaveGenerationData> triggers = new Dictionary<string, EnemyWaveGenerationData>();

    public Dictionary<GameObject, int> GetEnemyList() {
        Dictionary<GameObject, int> result = new Dictionary<GameObject, int>();

        if (enemyProportions != null && enemyProportions != null)
            FillEnemyList(result, enemyProportions.Values.ToList(), maxFillCount);

        if (waves != null)
            GetEnemyListFromWaveData(result, waves.Values.ToList());

        if (triggers != null)
            GetEnemyListFromWaveData(result, triggers.Values.ToList());

        return result;
    }

    void FillEnemyList(Dictionary<GameObject, int> targetList, List<Dictionary<GameObject, float>> sourceList, int multiplier) {
        if (sourceList != null) {
            foreach (var shares in sourceList) {
                if (shares != null) {
                    foreach (var share in shares) {
                        int poolSize = Mathf.Max((int)(share.Value * multiplier), 1);
                        if (targetList.GetValueOrDefault(share.Key, 0) < poolSize)
                            targetList[share.Key] = poolSize;
                    }
                }
            }
        }
    }

    void GetEnemyListFromWaveData(Dictionary<GameObject, int> targetList, List<EnemyWaveGenerationData> wavesList) {
        Dictionary<GameObject, int> result = new Dictionary<GameObject, int>();
        foreach (EnemyWaveGenerationData data in wavesList)
            if (data != null && data.shares != null) 
                FillEnemyList(result, data.shares, data.amount * data.wavesCount);
        
        result.ForEach(t => targetList[t.Key] = targetList.GetValueOrDefault(t.Key, 0) + t.Value);
    }
}
