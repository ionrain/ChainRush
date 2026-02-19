using UnityEngine;

public class EnemyLevelGoal : AmountLevelGoal {
    [SerializeField] EnemyData enemy;
    

    public EnemyData Enemy => enemy;

    public EnemyLevelGoal() {
        goalType = LevelGoalType.Enemy;
    }

    public override bool Suitable(LevelGoalType targetGoalType, string id) {
        return !Achieved && goalType == targetGoalType && enemy != null && enemy.name.Equals(id);
    }
}
