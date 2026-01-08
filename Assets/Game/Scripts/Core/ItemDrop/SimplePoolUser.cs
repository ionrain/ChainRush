using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;

public class SimplePoolUser : SerializedMonoBehaviour {
    [Header("Simple Pool User")]
    [SerializeField] protected Dictionary<GameObject, int> poolerObjects = new Dictionary<GameObject, int>();
    protected Dictionary<GameObject, MMSimpleObjectPooler> _poolers = new Dictionary<GameObject, MMSimpleObjectPooler>();

    public virtual void SetupPools() {
        foreach (var pair in poolerObjects) {
            GameObject poolerObject = pair.Key;
            if (Suitable(poolerObject)) {
                GameObject pooler = new GameObject("[Pool] " + poolerObject.name);
                pooler.transform.SetParent(transform);

                MMSimpleObjectPooler pool = pooler.AddComponent<MMSimpleObjectPooler>();
                pool.GameObjectToPool = poolerObject;
                pool.PoolCanExpand = true;
                pool.NestWaitingPool = true;
                pool.NestUnderThis = true;
                pool.PoolSize = pair.Value;
                pool.FillObjectPool();

                _poolers.Add(poolerObject, pool);
            } else
                Debug.LogErrorFormat("SimplePoolUser SetupPools: cannot create pool for {0}", poolerObject.name);
        }
    }

    protected virtual bool Suitable(GameObject poolerObject) {
        return poolerObject.GetComponent<MMPoolableObject>() != null;
    }

    public virtual GameObject Spawn(GameObject key, Vector2 position, Quaternion rotation) {
        if (key != null && _poolers.ContainsKey(key)) {
            GameObject instance = _poolers[key].GetPooledGameObject();
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.SetActive(true);
            instance.GetComponent<MMPoolableObject>().TriggerOnSpawnComplete();
            SpawnedSetup(instance);
            return instance;
        }
        return null;
    }

    protected virtual void SpawnedSetup(GameObject instance) { }
}
