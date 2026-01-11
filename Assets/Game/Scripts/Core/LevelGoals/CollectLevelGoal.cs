using UnityEngine;

public class CollectLevelGoal : LevelGoal {
    [SerializeField] ResourceType resource;
    
    public CollectLevelGoal() {
        goalType = LevelGoalType.CollectResource;
    }

    public ResourceType Resource => resource;

    public override bool Suitable(LevelGoalType targetGoalType, string id) {
        if (Achieved || goalType != targetGoalType)
            return false;
        ResourceType targetResource;
        bool success = System.Enum.TryParse<ResourceType>(id , out targetResource);
        if (!success || resource != targetResource)
            return false;
        return true;
    }
}
