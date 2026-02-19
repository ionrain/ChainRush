using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class UnitAIController : MonoBehaviour, MMEventListener<UnitActionEvent>, MMEventListener<LevelResultEvent> {

    [Header("Distance Thresholds")]
    [SerializeField] float returnRadius = 7f;                // (A) hysteresis: triggers forced return to hero — HARD cap, no exceptions
    [SerializeField] float followRadius = 5f;                // (A) hysteresis: clears forced return, resumes normal logic
    [SerializeField] float maxChaseDistanceFromAnchor = 4f;  // (B) soft leash: max distance from anchor while chasing enemy
    [SerializeField] float enemyDetectRadius = 6f;
    [SerializeField] float heroThreatRadius = 0f;            // hero-centered detection radius (0 = use enemyDetectRadius)
    [SerializeField] float allyDefendRadius = 8f;

    [Header("Speed")]
    [SerializeField] float heroSpeedMultiplier = 2f;

    [Header("Enemy Detection")]
    [SerializeField] LayerMask enemyLayer;
    [SerializeField] float targetEvaluationInterval = 0.3f;
    [SerializeField] int overlapMaximum = 10;
    [SerializeField] float maxUnitAcquireDistance = 0f;      // hard gate: skip enemies farther than this from unit (0 = use effective radius)

    [Header("Ally Separation (anti-clumping)")]
    [SerializeField] LayerMask allyLayer;
    [SerializeField] float personalSpaceRadius = 0.9f;            // personal-space circle radius around each unit
    [SerializeField] float separationStrength = 1.2f;             // how strongly we offset the target away from nearby allies
    [SerializeField] float maxSeparationOffset = 0.8f;            // clamp for offset (keeps unit near formation/rally)
    [SerializeField] int separationOverlapMax = 12;               // max allies considered for separation
    [SerializeField] bool separationOnlyForFormationOrRally = true;
    [SerializeField] bool separationUseIntervalUpdate = true;
    [SerializeField] float separationUpdateInterval = 0.15f;

    [Header("Crowd-Aware Destination (Shared)")]
    [SerializeField] int pointSamples = 12;
    [SerializeField] int pathChecks = 3;
    [SerializeField] float crowdRadiusMultiplier = 1.25f;
    [SerializeField] float wCrowdAtPoint = 1.0f;
    [SerializeField] float wCrowdAlongPath = 0.75f;
    [SerializeField] float wTravelCost = 0.15f;

    [Header("Lock + Stuck (Shared)")]
    [SerializeField] float lockTime = 0.9f;
    [SerializeField] float rerollCooldown = 0.6f;
    [SerializeField] float stuckCheckInterval = 0.25f;
    [SerializeField] float stuckTimeToReroll = 0.9f;
    [SerializeField] float stuckMinDistanceProgress = 0.05f;
    [SerializeField] float stuckMinMoveDelta = 0.03f;

    [Header("Combat Slot Phase (Two-Phase Target Swap)")]
    [SerializeField] int slotSamples = 12;
    [SerializeField] float enemyMovedThreshold = 1.5f;
    [SerializeField] float enterEnemyMargin = 0.6f;
    [SerializeField] float exitEnemyMargin = 1.4f;
    [SerializeField] float meleeRangeFallback = 1.5f;
    [SerializeField] float rangedRangeFallback = 4.0f;

    [Header("Idle Roam (Personal Anchor)")]
    [SerializeField] float idleRoamRadius = 2.0f;
    [SerializeField] float idleOffsetRefreshDistance = 4.0f;

    [Header("Target Scoring")]
    [SerializeField] float wHero = 3f;
    [SerializeField] float wUnit = 1f;
    [SerializeField] float wCrowd = 2f;
    [SerializeField] float wLeash = 1.5f;
    [SerializeField] float combatRadius = 5f;

    [Header("Hit Response")]
    [SerializeField] float heroHitResponseDuration = 2f;
    [SerializeField] float allyHitResponseDuration = 1.5f;

    [Header("Timing")]
    [SerializeField] float targetLockTime = 0.8f;            // (C) min time to keep an Enemy target before allowing switch to different enemy
    [SerializeField] float retargetCooldown = 0.35f;         // (C) min interval between actual _unit.SetTarget calls for same target+reason
    [SerializeField] float enemyCommitTime = 0.5f;           // (D) grace period: allows finishing attack before leash yank (only while within returnRadius)
    [SerializeField] float leashRecoveryTime = 1.5f;         // after leash abort, suppress enemy targeting for this duration
    [SerializeField] float combatLingerTime = 0.8f;          // after losing last enemy, hold position before retreating to rally
    [SerializeField] float idleRoamDelayAfterThreat = 1.2f;  // seconds after last threat before switching from formation to idle roam

    Unit _unit;
    Unit _heroUnit;
    Transform _heroTransform;
    AIBrain _brain;
    CharacterMovement _movement;
    float _baseWalkSpeed;
    bool _speedBoosted;
    bool _active;
    bool _heroAlive;

    // Target evaluation
    float _lastEvaluationTime;
    Transform _currentTarget;
    TargetReason _targetReason;
    Health _currentTargetHealth;

    // Hit tracking
    float _heroLastHitTime = -100f;
    Transform _lastHitAllyTransform;
    float _allyLastHitTime = -100f;

    // Formation
    UnitManager _unitManager;
    FormationProfile _formationProfile;
    int _slotIndex;
    Transform _formationPoint;
    UnitAIProfile _aiProfile;

    // Target distribution
    int _reservedEnemyInstanceId = -1;

    // Catch-up
    bool _isCatchingUp;

    // Threat tracking (for hybrid idle roam / formation)
    float _lastThreatTime = -999f;

    // Force re-evaluation flag (set when reserved enemy becomes invalid)
    bool _forceReevaluate;

    // Rally waypoint (parented to spawn area, moves with hero)
    Transform _rallyWaypoint;
    BoxCollider2D _spawnArea;
    float _rallyArrivalThreshold = 1.5f;
    [SerializeField] Vector2 rallyRestInterval = new Vector2(0.5f, 2f);
    bool _rallyResting;
    float _rallyRestUntil;

    // Enemy search
    Collider2D[] _overlapResults;
    ContactFilter2D _contactFilter;

    // Ally separation
    Collider2D[] _allyOverlap;
    Transform _steeringTarget;
    float _nextSeparationUpdateAt;
    Vector2 _cachedSeparationOffset;

    // Units currently fighting enemies (for ally assist)
    static readonly HashSet<UnitAIController> _engagedUnits = new();

    // --- Hysteresis state (A) ---
    bool _isReturningToHero;

    // --- Chase anchor / leash (B) ---
    Vector2 _chaseAnchorPosition;

    // --- Target lock + retarget cooldown (C) ---
    float _targetLockedUntil;
    float _retargetAllowedAt;

    // --- Commit window (D) ---
    float _enemyCommitUntil;

    // --- Leash recovery (separate from hysteresis) ---
    float _leashRecoveryUntil;

    // --- Combat linger ---
    float _combatLingerUntil;

    // --- Crowd-aware destination selection ---
    float _probeRadius;
    float _desiredRange;
    Collider2D[] _crowdBuffer = new Collider2D[32];
    ContactFilter2D _allyContactFilter;
    HashSet<Collider2D> _selfColliders;

    // --- Combat slot — two-phase target swap ---
    Transform _enemyReference;
    Health _enemyReferenceHealth;
    Transform _slotWaypoint;        // parented to this.transform, reused across enable/disable cycles
    Vector2 _currentSlotPos;
    Vector2 _enemyPosAtSlotCalc;
    float _combatLockedUntil;
    float _combatRerollAllowedAt;
    bool _approachingSlot;

    // Combat stuck tracking
    float _nextCombatStuckCheckAt;
    float _combatStuckAccum;
    float _lastCombatDestDist;
    Vector2 _lastCombatPos;

    // Idle stuck
    float _idleRerollAllowedAt;
    float _nextIdleStuckCheckAt;
    float _idleStuckAccum;
    float _lastIdleDestDist;
    Vector2 _lastIdlePos;

    // Personal idle anchor: offset from spawnArea center, stored at spawn time
    Vector2 _idleOffsetFromSpawnCenter;
    float _nextIdleOffsetRefreshAt;

    enum TargetReason {
        Hero,
        HeroDefend,
        AllyDefend,
        AllyAssist,
        Enemy,
        Idle
    }

    public void Initialize(Unit hero, BoxCollider2D spawnArea = null, UnitManager partyManager = null) {
        _unit = GetComponent<Unit>();
        if (_unit == null) return;

        _brain = _unit.Brain;
        _movement = _unit.MovementAbility;

        if (_movement != null)
            _baseWalkSpeed = _movement.WalkSpeed;

        _overlapResults = new Collider2D[overlapMaximum];
        _contactFilter = new ContactFilter2D();
        _contactFilter.SetLayerMask(enemyLayer);
        _contactFilter.useTriggers = true;

        _allyOverlap = new Collider2D[Mathf.Max(1, separationOverlapMax)];
        var stObj = new GameObject($"SteeringTarget_{gameObject.name}");
        _steeringTarget = stObj.transform;

        // Slot waypoint: parented to this transform, created once and reused
        if (_slotWaypoint == null) {
            var slotObj = new GameObject($"SlotWaypoint_{gameObject.name}");
            slotObj.transform.SetParent(transform, false);
            _slotWaypoint = slotObj.transform;
        }

        // Cache all own colliders for self-exclusion in crowd scoring (O(1) HashSet lookup)
        _selfColliders = new HashSet<Collider2D>(GetComponentsInChildren<Collider2D>(true));

        _allyContactFilter = new ContactFilter2D();
        _allyContactFilter.SetLayerMask(allyLayer);
        _allyContactFilter.useTriggers = true;

        SetHero(hero);

        _spawnArea = spawnArea;
        if (_spawnArea != null) {
            GameObject waypointObj = new GameObject($"RallyWaypoint_{gameObject.name}");
            waypointObj.transform.SetParent(_spawnArea.transform, false);
            _rallyWaypoint = waypointObj.transform;
            _idleOffsetFromSpawnCenter = (Vector2)transform.position - (Vector2)_spawnArea.bounds.center;
            PickBestIdleRallyPoint();
        }

        // Profile + formation setup (Normal units only)
        _unitManager = partyManager;
        if (_unitManager != null && _unit.Data != null && _unit.Data.type == UnitType.Normal) {
            _aiProfile = _unitManager.GetAIProfile(_unit.Data.unitClass);
            ApplyProfile(_aiProfile);

            // Formation: sourced exclusively from AI profile
            if (_aiProfile != null && _aiProfile.formation != null) {
                _formationProfile = _aiProfile.formation;
                _slotIndex = _unitManager.AssignSlotIndex(_unit.Data.unitClass);
                GameObject fpObj = new GameObject($"FormationPoint_{gameObject.name}");
                _formationPoint = fpObj.transform;
                UpdateFormationPointPosition();
            }
        }

        ComputeProbeRadius();
        ComputeDesiredRange();

        _active = true;
    }

    void ApplyProfile(UnitAIProfile p) {
        if (p == null) return;
        if (p.overrideDistances) {
            returnRadius = p.returnRadius;
            followRadius = p.followRadius;
            maxChaseDistanceFromAnchor = p.maxChaseDistanceFromAnchor;
            allyDefendRadius = p.allyDefendRadius;
        }
        if (p.overrideDetection) {
            enemyDetectRadius = p.enemyDetectRadius;
            heroThreatRadius = p.heroThreatRadius;
            maxUnitAcquireDistance = p.maxUnitAcquireDistance;
        }
        if (p.overrideTiming) {
            targetEvaluationInterval = p.targetEvaluationInterval;
            targetLockTime = p.targetLockTime;
            retargetCooldown = p.retargetCooldown;
            enemyCommitTime = p.enemyCommitTime;
            leashRecoveryTime = p.leashRecoveryTime;
            combatLingerTime = p.combatLingerTime;
        }
        if (p.overrideScoring) {
            wHero = p.wHero;
            wUnit = p.wUnit;
            wCrowd = p.wCrowd;
            wLeash = p.wLeash;
            combatRadius = p.combatRadius;
        }
        if (p.overrideSpeed) {
            heroSpeedMultiplier = p.heroSpeedMultiplier;
        }
        if (p.overrideCrowdAware) {
            pointSamples = p.pointSamples;
            pathChecks = p.pathChecks;
            crowdRadiusMultiplier = p.crowdRadiusMultiplier;
            wCrowdAtPoint = p.wCrowdAtPoint;
            wCrowdAlongPath = p.wCrowdAlongPath;
            wTravelCost = p.wTravelCost;
            lockTime = p.lockTime;
            rerollCooldown = p.rerollCooldown;
            slotSamples = p.slotSamples;
            enemyMovedThreshold = p.enemyMovedThreshold;
            enterEnemyMargin = p.enterEnemyMargin;
            exitEnemyMargin = p.exitEnemyMargin;
            meleeRangeFallback = p.meleeRangeFallback;
            rangedRangeFallback = p.rangedRangeFallback;
            idleRoamRadius = p.idleRoamRadius;
            idleOffsetRefreshDistance = p.idleOffsetRefreshDistance;
        }
    }

    void SetHero(Unit hero) {
        if (_heroUnit != null)
            _heroUnit.OnDeath -= OnHeroDeath;

        _heroUnit = hero;
        if (_heroUnit != null) {
            _heroTransform = _heroUnit.transform;
            _heroAlive = true;
            _heroUnit.OnDeath += OnHeroDeath;
        } else {
            _heroTransform = null;
            _heroAlive = false;
        }
    }

    void OnHeroDeath(Unit unit) {
        _heroAlive = false;
    }

    // =====================================================================
    //  Crowd-aware helpers
    // =====================================================================

    void ComputeProbeRadius() {
        float raw = personalSpaceRadius;

        if (_unit != null && _unit.Data != null) {
            var mergeData = _unit.Data.GetMergeData(_unit.MergeState);
            if (mergeData != null && mergeData.colliderSize != Vector2.zero) {
                raw = 0.5f * Mathf.Max(mergeData.colliderSize.x, mergeData.colliderSize.y);
            } else {
                var col = GetComponent<Collider2D>();
                if (col != null)
                    raw = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y);
            }
        }

        _probeRadius = raw * crowdRadiusMultiplier;
    }

    void ComputeDesiredRange() {
        if (_unit == null || _unit.Data == null) {
            _desiredRange = meleeRangeFallback;
            return;
        }

        float attackRange = -1f;

        // Priority 1: main skill
        Skill mainSkill = _unit.Skills != null ? _unit.Skills.Find(s => s.Main) : null;
        if (mainSkill != null && mainSkill.CurrentLevel != null) {
            if (_unit.Data.IsMelee)
                attackRange = mainSkill.CurrentLevel.GetParameterValue(SkillParameterType.Radius, -1);
            else
                attackRange = mainSkill.CurrentLevel.GetParameterValue(SkillParameterType.Distance, -1);
        }

        // Priority 2: scan all assigned attack skills for max range
        if (attackRange <= 0f && _unit.Skills != null) {
            foreach (var skill in _unit.Skills) {
                if (skill == null || !skill.IsAssigned || skill.CurrentLevel == null) continue;
                float d = skill.CurrentLevel.GetParameterValue(SkillParameterType.Distance, -1);
                if (d > attackRange) attackRange = d;
                float r = skill.CurrentLevel.GetParameterValue(SkillParameterType.Radius, -1);
                if (r > attackRange) attackRange = r;
            }
        }

        // Priority 3: fallback
        if (attackRange <= 0f)
            attackRange = _unit.Data.IsMelee ? meleeRangeFallback : rangedRangeFallback;

        float minRange = Mathf.Max(_probeRadius * 1.1f, 0.3f);
        _desiredRange = Mathf.Clamp(attackRange * 0.9f, minRange, attackRange - 0.1f);
    }

    /// <summary>
    /// Count allies near a point, excluding self colliders. O(1) self-check via HashSet.
    /// </summary>
    int CountCrowdNonAlloc(Vector2 center) {
        int raw = Physics2D.OverlapCircle(center, _probeRadius, _allyContactFilter, _crowdBuffer);
        int count = 0;
        for (int i = 0; i < raw; i++) {
            if (_crowdBuffer[i] != null && !_selfColliders.Contains(_crowdBuffer[i]))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Score a destination candidate. Lower score = better (less crowded, shorter path).
    /// </summary>
    float ScoreDestination(Vector2 candidate, Vector2 fromPos) {
        float score = 0f;

        // Crowd at destination point
        score += CountCrowdNonAlloc(candidate) * wCrowdAtPoint;

        // Crowd along path (probes at t = 1/(K+1), 2/(K+1), ..., K/(K+1))
        for (int i = 1; i <= pathChecks; i++) {
            float t = i / (pathChecks + 1f);
            Vector2 probePos = Vector2.Lerp(fromPos, candidate, t);
            score += CountCrowdNonAlloc(probePos) * wCrowdAlongPath;
        }

        // Travel distance cost
        score += Vector2.Distance(fromPos, candidate) * wTravelCost;

        return score;
    }

    /// <summary>
    /// Parameterized stuck detection. Returns true if unit should reroll destination.
    /// Bypasses lock timers — gated only by rerollAllowedAt (rerollCooldown).
    /// </summary>
    bool CheckStuck(ref float nextCheckAt, ref float accumulator, ref float lastDestDist,
                    ref Vector2 lastPos, Vector2 destination, ref float rerollAllowedAt) {
        if (Time.time < nextCheckAt) return false;
        nextCheckAt = Time.time + stuckCheckInterval;

        Vector2 unitPos = transform.position;
        float newDestDist = Vector2.Distance(unitPos, destination);
        float moveDelta = Vector2.Distance(unitPos, lastPos);

        bool progressing = (lastDestDist - newDestDist) >= stuckMinDistanceProgress
                        || moveDelta >= stuckMinMoveDelta;

        if (progressing) {
            accumulator = 0f;
        } else {
            accumulator += stuckCheckInterval;
            if (accumulator >= stuckTimeToReroll && Time.time >= rerollAllowedAt) {
                accumulator = 0f;
                rerollAllowedAt = Time.time + rerollCooldown;
                lastDestDist = newDestDist;
                lastPos = unitPos;
                return true;
            }
        }

        lastDestDist = newDestDist;
        lastPos = unitPos;
        return false;
    }

    // =====================================================================
    //  Idle: crowd-aware rally point selection (personal anchor)
    // =====================================================================

    Vector2 GetIdleAnchorWorld() {
        if (_spawnArea == null) return transform.position;
        return (Vector2)_spawnArea.bounds.center + _idleOffsetFromSpawnCenter;
    }

    /// <summary>
    /// Sample candidates around personal idle anchor, pick the least crowded.
    /// Candidates are local to the anchor (idleRoamRadius), clamped to spawnArea bounds.
    /// Sets _rallyWaypoint position and resets idle stuck tracking.
    /// </summary>
    void PickBestIdleRallyPoint() {
        if (_rallyWaypoint == null || _spawnArea == null) return;

        Bounds bounds = _spawnArea.bounds;
        Vector2 center = bounds.center;
        Vector2 anchor = center + _idleOffsetFromSpawnCenter;
        Vector2 unitPos = transform.position;

        // Refresh offset if unit drifted far from anchor (e.g. after combat chaos)
        // Gated by cooldown to prevent noisy re-anchoring when being pushed around
        if (Vector2.Distance(unitPos, anchor) > idleOffsetRefreshDistance
            && Time.time >= _nextIdleOffsetRefreshAt) {
            _idleOffsetFromSpawnCenter = unitPos - center;
            anchor = center + _idleOffsetFromSpawnCenter;
            _nextIdleOffsetRefreshAt = Time.time + 1.5f;
        }

        Vector2 bestWorld = anchor; // fallback
        float bestScore = float.MaxValue;

        int samples = Mathf.Max(1, pointSamples);
        for (int i = 0; i < samples; i++) {
            Vector2 candidate = anchor + Random.insideUnitCircle * idleRoamRadius;
            // Clamp to spawnArea bounds
            candidate.x = Mathf.Clamp(candidate.x, bounds.min.x, bounds.max.x);
            candidate.y = Mathf.Clamp(candidate.y, bounds.min.y, bounds.max.y);

            float score = ScoreDestination(candidate, unitPos);
            if (score < bestScore) {
                bestScore = score;
                bestWorld = candidate;
            }
        }

        // Convert world position to local space of rally waypoint parent (spawn area)
        if (_rallyWaypoint.parent != null)
            _rallyWaypoint.localPosition = _rallyWaypoint.parent.InverseTransformPoint(
                new Vector3(bestWorld.x, bestWorld.y, 0f));
        else
            _rallyWaypoint.position = new Vector3(bestWorld.x, bestWorld.y, 0f);

        // Reset idle stuck tracking
        _idleStuckAccum = 0f;
        _idleRerollAllowedAt = Time.time + rerollCooldown;
        _lastIdlePos = unitPos;
        _lastIdleDestDist = Vector2.Distance(unitPos, bestWorld);
    }

    // =====================================================================
    //  Combat: slot computation + two-phase target swap
    // =====================================================================

    /// <summary>
    /// Compute best slot position on a ring around enemy within ±90° of approach direction.
    /// </summary>
    Vector2 ComputeAttackSlot(Vector2 enemyPos, float desiredRange) {
        Vector2 unitPos = transform.position;
        Vector2 approachDir = (unitPos - enemyPos);
        if (approachDir.sqrMagnitude < 0.0001f)
            approachDir = Vector2.right;
        else
            approachDir.Normalize();

        float baseAngle = Mathf.Atan2(approachDir.y, approachDir.x);
        float halfArc = 90f * Mathf.Deg2Rad; // ±90° sector

        Vector2 bestCandidate = enemyPos + approachDir * desiredRange; // fallback
        float bestScore = float.MaxValue;

        int samples = Mathf.Max(2, slotSamples);
        for (int i = 0; i < samples; i++) {
            float t;
            if (i == 0) {
                t = 0f; // first candidate at exact approach angle (biased)
            } else {
                // spread evenly across ±90° arc
                t = -halfArc + (2f * halfArc * i / (samples - 1));
            }

            float angle = baseAngle + t;
            Vector2 candidate = enemyPos + new Vector2(
                Mathf.Cos(angle) * desiredRange,
                Mathf.Sin(angle) * desiredRange
            );

            float score = ScoreDestination(candidate, unitPos);
            if (score < bestScore) {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        return bestCandidate;
    }

    void ResetCombatStuckTracking(Vector2 destination) {
        _combatStuckAccum = 0f;
        _lastCombatDestDist = Vector2.Distance(transform.position, destination);
        _lastCombatPos = transform.position;
        _combatRerollAllowedAt = Time.time + rerollCooldown;
    }

    /// <summary>
    /// Two-phase combat update. Swaps _brain.Target between slot waypoint (Phase 1)
    /// and enemy transform (Phase 2) based on distance to enemy with hysteresis.
    /// </summary>
    void UpdateCombatSlotPhase() {
        if (_enemyReference == null || _brain == null) return;

        // Check enemy validity
        if (!_enemyReference.gameObject.activeInHierarchy
            || (_enemyReferenceHealth != null && _enemyReferenceHealth.CurrentHealth <= 0)) {
            _enemyReference = null;
            _enemyReferenceHealth = null;
            _approachingSlot = false;
            _forceReevaluate = true;
            return;
        }

        Vector2 unitPos = transform.position;
        Vector2 enemyPos = _enemyReference.position;
        float dEnemy = Vector2.Distance(unitPos, enemyPos);

        if (_approachingSlot) {
            // Phase 1 → Phase 2: close enough to enemy
            if (dEnemy <= _desiredRange + enterEnemyMargin) {
                _brain.Target = _enemyReference;
                _approachingSlot = false;
                ResetCombatStuckTracking(enemyPos);
                return;
            }

            // Check stuck FIRST (bypasses lock, gated by rerollCooldown)
            bool stuck = CheckStuck(ref _nextCombatStuckCheckAt, ref _combatStuckAccum,
                ref _lastCombatDestDist, ref _lastCombatPos, _currentSlotPos, ref _combatRerollAllowedAt);

            if (stuck) {
                Vector2 newSlot = ComputeAttackSlot(enemyPos, _desiredRange);
                _currentSlotPos = newSlot;
                _slotWaypoint.position = new Vector3(newSlot.x, newSlot.y, 0f);
                _enemyPosAtSlotCalc = enemyPos;
                _combatLockedUntil = Time.time + lockTime;
                _brain.Target = _slotWaypoint;
                ResetCombatStuckTracking(newSlot);
            }
            // Check enemy-moved (requires lock expired)
            else if (Time.time > _combatLockedUntil) {
                float enemyMoved = Vector2.Distance(_enemyPosAtSlotCalc, enemyPos);
                if (enemyMoved > enemyMovedThreshold) {
                    Vector2 newSlot = ComputeAttackSlot(enemyPos, _desiredRange);
                    _currentSlotPos = newSlot;
                    _slotWaypoint.position = new Vector3(newSlot.x, newSlot.y, 0f);
                    _enemyPosAtSlotCalc = enemyPos;
                    _combatLockedUntil = Time.time + lockTime;
                    _brain.Target = _slotWaypoint;
                    ResetCombatStuckTracking(newSlot);
                }
            }
        } else {
            // Phase 2 → Phase 1: too far from enemy, go back to slot approach
            if (dEnemy >= _desiredRange + exitEnemyMargin && Time.time > _combatLockedUntil) {
                Vector2 newSlot = ComputeAttackSlot(enemyPos, _desiredRange);
                _currentSlotPos = newSlot;
                _slotWaypoint.position = new Vector3(newSlot.x, newSlot.y, 0f);
                _enemyPosAtSlotCalc = enemyPos;
                _combatLockedUntil = Time.time + lockTime;
                _approachingSlot = true;
                _brain.Target = _slotWaypoint;
                ResetCombatStuckTracking(newSlot);
            } else {
                // Stay in Phase 2
                _brain.Target = _enemyReference;
            }
        }
    }

    // =====================================================================
    //  Main update loop
    // =====================================================================

    void Update() {
        if (!_active || _brain == null) return;

        // Immediate re-evaluation when current enemy becomes invalid (dead/destroyed/inactive).
        // Prevents the unit from walking toward a dead enemy for up to targetEvaluationInterval.
        if (_targetReason == TargetReason.Enemy && !IsReservedEnemyValid()) {
            // Release reservation immediately so other units see updated counts
            if (_reservedEnemyInstanceId >= 0 && _unitManager != null) {
                _unitManager.ReleaseEnemy(_reservedEnemyInstanceId);
                _reservedEnemyInstanceId = -1;
            }
            _enemyReference = null;
            _enemyReferenceHealth = null;
            _approachingSlot = false;
            _forceReevaluate = true;
        }

        EvaluateTarget();
        UpdateCatchUpState();
        UpdateSpeedBoost();

        // Two-phase combat slot update
        if (_enemyReference != null && _targetReason == TargetReason.Enemy)
            UpdateCombatSlotPhase();
    }

    bool IsReservedEnemyValid() {
        return _currentTarget != null
            && _currentTarget.gameObject.activeInHierarchy
            && (_currentTargetHealth == null || _currentTargetHealth.CurrentHealth > 0);
    }

    void EvaluateTarget() {
        if (!_forceReevaluate && Time.time - _lastEvaluationTime < targetEvaluationInterval) return;
        _forceReevaluate = false;
        _lastEvaluationTime = Time.time;

        // Update formation point to track hero position every tick
        UpdateFormationPointPosition();

        // === Phase 1: Compute hero distance and update hysteresis flag ===
        float distToHero = float.MaxValue;
        if (_heroTransform != null && _heroAlive)
            distToHero = Vector2.Distance(transform.position, _heroTransform.position);

        // Hysteresis toggle (A): two thresholds prevent oscillation.
        // Between followRadius and returnRadius the flag keeps its current value.
        if (distToHero > returnRadius)
            _isReturningToHero = true;
        else if (distToHero < followRadius)
            _isReturningToHero = false;

        // === Phase 2: Hero dead — fight independently ===
        if (!_heroAlive) {
            _isReturningToHero = false;
            if (!IsCurrentTargetValid()) {
                Transform enemy = FindNearestEnemy();
                TrySetTarget(enemy, enemy != null ? TargetReason.Enemy : TargetReason.Idle);
            }
            return;
        }

        // === Phase 3: Hard leash check (B) — abort chase if unit wandered too far from anchor ===
        if (_targetReason == TargetReason.Enemy && _currentTarget != null) {
            float distFromAnchor = Vector2.Distance(transform.position, _chaseAnchorPosition);
            if (distFromAnchor > maxChaseDistanceFromAnchor) {
                // Commit window (D): let unit finish attack volley before leash yank,
                // but ONLY if still within returnRadius (hard cap).
                if (distToHero <= returnRadius && Time.time < _enemyCommitUntil)
                    return;

                // Leash snapped. Suppress enemy targeting for leashRecoveryTime
                _leashRecoveryUntil = Time.time + leashRecoveryTime;
                ForceSetTarget(GetSafeRallyTarget(), TargetReason.Hero);
                return;
            }
        }

        // === Phase 4: Forced hero return (A) — returnRadius is HARD cap, no commit window ===
        if (_isReturningToHero) {
            ForceSetTarget(GetSafeRallyTarget(), TargetReason.Hero);
            return;
        }

        // === Phase 5: Target lock (C) — keep enemy if lock hasn't expired ===
        if (_targetReason == TargetReason.Enemy && Time.time < _targetLockedUntil) {
            if (IsCurrentTargetValid()) return;
            // Enemy died during lock — fall through to re-evaluate
        }

        // === Phase 6: Current target still valid — keep it ===
        if (IsCurrentTargetValid()) return;

        // === Phase 7: Priority-based target selection ===

        // P1: Hero under attack → defend
        if (Time.time - _heroLastHitTime < heroHitResponseDuration && _heroTransform != null) {
            TrySetTarget(_heroTransform, TargetReason.HeroDefend);
            return;
        }

        // P2: Ally under attack → defend
        if (Time.time - _allyLastHitTime < allyHitResponseDuration && _lastHitAllyTransform != null) {
            if (Vector2.Distance(transform.position, _lastHitAllyTransform.position) <= allyDefendRadius) {
                TrySetTarget(_lastHitAllyTransform, TargetReason.AllyDefend);
                return;
            }
        }

        // P3: Enemy in radius → attack
        // Suppressed during leash recovery to prevent immediate re-pick of the same enemy.
        if (Time.time >= _leashRecoveryUntil) {
            Transform nearestEnemy = FindNearestEnemy();
            if (nearestEnemy != null) {
                TrySetTarget(nearestEnemy, TargetReason.Enemy);
                return;
            }
        }

        // P4: Ally fighting nearby → assist
        // Also suppressed during leash recovery (assisting moves toward enemies).
        if (Time.time >= _leashRecoveryUntil) {
            Transform fightingAlly = FindNearestFightingAlly();
            if (fightingAlly != null) {
                TrySetTarget(fightingAlly, TargetReason.AllyAssist);
                return;
            }
        }

        // P5: No threats → follow hero
        // Combat linger: if we just lost our enemy target (no replacement found),
        // hold position briefly instead of immediately retreating to rally.
        if (_targetReason == TargetReason.Enemy) {
            _combatLingerUntil = Time.time + combatLingerTime;
        }
        if (Time.time < _combatLingerUntil)
            return; // hold position — don't retreat yet, new enemies may arrive

        TrySetTarget(GetSafeRallyTarget(), TargetReason.Hero);
    }

    // --- Ally separation (anti-clumping) ---

    Vector2 ComputeSeparationOffset() {
        if (personalSpaceRadius <= 0f || separationStrength <= 0f) return Vector2.zero;
        if (_allyOverlap == null || _allyOverlap.Length == 0) return Vector2.zero;

        int count = Physics2D.OverlapCircleNonAlloc(transform.position, personalSpaceRadius, _allyOverlap, allyLayer);
        if (count <= 0) return Vector2.zero;

        Vector2 sum = Vector2.zero;
        Vector2 selfPos = transform.position;

        for (int i = 0; i < count; i++) {
            Collider2D c = _allyOverlap[i];
            if (c == null) continue;
            if (c.transform == transform) continue;

            Vector2 otherPos = c.transform.position;
            Vector2 toSelf = selfPos - otherPos;
            float dist = toSelf.magnitude;
            if (dist <= 0.0001f) continue;

            // closer neighbor => stronger push
            float t = Mathf.Clamp01((personalSpaceRadius - dist) / personalSpaceRadius);
            sum += (toSelf / dist) * t;
        }

        if (sum == Vector2.zero) return Vector2.zero;

        Vector2 offset = sum.normalized * separationStrength;

        // clamp so we stay near formation/rally
        float mag = offset.magnitude;
        if (mag > maxSeparationOffset && mag > 0.0001f)
            offset = (offset / mag) * maxSeparationOffset;

        return offset;
    }

    Vector2 GetSeparationOffsetCached() {
        if (!separationUseIntervalUpdate)
            return ComputeSeparationOffset();

        if (Time.time >= _nextSeparationUpdateAt) {
            _nextSeparationUpdateAt = Time.time + Mathf.Max(0.01f, separationUpdateInterval);
            _cachedSeparationOffset = ComputeSeparationOffset();
        }

        return _cachedSeparationOffset;
    }

    Transform GetSteeredTarget(Transform baseTarget, TargetReason reason) {
        if (baseTarget == null || _steeringTarget == null) return baseTarget;

        // Never steer combat target (keeps reservations/lock/commit stable)
        if (reason == TargetReason.Enemy) return baseTarget;

        // Usually better not to steer defend targets
        if (reason == TargetReason.HeroDefend || reason == TargetReason.AllyDefend) return baseTarget;

        if (separationOnlyForFormationOrRally) {
            bool isFormationOrRally = (baseTarget == _formationPoint) || (baseTarget == _rallyWaypoint);
            if (!isFormationOrRally) return baseTarget;
        }

        Vector2 offset = GetSeparationOffsetCached();
        _steeringTarget.position = (Vector2)baseTarget.position + offset;
        return _steeringTarget;
    }

    // --- Target setters (C) ---

    /// <summary>
    /// Low-level setter. Updates internal state and calls _unit.SetTarget().
    /// Sets timing flags (lock, cooldown, commit, anchor) when switching to Enemy.
    /// For Enemy targets: also sets up two-phase combat slot system.
    /// </summary>
    void ApplyTarget(Transform target, TargetReason reason) {
        // Reservation management: determine new instanceId
        int newInstanceId = (reason == TargetReason.Enemy && target != null)
            ? target.gameObject.GetInstanceID()
            : -1;

        // Release previous reservation only if target actually changed
        if (_reservedEnemyInstanceId >= 0 && _reservedEnemyInstanceId != newInstanceId && _unitManager != null) {
            _unitManager.ReleaseEnemy(_reservedEnemyInstanceId);
            _reservedEnemyInstanceId = -1;
        }

        if (reason == TargetReason.Enemy)
            _engagedUnits.Add(this);
        else
            _engagedUnits.Remove(this);

        _currentTarget = target;
        _targetReason = reason;
        _currentTargetHealth = target != null ? target.GetComponent<Health>() : null;
        _retargetAllowedAt = Time.time + retargetCooldown;

        if (reason == TargetReason.Enemy) {
            _lastThreatTime = Time.time; // local threat: unit actually engaged an enemy
            _targetLockedUntil = Time.time + targetLockTime;
            _enemyCommitUntil = Time.time + enemyCommitTime;
            _combatLingerUntil = 0f; // clear linger when we have a new enemy
            // Anchor = hero position at the moment enemy was chosen (B).
            _chaseAnchorPosition = _heroTransform != null
                ? (Vector2)_heroTransform.position
                : (Vector2)transform.position;

            // Reserve the new enemy (only if not already reserved — same id)
            if (newInstanceId >= 0 && newInstanceId != _reservedEnemyInstanceId && _unitManager != null) {
                _reservedEnemyInstanceId = newInstanceId;
                _unitManager.ReserveEnemy(_reservedEnemyInstanceId);
            }
        }

        _unit.SetTarget(target);

        // --- Two-phase combat slot setup ---
        if (reason == TargetReason.Enemy && target != null && _slotWaypoint != null) {
            _enemyReference = target;
            _enemyReferenceHealth = _currentTargetHealth;

            // Recompute range in case merge state changed since last enemy
            ComputeProbeRadius();
            ComputeDesiredRange();

            float dEnemy = Vector2.Distance(transform.position, target.position);
            if (dEnemy <= _desiredRange + enterEnemyMargin) {
                // Already close → Phase 2 (direct enemy target, brain.Target stays as enemy)
                _approachingSlot = false;
                _lastCombatDestDist = dEnemy;
            } else {
                // Phase 1: compute slot, override brain.Target to slot waypoint
                Vector2 slotPos = ComputeAttackSlot(target.position, _desiredRange);
                _currentSlotPos = slotPos;
                _slotWaypoint.position = new Vector3(slotPos.x, slotPos.y, 0f);
                _enemyPosAtSlotCalc = target.position;
                _combatLockedUntil = Time.time + lockTime;
                _approachingSlot = true;
                _brain.Target = _slotWaypoint; // override what SetTarget just set
                _lastCombatDestDist = Vector2.Distance(transform.position, slotPos);
            }
            // Reset combat stuck tracking
            _combatStuckAccum = 0f;
            _combatRerollAllowedAt = Time.time + rerollCooldown;
            _lastCombatPos = transform.position;
        } else if (reason != TargetReason.Enemy) {
            _enemyReference = null;
            _enemyReferenceHealth = null;
            _approachingSlot = false;
        }
    }

    /// <summary>
    /// Respects retarget cooldown: skips ApplyTarget if same target + same reason
    /// and cooldown hasn't elapsed. Prevents redundant brain/animation resets.
    /// </summary>
    void TrySetTarget(Transform target, TargetReason reason) {
        target = GetSteeredTarget(target, reason);
        if (target == _currentTarget && reason == _targetReason && Time.time < _retargetAllowedAt)
            return;

        ApplyTarget(target, reason);
    }

    /// <summary>
    /// Bypasses cooldown for critical transitions (leash snap, forced hero return).
    /// </summary>
    void ForceSetTarget(Transform target, TargetReason reason) {
        target = GetSteeredTarget(target, reason);
        ApplyTarget(target, reason);
    }

    bool IsCurrentTargetValid() {
        if (_currentTarget == null) return false;

        switch (_targetReason) {
            case TargetReason.Enemy:
                return _currentTargetHealth == null || _currentTargetHealth.CurrentHealth > 0;
            case TargetReason.HeroDefend:
                return Time.time - _heroLastHitTime < heroHitResponseDuration;
            case TargetReason.AllyDefend:
                if (Time.time - _allyLastHitTime >= allyHitResponseDuration) return false;
                return _currentTargetHealth == null || _currentTargetHealth.CurrentHealth > 0;
            case TargetReason.AllyAssist:
                return false;
            case TargetReason.Hero:
                // Valid while forced return is active — prevents constant re-setting of brain target.
                // When not returning, return false so EvaluateTarget can detect enemies.
                return _isReturningToHero;
            case TargetReason.Idle:
            default:
                return false;
        }
    }

    void UpdateFormationPointPosition() {
        if (_formationPoint == null || _formationProfile == null) return;

        // Base position: spawnArea center (follows hero via PartyManager.FixedUpdate)
        // Fallback to hero position if no spawnArea
        Vector3 basePos = _spawnArea != null
            ? _spawnArea.bounds.center
            : (_heroTransform != null ? _heroTransform.position : transform.position);

        int maxPerLine = Mathf.Max(1, _formationProfile.maxPerLine);
        int line = _slotIndex / maxPerLine;
        int posInLine = _slotIndex % maxPerLine;

        float x = _formationProfile.forwardOffsetX - line * _formationProfile.rowBackstepX;
        float y = (posInLine - (maxPerLine - 1) * 0.5f) * _formationProfile.spreadY;

        _formationPoint.position = basePos + new Vector3(x, y, 0f);
    }

    bool ShouldUseFormationNow() {
        if (_formationPoint == null || _formationProfile == null) return false;
        if (_isReturningToHero || _isCatchingUp) return true;
        if (_targetReason == TargetReason.Enemy || _targetReason == TargetReason.HeroDefend
            || _targetReason == TargetReason.AllyDefend || _targetReason == TargetReason.AllyAssist)
            return true;
        if (Time.time - _lastThreatTime < idleRoamDelayAfterThreat) return true;
        return false;
    }

    /// <summary>
    /// Returns a valid rally target. Never returns null.
    /// During resting, returns the current waypoint so the unit stands still near it
    /// instead of receiving null which would cause idle/walk jitter.
    /// Includes crowd-aware idle stuck detection.
    /// </summary>
    Transform GetSafeRallyTarget() {
        // HYBRID: formation during threats/catchup/return, roam when idle
        if (ShouldUseFormationNow())
            return _formationPoint;

        // Idle roam: crowd-aware rally waypoint behavior
        if (_rallyWaypoint == null) return _heroTransform;

        // Idle stuck detection: reroll if stuck while moving to rally (gated by _idleRerollAllowedAt)
        if (!_rallyResting) {
            bool stuck = CheckStuck(ref _nextIdleStuckCheckAt, ref _idleStuckAccum,
                ref _lastIdleDestDist, ref _lastIdlePos, _rallyWaypoint.position, ref _idleRerollAllowedAt);
            if (stuck)
                PickBestIdleRallyPoint();
        }

        if (_rallyResting) {
            if (Time.time < _rallyRestUntil)
                return _rallyWaypoint;

            _rallyResting = false;
            PickBestIdleRallyPoint();
            return _rallyWaypoint;
        }

        float dist = Vector2.Distance(transform.position, _rallyWaypoint.position);
        if (dist < _rallyArrivalThreshold) {
            _rallyResting = true;
            _rallyRestUntil = Time.time + Random.Range(rallyRestInterval.x, rallyRestInterval.y);
            return _rallyWaypoint;
        }

        return _rallyWaypoint;
    }

    Transform FindNearestEnemy() {
        if (_heroTransform == null) return null;

        float effectiveRadius = heroThreatRadius > 0f ? heroThreatRadius : enemyDetectRadius;
        int count = Physics2D.OverlapCircle(_heroTransform.position, effectiveRadius, _contactFilter, _overlapResults);
        if (count == 0) return null;

        float acquireLimit = maxUnitAcquireDistance > 0f ? maxUnitAcquireDistance : effectiveRadius;
        float dUnitToHero = Vector2.Distance(transform.position, _heroTransform.position);

        Transform best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < count; i++) {
            if (_overlapResults[i] == null) continue;

            Health health = _overlapResults[i].GetComponent<Health>();
            if (health != null && health.CurrentHealth <= 0) continue;

            Transform candidate = _overlapResults[i].transform;
            float dHero = Vector2.Distance(_heroTransform.position, candidate.position);
            float dUnit = Vector2.Distance(transform.position, candidate.position);

            // Guard 2: hard gate — skip candidates physically too far from this unit
            if (dUnit > acquireLimit) continue;

            int assignedCount = _unitManager != null
                ? _unitManager.GetAssignedCount(candidate.gameObject.GetInstanceID())
                : 0;

            float score = wHero / (0.5f + dHero)
                        + wUnit / (0.5f + dUnit)
                        - wCrowd * assignedCount
                        - wLeash * Mathf.Max(0f, dHero - combatRadius)
                        - wLeash * Mathf.Max(0f, dUnit - dUnitToHero);

            if (score > bestScore) {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    Transform FindNearestFightingAlly() {
        Transform nearest = null;
        float minDist = float.MaxValue;

        foreach (var unit in _engagedUnits) {
            if (unit == null || unit == this) continue;
            float dist = Vector2.Distance(transform.position, unit.transform.position);
            if (dist < allyDefendRadius && dist < minDist) {
                minDist = dist;
                nearest = unit.transform;
            }
        }

        return nearest;
    }

    void UpdateCatchUpState() {
        if (_formationPoint == null || _formationProfile == null) {
            _isCatchingUp = false;
            return;
        }

        // Disable catch-up during combat or forced return
        if (_targetReason == TargetReason.Enemy || _isReturningToHero) {
            _isCatchingUp = false;
            return;
        }

        float distToFormation = Vector2.Distance(transform.position, _formationPoint.position);

        // Hysteresis: enter at catchupDistance, exit at catchupStopDistance
        if (distToFormation > _formationProfile.catchupDistance)
            _isCatchingUp = true;
        else if (distToFormation < _formationProfile.catchupStopDistance)
            _isCatchingUp = false;
        // Between thresholds: keep current state
    }

    /// <summary>
    /// Speed boost priority: forced return > catch-up > normal.
    /// Forced return activates beyond returnRadius, catch-up when lagging behind formation.
    /// </summary>
    void UpdateSpeedBoost() {
        if (_movement == null) return;

        float targetMultiplier = 1f;

        if (_isReturningToHero)
            targetMultiplier = heroSpeedMultiplier;
        else if (_isCatchingUp && _formationProfile != null)
            targetMultiplier = _formationProfile.catchupSpeedMultiplier;

        bool shouldBoost = targetMultiplier != 1f;

        if (shouldBoost) {
            _movement.WalkSpeed = _baseWalkSpeed * targetMultiplier;
            _speedBoosted = true;
        } else if (_speedBoosted) {
            _movement.WalkSpeed = _baseWalkSpeed;
            _speedBoosted = false;
        }
    }

    public void OnMMEvent(UnitActionEvent e) {
        if (!_active || e.Type != UnitActionType.Hit || e.Unit == null || e.Unit == _unit) return;

        if (e.Unit.Data != null && e.Unit.Data.type == UnitType.Hero) {
            _heroLastHitTime = Time.time;
            _lastThreatTime = Time.time;
        } else {
            float distance = Vector2.Distance(transform.position, e.Unit.transform.position);
            if (distance <= allyDefendRadius) {
                _lastHitAllyTransform = e.Unit.transform;
                _allyLastHitTime = Time.time;
                _lastThreatTime = Time.time;
            }
        }
    }

    public void OnMMEvent(LevelResultEvent e) {
        _active = false;
        if (_speedBoosted && _movement != null) {
            _movement.WalkSpeed = _baseWalkSpeed;
            _speedBoosted = false;
        }
    }

    void OnEnable() {
        this.MMEventStartListening<UnitActionEvent>();
        this.MMEventStartListening<LevelResultEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<UnitActionEvent>();
        this.MMEventStopListening<LevelResultEvent>();
        _engagedUnits.Remove(this);

        // Release enemy reservation
        if (_reservedEnemyInstanceId >= 0 && _unitManager != null) {
            _unitManager.ReleaseEnemy(_reservedEnemyInstanceId);
            _reservedEnemyInstanceId = -1;
        }

        // Clear combat slot state (do NOT destroy _slotWaypoint — parented to unit, reused for pooling)
        _enemyReference = null;
        _enemyReferenceHealth = null;
        _approachingSlot = false;

        if (_heroUnit != null)
            _heroUnit.OnDeath -= OnHeroDeath;

        if (_rallyWaypoint != null)
            Destroy(_rallyWaypoint.gameObject);

        if (_formationPoint != null)
            Destroy(_formationPoint.gameObject);

        if (_steeringTarget != null)
            Destroy(_steeringTarget.gameObject);
    }
}
