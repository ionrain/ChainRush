using UnityEngine;
using UnityEngine.Localization;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using MoreMountains.TopDownEngine;

public enum SkillListType { All, Aquired }
public enum SkillEventType { LevelUp, Buy }

public enum SkillParameterType {
    Amount, AmountOverTime, Duration, IntervalBetween, Count, Radius, Speed, Lifetime,
    Size, Knockback, Recoil, PiercingCount, Distance, OverTimeInterval, Vampirism, Spread, Projectiles, OrbitSpeed, Acceleration,
    OverTimeAmount, OverTimeCount, SpeedMultiplier, SpeedChangeDuration, ForcedStateDuration,
    RicochetCount, RicochetDepth, RicochetProjectiles, RicochetEffectMultiplier
}

public class SkillLevel {
    public LocalizedString description;
    public Dictionary<SkillParameterType, float> parameters = new Dictionary<SkillParameterType, float>();

    public float GetParameterValue(SkillParameterType parameterType, float notfoundValue = -1) {
        return parameters.GetValueOrDefault(parameterType, notfoundValue);
    }
}

public enum SkillType { None = 0, Attack = 30, Support = 10, Ultimate = 50 }
public enum SkillTarget { Enemies, Allies, Self, Place }
public enum TargetType { Single, Piercing, AreaOfEffect, Screen, All }
public enum TargetMode { None, Closest, Random, Area, Visible, All }

[CreateAssetMenu(fileName = "New SkillData", menuName = "Game/SkillData", order = 9)]
public class SkillData : SerializedScriptableObject {
    public LocalizedString title;
    public LocalizedString description;
    public Sprite icon;
    public SkillType skillType;
    public SkillTarget target;
    public Element element;
    public Attribute attribute;
    public MathAction mathAction;
    public TargetType targetType;
    public TargetMode targetMode;
    
    [Header("State changes")]
    public CharacterStates.CharacterConditions forcedState;

    [Header("Values")]
    public List<SkillLevel> levels = new List<SkillLevel>();
    public Skill prefab;

    [Header("Conditions")]
    public SkillData exchangeSkill;
    public SkillData complementarySkill;

    public string Title => GetDescription(title);
    public string Description => GetDescription(description);
    public Sprite Icon => icon;
    public int LevelsCount => levels != null ? levels.Count : 0;
    public bool Fake => false;
    public Sprite PairIcon => Pair != null ? Pair.Icon : null;
    public SkillData Pair { get; set; }
    public bool Aquired { get; set; }
    public bool CanBeAquired => skillType != SkillType.Ultimate || exchangeSkill == null || complementarySkill == null || (exchangeSkill.Aquired && complementarySkill.Aquired);

    public bool LevelIsValid(int level) => level >= 0 && level < LevelsCount;

    public SkillLevel GetLevel(int level) {
        if (LevelIsValid(level))
            return levels[level];
        return null;
    }

    string GetDescription(LocalizedString localized) {
        if (localized != null && !localized.IsEmpty)
            return skillType == SkillType.Support && element != Element.Any ? string.Format(localized.GetLocalizedString(), element) :
                   localized.GetLocalizedString();
        return name;
    }

    public void Initialize() {
        if (exchangeSkill != null && complementarySkill != null) {
            exchangeSkill.Pair = complementarySkill;
            complementarySkill.Pair = exchangeSkill;
        }
    }

    public void ResetPairs() {
        if (exchangeSkill != null && complementarySkill != null) {
            exchangeSkill.Pair = null;
            complementarySkill.Pair = null;
        }
    }

    public float GetParameterValue(SkillParameterType parameter, int level, float defaultValue, float notFoundValue = -1) {
        for (int i = level; i >= 0; i--) {
            float result = levels[i].GetParameterValue(parameter, notFoundValue);
            if (result != notFoundValue)
                return result;
        }
        return defaultValue;
    }
}
