using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using static MoreMountains.TopDownEngine.CharacterStates;

[RequireComponent(typeof(Rigidbody2D), typeof(Character), typeof(Collider2D))]
public class Unit : SerializedMonoBehaviour, MMEventListener<LevelResultEvent> {
    public delegate void UnitEvent(Unit unit);
    public event UnitEvent OnDeath;

    [SerializeField] SpriteRenderer image;
    [SerializeField] SpineSkeletonModel spine;
    [SerializeField] Transform skillRoot;

    [Header("UI")]
    [SerializeField] LocalizedString levelPattern;
    [SerializeField] TextMeshPro levelLabel;

    [Header("Resistance")]
    [SerializeField] GameObject resistanceHost;
    [SerializeField] DamageResistanceProcessor resistanceProcessor;
    [SerializeField] ElementsData elements;

    [Header("Feedbacks")]
    [SerializeField] Transform cellPosition;
    [SerializeField] Transform deathParticlesRoot;
    [SerializeField] MMF_Player spawnFeedback;
    [SerializeField] MMF_Player mergeFeedback;
    [SerializeField] SpriteRenderer statIcon;
    [SerializeField] TextMeshPro statLabel;
    [SerializeField] MMF_Player buffResultFeedback;
    [SerializeField] MMF_Player buffFeedback;
    [SerializeField] float buffDelay = .75f;
    [SerializeField] MMF_Player debuffFeedback;
    [SerializeField] float debuffDelay = .75f;
    [SerializeField] MMF_Player debuffClearFeedback;
    [SerializeField] float debuffClearDelay = .75f;
    [SerializeField] MMF_Player healFeedback;
    [SerializeField] float healDelay = .75f;
    [SerializeField] MMF_Player bombFeedback;
    [SerializeField] float bombDamageDelay = 1.5f;
    [SerializeField] MMF_Player freezerFeedback;
    [SerializeField] float freezeDelay = 1.5f;
    [SerializeField] MMF_Player antifreezeFeedback;
    [SerializeField] float antifreezeDelay = 1.5f;
    [SerializeField] float afterActionDelay = 0.3f;

    public UnitData Data { get; private set; }
    public Dictionary<Attribute, float> Buffs { get; private set; } = new ();
    public Dictionary<Attribute, float> Debuffs { get; private set; } = new ();
    public List<Skill> Skills { get; private set; } = new ();
    public UnitMergeState MergeState { get; private set; }
    public List<Skill> ActiveSkills => Skills.FindAll(t => t.IsAssigned);
    public float HPLoss => _health ? _health.CurrentHealth - _health.MaximumHealth : 0;
    public float DebuffTotal => Debuffs.Sum(t => t.Value);
    public bool IsFrozen => _character != null ? _character.ConditionState.CurrentState == CharacterConditions.Frozen : false;

    Dictionary<MovementStates, AnimationState> _movementToAnimation = new Dictionary<MovementStates, AnimationState>() {
        { MovementStates.Idle, AnimationState.Idle}, {MovementStates.Walking, AnimationState.Walk },
    };

    float _skillSpeed = 1;
    Weapon.WeaponStates _attackState;
    Collider2D _collider;
    Character _character;
    CharacterHandleWeapon _handleWeapon;
    TopDownController2D _controller;
    CharacterMovement _movement;
    Health _health;
    MMProgressBar _healthBar;
    Vector3 _returnPosition;
    AIDecisionDistanceToTarget _distanceDecision;
    string _levelPattern;
    int _layer;
    bool _healthbarVisible = true;

    public void TogglePhysics(bool value) {
        if (_collider != null)
            _collider.enabled = value;
    }

    void PlayCellFeedback(MMF_Player feedback, Vector2 position) {
        if (cellPosition != null)
            cellPosition.position = position;
        feedback?.PlayFeedbacks();       
    }

    public void ToggleFreeze(bool value, Vector2 position) {
        StartCoroutine(ToggleFreezeCo(value));
        PlayCellFeedback(value ? freezerFeedback : antifreezeFeedback, position);
    }

    public IEnumerator ToggleFreezeCo(bool value, bool applyDelay = true) {
        if (applyDelay)
            yield return new WaitForSeconds(value? freezeDelay : antifreezeDelay);
        if (_character != null) {
            _character.ConditionState.ChangeState(value ? CharacterConditions.Frozen : CharacterConditions.Normal);
            _controller.enabled = !value;
            _character.CharacterBrain.enabled = !value;
        }
        
        gameObject.layer = value ? LayerMask.NameToLayer("Default") : _layer;

        if (skillRoot != null)
            skillRoot.gameObject.SetActive(!value);
        
        if (_collider != null && _collider.attachedRigidbody != null)
            _collider.attachedRigidbody.constraints = value ? RigidbodyConstraints2D.FreezeAll : RigidbodyConstraints2D.FreezeRotation;
        
        if (_healthbarVisible)
            SetHealthbarVisibility(!value, false);
        
        if (applyDelay)
            yield return new WaitForSeconds(afterActionDelay);

        if (value)
            TrapEvent.Trigger(EventStage.End, TrapType.Freeze, null);
        else
            PartyBoostEvent.Trigger(EventStage.End, PartyBoosterType.Antifreeze, PartyBoosterSource.None, transform.position);
    
    }    

    public void Heal(float value, Vector2 position) {
        StartCoroutine(HealCo(value));
        PlayCellFeedback(healFeedback, position);
    }

    IEnumerator HealCo(float value) {
        yield return new WaitForSeconds(healDelay);
        _health.ReceiveHealth(value, null);
        yield return new WaitForSeconds(afterActionDelay);
        PartyBoostEvent.Trigger(EventStage.End, PartyBoosterType.Heal, PartyBoosterSource.None, transform.position);
    }

    public void HitWithBomb(float damage, Vector2 position) {
        StartCoroutine(HitWithBombCo(damage, bombDamageDelay));        
        PlayCellFeedback(bombFeedback, position);
    }

    public IEnumerator HitWithBombCo(float damage, float delay) {
        yield return new WaitForSeconds(delay);
        _health?.Damage(damage, gameObject, 0, 0, Vector3.down);
        yield return new WaitForSeconds(afterActionDelay);
        TrapEvent.Trigger(EventStage.End, TrapType.Damage, null);
    }

    public void ClearDebuffs(Vector2 position) {
        Debuffs.Clear();
        StartCoroutine(ClearDebuffsCo());        
        PlayCellFeedback(debuffClearFeedback, position);
    }

    public IEnumerator ClearDebuffsCo() {
        yield return new WaitForSeconds(debuffClearDelay + afterActionDelay);
        PartyBoostEvent.Trigger(EventStage.End, PartyBoosterType.DebuffClear, PartyBoosterSource.None, transform.position);
    }    

    public void AddDebuff(Sprite icon, Attribute attribute, float value, Vector2 position) {
        Debuffs[attribute] = Debuffs.GetValueOrDefault(attribute) + value;
        StartCoroutine(AddDebuffsCo(icon, attribute, value));        
        PlayCellFeedback(debuffFeedback, position);
    }

    public IEnumerator AddDebuffsCo(Sprite icon, Attribute attribute, float value) {
        yield return new WaitForSeconds(debuffDelay + afterActionDelay);
        TrapEvent.Trigger(EventStage.End, TrapType.Debuff, null);
    }    

    public void AddBuff(Sprite icon, Attribute attribute, float value, Vector2 position, bool playFeedback = true) {
        Buffs[attribute] = Buffs.GetValueOrDefault(attribute) + value;
        if (playFeedback) {
            StartCoroutine(AddBuffsCo(icon, attribute, value));
            PlayCellFeedback(buffFeedback, position);
        }
    }

    public IEnumerator AddBuffsCo(Sprite icon, Attribute attribute, float value) {
        SetupStat(icon, value);
        yield return new WaitForSeconds(buffDelay + afterActionDelay);
        PartyBoostEvent.Trigger(EventStage.End, PartyBoosterType.Buff, PartyBoosterSource.None, transform.position);
    }

    void SetupStat(Sprite icon, float value) {
        if (statIcon != null)
            statIcon.sprite = icon;
        statLabel?.SetText(string.Format(value >= 0 ? "+{0}%" : "{0}%", Mathf.RoundToInt(value * 100)));
    }

    void Initialize() {
        _levelPattern = levelPattern != null && !levelPattern.IsEmpty ? levelPattern.GetLocalizedString() : "{0}";
        _character = GetComponent<Character>();
        _collider = GetComponent<Collider2D>();
        _layer = gameObject.layer;
        _healthbarVisible = true;
        if (_character != null) {
            _character.MovementState.OnStateChange += OnMovementStateChange;
            _character.ConditionState.OnStateChange += OnConditionStateChange;
            _controller = GetComponent<TopDownController2D>();
            _handleWeapon = _character.FindAbility<CharacterHandleWeapon>();
            _movement = _character.FindAbility<CharacterMovement>();
            _health = _character.CharacterHealth;
            _healthBar = GetComponent<MMHealthBar>()?.TargetProgressBar;

            if (_character.CharacterBrain != null)
                _distanceDecision = _character.CharacterBrain.gameObject.GetComponent<AIDecisionDistanceToTarget>();

            if (_health != null) {
                _health.OnHit += OnHit;
                _health.OnDeath += OnHealthDead;
            }
        }

        _attackState = Data.attackState;
        CreateSkills();

        if (Data != null && _distanceDecision != null) {
            Skill main = Skills.Find(t => t.Main);
            if (main != null && main.CurrentLevel != null)
                _distanceDecision.Distance = main is MeleeAttackSkill ? 4 : main.CurrentLevel.GetParameterValue(SkillParameterType.Distance) - 2;
        }

        
        //spine?.PlayAnimation(AnimationState.Appear);
    }

     public void SetHealthbarVisibility(bool value, bool setValue = true) {
        if (_healthBar != null) {
            _healthBar.gameObject.SetActive(value);
            if (setValue)
                _healthbarVisible = value;
        }
    }

    public void Setup(UnitData data, UnitMergeState mergeState) {
        Data = data;
        MergeState = mergeState;
        _returnPosition = transform.position;
        TogglePhysics(true);
        if (Data != null) {
            Initialize();
            UpdateMergeState();
            UpdateStats();

            if (deathParticlesRoot != null && Data.deathParticleFX != null) {
                deathParticlesRoot.DestroyChildren();
                Instantiate(Data.deathParticleFX, deathParticlesRoot);
            }
        }
        spawnFeedback?.PlayFeedbacks();        
    }

    void UpdateStats() {
        float tempSkillSpeed = GetAttributeValue(Attribute.SkillSpeed);
        _skillSpeed = tempSkillSpeed > 0 ? 1 / tempSkillSpeed : 1;
        
        SetupSpeed();
        SetupHealth();
        SetupDefense();
    }

    void UpdateMergeState() {
        int mergeIndex = (int)MergeState + 1;
        name = string.Format("{0}-Grade-{1}", Data.name, mergeIndex);
        MergeStateData mergeData = Data.GetMergeData(MergeState);
        if (mergeData != null) {
            if (_collider != null && _collider.attachedRigidbody != null) {
                _collider.attachedRigidbody.mass = mergeData.mass;
                _collider.offset = mergeData.colliderOffset;
                if (_collider is BoxCollider2D boxCollider)
                    boxCollider.size = mergeData.colliderSize;
            }
            if (_healthBar != null) {
                Vector3 position = _healthBar.transform.localPosition;
                position.y = mergeData.healthBarOffset;
                _healthBar.transform.localPosition = position;
            }
            if (spine != null)
                spine.Setup(mergeData.spineData, string.Empty, Data.animations);
        }
        levelLabel?.SetText(string.Format(_levelPattern, mergeIndex));
    }

    public void Upgrade() {
        if (MergeState < UnitMergeState.Fifth) {
            int intState = (int)MergeState + 1;
            MergeState = (UnitMergeState)intState;
            UpdateMergeState();            
            Skills.ForEach(t => t.LevelUp());
            UpdateStats();
            if (_health != null)
                _health.ResetHealthToMaxHealth();
            mergeFeedback?.PlayFeedbacks();
        }
    }    

    void CreateSkills() {
        List<UnitSkill> skillList = Data.GetSkills(SkillListType.Aquired);
        if (skillRoot != null) {
            foreach (UnitSkill unitSkill in skillList) {
                if (unitSkill != null) {
                    SkillData skillData = unitSkill.data;
                    if (skillData != null && skillData.prefab != null) {
                        Skill skill = null;
                        if (skillData.skillType == SkillType.Attack)
                            skill = CreateAttackSkill(unitSkill.main, skillData.prefab);
                        else if (skillData.skillType == SkillType.Support)
                            skill = Instantiate(skillData.prefab, skillRoot);

                        if (skill != null) {
                            skill.Setup(gameObject, skillData, Data.GetAttributeValues(Attribute.Power), _skillSpeed, unitSkill.defaultSkill);
                            Skills.Add(skill);
                        } else
                            Debug.LogErrorFormat("Unit CreateSkills: Skill is NULL for {0}", skillData.name);
                    } else
                        Debug.LogError("Unit CreateSkills: SkillData or Skill Prefab is NULL");
                } else
                    Debug.LogError("Unit CreateSkills: UnitSkill is NULL");
            }
        } else
            Debug.LogError("Unit CreateSkills: Skill Root is NULL");
    }

    Dictionary<Element, float> GetAttributeValues(Attribute attribute) {
        Dictionary<Element, float> result =new();
        if (Data != null) {
            float mergeMultiplier = 1;
            float buffDebuffMultipleir = GetBuffDebuffMultiplier(attribute);
            MergeStateData mergeData = Data.GetMergeData(MergeState);
            if (mergeData != null)
                mergeMultiplier = mergeData.GetAttributeMultiplier(attribute);
            Dictionary<Element, float> multipliers = new();
            Dictionary<Element, float> values = Data.GetAttributeValues(attribute);
            Skills.ForEach(t => {
                if (t.Target == SkillTarget.Self && t.IsAssigned && t.Attribute == attribute) {
                    SkillLevel current = t.CurrentLevel;
                    if (current != null)
                        multipliers[t.Element] =  multipliers.GetValueOrDefault(t.Element, 0) + current.GetParameterValue(SkillParameterType.Amount, 1) - 1;
                }
            });
            foreach (var pair in values)
                result[pair.Key] = pair.Value * mergeMultiplier * buffDebuffMultipleir * (multipliers.GetValueOrDefault(pair.Key, 0) + 1);
        }
        return result;
    }

    float GetAttributeValue(Attribute attribute) {
        float result = 0;
        float mergeMultiplier = 1;
        MergeStateData mergeData = Data.GetMergeData(MergeState);
        if (mergeData != null)
            mergeMultiplier = mergeData.GetAttributeMultiplier(attribute);
        float value = Data.GetAttributeValue(attribute) * mergeMultiplier * GetBuffDebuffMultiplier(attribute);

        Skills.ForEach(t => {
            if (t.Target == SkillTarget.Self && t.IsAssigned && t.Attribute == attribute) {
                SkillLevel current = t.CurrentLevel;
                if (current != null)
                    result += value * (current.GetParameterValue(SkillParameterType.Amount, 1) - 1);
            }
        });
        return result + value;
    }

    float GetBuffDebuffMultiplier(Attribute attribute) {
        return 1 + Buffs.GetValueOrDefault(attribute) - Debuffs.GetValueOrDefault(attribute);
    }

    Skill CreateAttackSkill(bool main, Skill prefab) {
        Skill result = null;
        if (main && _handleWeapon != null) {
            AttackSkill weaponSkill = prefab as AttackSkill;
            _handleWeapon.ChangeWeapon(weaponSkill.Weapon, weaponSkill.Weapon.WeaponName, false);
            _handleWeapon.CurrentWeapon.WeaponState.OnStateChange += OnMainWeaponStateChange;
            result = _handleWeapon.CurrentWeapon.GetComponent<AttackSkill>();
            result.Main = true;
        } else if (!main && _character != null) {
            result = Instantiate(prefab, skillRoot);
            AttackSkill weaponSkill = result as AttackSkill;
            if (weaponSkill != null && weaponSkill.Weapon != null) {
                weaponSkill.Weapon.SetOwner(_character, null);
                weaponSkill.Weapon.Initialization();
            }
        }
        return result;
    }

    public void CreateUltimateSkill(SkillData skillData, Dictionary<Element, float> multipliers) {
        Skill exchanged = Skills.Find(t => t.Data == skillData.exchangeSkill);
        if (exchanged != null && multipliers != null) {
            int index = Skills.IndexOf(exchanged);
            bool main = exchanged.Main;
            Skill ultimate = CreateAttackSkill(main, skillData.prefab);
            if (ultimate != null) {
                skillData.ResetPairs();
                ultimate.Setup(gameObject, skillData, CalculateAttackValues(multipliers), _skillSpeed, false);
                Skills[index] = ultimate;
                if (!main)
                    Destroy(exchanged.gameObject);
            } else
                Debug.LogFormat("Unit CreateUltimateSkill: Couldn't create Skill for {0}", skillData.name);
        } else
            Debug.LogFormat("Unit CreateUltimateSkill: Exchanged Skill or Multipliers List is NULL for {0}", skillData.name);
    }

    void UpdateAttackSkills(Dictionary<Element, float> multipliers) {
        if (multipliers != null) {
            List<Skill> attackSkills = Skills.FindAll(t => t.Type == SkillType.Attack);
            var attackMultipliers = CalculateAttackValues(multipliers);
            attackSkills.ForEach(t => t.UpdateMultipliers(attackMultipliers));
        }
    }

    Dictionary<Element, float> CalculateAttackValues(Dictionary<Element, float> multipliers) {
        Dictionary<Element, float> result = new Dictionary<Element, float>();
        Dictionary<Element, float> attributeValues = GetAttributeValues(Attribute.Power);
        foreach (var pair in attributeValues)
            result[pair.Key] = attributeValues[pair.Key] * multipliers.GetValueOrDefault(pair.Key, 1);
        return result;
    }

    void SetupSpeed(float multiplier = 1) {
        if (_movement != null)
            _movement.WalkSpeed = GetAttributeValue(Attribute.Speed) * multiplier;
    }

    void SetupSkillSpeed(float multiplier = 1) {
        float result = GetAttributeValue(Attribute.SkillSpeed) * multiplier;
        Skills.ForEach(t => t.SetSkillSpeed(result));
    }

   void SetupDefense(Dictionary<Element, float> multipliers = null) {
        if (resistanceProcessor != null && elements != null && resistanceHost != null) {
            resistanceProcessor.DamageResistanceList.Clear();
            var resistanceComponents = resistanceHost.GetComponents<DamageResistance>();
            foreach (var component in resistanceComponents)
                Destroy(component);
            Dictionary<Element, float> defenseValues = GetAttributeValues(Attribute.Defense);

            foreach (var pair in defenseValues) {
                if (pair.Key != Element.Any && pair.Value > 0) {
                    float multiplier = multipliers != null ? multipliers.GetValueOrDefault(pair.Key, 1) : 1;
                    ElementData elementData = elements.GetData(pair.Key);
                    if (elementData != null) {
                        DamageResistance resistance = resistanceHost.AddComponent<DamageResistance>();
                        resistance.DamageTypeMode = DamageTypeModes.TypedDamage;
                        resistance.TypeResistance = elementData.damageType;
                        resistance.DamageModifierMode = DamageResistance.DamageModifierModes.Flat;
                        resistance.FlatDamageReduction = pair.Value * multiplier;
                        resistance.ClampDamage = true;
                        resistance.DamageModifierClamps = new Vector2(1, int.MaxValue);
                        resistanceProcessor.DamageResistanceList.Add(resistance);
                    }
                }
            }
        }
    }

    void SetupHealth(float multiplier = 1, bool reset = true) {
        if (_character != null && _health != null) {
            float newHealth = GetAttributeValue(Attribute.Health) * multiplier;
            float delta = newHealth - _health.MaximumHealth;
            _health.MaximumHealth = newHealth;
            _health.InitialHealth = _health.MaximumHealth;
            if (reset)
                _health.ResetHealthToMaxHealth();
            else
                _health.ReceiveHealth(delta, null);
        }
    }

    void OnHit() {
        //Freeze(false);
    }

    void OnMainWeaponStateChange() {
        if (_handleWeapon.CurrentWeapon.WeaponState.CurrentState == _attackState)
            spine?.PlayAnimation(AnimationState.Attack);
    }

    void OnMovementStateChange() {
        if (_character.ConditionState.CurrentState != CharacterConditions.Dead && _movementToAnimation.ContainsKey(_character.MovementState.CurrentState))
            spine?.PlayAnimation(_movementToAnimation[_character.MovementState.CurrentState]);
    }

    void OnConditionStateChange() {
        if (_character.ConditionState.CurrentState == CharacterConditions.Dead) {
            spine?.PlayAnimation(AnimationState.Dead);
            Stop();
        }
    }

    public void OnHealthDead() {
        OnDeath?.Invoke(this);
    }

    public void OnMMEvent(LevelResultEvent e) {
        if (e.Result == LevelResult.Success) {
            if (_character != null && _health != null)
                _health.DamageDisabled();

            if (_controller != null)
                _controller.enabled = false;
            
            if (_movement != null)
                _movement.enabled = false;
            
            Stop();

            spine?.PlayAnimation(AnimationState.Idle);
        }
    }

    public void OnSupportSkillBuff(Attribute attribute, Sprite icon, Dictionary<Element, float> multipliers, bool playFeedback) {
        if (multipliers != null) {
            float multiplier = multipliers.GetValueOrDefault(Element.Any, 1);
            if (attribute == Attribute.Power)
                UpdateAttackSkills(multipliers);
            else if (attribute == Attribute.Speed)
                SetupSpeed(multiplier);
            else if (attribute == Attribute.SkillSpeed)
                SetupSkillSpeed(multiplier);
            else if (attribute == Attribute.Health)
                SetupHealth(multiplier, false);
            else if (attribute == Attribute.Defense)
                SetupDefense(multipliers);
            if (multiplier != 1 && playFeedback && gameObject.activeInHierarchy) {
                SetupStat(icon, multiplier - 1);
                StartCoroutine(PlayBuffFeedback());
            }
        }
    }

    IEnumerator PlayBuffFeedback() {
        yield return new WaitForSeconds(0.3f);
        buffResultFeedback?.PlayFeedbacks();
    }

    public void Stop() {
        if (_handleWeapon != null && _handleWeapon.CurrentWeapon != null) {
            _handleWeapon.CurrentWeapon.WeaponState.OnStateChange -= OnMainWeaponStateChange;
            _handleWeapon.ChangeWeapon(null, "");
        }

        foreach (Skill skill in Skills)
            if (skill != null)
                Destroy(skill.gameObject);
        Skills.Clear();
    }

    public void SetInvulnerable(bool value) {
        if (_character != null && _health != null) {
            if (value)
                _health.DamageDisabled();
            else
                _health.DamageEnabled();
        }
    }

    void OnEnable() {
        this.MMEventStartListening<LevelResultEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<LevelResultEvent>();
    }
}
