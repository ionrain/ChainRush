using UnityEngine;

[CreateAssetMenu(fileName = "New UnitAIProfile", menuName = "Game/Unit AI Profile")]
public class UnitAIProfile : ScriptableObject {

    [Header("Formation")]
    public FormationProfile formation;

    [Header("Overrides")]
    public bool overrideDistances;
    public bool overrideDetection;
    public bool overrideTiming;
    public bool overrideScoring;
    public bool overrideSpeed;

    [Header("Distances")]
    public float returnRadius = 7f;
    public float followRadius = 5f;
    public float maxChaseDistanceFromAnchor = 4f;
    public float allyDefendRadius = 8f;

    [Header("Detection")]
    public float enemyDetectRadius = 6f;
    public float heroThreatRadius = 0f;
    public float maxUnitAcquireDistance = 0f;

    [Header("Timing")]
    public float targetEvaluationInterval = 0.3f;
    public float targetLockTime = 0.8f;
    public float retargetCooldown = 0.35f;
    public float enemyCommitTime = 0.5f;
    public float leashRecoveryTime = 1.5f;
    public float combatLingerTime = 0.8f;

    [Header("Scoring")]
    public float wHero = 3f;
    public float wUnit = 1f;
    public float wCrowd = 2f;
    public float wLeash = 1.5f;
    public float combatRadius = 5f;

    [Header("Speed")]
    public float heroSpeedMultiplier = 2f;
}

[System.Serializable]
public class UnitAIProfileEntry {
    public UnitSpeciality speciality;
    public UnitAIProfile profile;
}
