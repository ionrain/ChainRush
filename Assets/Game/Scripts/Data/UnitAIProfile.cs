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

    [Header("Crowd-Aware Positioning")]
    public bool overrideCrowdAware;
    public int pointSamples = 12;
    public int pathChecks = 3;
    public float crowdRadiusMultiplier = 1.25f;
    public float wCrowdAtPoint = 1.0f;
    public float wCrowdAlongPath = 0.75f;
    public float wTravelCost = 0.15f;
    public float lockTime = 0.9f;
    public float rerollCooldown = 0.6f;
    public int slotSamples = 12;
    public float enemyMovedThreshold = 1.5f;
    public float enterEnemyMargin = 0.6f;
    public float exitEnemyMargin = 1.4f;
    public float meleeRangeFallback = 1.5f;
    public float rangedRangeFallback = 4.0f;
    public float idleRoamRadius = 2.0f;
    public float idleOffsetRefreshDistance = 4.0f;
}

[System.Serializable]
public class UnitAIProfileEntry {
    public UnitClass unitClass;
    public UnitAIProfile profile;
}
