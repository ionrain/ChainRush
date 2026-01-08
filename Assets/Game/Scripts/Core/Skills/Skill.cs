using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public struct SkillLevelUpEvent {
    public Skill Skill { get; private set; }

    static SkillLevelUpEvent e;
    public static void Trigger(Skill skill) {
        e.Skill = skill;
        MMEventManager.TriggerEvent(e);
    }
}

public class Skill : SerializedMonoBehaviour {
    [SerializeField] protected SkillData data;

    [Header("Events")]
    [SerializeField] protected UnityEvent OnActivate;
    [SerializeField] protected UnityEvent OnDeactivate;

    [Header("Feedbacks")]
    [SerializeField] protected Dictionary<int, MMFeedbacks> feedbacks = new();
    protected Dictionary <Element, float> _multipliers = new();
    protected float _skillSpeed = 1;
    protected int _level = -1;
    protected bool _active = true;
    protected Skill _exchangeSkill;
    protected Skill _complementarySkill;

    public virtual SkillType Type { get; protected set; }
    public virtual SkillTarget Target { get; protected set; }
    public virtual Attribute Attribute { get; protected set; }
    public virtual Element Element { get; protected set; }
    public SkillData Data => data;
    public SkillLevel CurrentLevel => Data != null ? Data.GetLevel(_level) : null;
    public bool IsMaxLevel => _level >= LevelsCount - 1;
    public bool IsAssigned => _level > -1 && _level < LevelsCount;
    public bool IsLevelable => _level == -1 && Type == SkillType.Ultimate ? _exchangeSkill != null && _complementarySkill != null
                               && _exchangeSkill.IsMaxLevel && _complementarySkill.IsAssigned : !IsMaxLevel;

    public bool Main { get; set; }
    public int LevelsCount { get; protected set; }
    public GameObject Owner { get; protected set; }

    public virtual void LevelUp() {
        if (IsLevelable) {
            _level++;
            UpdateMultipliers(_multipliers);
            SkillLevelUpEvent.Trigger(this);
        }
    }

    public virtual void SetSkillSpeed(float skillSpeed) {
        _skillSpeed = skillSpeed;
    }

    public virtual bool UpdateMultipliers(Dictionary<Element, float> multipliers) {
        return Setup(Owner, Data, multipliers, _skillSpeed, false);
    }

    public virtual bool Setup(GameObject owner, SkillData skillData, Dictionary<Element, float> multipliers, float skillSpeed, bool activate) {
        if ((data == null && skillData == null) || multipliers == null) {
            Debug.LogErrorFormat("Skill Setup: owner, data or multipliers is NULL for {0}", gameObject.name);
            return false;
        }

        if (_level == -1 && activate) {
            _level = 0;
            _active = false;
        }

        if (Owner == null)
            Owner = owner != null ? owner : gameObject;

        if (Data != skillData && skillData != null)
            data = skillData;

        if (Data != null) {
            LevelsCount = Data.LevelsCount;
            Type = Data.skillType;
            Target = Data.target;
            Attribute = Data.attribute;
            Element = Data.element;
            gameObject.name = Data.name;
        }

        if (_multipliers != multipliers)
            _multipliers = multipliers;

        if (_level < 0 && _active) {
            _active = false;
            OnDeactivate?.Invoke();
        } else if (_level >= 0 && !_active) {
            _active = true;
            OnActivate?.Invoke();
        }

        SetSkillSpeed(skillSpeed);

        foreach (var pair in feedbacks)
            if (pair.Value != null)
                pair.Value.gameObject.SetActive(false);
        if (feedbacks.ContainsKey(_level)) {
            MMFeedbacks feedback = feedbacks[_level];
            feedback.gameObject.SetActive(true);
            AssignFeedback(feedback);
        }
        
        return true;
    }

    protected virtual void AssignFeedback(MMFeedbacks feedback) {
    }
}
