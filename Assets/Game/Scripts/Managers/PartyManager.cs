using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;
using UnityEngine.Events;

public class PartyManager : SerializedMonoBehaviour, MMEventListener<LevelLoadEvent>, MMEventListener<CellUiItemSelectEvent> {
    [SerializeField] AllUnitsData unitsData;
    [SerializeField] BuffsData buffsData;    
    [SerializeField] AttributesData attributes;
    [SerializeField] Dictionary<LevelGoalType, Unit> heroPrefabs = new();
    [SerializeField] Transform heroSpawnPoint;
    [SerializeField] Unit unitPrefab;
    [SerializeField] Dictionary<UnitSpeciality, BoxCollider2D> spawnAreas = new();

    [Header("Events")]
    [SerializeField] UnityEvent OnBuff;
    [SerializeField] UnityEvent OnHeal;

    [Header("UI")]
    [SerializeField] Progressbar heroHealthBar;

    public bool Valid => _units.Count > 0;

    Dictionary<Attribute, float> _buffs = new();
    List<Unit> _units = new List<Unit>();

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
            SpawnHero(e.Data);
        }
    }

    public void Setup(List<UnitData> selected = null) {
        _units.Clear();
        if (selected != null && spawnAreas != null && spawnAreas.Count > 0)
            selected.ForEach(t => CreateUnit(t, 0));
    }

    void SpawnHero(LevelData levelData) {
        if (heroSpawnPoint == null || unitsData == null) return;

        List<UnitData> selectedHeroes = unitsData.Get(UnitType.Hero, UnitListType.Selected);
        if (selectedHeroes == null || selectedHeroes.Count == 0) return;

        UnitData heroData = selectedHeroes[0];
        if (heroData == null) return;

        LevelGoalType goalType = (levelData != null && levelData.goal != null && levelData.goal.goal != null)
            ? levelData.goal.goal.Type
            : LevelGoalType.Survive;

        if (heroPrefabs == null || !heroPrefabs.TryGetValue(goalType, out Unit heroPrefab) || heroPrefab == null) return;

        CreateUnit(heroData, 0, heroSpawnPoint.position, heroPrefab, heroSpawnPoint);
    }

    IEnumerator CreateUnitWithDelay(UnitData data, int mergeState, float delay) {
        yield return new WaitForSeconds(delay);
        CreateUnit(data, mergeState);
    }

    void CreateUnit(UnitData data, int mergeState, Vector3? position = null, Unit prefab = null, Transform parent = null) {
        Vector3 spawnPosition;
        Transform spawnParent;

        if (position.HasValue) {
            spawnPosition = position.Value;
            spawnParent = parent;
        } else {
            var area = spawnAreas.GetValueOrDefault(data.speciality, null);
            if (area == null) return;
            spawnPosition = new Vector2(Random.Range(0, area.size.x), Random.Range(0, area.size.y)) - area.size * 0.5f + (Vector2)area.transform.position;
            spawnParent = area.transform;
        }

        Unit unitToSpawn = prefab != null ? prefab : unitPrefab;
        Unit unit = Instantiate(unitToSpawn, spawnPosition, Quaternion.identity, spawnParent);
        data.ResetSkillLevels();
        unit.Setup(data, mergeState);
        unit.SetHealthbarVisibility(_showHealthBars);
        unit.OnDeath += OnUnitDeath;
        _units.Add(unit);
        ApplySupportMultipliers(unit);

        if (data.type == UnitType.Hero) {
            unit.OnHit += OnHeroHit;
            unit.OnDeath += OnHeroDeath;
            if (heroHealthBar != null) {
                heroHealthBar.Setup();
                heroHealthBar.SetTotal(unit.MaxHealth);
                heroHealthBar.SetValue(unit.MaxHealth);
            }
        }
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

    void OnHeroHit(Unit unit) {
        if (heroHealthBar != null)
            heroHealthBar.SetValue(unit.CurrentHealth);
    }

    void OnUnitDeath(Unit unit) {
        _units.Remove(unit);
    }

    void OnHeroDeath(Unit unit) {
        LevelActionEvent.Trigger(EventStage.Start, LevelActionType.Fail);
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

    public void OnMMEvent(CellUiItemSelectEvent e) {
        if (e.Stage != EventStage.End || e.Item == null || e.Count <= 0) return;

        switch (e.Item.Type) {
            case CellItemType.Unit:
                HandleUnitSelection(e.Item.Id, e.Count);
                break;
            case CellItemType.Buff:
                HandleBuffSelection(e.Item.Id, e.Count);
                break;
            case CellItemType.Booster:
                HandleBoosterSelection(e.Item.Id, e.Count);
                break;
            case CellItemType.SoftCurrency:
                break;
        }
    }

    void HandleUnitSelection(string unitId, int count) {
        if (unitsData == null) return;
        UnitData unitData = unitsData.GetByName(unitId);
        if (unitData == null) return;
        int maxMergeLevel = unitData.MergeStatesCount;

        int fullUnits = count / maxMergeLevel;
        int remainder = count % maxMergeLevel;

        for (int i = 0; i < fullUnits; i++)
            CreateUnit(unitData, maxMergeLevel);

        if (remainder > 0)
            CreateUnit(unitData, remainder - 1);
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
            OnBuff?.Invoke();
        }
    }

    void HandleBoosterSelection(string boosterId, int count) {
        if (!System.Enum.TryParse(boosterId, out BoosterType boosterType)) return;
        if (boosterType == BoosterType.Heal) {
            _units.ForEach(t => t.Heal(count * 0.1f));
            OnHeal?.Invoke();
        }
        else if (boosterType == BoosterType.Bomb) {
            
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
