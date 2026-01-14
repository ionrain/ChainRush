using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "New LevelDifficultyData", menuName = "Game/LevelDifficultyData", order = 25)]
public class LevelDifficultyData : SerializedScriptableObject {
    //Refresh Intervals for CellItemTypes here are defined in seconds though AnimationCurves
    //New value is determined based on level time (0-1) and is updated at each board refresh
    //The accumulated time for each CellItemType is not reset, only compared to the new one
    public Dictionary<CellItemType, AnimationCurve> refreshIntervals = new();
    //Plus to the above intervals there can be defined a chance [0-1] that certain CellItemTypes will always be available on refresh
    public Dictionary<CellItemType, float> alwaysAvailableOnRefresh = new();
    public AnimationCurve generalRefreshInterval = AnimationCurve.Linear(0, 5, 1, 5);
    public AnimationCurve meaningfulRefreshInterval = AnimationCurve.Linear(0, 2, 1, 2);

    public Dictionary<CellItemType, float> meaningfulWeights = new();
    public Dictionary<CellItemType, float> fillWeights = new();

    public Dictionary<CellSelectPatternType, int> patterns = new Dictionary<CellSelectPatternType, int>()
    {
        { CellSelectPatternType.SelectOne, 1 },
        { CellSelectPatternType.Line, 2 },
        { CellSelectPatternType.Corner, 3 },
        { CellSelectPatternType.Box, 4 },
        { CellSelectPatternType.Zigzag, 5 }
    };    
}