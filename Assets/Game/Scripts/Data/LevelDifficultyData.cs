using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "New LevelDifficultyData", menuName = "Game/LevelDifficultyData", order = 25)]
public class LevelDifficultyData : SerializedScriptableObject {
    public AnimationCurve generalRefreshInterval = AnimationCurve.Linear(0, 5, 1, 5);
    public AnimationCurve meaningfulRefreshInterval = AnimationCurve.Linear(0, 2, 1, 2);

    //Заменить значимые/незначимые на интервалы для каждого типа предметов с использованием анимационной кривой
    //Новое значение выставляется исходя времени уровня (0-1) и обновляется в момент текущего обновления поля
    //при этом накопленное время для каждого типа предмета не обнуляется, а только сравнивается с новым интервалом
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