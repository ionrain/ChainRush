using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Project-bridge adapter that mirrors Unit/Enemy lifecycle into the kernel entity backbone.
/// Keeps existing gameplay managers as behavior owners; this is lifecycle wiring only.
/// </summary>
public sealed class GameEntityBackboneBridge : MonoBehaviour
{
    [Header("Game Managers")]
    [SerializeField] UnitManager unitManager;
    [SerializeField] EnemyManager enemyManager;

    [Header("Debug")]
    [SerializeField] bool logLifecycleEvents;
    [SerializeField] int registeredEntityCount;
    [SerializeField] int createdEventsCount;
    [SerializeField] int destroyedEventsCount;

    public InMemoryEntityLifecycleService Lifecycle { get; private set; }
    public InMemoryEntityRegistry Registry { get; private set; }
    public InMemoryEntityFactory Factory { get; private set; }
    public InMemoryEntityViewBinder ViewBinder { get; private set; }

    readonly Dictionary<int, EntityId> _entityByInstanceId = new Dictionary<int, EntityId>();

    void Awake()
    {
        InitializeBackbone();
    }

    void InitializeBackbone()
    {
        createdEventsCount = 0;
        destroyedEventsCount = 0;

        Lifecycle = new InMemoryEntityLifecycleService();
        Lifecycle.EntityCreated += OnEntityCreated;
        Lifecycle.EntityDestroyed += OnEntityDestroyed;
        Registry = new InMemoryEntityRegistry();
        Factory = new InMemoryEntityFactory(Registry, Lifecycle);
        ViewBinder = new InMemoryEntityViewBinder();
        _entityByInstanceId.Clear();
        UpdateDebugState();
    }

    void OnEnable()
    {
        InitializeBackbone();

        if (unitManager != null)
        {
            unitManager.UnitSpawned += OnUnitSpawned;
            unitManager.UnitDespawned += OnUnitDespawned;
        }

        if (enemyManager != null)
        {
            enemyManager.EnemySpawned += OnEnemySpawned;
            enemyManager.EnemyDespawned += OnEnemyDespawned;
        }

        RebuildFromCurrentManagers();
    }

    void OnDisable()
    {
        if (unitManager != null)
        {
            unitManager.UnitSpawned -= OnUnitSpawned;
            unitManager.UnitDespawned -= OnUnitDespawned;
        }

        if (enemyManager != null)
        {
            enemyManager.EnemySpawned -= OnEnemySpawned;
            enemyManager.EnemyDespawned -= OnEnemyDespawned;
        }
    }

    public bool TryGetEntityId(Component component, out EntityId entityId)
    {
        if (component != null && _entityByInstanceId.TryGetValue(component.gameObject.GetInstanceID(), out entityId))
            return true;

        entityId = EntityId.None;
        return false;
    }

    void RebuildFromCurrentManagers()
    {
        if (unitManager != null)
        {
            IReadOnlyList<Unit> units = unitManager.Units;
            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (unit != null)
                    RegisterUnit(unit);
            }
        }

        if (enemyManager != null)
        {
            IReadOnlyList<Enemy> enemies = enemyManager.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy != null)
                    RegisterEnemy(enemy);
            }
        }

        UpdateDebugState();
    }

    void OnUnitSpawned(Unit unit)
    {
        RegisterUnit(unit);
    }

    void OnUnitDespawned(Unit unit)
    {
        UnregisterObject(unit == null ? null : unit.gameObject);
    }

    void OnEnemySpawned(Enemy enemy)
    {
        RegisterEnemy(enemy);
    }

    void OnEnemyDespawned(Enemy enemy)
    {
        UnregisterObject(enemy == null ? null : enemy.gameObject);
    }

    void RegisterUnit(Unit unit)
    {
        if (unit == null)
            return;

        string archetype = BuildUnitArchetype(unit);
        RegisterObject(unit.gameObject, archetype, "unit");
    }

    void RegisterEnemy(Enemy enemy)
    {
        if (enemy == null)
            return;

        string archetype = BuildEnemyArchetype(enemy);
        RegisterObject(enemy.gameObject, archetype, "enemy");
    }

    void RegisterObject(GameObject gameObject, string archetype, string kind)
    {
        if (gameObject == null)
            return;

        int instanceId = gameObject.GetInstanceID();
        if (_entityByInstanceId.ContainsKey(instanceId))
            return;

        EntityId entityId = Factory.Create(new EntityArchetypeId(archetype));
        if (entityId.IsNone)
            return;

        _entityByInstanceId[instanceId] = entityId;
        ViewBinder.Bind(entityId, new EntityViewId(instanceId));

        if (Registry.TryGet(entityId, out IEntityModel model) && model is EntityState state)
        {
            state.AddTag(kind);
            state.SetTrait("unity.instanceId", instanceId.ToString());
            state.SetTrait("archetype", archetype);
        }

        UpdateDebugState();

        if (logLifecycleEvents)
            Debug.Log($"[GameEntityBackboneBridge] Registered {kind} '{gameObject.name}' -> {entityId}");
    }

    void UnregisterObject(GameObject gameObject)
    {
        if (gameObject == null)
            return;

        int instanceId = gameObject.GetInstanceID();
        if (!_entityByInstanceId.TryGetValue(instanceId, out EntityId entityId))
            return;

        Factory.Destroy(entityId);
        _entityByInstanceId.Remove(instanceId);
        UpdateDebugState();

        if (logLifecycleEvents)
            Debug.Log($"[GameEntityBackboneBridge] Unregistered '{gameObject.name}' -> {entityId}");
    }

    static string BuildUnitArchetype(Unit unit)
    {
        UnitData data = unit.Data;
        if (data == null)
            return "unit.unknown";

        string type = data.type.ToString().ToLowerInvariant();
        string unitClass = data.unitClass.ToString().ToLowerInvariant();
        return $"unit.{type}.{unitClass}";
    }

    static string BuildEnemyArchetype(Enemy enemy)
    {
        string enemyType = enemy.Type.ToString().ToLowerInvariant();
        return $"enemy.{enemyType}";
    }

    void OnEntityCreated(EntityId entityId)
    {
        createdEventsCount++;
        UpdateDebugState();
    }

    void OnEntityDestroyed(EntityId entityId)
    {
        destroyedEventsCount++;
        UpdateDebugState();
    }

    void UpdateDebugState()
    {
        registeredEntityCount = _entityByInstanceId.Count;
    }
}
