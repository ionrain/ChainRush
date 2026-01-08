using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using DG.Tweening;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

public enum PartyUnitEventType { Create, Merge, Death }
public enum PartyExpEventType { Consume, Update, Cap, Level }
public enum PartyBoosterType { Heal, Silencer, RandomCellOpener, Shield, DebuffClear, Buff, Antifreeze }
public enum PartyBoosterSource { None, Board, Battle, Backpack, ClearReward }

public struct PartyExpEvent {
    public PartyExpEventType EventType { get; private set; }
    public int Value { get; private set; }

    static PartyExpEvent e;
    public static void Trigger(PartyExpEventType eventType, int value) {
        e.EventType = eventType;
        e.Value = value;
        MMEventManager.TriggerEvent(e);
    }
}

public struct PartyBoostEvent {
    public EventStage EventStage { get; private set; }
    public PartyBoosterType Booster { get; private set; }
    public PartyBoosterSource Source { get; private set; }
    public Vector2 Position { get; private set; }

    static PartyBoostEvent e;
    public static void Trigger(EventStage eventStage, PartyBoosterType booster, PartyBoosterSource source, Vector2 position) {
        e.EventStage = eventStage;
        e.Booster = booster;
        e.Source = source;
        e.Position = position;
        MMEventManager.TriggerEvent(e);
    }
}

public struct PartyUnitEvent {
    public EventStage EventStage { get; private set; }
    public PartyUnitEventType Type { get; private set; }
    public UnitData Unit { get; private set; }

    static PartyUnitEvent e;
    public static void Trigger(EventStage eventStage, PartyUnitEventType type, UnitData unit) {
        e.EventStage = eventStage;
        e.Type = type;
        e.Unit = unit;
        MMEventManager.TriggerEvent(e);
    }
}

public class BuffGradesData {
    public Attribute attribute;
    public Dictionary<Grade, float> values = new ();

    public float GetValue(Grade grade) => values.GetValueOrDefault(grade);
}

public class BuffData: IListItemData {
    public Attribute attribute;
    public Grade grade;
    public Sprite icon;
    public Color borderColor;
    public Color backgroundColor;
    public string title;
    public float oldValue;
    public float value;

    public BuffData() {
        attribute = Attribute.Power;
        grade = Grade.Common;
        icon = null;
        borderColor = Color.white;
        backgroundColor = Color.white;
        title = string.Empty;
        oldValue = 0f;
        value = 0f;
    }

    public BuffData(Attribute attribute, Grade grade, Sprite icon, Color borderColor, Color backgroundColor, string title, float oldValue, float value) {
        this.attribute = attribute;
        this.grade = grade;
        this.icon = icon;
        this.borderColor = borderColor;
        this.backgroundColor = backgroundColor;
        this.title = title;
        this.oldValue = oldValue;
        this.value = value;
    }

    public string Title => title;
    public string Description => GetString(oldValue + value);
    public string OldDescription => GetString(oldValue);
    public Sprite Icon => icon;    
    string GetString(float value) => string.Format(value > 0 ? "+{0}%" : "{0}%", Mathf.Round(value * 100));
}

public class PartyManager : SerializedMonoBehaviour, MMEventListener<PartyExpEvent>, MMEventListener<PartyBoostEvent>,
    MMEventListener<PartyUnitEvent>, MMEventListener<SkillLevelUpEvent>, MMEventListener<LevelLoadEvent>,
    MMEventListener<TrapEvent>, MMEventListener<LevelStageStateEvent>, MMEventListener<BuffSelectEvent> {

    static  List<float> GradeProbabilities = new List<float>() { 0, 0.6f, 0.9f, 0.95f, 0.97f, 0.99f };
    static List<Grade> predefinedGrades = new List<Grade>() { Grade.Common, Grade.Uncommon, Grade.Rare, Grade.Epic };

    [SerializeField] AllUnitsData unitsData;
    [SerializeField] AttributesData attributes;
    [SerializeField] GradesData grades;
    [SerializeField] Citadel citadel;
    [SerializeField] Unit unitPrefab;
    [SerializeField] BoxCollider2D spawnArea;

    [Header("Buffs")]
    [SerializeField] List<BuffGradesData> buffsData = new ();
    [SerializeField] int epicBuffRequestCondition = 5;
    [SerializeField] int maxBuffCount = 3;

    [Header("Timings")]
    [SerializeField] float mergeMoveTime = 0.3f;
    [SerializeField] float mergeInitDelay = 0.5f;
    [SerializeField] float mergeUpgradeDelay = 0.35f;
    [SerializeField] float mergeEndDelay = 0.3f;

    public int Level => _level;
    public bool Valid => _units.Count > 0;

    Dictionary<Attribute, float> _debuffsValues = new();
    Dictionary<Attribute, float> _buffs = new();
    int _damageAmount = 0;
    List<Unit> _units = new List<Unit>();
    Vector2 _spawnSize;
    Transform _spawnRoot;
    int _shieldsCount;
    int _silencersCount;
    int _antifreezeCount;
    float _healAmount;
    
    int _exp;
    int _cap;
    int _level;
    bool _isMerging;
    int _buffRequestCount;

    bool _showHealthBars = true;

    public void SetHealthbarVisibility(bool value) {
        if (_showHealthBars != value) {
            _showHealthBars = value;
            _units.ForEach(t => t.SetHealthbarVisibility(value));
            if (citadel != null)
                citadel.SetHealthbarVisibility(value);
        }
    }

    public void Clear() {
        _units.ForEach(t => Destroy(t.gameObject));
        _units.Clear();
    }

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start && e.Data != null) {
            _debuffsValues = e.Data.debuffsValues;
            _damageAmount = e.Data.damageAmount;
            _healAmount = e.Data.healAmount;
            Setup();
        }
    }

    public void Setup(List<UnitData> selected = null) {
        _exp = 0;
        _level = 0;
        _cap = 10;
        _units.Clear();

        if (spawnArea != null) {
            _spawnSize = spawnArea.size;
            _spawnRoot = spawnArea.transform;
        }

        PartyExpEvent.Trigger(PartyExpEventType.Update, _exp);
        PartyExpEvent.Trigger(PartyExpEventType.Level, _level);
        PartyExpEvent.Trigger(PartyExpEventType.Cap, _cap);

        if (citadel != null)
            citadel.OnDeath += OnCitadelDestroyed;

        if (selected != null)
            selected.ForEach(t => CreateUnit(t));
    }

    IEnumerator CreateUnitWithDelay(UnitData data, float delay) {
        yield return new WaitForSeconds(delay);
        CreateUnit(data);
        PartyUnitEvent.Trigger(EventStage.End, PartyUnitEventType.Create, data);
    }

    void CreateUnit(UnitData data) {
        Vector2 half = data.IsMelee ? new Vector2(0, _spawnSize.y * 0.5f) : new Vector2(_spawnSize.y * 0.5f, _spawnSize.y);
        Vector2 position = new Vector2(Random.Range(0, _spawnSize.x), Random.Range(half.x, half.y)) - _spawnSize * 0.5f + (Vector2)_spawnRoot.position;
        Unit unit = Instantiate(unitPrefab, position, Quaternion.identity, _spawnRoot);
        data.ResetSkillLevels();
        unit.Setup(data, UnitMergeState.First);
        unit.SetHealthbarVisibility(_showHealthBars);
        unit.OnDeath += OnUnitDeath;
        _units.Add(unit);
        ApplySupportMultipliers(unit);
        PartyUnitEvent.Trigger(EventStage.Start, PartyUnitEventType.Merge, null);
        CheckMerge();
    }

    void ApplySupportMultipliers(Unit unit = null, bool playFeedback = false) {
        System.Array array = System.Enum.GetValues(typeof(Attribute));
        foreach (Attribute attribute in array) {
            if (unit != null)
                ApplySupportMultipliers(new List<Unit> { unit }, attribute, playFeedback);
            else
                ApplySupportMultipliers(_units, attribute, playFeedback);
        }
    }

    void OnCitadelDestroyed() {
        LevelActionEvent.Trigger(EventStage.Start, LevelActionType.Fail);
    }

    void OnUnitDeath(Unit unit) {
        PartyUnitEvent.Trigger(EventStage.End, PartyUnitEventType.Death, unit.Data);
        _units.Remove(unit);
    }

    void CheckMerge() {
        Dictionary<string, Dictionary<UnitMergeState, int>> unitDic = new();
        foreach (Unit unit in _units) {
            string title = unit.Data.name;
            if (!unitDic.ContainsKey(title))
                unitDic[title] = new();
            Dictionary<UnitMergeState, int> mergeDic = unitDic[title];
            mergeDic[unit.MergeState] = mergeDic.GetValueOrDefault(unit.MergeState, 0) + 1;
        }

        foreach (var unitPair in unitDic)
            foreach (var mergePair in unitPair.Value)
                if (!_isMerging && mergePair.Value >= 3) {
                    StartCoroutine(Merge(_units.FindAll(t => t.Data.name.Equals(unitPair.Key) && t.MergeState == mergePair.Key)));
                    break;
                }
        
        if (!_isMerging)
            PartyUnitEvent.Trigger(EventStage.End, PartyUnitEventType.Merge, null);
    }

    IEnumerator Merge(List<Unit> mergeList) {
        _isMerging = true;
        yield return new WaitForSeconds(mergeInitDelay);
        List<Unit> suitable = mergeList.FindAll(t => !t.IsFrozen);
        Unit target = suitable[Random.Range(0, suitable.Count)];
        mergeList.Remove(target);
        foreach (Unit u in mergeList) {
            _units.Remove(u);
            u.TogglePhysics(false);
            u.transform.DOMove(target.transform.position, mergeMoveTime).OnComplete(() => { Destroy(u.gameObject); });
        }
        yield return new WaitForSeconds(mergeUpgradeDelay);
        target.Upgrade();
        ApplySupportMultipliers();
        yield return new WaitForSeconds(mergeEndDelay);
        _isMerging = false;
        CheckMerge();
    }

    public void OnMMEvent(PartyExpEvent e) {
        if (e.EventType == PartyExpEventType.Consume) {
            if (e.Value > 0)
                _exp += e.Value;
            else 
                _exp = _cap;
            
            if (_exp >= _cap) {
                while (_exp >= _cap) {
                    _exp -= _cap;
                    _level++;
                    _cap += 30 + 10 * Mathf.FloorToInt((float)_level / 3);
                }
                PartyExpEvent.Trigger(PartyExpEventType.Level, _level);
                PartyExpEvent.Trigger(PartyExpEventType.Cap, _cap);
            }
            PartyExpEvent.Trigger(PartyExpEventType.Update, _exp);
        }
    }

    public void OnMMEvent(PartyBoostEvent e) {
        bool success = false;
        int count = _units.Count;
        if (e.EventStage == EventStage.Start && e.Booster != PartyBoosterType.RandomCellOpener) {
            if (e.Booster == PartyBoosterType.Antifreeze) {
                success = true;
                var list = _units.FindAll(t => t.IsFrozen);
                if (list.Count() > 0) {
                    var target = list[Random.Range(0, list.Count())];
                    target?.ToggleFreeze(false, e.Position);
                } else {
                    _antifreezeCount++;
                    PartyBoostEvent.Trigger(EventStage.Process, e.Booster, e.Source, e.Position);
                }
            } else if (e.Booster == PartyBoosterType.Buff) {
                success = true;
                BuffSelectEvent.Trigger(EventStage.Start, GetRandomBuffItems(), _buffRequestCount);
                _buffRequestCount++;
                if (_buffRequestCount >= epicBuffRequestCondition)
                    _buffRequestCount = 0;                
                // BuffData buff = GetRandomBuff();
                // if (success = buff != null) {
                //     Unit random = _units[Random.Range(0, count)];
                //     random.AddBuff(buff.icon, buff.attribute, buff.value, e.Position);
                //     ApplySupportMultipliers(random);
                // }
            } else if (count > 0) {
                if (e.Booster == PartyBoosterType.Heal) {
                    var sorted = _units.OrderBy(t => t.HPLoss);
                    if (success = sorted.Count() > 0)
                        sorted.First().Heal(_healAmount, e.Position);
                } else if (e.Booster == PartyBoosterType.DebuffClear) {
                    var sorted = _units.OrderBy(t => t.DebuffTotal);
                    if (success = sorted.Count() > 0)
                        sorted.First().ClearDebuffs(e.Position);
                }
            }

            if (!success)
                PartyBoostEvent.Trigger(EventStage.End, e.Booster, e.Source, e.Position);
        }
    }

    public void OnMMEvent(PartyUnitEvent e) {
        if (e.Type == PartyUnitEventType.Create && e.EventStage == EventStage.Start && e.Unit != null && spawnArea != null) 
            StartCoroutine(CreateUnitWithDelay(e.Unit, 0.1f));
    }

    public List<UnitSkill> GetSkills(SkillListType listType) {
        List<UnitSkill> result = new List<UnitSkill>();
        _units.ForEach(t => result.AddRange(t.Data.GetSkills(listType)));
        return result;
    }

    public List<SkillData> GetSkillsData(SkillListType listType) {
        List<SkillData> result = new List<SkillData>();
        List<UnitSkill> skills = GetSkills(listType);
        skills.ForEach(t => result.Add(t.data));
        return result;
    }

    public virtual void OnMMEvent(SkillLevelUpEvent e) {
        if (e.Skill != null && e.Skill.Data != null) {
            if (e.Skill.Type == SkillType.Ultimate && e.Skill is AttackSkill attackSkill && attackSkill.Unit != null) {
                Dictionary<Element, float> multipliers = CalculateSupportMultipliers(e.Skill.Attribute);
                attackSkill.Unit.CreateUltimateSkill(e.Skill.Data, multipliers);
            } else if (e.Skill.Type == SkillType.Support && e.Skill.Target == SkillTarget.Allies)
                ApplySupportMultipliers(_units, e.Skill.Data.attribute, true);
        }
    }

    protected virtual Dictionary<Element, float> CalculateSupportMultipliers(Attribute attribute) {
        Dictionary<Element, float> result = new Dictionary<Element, float> ();
        if (attribute == Attribute.Power || attribute == Attribute.Defense) {
            Element[] elements = (Element[])System.Enum.GetValues(typeof(Element));
            foreach (Element element in elements)
                result[element] = 1f;
        } else
            result[Element.Any] = 1f;

        foreach (Unit unit in _units) {
            List<Skill> skills = unit.ActiveSkills;
            foreach (Skill skill in skills) {
                if (skill.Type == SkillType.Support && skill.Target == SkillTarget.Allies && skill.Attribute == attribute) {
                    SkillLevel current = skill.CurrentLevel;
                    if (current != null)
                        result[skill.Element] += current.GetParameterValue(SkillParameterType.Amount, 1) - 1;
                }
            }
        }
        return result;
    }

    protected virtual void ApplySupportMultipliers(List<Unit> units, Attribute attribute, bool playFeedback) {
        Sprite icon = null;
        if (attributes != null) {
            AttributeData attributeData = attributes.GetData(attribute);
            if (attributeData != null)
                icon = attributeData.icon;
        }
        Dictionary<Element, float> multipliers = ApplyBuffsAndDebuffs(attribute, CalculateSupportMultipliers(attribute));
        units.ForEach(t => t.OnSupportSkillBuff(attribute, icon, multipliers, playFeedback));
    }

    protected Dictionary<Element, float> ApplyBuffsAndDebuffs(Attribute attribute, Dictionary<Element, float> multipliers) {
        Dictionary<Element, float> result = new();
        if (multipliers != null) {
            float buff = _buffs.GetValueOrDefault(attribute, 0);
            multipliers.ForEach(pair => result[pair.Key] = pair.Value + buff);
        }
        return result;
    }

    public void OnMMEvent(TrapEvent e) {
        int count = _units.Count;
        if (e.EventStage == EventStage.Start && e.Type != TrapType.Alarm && e.Trap != null) {
            if (e.Type == TrapType.Damage) {
                if (count > 0) {
                    Unit target = _units.OrderBy(t => t.HPLoss).Last();
                    target.HitWithBomb(_damageAmount, e.Position);
                } else
                    citadel?.HitWithBomb(_damageAmount, e.Position);
            } else if (e.Type == TrapType.DamageAll) {
                _units.ForEach(t => t.HitWithBomb(_damageAmount, e.Position));
                citadel?.HitWithBomb(_damageAmount, e.Position);
            } else if (e.Type == TrapType.Debuff || e.Type == TrapType.DebuffAll) {
                List<Attribute> list = new List<Attribute>(_debuffsValues.Keys);
                if (list.Count > 0) {
                    Attribute key = list[Random.Range(0, list.Count)];
                    AttributeData attributeData = attributes.GetData(key);
                    if (attributeData != null) {
                        if (e.Type == TrapType.Debuff) {
                            Unit target = _units[Random.Range(0, count)];
                            target.AddDebuff(attributeData.icon, key, _debuffsValues[key], e.Position);
                        } else
                            _units.ForEach(t => t.AddDebuff(attributeData.icon, key, _debuffsValues[key], e.Position));
                    } else
                        Debug.LogErrorFormat("PlayerParty TrapEvent: AttributeData not found for {0}", key);
                } else
                    TrapEvent.Trigger(EventStage.End, e.Type, e.Trap);
            } else if (e.Type == TrapType.Freeze) {
                List<Unit> units = _units.FindAll(t => !t.IsFrozen);
                List<Unit> list = units.Count > 0 ? units : _units;
                if (list.Count > 0) {
                    if (_antifreezeCount > 0) {
                        _antifreezeCount--;
                        e.Trap.Block();
                        TrapEvent.Trigger(EventStage.Process, e.Type, e.Trap);
                    } else {
                        Unit target = list[Random.Range(0, list.Count)];
                        target.ToggleFreeze(true, e.Position);
                    }
                } else
                    TrapEvent.Trigger(EventStage.End, e.Type, e.Trap);
            }
        }
    }

    public void OnMMEvent(LevelStageStateEvent e) {
        if (e.State == LevelStageState.Start && e.EventStage == EventStage.Start) {
            StageClearBonus bonus = e.Data.Stage.ClearBonus;
            if (bonus != null && bonus is UnitStageClearBonus unitBonus && unitBonus.Data == null) {
                List<UnitData> selected = unitsData.Get(UnitListType.Selected);
                if (selected.Count > 0)
                    unitBonus.SetData(selected[Random.Range(0, selected.Count)]);
            }
        } else if (e.State == LevelStageState.ClearBonus && e.EventStage == EventStage.End) {            
            StageClearBonus bonus = e.Data.Stage.ClearBonus;
            if (bonus is UnitStageClearBonus unitBonus && unitBonus.Data != null) {
                StartCoroutine(CreateUnitWithDelay(unitBonus.Data, e.Data.Delay));
            } else {
                if (bonus is BoosterStageClearBonus boosterBonus && boosterBonus.Booster != PartyBoosterType.RandomCellOpener)
                    PartyBoostEvent.Trigger(EventStage.Start, boosterBonus.Booster, PartyBoosterSource.ClearReward, Vector2.zero);
                PartyUnitEvent.Trigger(EventStage.End, PartyUnitEventType.Merge, null);
            }
        }
    }
    
    public List<BuffData> GetRandomBuffItems() {
        List<BuffData> result = new List<BuffData>();
        if (attributes != null && grades != null) {
            List<Grade> requiredGrades = _buffRequestCount < epicBuffRequestCondition - 1 ?
                                         new List<Grade>() { Grade.Common, Grade.Uncommon, Grade.Rare } : new List<Grade>() { Grade.Epic };
            int requiredCount = requiredGrades.Count;
            List<Attribute> attributesList = new List<Attribute>(attributes.data.Keys);
            Grade grade = requiredCount == 1 ? requiredGrades[0] : Grade.Common;
            for (int i = 0; i < maxBuffCount; i++) {
                float random = Random.value;
                for (int p = requiredCount - 1; p >= 0; p--) {
                    Grade possibleGrade = requiredGrades[p];
                    if (random >= GradeProbabilities[p]) {
                        grade = possibleGrade;
                        break;
                    }
                }
                Attribute attribute = attributesList[Random.Range(0, attributesList.Count)];
                BuffData buff = GetBuff(attribute, grade);
                if (buff != null) {
                    attributesList.Remove(attribute);
                    result.Add(buff);
                }
            }
        }
        return result;
    }

    public BuffData GetBuff(Attribute attribute, Grade grade) {
        AttributeData attributeData = attributes.GetData(attribute);
        GradeData gradeData = grades.GetData(grade);
        BuffGradesData gradeDataItem = buffsData.Find(t => t.attribute == attribute);
        if (attributeData != null && gradeData != null && gradeDataItem != null) {
            BuffData result = new BuffData();
            result.attribute = attribute;
            result.grade = grade;
            result.oldValue = _buffs.GetValueOrDefault(attribute);
            result.value = gradeDataItem.GetValue(grade);
            result.borderColor = gradeData.color;
            result.backgroundColor = gradeData.borderColor;
            result.title = attributeData.Title;
            result.icon = attributeData.icon;
            return result;
        } else
            Debug.LogErrorFormat("PlayerParty GetBuff: AttributeData, GradeData or BuffGradesData not found for {0} and {1}", attribute, grade);
        return null;
    }  

    // public BuffData GetRandomBuff() {
    //     BuffData result = new BuffData();
    //     if (unitsData != null && attributes != null) {
    //         List<Attribute> predefinedAttributes = new List<Attribute>(attributes.data.Keys);
    //         Attribute attribute = predefinedAttributes[Random.Range(0, predefinedAttributes.Count)];
    //         result.attribute = attribute;

    //         int gradeProbabilitiesCount = GradeProbabilities.Count - 1;
    //         Grade grade = predefinedGrades.Count == 1 ? predefinedGrades[0] : Grade.Common;
    //         float random = Random.value;
    //         for (int p = gradeProbabilitiesCount; p >= 0; p--) {
    //             Grade possibleGrade = (Grade)p;
    //             if (predefinedGrades.Contains(possibleGrade) && random >= GradeProbabilities[p]) {
    //                 grade = possibleGrade;
    //                 break;
    //             }
    //         }
    //         result.grade = grade;

    //         if (attributes != null) {
    //             AttributeData attributeData = attributes.GetData(attribute);
    //             if (attributeData != null)
    //                 result.icon = attributeData.icon;
    //         }

    //         if (buffsData != null) {
    //             BuffGradesData buffGradesData = buffsData.Find(t => t.attribute == attribute);
    //             if (buffGradesData != null)
    //                 result.value = buffGradesData.GetValue(grade);
    //         }
    //     }
    //     return result;
    // }

    public void OnMMEvent(BuffSelectEvent e) {
        if (e.EventStage == EventStage.End && e.Data != null && e.Data.Count > 0) {
            e.Data.ForEach(t => _buffs[t.attribute] = _buffs.GetValueOrDefault(t.attribute) + t.value);
            ApplySupportMultipliers(null, true);
            PartyBoostEvent.Trigger(EventStage.End, PartyBoosterType.Buff, PartyBoosterSource.Battle, Vector2.zero);
        }
    }

    void OnEnable() {
        this.MMEventStartListening<LevelLoadEvent>();
        this.MMEventStartListening<PartyExpEvent>();
        this.MMEventStartListening<PartyBoostEvent>();
        this.MMEventStartListening<PartyUnitEvent>();
        this.MMEventStartListening<SkillLevelUpEvent>();
        this.MMEventStartListening<TrapEvent>();
        this.MMEventStartListening<LevelStageStateEvent>();
        this.MMEventStartListening<BuffSelectEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<LevelLoadEvent>();
        this.MMEventStopListening<PartyExpEvent>();
        this.MMEventStopListening<PartyBoostEvent>();
        this.MMEventStopListening<PartyUnitEvent>();
        this.MMEventStopListening<SkillLevelUpEvent>();
        this.MMEventStopListening<TrapEvent>();
        this.MMEventStopListening<LevelStageStateEvent>();
        this.MMEventStopListening<BuffSelectEvent>();
    }
}
