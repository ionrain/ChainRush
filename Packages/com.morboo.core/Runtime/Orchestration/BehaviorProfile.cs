using UnityEngine;

[CreateAssetMenu(fileName = "New BehaviorProfile", menuName = "Game/Orchestration/Behavior Profile")]
public class BehaviorProfile : ScriptableObject
{
    [Header("Identity")]
    public string ProfileId;
    public string RoleTag;

    [Header("Personality")]
    [Range(0f, 1f)] public float RiskTolerance = 0.5f;
    [Range(0f, 1f)] public float Cooperation = 0.5f;

    [Header("Domain Traits")]
    public ParamSet Traits;
}
