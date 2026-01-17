using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;
using Sirenix.Utilities;

public enum FullProgressSpawnAction { Continue, Stop }
public enum EnemySpawnEventType { Started, InProgress, Finished, Canceled, Cleared }

[System.Flags]
public enum SpawnSide { None, Right = 1, Bottom = 2, Left = 4, Top = 8 }

public struct EnemySpawnTriggerEvent {
    public string Name { get; private set; }
    public SpawnShape SpawnShape { get; private set; }
    public Vector2 Position { get; private set; }
    public Vector2 Size { get; private set; }
    public Quaternion Rotation { get; private set; }
    public bool FollowTarget { get; private set; }

    static EnemySpawnTriggerEvent e;
    public static void Trigger(string name, SpawnShape SpawnShape, Vector2 position, Vector2 size, Quaternion rotation, bool followTarget) {
        e.Name = name;
        e.SpawnShape = SpawnShape;
        e.Position = position;
        e.Size = size;
        e.Rotation = rotation;
        e.FollowTarget = followTarget;
        MMEventManager.TriggerEvent(e);
    }
}

public struct EnemySpawnEvent {
    public EnemySpawnEventType EventType { get; private set; }
    public float Progress { get; private set; }
    static EnemySpawnEvent e;
    public static void Trigger(EnemySpawnEventType eventType, float progress = 0) {
        e.EventType = eventType;
        e.Progress = progress;
        MMEventManager.TriggerEvent(e);
    }   
}

public class EnemyManager : SimplePoolUser, MMEventListener<EnemySpawnTriggerEvent>, 
                            MMEventListener<SkillLevelUpEvent>, MMEventListener<LevelLoadEvent> {
    [Header("Enemy Manager")]
    [SerializeField] GameNotificationTrigger notificationTrigger;
    [SerializeField] Transform defaultTarget;

    [Header("Spawning")]
    [SerializeField] Collider2D fillBounds;
    [SerializeField] FullProgressSpawnAction fullProgressAction = FullProgressSpawnAction.Continue;
    [SerializeField] SpawnShape fillShape = SpawnShape.OnBox;
    [SerializeField] LayerMask spawnBlock;
    [SerializeField] LayerMask targetLayers;
    [SerializeField] int maxSpawnAttempts = 100;

    [Header("Removing")]
    [SerializeField] bool removeOffscreen;
    [MMCondition("removeOffscreen", true)]
    [SerializeField] Vector2 removerOffset;
    [MMCondition("removeOffscreen", true)]
    [SerializeField] Vector2 removerSize;
    [MMCondition("removeOffscreen", true)]
    [SerializeField] BoxCollider2D topRemover;
    [MMCondition("removeOffscreen", true)]
    [SerializeField] BoxCollider2D bottomRemover;
    [MMCondition("removeOffscreen", true)]
    [SerializeField] BoxCollider2D leftRemover;
    [MMCondition("removeOffscreen", true)]
    [SerializeField] BoxCollider2D rightRemover;

    int EnemiesCount => _enemies.Count;
    public int MaxFillCount { get; set; }
    public int MaxSimulteneousCount { get; set; }
    bool CheckPositions => spawnBlock != 0 || targetLayers != 0;

    EnemyGenerationData _data;
    bool _dataOK = false;
    bool _proportionsOK = false;
    int _fillCount;
    int _maxNow;
    int _maxSimultanious;
    float _nextFillTime;
    int _fillTimingsFinal;
    int _fillTimeIndex;
    Dictionary<GameObject, float> _fillEnemyShares = new Dictionary<GameObject, float>();
    List<float> _fillTimings = new List<float>();
    List<float> _waveTimings = new List<float>();
    Dictionary<Attribute, float> _multipliers = new Dictionary<Attribute, float>();
    Dictionary<Attribute, float> _multipliersFromSkills = new Dictionary<Attribute, float>();
    List<Enemy> _enemies = new List<Enemy>();
    LayerMask _scanLayers;
    bool _spawning;
    int _duration;
    float _time;
    int _timeInSeconds;
    float _progress;
    bool _showHealthBars = true;
    //Vector2 _spawnAngleRange = new Vector2(0, 360);

    public void SetHealthbarVisibility(bool value) {
        if (_showHealthBars != value) {
            _showHealthBars = value;
            _enemies.ForEach(t => t.SetHealthbarVisibility(value));
        }
    }

    public void StartSpawn(LevelData levelData) {
        _data = levelData.enemyData;
        _dataOK = _data != null && _data.waves != null && _data.triggers != null;
        _proportionsOK = _dataOK && _data.enemyProportions != null && _data.enemyProportions.Count > 0;
        _fillTimings.Clear();
        _fillEnemyShares.Clear();
        _waveTimings.Clear();
        _nextFillTime = 0;
        _duration = levelData.duration;
        _progress = 0;
        _timeInSeconds = 0;
        _time = 0;
        EnemySpawnEvent.Trigger(EnemySpawnEventType.Started);

        if (_dataOK) {
            MaxFillCount = _data.maxFillCount;
            MaxSimulteneousCount = _data.maxSimulteneousCount;

            _waveTimings.AddRange(_data.waves.Keys);
            _waveTimings.Sort();

            _data.triggers.ForEach(t => t.Value.Reset());

            if (_proportionsOK) {
                _fillTimeIndex = 0;
                _fillTimings.AddRange(_data.enemyProportions.Keys);
                _fillTimings.Sort();
                _fillTimingsFinal = _fillTimings.Count - 1;
                if (_fillTimings[0] == 0)
                    UpdateEnemyShares();                
                else
                    _nextFillTime = _fillTimings[0];
            }
            _spawning = true;
            //UpdateFillBounds();
        } else
            Debug.LogError("EnemyManager StartSpawn: data is not OK");
    }

    void FixedUpdate() {
        if (_dataOK && _spawning) {
            float deltaTime = Time.fixedDeltaTime;
            _time += deltaTime;
            int intTime = (int)Mathf.Floor(_time);

            if (_timeInSeconds < intTime) {
                _timeInSeconds++;
                _progress = Mathf.Clamp01((float)_timeInSeconds / _duration);

                if (_progress < 1) {
                    if (_proportionsOK) {
                        _fillCount = (int)(_data.enemyCountCurve.Evaluate(_progress) * MaxFillCount);
                        UpdateEnemyShares();
                        SpawnFill();
                    }
                    CheckWaves();
                    EnemySpawnEvent.Trigger(EnemySpawnEventType.InProgress, _progress);
                } else if (fullProgressAction == FullProgressSpawnAction.Stop) {
                    _spawning = false;
                    EnemySpawnEvent.Trigger(EnemySpawnEventType.Finished, _progress);
                    if (_enemies.Count == 0)
                        EnemySpawnEvent.Trigger(EnemySpawnEventType.Cleared, _progress);
                }
            }
        }
    }

    public void Initialize(Dictionary<Attribute, float> multipliers, Dictionary<GameObject, int> poolData) {
        _multipliersFromSkills.Clear();
        _enemies.Clear();
        _scanLayers = targetLayers | spawnBlock;
        
        /*f (spawnSides != SpawnSide.None) {
            List<SpawnSide> sideList = new List<SpawnSide> { SpawnSide.Right, SpawnSide.Bottom, SpawnSide.Left, SpawnSide.Top };
            _spawnAngleRange = new Vector2(360, 0);
            for (int i = 0; i < 4; i++) {
                if (spawnSides.HasFlag(sideList[i])) {
                    _spawnAngleRange.x = Mathf.Min(_spawnAngleRange.x, -45 + 90 * i);
                    _spawnAngleRange.y = Mathf.Max(_spawnAngleRange.y, 45 + 90 * i);
                }
            }
        } else
            Debug.LogError("Enemy Manager Initialize: SpawnSides is None, fill and wave OnBox spawning won't happen");*/

        if (multipliers != null)
            _multipliers = multipliers;
        
        if (poolData != null) {
            poolerObjects = poolData;
            SetupPools();
        }

        //UpdateFillBounds();
    }

    public void UpdateFillBounds() {
        if (fillBounds != null) {
            Camera camera = Camera.main;
            Vector2 size = camera.GetScreenSize() * 1.1f;
            //fillBounds.size = size;

            if (topRemover != null && bottomRemover != null && leftRemover != null && rightRemover != null) {
                bool useRemover = _data != null;// && _data.teleportEnemies;

                if (useRemover) {
                    topRemover.offset = new Vector2(0, size.y * 0.5f + removerOffset.y + removerSize.y * 0.5f);
                    topRemover.size = new Vector2(size.x + 2 * removerOffset.x + 2 * removerSize.y, removerSize.y);
                    bottomRemover.offset = -topRemover.offset;
                    bottomRemover.size = topRemover.size;

                    rightRemover.offset = new Vector2(size.x * 0.5f + removerOffset.x + removerSize.x * 0.5f, 0);
                    rightRemover.size = new Vector2(removerSize.x, size.y + 2 * removerOffset.y + 2 * removerSize.x);
                    leftRemover.offset = - rightRemover.offset;
                    leftRemover.size = rightRemover.size;
                }

                topRemover.gameObject.SetActive(useRemover);
                bottomRemover.gameObject.SetActive(useRemover);
                leftRemover.gameObject.SetActive(useRemover);
                rightRemover.gameObject.SetActive(useRemover);
            } else
                Debug.LogWarning("EnemyManager UpdateFillBounds: one of remover colliders is NULL. Removing off screen units won't happen.");
        } else
            Debug.LogError("EnemyManager UpdateFillBounds: FillBounds is NULL");
    }

    protected override bool Suitable(GameObject poolerObject) {
        if (poolerObject.GetComponent<Enemy>() == null)
            return false;
        return base.Suitable(poolerObject);
    }

    void UpdateEnemyShares() {
        if (_fillTimeIndex <= _fillTimingsFinal && _progress >= _nextFillTime) {
            if (_fillTimeIndex < _fillTimingsFinal)
                _nextFillTime = _fillTimings[_fillTimeIndex + 1];
                
            float fillTime = _fillTimings[_fillTimeIndex];
            if (_data.enemyProportions.ContainsKey(fillTime))
                _fillEnemyShares = new Dictionary<GameObject, float>(_data.enemyProportions[fillTime]);
            
            _fillTimeIndex++;
        }
    }

    void SpawnFill() {
        if (_fillEnemyShares != null && _fillEnemyShares.Count > 0) {
            _maxNow = _fillCount - EnemiesCount;
            _maxSimultanious = MaxSimulteneousCount - EnemiesCount;
            if (_fillCount > 0 && _maxSimultanious > 0) {
                int slots = Mathf.Min(_fillCount, _maxSimultanious);
                
                for (int i = 0; i < slots; i++) {
                    GameObject prefab = SelectEnemyPrefab(_fillEnemyShares);
                    if (prefab != null) {
                        bool success;
                        Vector2 position = CalculateFillEnemyPosition(out success);
                        if (success) {
                            GameObject enemy = Spawn(prefab, position, Quaternion.identity);
                            SetupEnemy(enemy, null);
                        }
                    } else
                        Debug.Log("EnemyManager SpawnFill: SelectEnemyPrefab returned NULL");
                }
            }
        }
    }

    GameObject SelectEnemyPrefab(Dictionary<GameObject, float> shares) {
        float random = Random.value;
        float accumulative = 0;
        foreach (KeyValuePair<GameObject, float> share in shares) {
            accumulative += share.Value;
            if (random <= accumulative)
                return share.Key;
        }
        return null;
    }


    //
    // Getting random position for different methods
    //
    Vector2 RandomPositionInsideBox(Vector2 position, Vector2 size) {
        return new Vector2(Random.Range(0, size.x), Random.Range(0, size.y)) - size * 0.5f + position;
    }

    Vector2 RandomPositionInsideCircle(Vector2 position, Vector2 size) {
        Vector2 pos = Random.insideUnitCircle * 0.5f;
        return new Vector2(pos.x * size.x, pos.y * size.y) + position;
    }

    Vector2 RandomPositionOnCircle(Vector2 position, Vector2 size) {
        Vector2 pos = Random.insideUnitCircle.normalized * 0.5f;
        return new Vector2(pos.x * size.x, pos.y * size.y) + position;
    }

    Vector2 RandomPositionOnBox(Vector2 position, Vector2 size) {
        return calCoor(Random.Range(0, 360 /*_spawnAngleRange.x, _spawnAngleRange.y*/), size.x, size.y) * 0.5f + position;
    }

    Vector2 calCoor(float theta, float a, float b) {
        float rad = theta * Mathf.PI / 180f;
        float x, y;
        float tan = Mathf.Tan(rad);
        if (Mathf.Abs(tan) > b / a) {
            x = tan > 0 ? a : -a;
            y = b / tan;
        } else {
            x = a * tan;
            y = tan < 0 ? b : -b;
        }
        return new Vector2(x, y);
    }

    
    bool CheckSpawnPosition(Vector2 position) {
        bool result = false;
        Collider2D[] colliders = Physics2D.OverlapCircleAll(position, 1f, _scanLayers);
        foreach (Collider2D collider in colliders) {
            int layer = 1 << collider.gameObject.layer;
            if ((layer & spawnBlock) != 0)
                return false;
            else if ((layer & targetLayers) != 0)
                result = true;
        }
        return result;
    }

    Vector2 CalculateFillEnemyPosition(out bool success) {
        if (fillBounds != null) {
            Vector2 position = fillBounds.bounds.center;
            Vector2 size = fillBounds.bounds.size;
            int attempt = 0;
            while (attempt <= maxSpawnAttempts) {
                Vector2 pos = RandomPositionInsideBox(position, size);
                if (!CheckPositions || CheckSpawnPosition(pos)) {
                    success = true;
                    return pos;
                }
                attempt++;
            }
        }
        success = false;
        return Vector2.zero;
    }

    Vector2 CalculateWaveEnemyPosition(SpawnShape shape, Vector2 position, Vector2 size, out bool success) {
        int attempt = 0;
        while (attempt <= maxSpawnAttempts) {
            Vector2 pos = Vector2.zero;

            switch (shape) {
                case SpawnShape.InsideBox:
                    pos = RandomPositionInsideBox(position, size);
                    break;
                case SpawnShape.OnBox:
                    pos = RandomPositionOnBox(position, size);
                    break;
                case SpawnShape.OnCircle:
                    pos = RandomPositionOnCircle(position, size);
                    break;
                case SpawnShape.InsideCircle:
                    pos = RandomPositionInsideCircle(position, size);
                    break;
                default:
                    break;
            }

            if (!CheckPositions || CheckSpawnPosition(pos)) {
                success = true;
                return pos;
            }
            attempt++;
        }
        success = false;
        return Vector2.zero;
    }

    void CheckWaves() {
        if (_waveTimings.Count > 0) {
            float waveTime = _waveTimings[0];
            if (_progress >= waveTime) {
                _waveTimings.RemoveAt(0);
                StartCoroutine(SpawnWave(_data.waves[waveTime], defaultTarget, Vector2.zero, Vector2.zero, Quaternion.identity));
            }
        }
    }
   
    IEnumerator SpawnWave(EnemyWaveGenerationData data, Transform target, Vector2 size, Vector2 position, Quaternion rotation, SpawnShape spawnShape = SpawnShape.None) {
        if (data != null && data.multipliers != null) {
            int currentWavesCount = 0;
            if (size == Vector2.zero)
                size = data.spawnerSize;
            if (target != null)
                position = target.position;

            position += data.spawnerDistance;

            if (notificationTrigger != null && data.notify)
                notificationTrigger.Trigger();

            var enemies = data.shares;
            int amount = data.amount;
            int wavesCount = data.wavesCount;
            float interval = data.waveInterval;
            if (spawnShape == SpawnShape.None)
                spawnShape = data.spawnShape;
        
            if (enemies != null && enemies.Count > 0) {
                while (_spawning && currentWavesCount < wavesCount) {
                    int result = 0;
                    
                    if (data.shareSelection == WaveSelectionMethod.Sequential) {
                        result = data.IncreaseUseCount();
                    } else if (data.shareSelection == WaveSelectionMethod.Random) {
                        int randomCount = Random.Range(5, 20);
                        for (int i = 0; i <= randomCount; i++)
                            result = Random.Range(0, enemies.Count);
                    }

                    var enemyShares = enemies[result];
                    if (enemyShares == null || enemyShares.Count == 0)
                        continue;
                    
                    for (int i = 0; i < amount; i++) {
                        GameObject prefab = SelectEnemyPrefab(enemyShares);
                        if (prefab != null) {
                            bool success = true;
                            Vector2 enemyPosition = position;
                            if (spawnShape == SpawnShape.FillBounds)
                                enemyPosition = CalculateFillEnemyPosition(out success);
                            else if (spawnShape > SpawnShape.Point)
                                enemyPosition = CalculateWaveEnemyPosition(spawnShape, position, size, out success);

                            if (success) {
                                GameObject enemy = Spawn(prefab, enemyPosition, rotation);
                                SetupEnemy(enemy, data.multipliers);
                            }
                        } else 
                            Debug.LogFormat("EnemyManager SpawnWave: SelectEnemyPrefab returned NULL");
                    }

                    currentWavesCount++;
                    yield return new WaitForSeconds(interval);
                }
            }
        } else 
            Debug.LogFormat("EnemyManager SpawnWave: EnemyWaveMultiplyData is not OK");
    }

    protected virtual void SetupEnemy(GameObject instance, Dictionary<Attribute, float> additional) {
        if (instance != null) {
            Enemy enemy = instance.GetComponent<Enemy>();
            Dictionary<Attribute, float> combined = new Dictionary<Attribute, float>(_multipliers);
            if (additional != null && additional.Count > 0)
                foreach (var pair in additional)
                    combined[pair.Key] = combined.GetValueOrDefault(pair.Key, 1) * pair.Value;
            if (_multipliersFromSkills.Count > 0)
                foreach (var pair in _multipliersFromSkills)
                    combined[pair.Key] = combined.GetValueOrDefault(pair.Key, 1) * pair.Value;
            enemy.Setup(combined, defaultTarget, _showHealthBars);
            enemy.OnDead += OnEnemyDead;
            _enemies.Add(enemy);
        } else
            Debug.LogWarning("EnemyManager SetupEnemy: enemy gameobject or player is NULL.");
    }

    public void OnEnemyDead(Enemy enemy) {
        enemy.OnDead -= OnEnemyDead;
        _enemies.Remove(enemy);
        if ((_progress >= 1 || !_spawning) && _enemies.Count == 0)
            StartCoroutine(LevelClearedCo());
    }

    IEnumerator LevelClearedCo() {
        yield return null;
        EnemySpawnEvent.Trigger(EnemySpawnEventType.Cleared, _progress);
    }

    public void OnMMEvent(EnemySpawnTriggerEvent e) {
        if (_dataOK && _spawning) {
            var triggers = _data.triggers;
            var generationData = triggers.GetValueOrDefault(e.Name, null);
            if (generationData != null)
                StartCoroutine(SpawnWave(generationData, e.FollowTarget ? defaultTarget : null, e.Size, e.Position, e.Rotation, e.SpawnShape));
        }
    }

    public void OnMMEvent(SkillLevelUpEvent e) {
        if (e.Skill != null && e.Skill.Data != null) {
            if (e.Skill.Type == SkillType.Support && e.Skill.Target == SkillTarget.Enemies) {
                SkillLevel current = e.Skill.CurrentLevel;
                if (current != null)
                    _multipliersFromSkills[e.Skill.Attribute] = current.GetParameterValue(SkillParameterType.Amount, 1);
                else
                    Debug.LogErrorFormat("EnemyManager SkillEvent: current SkillLevel is NULL for {0}", e.Skill.name);
            }
        }
    }

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start && e.Data != null && e.Data.enemyMultipliers != null) {
            Initialize(e.Data.enemyMultipliers, e.Data.GetEnemyPoolData());
            StartSpawn(e.Data);
        }
    }

    /*public void SetHealthbarVisibiity(bool visible) {
        _showHealthBars = visible;

        if (_player != null) {
            MMHealthBar playerHealthBar = _player.GetComponent<MMHealthBar>();
            if (playerHealthBar != null)
                playerHealthBar.SetCompleteVisibility(visible);
        }

        _enemies.ForEach(t => t.SetHealthbarVisibility(visible));
    }*/

    void OnEnable() {
        this.MMEventStartListening<LevelLoadEvent>();
        this.MMEventStartListening<EnemySpawnTriggerEvent>();
        this.MMEventStartListening<SkillLevelUpEvent>();

    }

    void OnDisable() {
        this.MMEventStopListening<LevelLoadEvent>();
        this.MMEventStopListening<EnemySpawnTriggerEvent>();
        this.MMEventStopListening<SkillLevelUpEvent>();
    }
}
