using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ChainRush.Gameplay
{
    /// <summary>Immutable input of one run. Contains no mutable source definitions or managers.</summary>
    public sealed class ChainRushRunSnapshot
    {
        public const string RuntimeKey = "chainrush.run.input";
        public string LevelId { get; }
        public LevelGoalType GoalType { get; }
        public int GoalAmount { get; }
        public int BoardWidth { get; }
        public int BoardHeight { get; }
        public int Seed { get; }
        public ChainRushCharacterSnapshot Hero { get; }
        public IReadOnlyList<ChainRushCharacterSnapshot> Units { get; }

        public ChainRushRunSnapshot(string levelId, LevelGoalType goalType, int goalAmount,
            int boardWidth, int boardHeight, int seed, ChainRushCharacterSnapshot hero,
            IEnumerable<ChainRushCharacterSnapshot> units)
        {
            if (string.IsNullOrWhiteSpace(levelId)) throw new ArgumentException("A run requires a level identity.", nameof(levelId));
            if (goalType != LevelGoalType.Survive && goalType != LevelGoalType.Distance)
                throw new ArgumentOutOfRangeException(nameof(goalType));
            if (goalAmount <= 0 || boardWidth <= 0 || boardHeight <= 0)
                throw new ArgumentException("Goal and board dimensions must be positive.");
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            if (hero.Type != UnitType.Hero) throw new ArgumentException("The hero slot requires a hero.", nameof(hero));
            var roster = new List<ChainRushCharacterSnapshot>(units ?? throw new ArgumentNullException(nameof(units)));
            var identities = new HashSet<string>(StringComparer.Ordinal) { hero.ContentId };
            foreach (ChainRushCharacterSnapshot unit in roster)
                if (unit == null || unit.Type != UnitType.Normal || !identities.Add(unit.ContentId))
                    throw new ArgumentException("The unit roster requires unique normal units.", nameof(units));
            LevelId = levelId;
            GoalType = goalType;
            GoalAmount = goalAmount;
            BoardWidth = boardWidth;
            BoardHeight = boardHeight;
            Seed = seed;
            Units = new ReadOnlyCollection<ChainRushCharacterSnapshot>(roster);
        }
    }
}
