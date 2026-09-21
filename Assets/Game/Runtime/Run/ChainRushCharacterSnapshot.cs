using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ChainRush.Gameplay
{
    public sealed class ChainRushCharacterSnapshot
    {
        public string ContentId { get; }
        public UnitType Type { get; }
        public int Level { get; }
        public Element Element { get; }
        public IReadOnlyDictionary<ElementalAttribute, float> Attributes { get; }
        public IReadOnlyList<string> AcquiredSkills { get; }

        public ChainRushCharacterSnapshot(string contentId, UnitType type, int level, Element element,
            IDictionary<ElementalAttribute, float> attributes, IEnumerable<string> acquiredSkills)
        {
            if (string.IsNullOrWhiteSpace(contentId)) throw new ArgumentException("A character requires a content identity.", nameof(contentId));
            if (type != UnitType.Normal && type != UnitType.Hero) throw new ArgumentOutOfRangeException(nameof(type));
            if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
            var values = new Dictionary<ElementalAttribute, float>(attributes ?? throw new ArgumentNullException(nameof(attributes)));
            foreach (float value in values.Values)
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Character attributes must be finite.", nameof(attributes));
            var skills = new List<string>(acquiredSkills ?? throw new ArgumentNullException(nameof(acquiredSkills)));
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (string skill in skills)
                if (string.IsNullOrWhiteSpace(skill) || !identities.Add(skill))
                    throw new ArgumentException("Acquired skills require unique identities.", nameof(acquiredSkills));
            ContentId = contentId;
            Type = type;
            Level = level;
            Element = element;
            Attributes = new ReadOnlyDictionary<ElementalAttribute, float>(values);
            AcquiredSkills = new ReadOnlyCollection<string>(skills);
        }
    }
}
