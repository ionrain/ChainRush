using System.Collections;
using System.Collections.Generic;
using Sirenix.Utilities;
using UnityEngine;

public enum UnitListType { All, Unlocked, Available, Selected }
[System.Flags]
public enum UnitType { None = 0, Normal = 1, Hero = 2 }

[CreateAssetMenu(fileName = "New AllUnitsData", menuName = "Game/AllUnitsData", order = 18)]
public class AllUnitsData : GameSettings {
    public AllItemsData itemsData;
    public Dictionary<UnitType, int> capacity = new();
    public int energyPrice = 5;
    public UnitType upgradable = UnitType.Hero;
    public List<UnitData> units = new();

    public bool SomeoneHasUltimate(UnitType unitType) {
        var filteredUnits = units.FindAll(t => unitType.HasFlag(t.type));
        if (filteredUnits != null) {
            foreach (UnitData unit in filteredUnits) {
                if (unit.Unlocked) {
                    List<UnitSkill> skills = unit.GetSkills(SkillListType.Aquired);
                    return skills.Find(t => t.data.skillType == SkillType.Ultimate) != null;
                }
            }
        }
        return false;
    }

    public List<ElementalAttribute> GetActiveAttributes(UnitType unitType) {
        var filteredUnits = units.FindAll(t => unitType.HasFlag(t.type));
        List<ElementalAttribute> result = new List<ElementalAttribute>();
        if (filteredUnits != null) {
            foreach (UnitData unit in filteredUnits) {
                if (unit.State == UnitState.Selected) {
                    Dictionary<ElementalAttribute, float> attributeValue = unit.GetAllAttributes(false);
                    attributeValue.ForEach(t => {
                        if (!result.Contains(t.Key))
                            result.Add(t.Key);
                    });
                }
            }
        }
        return result;
    }

    public UnitData GetByName(string name) {
        return units.Find(t => t.name.Equals(name));
    }

    public int GetMaxLevel(UnitType unitType) {
        int result = 0;
        List<UnitData> list = Get(unitType, UnitListType.Unlocked);
        foreach (UnitData data in list) 
            if (data.Level > result)
                result = data.Level;
        return result;
    }

    public List<UnitData> Get(UnitType unitType, UnitListType listType) {
        var filteredUnits = units.FindAll(t => unitType.HasFlag(t.type));
        if (filteredUnits == null && filteredUnits.Count == 0)
            return new List<UnitData>();

        if (listType == UnitListType.Unlocked)
            return filteredUnits.FindAll(t => t != null && t.Unlocked);

        if (listType == UnitListType.Available)
            return filteredUnits.FindAll(t => t != null && t.State == UnitState.Available);

        if (listType == UnitListType.Selected)
            return filteredUnits.FindAll(t => t != null && t.State == UnitState.Selected);

        return filteredUnits.FindAll(t => t != null);
    }

    public List<SkillData> GetPartySkills(UnitType unitType, SkillListType listType) {
        List<UnitSkill> temp = new List<UnitSkill>();
        List<SkillData> result = new List<SkillData>();
        List<UnitData> selected = Get(unitType, UnitListType.Selected);
        foreach (UnitData data in selected)
            temp.AddRange(data.GetSkills(listType));
        temp.ForEach(t => result.Add(t.data));
        return result;
    }

    public bool TrySetState(UnitData data, UnitState state) {
        if (data.State == state)
            return false;

        if ((state == UnitState.Selected && GetCount(data.type, state) >= capacity.GetValueOrDefault(data.type))
            || (state == UnitState.Available && GetCount(data.type, state) <= 1))
            return false;

        data.SetState(state);
        return true;
    }

    public int GetCount(UnitType unitType, UnitState state) => units.FindAll(t => t.type == unitType && t.State == state).Count;

    public override void Reset() {
        units.ForEach(t => t.Reset());
    }

    public override void Save(GameData data) {
        units.ForEach(t => data.units.Add(t.GetStateData()));
    }

    public override void Load(GameData data) {
        if (itemsData != null) {
            foreach (UnitStateData stateData in data.units) {
                UnitData unit = units.Find(p => p.name.Equals(stateData.unit));
                if (unit != null)
                    unit.SetStateData(stateData, itemsData);
            }
        }
    }
}