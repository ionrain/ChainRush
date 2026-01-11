using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum CellSelectPatternType { None = 0, SelectOne = 1, Line = 2, Corner = 4, Box = 8,Zigzag = 16 }

[CreateAssetMenu(fileName = "New LevelDifficultyData", menuName = "Game/LevelDifficultyData", order = 25)]
public class LevelDifficultyData : SerializedScriptableObject {
    public AnimationCurve generalRefreshInterval = AnimationCurve.Linear(0, 5, 1, 5);
    public AnimationCurve meaningfulRefreshInterval = AnimationCurve.Linear(0, 2, 1, 2);

    public Dictionary<CellType, float> meaningfulWeights = new();
    public Dictionary<CellType, float> fillWeights = new();

    public Dictionary<CellSelectPatternType, int> patterns = new Dictionary<CellSelectPatternType, int>()
    {
        { CellSelectPatternType.SelectOne, 1 },
        { CellSelectPatternType.Line, 2 },
        { CellSelectPatternType.Corner, 3 },
        { CellSelectPatternType.Box, 4 },
        { CellSelectPatternType.Zigzag, 5 }
    };    
}