using UnityEngine;

public class EnemyTypeLevelGoal : AmountLevelGoal {
    [SerializeField] EnemyType enemyType;

    public EnemyType EnemyType => enemyType;

    public EnemyTypeLevelGoal() {
        goalType = LevelGoalType.EnemyType;
    }

    public override bool Suitable(LevelGoalType targetGoalType, string id) {
        return !Achieved && goalType == targetGoalType && enemyType.ToString().Equals(id);
    }
}
