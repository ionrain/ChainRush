using System.Collections;
using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using MoreMountains.Feedbacks;
using UnityEngine;
using MoreMountains.Tools;

public enum WeaponFeedbackTrigger { None, Constant, Use, Activate }

public class AttackSkill : Skill, MMEventListener<MMDamageTakenEvent> {
    const string ENEMY_TAG = "Enemy";

    [Header("Weapon Skill")]
    [SerializeField] protected ElementsData elements;
    [SerializeField] protected Weapon weapon;
    [SerializeField] protected bool autoattack = true;
    [SerializeField] protected float autoattackInterval = 0.1f;
    [SerializeField] WeaponFeedbackTrigger feedbackTrigger = WeaponFeedbackTrigger.Use;

    public Weapon Weapon => weapon;
    public Unit Unit { get; protected set; }
    protected float _vampirism;
    protected float _timeBetweenUses;
    protected float _burstTimeBetweenShots;
    protected virtual string WeaponName => gameObject.name;

    public void SetWeapon(Weapon value) {
        if (value == null) {
            Debug.LogErrorFormat("AttackSkill SetWeapon: weapon is NULL for {0}", Data.name);
            return;
        }

        weapon = value;
    }

    public override void SetSkillSpeed(float skillSpeed){
        base.SetSkillSpeed(skillSpeed);
        if (weapon != null) {
            weapon.TimeBetweenUses = Data.GetParameterValue(SkillParameterType.Duration, _level, _timeBetweenUses) * _skillSpeed;
            weapon.BurstTimeBetweenShots = Data.GetParameterValue(SkillParameterType.IntervalBetween, _level, _burstTimeBetweenShots) * _skillSpeed;
        }
    }

    public override bool Setup(GameObject owner, SkillData skillData, Dictionary<Element, float> multipliers, float skillSpeed, bool activate) {
        if (weapon != null) {
            _timeBetweenUses = weapon.TimeBetweenUses;
            _burstTimeBetweenShots = weapon.BurstTimeBetweenShots;
        }

        if (!base.Setup(owner, skillData, multipliers, skillSpeed, activate))
            return false;

        if (weapon == null || elements == null) {
            Debug.LogErrorFormat("AttackSkill Setup: weapon or elements is NULL for {0}", Data.name);
            return false;
        }
        
        Unit = Owner.GetComponent<Unit>();

        _vampirism = Data.GetParameterValue(SkillParameterType.Vampirism, 0, 0);
        weapon.UseBurstMode = true;
        weapon.BurstLength = (int)Data.GetParameterValue(SkillParameterType.Count, _level, weapon.BurstLength);

        if (!IsAssigned) {
            Debug.LogFormat("AttackSkill Setup: SkillData.level < 0 for {0}", Data.name);
            return false;
        }

        if (Unit != null) {
            OnDisable();
            OnEnable();
        }
        
        if (weapon != null && weapon.enabled) {
            if (autoattack) {
                StopAllCoroutines();
                StartCoroutine(AutoAttack());
            } else
                autoattackInterval = weapon.TimeBetweenUses;
        }

        return true;
    }

    protected override void AssignFeedback(MMFeedbacks feedback) {
        if (weapon != null && feedbackTrigger != WeaponFeedbackTrigger.None) {
            if (feedbackTrigger == WeaponFeedbackTrigger.Use)
                weapon.WeaponUsedMMFeedback = feedback;
            else if (feedbackTrigger == WeaponFeedbackTrigger.Activate)
                weapon.WeaponStartMMFeedback = feedback;
            else if (feedbackTrigger == WeaponFeedbackTrigger.Constant)
                feedback.PlayFeedbacks();
        }
    }

    protected virtual IEnumerator AutoAttack() {
        while (true) {
            weapon.WeaponInputStart();
            yield return new WaitForSeconds(autoattackInterval);
        }
    }

    public virtual void StartAttack() {
        if (_active)
            weapon.WeaponInputStart();
    }

    public virtual void StopAttack() {
        if (_active)
            weapon.WeaponInputStop();
    }

    public void OnMMEvent(MMDamageTakenEvent e) {
        if (Unit != null && e.AffectedHealth.tag.Equals(ENEMY_TAG) && e.Instigator.name.Equals(WeaponName))
            Unit.Heal(e.DamageCaused * _vampirism, Vector2.zero);
    }

    protected virtual void OnEnable() {
        if (_vampirism > 0)
            this.MMEventStartListening<MMDamageTakenEvent>();
    }

    protected virtual void OnDisable() {
        this.MMEventStopListening<MMDamageTakenEvent>();
    }
}
