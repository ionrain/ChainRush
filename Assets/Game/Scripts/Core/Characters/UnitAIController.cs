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
    // When true, unit is being recalled to hero and cannot pick combat targets.
    // Flips true at returnRadius, flips false at followRadius. The gap prevents oscillation.
    bool _isReturningToHero;

    // --- Chase anchor / leash (B) ---
    // Captured hero position at the moment an Enemy target is chosen.
    // If unit wanders beyond maxChaseDistanceFromAnchor from this point, chase is aborted.
    // We check unit-to-anchor (not enemy-to-anchor) because the unit is what we want to keep close.
    Vector2 _chaseAnchorPosition;

    // --- Target lock + retarget cooldown (C) ---
    // _targetLockedUntil: prevents switching away from an Enemy target too quickly (avoids ping-ponging between enemies).
    // _retargetAllowedAt: prevents redundant _unit.SetTarget calls that reset brain/animations.
    float _targetLockedUntil;
    float _retargetAllowedAt;

    // --- Commit window (D) ---
    // Short grace period after choosing an Enemy target. During this time, a leash yank
    // is delayed so the unit can finish its attack. Only applies while within returnRadius.
    // returnRadius is a HARD cap — commit window never overrides it.
    float _enemyCommitUntil;

    // --- Leash recovery (separate from hysteresis) ---
    // After leash abort, enemy/ally-assist targeting is suppressed for leashRecoveryTime.
    // Uses its own timer instead of _isReturningToHero to avoid Phase 1 immediately clearing it.
    float _leashRecoveryUntil;

    // --- Combat linger ---
    // After losing the last enemy (no replacement found), hold position briefly
    // instead of immediately retreating to rally. Prevents back-and-forth when enemies die fast.
    float _combatLingerUntil;

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

        SetHero(hero);

        _spawnArea = spawnArea;
        if (_spawnArea != null) {
            GameObject waypointObj = new GameObject($"RallyWaypoint_{gameObject.name}");
            waypointObj.transform.SetParent(_spawnArea.transform, false);
            _rallyWaypoint = waypointObj.transform;
            RandomizeRallyPosition();
        }

        // Profile + formation setup (Normal units only)
        _unitManager = partyManager;
        if (_unitManager != null && _unit.Data != null && _unit.Data.type == UnitType.Normal) {
            _aiProfile = _unitManager.GetAIProfile(_unit.Data.speciality);
            ApplyProfile(_aiProfile);

            // Formation: sourced exclusively from AI profile
            if (_aiProfile != null && _aiProfile.formation != null) {
                _formationProfile = _aiProfile.formation;
                _slotIndex = _unitManager.AssignSlotIndex(_unit.Data.speciality);
                GameObject fpObj = new GameObject($"FormationPoint_{gameObject.name}");
                _formationPoint = fpObj.transform;
                UpdateFormationPointPosition();
            }
        }

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
            _forceReevaluate = true;
        }

        EvaluateTarget();
        UpdateCatchUpState();
        UpdateSpeedBoost();
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
                // so the unit doesn't immediately re-pick the same enemy.
                // NOTE: we do NOT set _isReturningToHero here — that's for hysteresis only.
                // If we did, Phase 1 would clear it next tick (unit is within followRadius).
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
        // Prevents back-and-forth jitter when enemies die frequently.
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

    void RandomizeRallyPosition() {
        if (_rallyWaypoint == null || _spawnArea == null) return;
        Vector2 halfSize = _spawnArea.size * 0.5f;
        _rallyWaypoint.localPosition = new Vector3(
            Random.Range(-halfSize.x, halfSize.x),
            Random.Range(-halfSize.y, halfSize.y),
            0f
        );
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
    /// </summary>
    Transform GetSafeRallyTarget() {
        // HYBRID: formation during threats/catchup/return, roam when idle
        if (ShouldUseFormationNow())
            return _formationPoint;

        // Idle roam: original rally waypoint behavior
        if (_rallyWaypoint == null) return _heroTransform;

        if (_rallyResting) {
            if (Time.time < _rallyRestUntil)
                return _rallyWaypoint;

            _rallyResting = false;
            RandomizeRallyPosition();
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
        bool anyValidCandidate = false;

        for (int i = 0; i < count; i++) {
            if (_overlapResults[i] == null) continue;

            Health health = _overlapResults[i].GetComponent<Health>();
            if (health != null && health.CurrentHealth <= 0) continue;

            Transform candidate = _overlapResults[i].transform;
            float dHero = Vector2.Distance(_heroTransform.position, candidate.position);
            float dUnit = Vector2.Distance(transform.position, candidate.position);

            // Guard 2: hard gate — skip candidates physically too far from this unit
            if (dUnit > acquireLimit) continue;

            anyValidCandidate = true;

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

        if (anyValidCandidate)
            _lastThreatTime = Time.time;

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