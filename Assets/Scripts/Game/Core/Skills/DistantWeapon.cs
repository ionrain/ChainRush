using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class DistantWeapon : ProjectileWeapon, MMEventListener<TargetListRequestEvent> {
    public delegate void ProjectileEvent(DistantWeapon weapon, Projectile projectile);
    public event ProjectileEvent OnProjectileSpawned;

    [MMInspectorGroup("Distant Weapon", true, 30)]
    [SerializeField] bool scaleOnlyCollider;
    [SerializeField] bool randomRotation;

    protected SkillData _skillData;
    protected List<TypedDamage> _typedDamages = new List<TypedDamage>();
    protected List<Transform> _spawnPositions = new List<Transform>();
    protected int _spawnIndex;
    protected float _baseDamage;
    protected float _lifetime;
    protected float _speed;
    protected float _size;
    protected float _knockback;
    protected string _skillName;
    protected float _orbitSpeed;
    protected float _acceleration;
    

    public virtual bool Setup(SkillData data, int level, Dictionary<DamageType, float> typedDamageValues) {
        if (data != null && typedDamageValues != null) {
            _skillData = data;
            _skillName = _skillData.name;
            _baseDamage = _skillData.GetParameterValue(SkillParameterType.Amount, level, 0);
            _lifetime = _skillData.GetParameterValue(SkillParameterType.Lifetime, level, 0);
            _speed = _skillData.GetParameterValue(SkillParameterType.Speed, level, 0);
            _size = _skillData.GetParameterValue(SkillParameterType.Size, level, 0);
            _knockback = _skillData.GetParameterValue(SkillParameterType.Knockback, level, 0);
            _orbitSpeed = _skillData.GetParameterValue(SkillParameterType.OrbitSpeed, level, 0);
            _acceleration = _skillData.GetParameterValue(SkillParameterType.Acceleration, level, 0);
            
            ProjectilesPerShot = (int)_skillData.GetParameterValue(SkillParameterType.Projectiles, level, 1);
            float spread = _skillData.GetParameterValue(SkillParameterType.Spread, level, 0);
            float distance = _skillData.GetParameterValue(SkillParameterType.Radius, level, 0);

            if (distance > 0 && spread > 0) {
                int spawnCount = _spawnPositions.Count;
                if (spawnCount < ProjectilesPerShot) {
                    for (int i = spawnCount; i < ProjectilesPerShot; i ++) {
                        GameObject obj = new GameObject("SpawnPosition" + i);
                        obj.transform.SetParent(transform, false);
                        _spawnPositions.Add(obj.transform);
                    }
                }

                for (int i = 0; i < ProjectilesPerShot; i++) {
                    Transform spawnTransform = _spawnPositions[i];
                    spawnTransform.localPosition = new Vector3(distance, 0, 0);
                    spawnTransform.RotateAround(transform.position, Vector3.forward, i * spread);
                }
            }
             
            if (typedDamageValues != null)
                foreach (var pair in typedDamageValues)
                    _typedDamages.ManageEntry(pair.Key, _baseDamage * pair.Value);

            if (_typedDamages.Count > 0) {
                var typedDamage = _typedDamages[0];
                float speedMultiplier = _skillData.GetParameterValue(SkillParameterType.SpeedMultiplier, level, 0);
                float speedChangeDuration = _skillData.GetParameterValue(SkillParameterType.SpeedChangeDuration, level, 0);
                float forcedStateDuration = _skillData.GetParameterValue(SkillParameterType.ForcedStateDuration, level, 0);
                CharacterStates.CharacterConditions forcedState = _skillData.forcedState;

                if (speedMultiplier > 0 && speedChangeDuration > 0) {
                    typedDamage.ApplyMovementMultiplier = true;
                    typedDamage.MovementMultiplier = speedMultiplier;
                    typedDamage.MovementMultiplierDuration = speedChangeDuration;
                }

                if (forcedState != CharacterStates.CharacterConditions.Normal && forcedStateDuration > 0) {
                    typedDamage.ForceCharacterCondition = true;
                    typedDamage.ForcedCondition = forcedState;
                    typedDamage.ForcedConditionDuration = forcedStateDuration;
                }
            }
            return true;
        }
        return false;
    }

    public override IEnumerator ShootRequestCo() {
        if (randomRotation)
            transform.localRotation = Quaternion.AngleAxis(Random.Range(0, 360), Vector3.forward);
        return base.ShootRequestCo();
    }

    public override void DetermineSpawnPosition() {
        if (_skillData != null && _skillData.target == SkillTarget.Enemies && _skillData.targetMode == TargetMode.Random)
            TargetListRequestEvent.Trigger(EventStage.Start, gameObject.name, _skillData.targetMode);
        else 
            base.DetermineSpawnPosition();
    }

    public override GameObject SpawnProjectile(Vector3 spawnPosition, int projectileIndex, int totalProjectiles, bool triggerObjectActivation = true) {        
        GameObject result = base.SpawnProjectile(spawnPosition, projectileIndex, totalProjectiles, triggerObjectActivation);
        if (result != null) {
            result.name = _skillName;
            Projectile projectile = result.GetComponent<Projectile>();
            if (projectile != null) {
                SetupProjectile(projectile);
                OnProjectileSpawned?.Invoke(this, projectile);
            } else
                Debug.LogErrorFormat("DistantWeapon SpawnProjectile: projectile component is NULL for {0}", _skillName);
        }
        return result;
    }

    protected virtual void SetupProjectile(Projectile projectile) {
        if (projectile != null && _skillData != null) {         
            if (_acceleration != 0)
                projectile.Acceleration = _acceleration;   
            if (_lifetime > 0)
                projectile.LifeTime = _lifetime;
            if (_speed != 0)
                projectile.Speed = _speed;
            if (_size > 0 && !scaleOnlyCollider)
                projectile.transform.localScale = new Vector3(_size, _size, _size);

            if (projectile.Speed == 0)
                projectile.transform.rotation = Quaternion.identity; 

            DamageOnTouch damageOnTouch = projectile.TargetDamageOnTouch;
            if (damageOnTouch != null) {
                damageOnTouch.TypedDamages = _typedDamages;
                if (_knockback > 0)
                    damageOnTouch.DamageCausedKnockbackForce.x = _knockback;

                if (_size > 0 && scaleOnlyCollider) {
                    CircleCollider2D collider = damageOnTouch.GetComponent<CircleCollider2D>();
                    if (collider != null && _size > 0)
                        collider.radius = _size;
                }
            }
        }

        int spawnCount = _spawnPositions.Count;
        if (spawnCount > 0 && _spawnIndex >= 0 && _spawnIndex < spawnCount) {
            projectile.transform.position = _spawnPositions[_spawnIndex].position;
            _spawnIndex++;
            if (_spawnIndex >= spawnCount)
                _spawnIndex = 0;
        }

        if (_orbitSpeed > 0) {
            TargetOrbiter orbiter = projectile.GetComponent<TargetOrbiter>();
            if (orbiter != null)
                orbiter.Setup(Owner.transform, _orbitSpeed, _speed);
        }
    }

    public void OnMMEvent(TargetListRequestEvent e) {
        if (e.Stage == EventStage.End && e.RequesterId.Equals(gameObject.name)) {
            if (e.Positions.Count > 0) {
                if (e.TargetMode == TargetMode.Random)
                    SpawnPosition = e.Positions[0];
            }
        }
    }

    protected override void OnEnable() {
        this.MMEventStartListening<TargetListRequestEvent>();
        base.OnEnable();
    }

    protected override void OnDisable() {
        this.MMEventStopListening<TargetListRequestEvent>();
        base.OnDisable();
    }
}
