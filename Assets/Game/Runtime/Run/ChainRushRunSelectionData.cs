using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChainRush.Gameplay
{
    /// <summary>Explicit integration inputs; capture is the boundary to mutable source content.</summary>
    [CreateAssetMenu(fileName = "ChainRushRunSelection", menuName = "ChainRush/Gameplay/Run Selection")]
    public sealed class ChainRushRunSelectionData : ScriptableObject
    {
        [SerializeField] LevelData level;
        [SerializeField] ChainRushCharacterSelectionData hero = new ChainRushCharacterSelectionData();
        [SerializeField] List<ChainRushCharacterSelectionData> units = new List<ChainRushCharacterSelectionData>();

        public ChainRushRunSnapshot Capture(int seed)
        {
            if (level == null || level.Goal?.goal == null || level.difficulty == null || level.enemyData == null)
                throw new InvalidOperationException("Run selection requires a fully authored level.");
            var roster = new List<ChainRushCharacterSnapshot>();
            foreach (ChainRushCharacterSelectionData unit in units)
                roster.Add((unit ?? throw new InvalidOperationException("Missing roster entry.")).Capture());
            return new ChainRushRunSnapshot(level.Id, level.Goal.GoalType, level.Goal.GoalAmount,
                level.boardSize.x, level.boardSize.y, seed, hero.Capture(), roster);
        }
    }

    [Serializable]
    public sealed class ChainRushCharacterSelectionData
    {
        [SerializeField] UnitData definition;
        [SerializeField, Min(0)] int level;
        [SerializeField] List<ItemData> equipment = new List<ItemData>();
        [SerializeField] List<SkillData> acquiredSkills = new List<SkillData>();

        public ChainRushCharacterSnapshot Capture()
        {
            if (definition == null) throw new InvalidOperationException("Missing character definition.");
            var attributes = new Dictionary<ElementalAttribute, float>();
            foreach (global::Attribute attribute in Enum.GetValues(typeof(global::Attribute)))
            {
                bool elemental = attribute == global::Attribute.Power || attribute == global::Attribute.Defense;
                foreach (Element element in Enum.GetValues(typeof(Element)))
                {
                    if (elemental ? element == Element.Any : element != Element.Any) continue;
                    float amount = 0;
                    if ((element == Element.Any || element == definition.element)
                        && definition.attributes.TryGetValue(attribute, out AttributeUpgradeData upgrade) && upgrade != null)
                        amount = upgrade.GetValue(level);
                    foreach (ItemData item in equipment)
                    {
                        if (item == null) throw new InvalidOperationException("Missing equipped item.");
                        foreach (ItemAttribute modifier in item.attributes)
                        {
                            if (modifier.attribute != attribute || (element != Element.Any && modifier.element != element)) continue;
                            if (modifier.action == MathAction.Add) amount += modifier.value;
                            else if (modifier.action == MathAction.Substract) amount -= modifier.value;
                        }
                    }
                    attributes.Add(new ElementalAttribute(element, attribute), amount);
                }
            }
            var skills = new List<string>();
            foreach (SkillData skill in acquiredSkills)
            {
                if (skill == null || !definition.skills.Exists(entry => entry?.data == skill))
                    throw new InvalidOperationException("Acquired skill must belong to the selected character.");
                skills.Add(skill.name);
            }
            return new ChainRushCharacterSnapshot(definition.name, definition.type, level, definition.element, attributes, skills);
        }
    }
}
