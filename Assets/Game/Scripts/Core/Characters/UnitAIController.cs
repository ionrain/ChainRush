using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class UnitAIController : MonoBehaviour, MMEventListener<UnitActionEvent>, MMEventListener<LevelResultEvent> {

    [Header("Distance Thresholds")]
    [SerializeField] float heroFarDistance = 8f;
    [SerializeField] float heroSpeedBoostDistance = 8f;
    [SerializeField] float enemyDetectRadius = 6f;
    [SerializeField] float allyDefendRadius = 8f;

    [Header("Speed")]
    [SerializeField] float heroSpeedMultiplier = 2f;

    [Header("Enemy Detection")]
    [SerializeField] LayerMask enemyLayer;
    [SerializeField] float targetEvaluationInterval = 0.3f;
    [SerializeField] int overlapMaximum = 10;

    [Header("Hit Response")]
    [SerializeField] float heroHitResponseDuration = 2f;
    [SerializeField] float allyHitResponseDuration = 1.5f;

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

    // Units currently fighting enemies (for ally assist)
    static readonly HashSet<UnitAIController> _engagedUnits = new();

    enum TargetReason {
        Hero,
        HeroDefend,
        AllyDefend,
        AllyAssist,
        Enemy,
        Idle
    }

    public void Initialize(Unit hero, BoxCollider2D spawnArea = null) {
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

        SetHero(hero);

        _spawnArea = spawnArea;
        if (_spawnArea != null) {
            GameObject waypointObj = new GameObject($"RallyWaypoint_{gameObject.name}");
            waypointObj.transform.SetParent(_spawnArea.transform, false);
            _rallyWaypoint = waypointObj.transform;
            RandomizeRallyPosition();
        }

        _active = true;
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

        EvaluateTarget();
        UpdateSpeedBoost();
    }

    void EvaluateTarget() {
        if (Time.time - _lastEvaluationTime < targetEvaluationInterval) return;
        _lastEvaluationTime = Time.time;

        // 1. Hero dead → only re-evaluate if target lost
        if (!_heroAlive) {
            if (!IsCurrentTargetValid()) {
                Transform enemy = FindNearestEnemy();
                SetTarget(enemy, enemy != null ? TargetReason.Enemy : TargetReason.Idle);
            }
            return;
        }

        // 2. Far from hero → always return (hard override)
        if (_heroTransform != null) {
            float distanceToHero = Vector2.Distance(transform.position, _heroTransform.position);
            if (distanceToHero > heroFarDistance) {
                SetTarget(GetRallyTarget(), TargetReason.Hero);
                return;
            }
        }

        // 3. Current target still valid → keep it
        if (IsCurrentTargetValid()) return;

        // 4. P1: Hero under attack → defend
        if (Time.time - _heroLastHitTime < heroHitResponseDuration && _heroTransform != null) {
            SetTarget(_heroTransform, TargetReason.HeroDefend);
            return;
        }

        // 5. P2: Ally under attack → defend
        if (Time.time - _allyLastHitTime < allyHitResponseDuration && _lastHitAllyTransform != null) {
            if (Vector2.Distance(transform.position, _lastHitAllyTransform.position) <= allyDefendRadius) {
                SetTarget(_lastHitAllyTransform, TargetReason.AllyDefend);
                return;
            }
        }

        // 6. P3: Enemy in radius → attack
        Transform nearestEnemy = FindNearestEnemy();
        if (nearestEnemy != null) {
            SetTarget(nearestEnemy, TargetReason.Enemy);
            return;
        }

        // 7. Ally fighting nearby → assist
        Transform fightingAlly = FindNearestFightingAlly();
        if (fightingAlly != null) {
            SetTarget(fightingAlly, TargetReason.AllyAssist);
            return;
        }

        // 8. No threats → follow hero
        SetTarget(GetRallyTarget(), TargetReason.Hero);
    }

    void SetTarget(Transform target, TargetReason reason) {
        if (reason == TargetReason.Enemy)
            _engagedUnits.Add(this);
        else
            _engagedUnits.Remove(this);

        _currentTarget = target;
        _targetReason = reason;
        _currentTargetHealth = target != null ? target.GetComponent<Health>() : null;
        _unit.SetTarget(target);
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
            case TargetReason.Hero:
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

    Transform GetRallyTarget() {
        if (_rallyWaypoint == null) return _heroTransform;

        if (_rallyResting) {
            if (Time.time < _rallyRestUntil) return null;
            _rallyResting = false;
            RandomizeRallyPosition();
            return _rallyWaypoint;
        }

        float dist = Vector2.Distance(transform.position, _rallyWaypoint.position);
        if (dist < _rallyArrivalThreshold) {
            _rallyResting = true;
            _rallyRestUntil = Time.time + Random.Range(rallyRestInterval.x, rallyRestInterval.y);
            return null;
        }

        return _rallyWaypoint;
    }

    Transform FindNearestEnemy() {
        int count = Physics2D.OverlapCircle(transform.position, enemyDetectRadius, _contactFilter, _overlapResults);
        if (count == 0) return null;

        Transform nearest = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < count; i++) {
            if (_overlapResults[i] == null) continue;

            Health health = _overlapResults[i].GetComponent<Health>();
            if (health != null && health.CurrentHealth <= 0) continue;

            float dist = Vector2.Distance(transform.position, _overlapResults[i].transform.position);
            if (dist < minDistance) {
                minDistance = dist;
                nearest = _overlapResults[i].transform;
            }
        }

        return nearest;
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

    void UpdateSpeedBoost() {
        if (_movement == null) return;

        bool farFromHero = _heroTransform != null
            && Vector2.Distance(transform.position, _heroTransform.position) > heroSpeedBoostDistance;

        bool farFromWaypoint = _rallyWaypoint != null
            && Vector2.Distance(transform.position, _rallyWaypoint.position) > heroSpeedBoostDistance;

        bool shouldBoost = (_targetReason == TargetReason.Hero || _targetReason == TargetReason.HeroDefend)
            && (farFromHero || farFromWaypoint);

        if (shouldBoost && !_speedBoosted) {
            _movement.WalkSpeed = _baseWalkSpeed * heroSpeedMultiplier;
            _speedBoosted = true;
        } else if (!shouldBoost && _speedBoosted) {
            _movement.WalkSpeed = _baseWalkSpeed;
            _speedBoosted = false;
        }
    }

    public void OnMMEvent(UnitActionEvent e) {
        if (!_active || e.Type != UnitActionType.Hit || e.Unit == null || e.Unit == _unit) return;

        if (e.Unit.Data != null && e.Unit.Data.type == UnitType.Hero) {
            _heroLastHitTime = Time.time;
        } else {
            float distance = Vector2.Distance(transform.position, e.Unit.transform.position);
            if (distance <= allyDefendRadius) {
                _lastHitAllyTransform = e.Unit.transform;
                _allyLastHitTime = Time.time;
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

        if (_heroUnit != null)
            _heroUnit.OnDeath -= OnHeroDeath;

        if (_rallyWaypoint != null)
            Destroy(_rallyWaypoint.gameObject);
    }
}
