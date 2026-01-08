using System.Collections;
using System.Collections.Generic;
using Sirenix.Utilities;
using UnityEngine;

public enum UnitListType { All, Unlocked, Available, Selected }

[CreateAssetMenu(fileName = "New AllUnitsData", menuName = "Game/AllUnitsData", order = 18)]
public class AllUnitsData : GameSettings {
    public AllItemsData itemsData;
    public int partySize = 3;
    public int energyPrice = 5;
    public List<UnitData> units = new List<UnitData>();

    public bool SomeoneHasUltimate() {
        foreach (UnitData unit in units) {
            if (unit.Unlocked) {
                List<UnitSkill> skills = unit.GetSkills(SkillListType.Aquired);
                return skills.Find(t => t.data.skillType == SkillType.Ultimate) != null;
            }
        }
        return false;
    }

    public List<ElementalAttribute> GetActiveAttributes() {
        List<ElementalAttribute> result = new List<ElementalAttribute>();
        foreach (UnitData unit in units) {
            if (unit.State == UnitState.Selected) {
                Dictionary<ElementalAttribute, float> attributeValue = unit.GetAllAttributes(false);
                attributeValue.ForEach(t => {
                    if (!result.Contains(t.Key))
                        result.Add(t.Key);
                });
            }
        }
        return result;
    }

    public UnitData GetByName(string name) {
        return units.Find(t => t.name.Equals(name));
    }

    public int GetMaxLevel() {
        int result = 0;
        List<UnitData> list = Get(UnitListType.Unlocked);
        foreach (UnitData data in list) 
            if (data.Level > result)
                result = data.Level;
        return result;
    }

    public List<UnitData> Get(UnitListType listType) {
        if (listType == UnitListType.Unlocked)
            return units.FindAll(t => t != null && t.Unlocked);

        if (listType == UnitListType.Available)
            return units.FindAll(t => t != null && t.State == UnitState.Available);

        if (listType == UnitListType.Selected)
            return units.FindAll(t => t != null && t.State == UnitState.Selected);

        return units.FindAll(t => t != null);
    }

    public List<SkillData> GetPartySkills(SkillListType listType) {
        List<UnitSkill> temp = new List<UnitSkill>();
        List<SkillData> result = new List<SkillData>();
        List<UnitData> selected = Get(UnitListType.Selected);
        foreach (UnitData data in selected)
            temp.AddRange(data.GetSkills(listType));
        temp.ForEach(t => result.Add(t.data));
        return result;
    }

    public bool TrySetState(UnitData data, UnitState state) {
        if (data.State == state)
            return false;

        if (state == UnitState.Selected) {
            List<UnitData> searchResult = units.FindAll(t => t.State == state);
            if (searchResult.Count >= partySize)
                return false;
        }

        data.SetState(state);
        return true;
    }

    public override void Reset() {
        units.ForEach(t => t.Reset());
    }

    public override void Save(GameData data) {
        units.ForEach(t => data.units.Add(t.GetStateData()));
    }

    public override void Load(GameData data) {
        if (itemsData != null) {
            foreach (UnitStateData stateData in data.units) {
                UnitData unit = units.Find(t => t.name.Equals(stateData.unit));
                if (unit != null)
                    unit.SetStateData(stateData, itemsData);
            }
        }
    }
}