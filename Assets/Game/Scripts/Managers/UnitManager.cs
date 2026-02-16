using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;
using UnityEngine.Events;

public enum UnitActionType { Spawn, Hit }

public struct UnitActionEvent {
    public Unit Unit { get; private set; }
    public UnitActionType Type { get; private set; }

    static UnitActionEvent e;
    public static void Trigger(Unit unit, UnitActionType type) {
        e.Unit = unit;
        e.Type = type;
        MMEventManager.TriggerEvent(e);
    }
}

public class UnitManager : SerializedMonoBehaviour, MMEventListener<LevelLoadEvent>, MMEventListener<CellUiItemSelectEvent> {
    [SerializeField] AllUnitsData unitsData;
    [SerializeField] BuffsData buffsData;    
    [SerializeField] AttributesData attributes;
    [SerializeField] Dictionary<LevelGoalType, Unit> heroPrefabs = new();
    [SerializeField] Transform heroSpawnPoint;
    [SerializeField] Unit unitPrefab;
    [SerializeField] Dictionary<UnitClass, BoxCollider2D> spawnAreas = new();

    [Header("AI Profiles")]
    [SerializeField] List<UnitAIProfileEntry> aiProfileEntries = new();

    [Header("Events")]
    [SerializeField] UnityEvent OnBuff;
    [SerializeField] UnityEvent OnHeal;

    [Header("UI")]
    [SerializeField] Progressbar heroHealthBar;

    public bool Valid => _units.Count > 0;
    public Unit Hero { get; private set; }

    Dictionary<Attribute, float> _buffs = new();
    List<Unit> _units = new List<Unit>();
    Dictionary<BoxCollider2D, float> _spawnAreaXOffsets = new();

    Dictionary<UnitClass, UnitAIProfile> _aiProfiles = new();
    Dictionary<UnitClass, int> _slotCounters = new();
    Dictionary<int, int> _enemyAssignedCount = new();

    bool _showHealthBars = true;

    void FixedUpdate() {
        if (Hero == null || spawnAreas == null || _spawnAreaXOffsets == null) return;
        
        foreach (var pair in _spawnAreaXOffsets) {
            Vector3 pos = pair.Key.transform.position;
            pos.x = Hero.transform.position.x + pair.Value;
            pair.Key.transform.position = pos;
        }
    }

    // --- AI Profile API ---

    public UnitAIProfile GetAIProfile(UnitClass unitClass) {
        return _aiProfiles.TryGetValue(unitClass, out var p) ? p : null;
    }

    public int AssignSlotIndex(UnitClass unitClass) {
        int index = _slotCounters.GetValueOrDefault(unitClass, 0);
        _slotCounters[unitClass] = index + 1;
        return index;
    }

    // --- Target Distribution API ---

    public int GetAssignedCount(int instanceId) {
        return _enemyAssignedCount.GetValueOrDefault(instanceId, 0);
    }

    public void ReserveEnemy(int instanceId) {
        _enemyAssignedCount[instanceId] = _enemyAssignedCount.GetValueOrDefault(instanceId, 0) + 1;
    }

    public void ReleaseEnemy(int instanceId) {
        if (_enemyAssignedCount.TryGetValue(instanceId, out int count)) {
            if (count <= 1)
                _enemyAssignedCount.Remove(instanceId);
            else
                _enemyAssignedCount[instanceId] = count - 1;
        }
    }

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
        _slotCounters.Clear();
        _enemyAssignedCount.Clear();

        _aiProfiles.Clear();
        if (aiProfileEntries != null) {
            foreach (var entry in aiProfileEntries)
                if (entry != null && entry.profile != null)
                    _aiProfiles[entry.unitClass] = entry.profile;
        }

        if (selected != null && spawnAreas != null && spawnAreas.Count > 0)
            selected.ForEach(t => CreateUnit(t, 0));

        if (heroSpawnPoint != null)
            foreach (var pair in spawnAreas)
                if (pair.Value != null && !_spawnAreaXOffsets.ContainsKey(pair.Value))
                    _spawnAreaXOffsets[pair.Value] = pair.Value.transform.position.x - heroSpawnPoint.position.x;
    }

    void SpawnHero(LevelData levelData) {
        if (heroSpawnPoint == null || unitsData == null) return;

        List<UnitData> selectedHeroes = unitsData.Get(UnitType.Hero, UnitListType.Selected);
        if (selectedHeroes == null || selectedHeroes.Count == 0) return;

        UnitData heroData = selectedHeroes[0];
        if (heroData == null) return;

        LevelGoalType goalType = (levelData != null && levelData.goal != null && levelData.goal.goal != null)
            ? levelData.goal.GoalType
            : LevelGoalType.Survive;

        if (heroPrefabs == null || !heroPrefabs.TryGetValue(goalType, out Unit heroPrefab) || heroPrefab == null) return;

        Hero = CreateUnit(heroData, 0, heroSpawnPoint.position, heroPrefab, heroSpawnPoint);

        if (Hero != null && goalType == LevelGoalType.Distance) {
            GameObject targetObject = new GameObject("HeroTarget");
            
            Vector3 position = Hero.transform.position;
            position.x += levelData.goal.GoalAmount;
            targetObject.transform.position = position;
            targetObject.transform.SetParent(heroSpawnPoint, true);
            Hero.SetTarget(targetObject.transform);
            UnitActionEvent.Trigger(Hero, UnitActionType.Spawn);
        }
    }

    IEnumerator CreateUnitWithDelay(UnitData data, int mergeState, float delay) {
        yield return new WaitForSeconds(delay);
        CreateUnit(data, mergeState);
    }

    Unit CreateUnit(UnitData data, int mergeState, Vector3? position = null, Unit prefab = null, Transform parent = null) {
        Vector3 spawnPosition;
        Transform spawnParent = null;
        Unit unit = null;

        if (position.HasValue) {
            spawnPosition = position.Value;
            spawnParent = parent;
        } else {
            prefab = unitPrefab;
            var area = spawnAreas.GetValueOrDefault(data.unitClass, null);
            if (area == null) return null;
            spawnPosition = new Vector2(Random.Range(0, area.size.x), Random.Range(0, area.size.y)) - area.size * 0.5f + (Vector2)area.transform.position;
        }

        if (prefab != null) {
            unit = Instantiate(prefab, spawnPosition, Quaternion.identity, spawnParent);
            data.ResetSkillLevels();
            unit.Setup(data, mergeState);
            
            UnitAISetup aiSetup = unit.GetComponent<UnitAISetup>();
            if (aiSetup != null)
                aiSetup.Apply();

            unit.SetHealthbarVisibility(_showHealthBars);
            unit.OnDeath += OnUnitDeath;
            unit.OnHit += (u) => UnitActionEvent.Trigger(u, UnitActionType.Hit);
            _units.Add(unit);
            ApplySupportMultipliers(unit);

            if (data.type == UnitType.Hero) {
                unit.OnHit += OnHeroHit;
                unit.OnDeath += OnHeroDeath;
                UpdateHeroHealthBar(unit);
            } else if (Hero != null) {
                unit.SetTarget(Hero.transform);
                UnitAIController aiController = unit.GetComponent<UnitAIController>();
                if (aiController != null) {
                    BoxCollider2D area = spawnAreas.GetValueOrDefault(data.unitClass, null);
                    aiController.Initialize(Hero, area, this);
                }
            }
        }
        return unit;
    }

    void UpdateHeroHealthBar(Unit unit = null) {
        Unit hero = unit == null ? Hero : unit;
        if (hero != null && heroHealthBar != null) {
            heroHealthBar.SetTotal(hero.MaxHealth);
            heroHealthBar.SetValue(hero.CurrentHealth);
        }
    }

    void ApplySupportMultipliers(Unit unit = null, bool playFeedback = false) {
        System.Array array = System.Enum.GetValues(typeof(Attribute));
        foreach (Attribute attribute in array) {
            if (unit != null)
                ApplySupportMultipliers(new List<Unit> { unit }, attribute, playFeedback);
            else {
                ApplySupportMultipliers(_units, attribute, playFeedback);
                UpdateHeroHealthBar();
            }
        }
    }

    void OnHeroHit(Unit unit) {
        UpdateHeroHealthBar(unit);
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
            case CellItemType.HeroSkill:
                HandleHeroSkillSelection(e.Item.Id, e.Count);
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
            CreateUnit(unitData, maxMergeLevel - 1);

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

    void HandleHeroSkillSelection(string skillId, int count) {
        if (Hero == null) return;

        Skill skill = Hero.Skills.Find(s => s.Data != null && s.Data.name == skillId);
        if (skill == null) return;

        int maxLevelIndex = skill.LevelsCount - 1;
        int skillLevel = Mathf.Min(count - 1, maxLevelIndex);

        skill.SetLevel(skillLevel);

        AttackSkill attackSkill = skill as AttackSkill;
        if (attackSkill != null) {
            attackSkill.StartAttack();
            attackSkill.StopAttack();
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
