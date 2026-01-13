using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

public class PartyManager : SerializedMonoBehaviour, MMEventListener<LevelLoadEvent>, MMEventListener<CellUiItemSelectEvent> {
    [SerializeField] AllUnitsData unitsData;
    [SerializeField] BuffsData buffsData;    
    [SerializeField] AttributesData attributes;
    [SerializeField] Unit unitPrefab;
    [SerializeField] UnitMergeState maxUnitLevel = UnitMergeState.Fifth;
    [SerializeField] BoxCollider2D spawnArea;

    public bool Valid => _units.Count > 0;

    Dictionary<Attribute, float> _buffs = new();
    List<Unit> _units = new List<Unit>();
    Vector2 _spawnSize;
    Transform _spawnRoot;

    bool _showHealthBars = true;

    public void SetHealthbarVisibility(bool value) {
        if (_showHealthBars != value) {
            _showHealthBars = value;
            _units.ForEach(t => t.SetHealthbarVisibility(value));
        }
    }

    public void Clear() {
        _units.ForEach(t => Destroy(t.gameObject));
        _units.Clear();
    }

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start && e.Data != null) {
            Setup();
        }
    }

    public void Setup(List<UnitData> selected = null) {
        _units.Clear();

        if (spawnArea != null) {
            _spawnSize = spawnArea.size;
            _spawnRoot = spawnArea.transform;
        }

        if (selected != null)
            selected.ForEach(t => CreateUnit(t, UnitMergeState.First));
    }

    IEnumerator CreateUnitWithDelay(UnitData data, UnitMergeState mergeState, float delay) {
        yield return new WaitForSeconds(delay);
        CreateUnit(data, mergeState);
    }

    void CreateUnit(UnitData data, UnitMergeState mergeState) {
        Vector2 half = data.IsMelee ? new Vector2(0, _spawnSize.y * 0.5f) : new Vector2(_spawnSize.y * 0.5f, _spawnSize.y);
        Vector2 position = new Vector2(Random.Range(0, _spawnSize.x), Random.Range(half.x, half.y)) - _spawnSize * 0.5f + (Vector2)_spawnRoot.position;
        Unit unit = Instantiate(unitPrefab, position, Quaternion.identity, _spawnRoot);
        data.ResetSkillLevels();
        unit.Setup(data, mergeState);
        unit.SetHealthbarVisibility(_showHealthBars);
        unit.OnDeath += OnUnitDeath;
        _units.Add(unit);
        ApplySupportMultipliers(unit);
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

    void OnUnitDeath(Unit unit) {
        _units.Remove(unit);
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

    /*public void OnMMEvent(BuffSelectEvent e) {
        if (e.EventStage == EventStage.End && e.Data != null && e.Data.Count > 0) {
            e.Data.ForEach(t => _buffs[t.attribute] = _buffs.GetValueOrDefault(t.attribute) + t.value);
            ApplySupportMultipliers(null, true);
        }
    }*/

    public void OnMMEvent(CellUiItemSelectEvent e) {
        if (e.Item == null || e.Count <= 0) return;

        switch (e.Item.Type) {
            case CellItemType.Unit:
                HandleUnitSelection(e.Item.Id, e.Count);
                break;
            case CellItemType.Buff:
                HandleBuffSelection(e.Item.Id, e.Count);
                break;
        }
    }

    void HandleUnitSelection(string unitId, int count) {
        if (unitsData == null) return;
        UnitData unitData = unitsData.GetByName(unitId);
        if (unitData == null) return;

        int maxMergeValue = (int)maxUnitLevel + 1;

        int fullUnits = count / maxMergeValue;
        int remainder = count % maxMergeValue;

        for (int i = 0; i < fullUnits; i++)
            CreateUnit(unitData, maxUnitLevel);

        if (remainder > 0) {
            UnitMergeState mergeState = (UnitMergeState)(remainder - 1);
            CreateUnit(unitData, mergeState);
        }
    }

    void HandleBuffSelection(string buffId, int count) {
        if (!System.Enum.TryParse(buffId, out Attribute attribute)) return;
        if (buffsData == null) return;

        BuffGradesData buffData = buffsData.Get(attribute);
        if (buffData == null) return;

        int gradeIndex = Mathf.Min(count - 1, (int)Grade.Divine);
        Grade grade = (Grade)gradeIndex;

        float value = buffData.GetValue(grade);
        if (value != 0) {
            _buffs[attribute] = _buffs.GetValueOrDefault(attribute) + value;
            ApplySupportMultipliers(null, true);
        }
    }    

    void OnEnable() {
        this.MMEventStartListening<LevelLoadEvent>();
        this.MMEventStartListening<CellUiItemSelectEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<LevelLoadEvent>();
        this.MMEventStopListening<CellUiItemSelectEvent>();

    }
}
