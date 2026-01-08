using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using Sirenix.Utilities;
using UnityEngine;

public struct EnemyDeathEvent {
    public EventStage Stage { get; private set; }
    public Enemy Enemy { get; private set; }

    static EnemyDeathEvent e;
    public static void Trigger(EventStage stage, Enemy enemy) {
        e.Stage = stage;
        e.Enemy = enemy;
        MMEventManager.TriggerEvent(e);
    }
}

public class Enemy : ItemDropRequester {
    public delegate void EnemyEventHandler(Enemy enemy);
    public event EnemyEventHandler OnDead;

    [SerializeField] protected EnemyData data;
    [SerializeField] protected int citadelDamage = 10;
    [SerializeField] protected WeaponManager weaponManager; 
    [SerializeField] protected SpineSkeletonModel spine;
    [SerializeField] protected SpriteRenderer shadow;

    public string EnemyName => gameObject.name;
    public EnemyType Type => data != null ? data.Type : EnemyType.Normal;
    public float CitadelDamage => citadelDamage;

    protected Character _character;
    protected AIBrain _brain;
    protected CharacterMovement _movement;
    protected CharacterHandleWeapon _handleWeapon;
    protected Rigidbody2D _rb;
    protected Health _health;
    protected MMHealthBar _healthBar;
    protected float _speed;
    protected float _healthAmount;
    protected bool _initialized;
    protected float _citadelDamageAmount;
    protected bool _requestLootDrop;

    protected virtual void Initialize() {
        if (!_initialized) {
            _rb = GetComponent<Rigidbody2D>();
            _character = GetComponent<Character>();
            if (_character != null) {
                _movement = _character.FindAbility<CharacterMovement>();
                _handleWeapon = _character.FindAbility<CharacterHandleWeapon>();
                _health = _character.CharacterHealth;
                _health.OnDeath += OnDeath;
                _health.OnHit += OnHit;
                _healthBar = _character.GetComponent<MMHealthBar>();
                _brain = _character.CharacterBrain;
                if (_movement != null)
                    _speed = _movement.WalkSpeed;
                if (_handleWeapon != null)
                    _handleWeapon.OnWeaponChange += OnWeaponChanged;
                if (_character != null)
                    _character.MovementState.OnStateChange += OnMovementStateChanged;
            }
            _initialized = true;
        }
    }

    protected virtual void OnEnable() { }

    protected virtual void OnDisable() {
        StopAllCoroutines();
    }

    public void SetHealthbarVisibility(bool value) {
        if (_healthBar != null)
            _healthBar.SetCompleteVisibility(value);
    }

    protected virtual void OnHit() {
        if (spine != null)
            spine.PlayAnimation(AnimationState.Hit);
    }
    protected virtual void OnDeath() {
        if (_requestLootDrop)
            RequestDrop();
        if (shadow != null)
            shadow.enabled = false;
        if (spine != null)
            spine.PlayAnimation(AnimationState.Dead);
        if (_rb != null)
            _rb.linearVelocity = Vector2.zero;     
        OnDead?.Invoke(this);
        EnemyDeathEvent.Trigger(EventStage.End, this);
    }

    public void Kill() {
        _requestLootDrop = false;
        if (_health != null)
            _health.Kill();
    }

    public virtual void Setup(Dictionary<Attribute, float> values, Transform target, bool healthbarVisible) {
        Initialize();

        SetHealthbarVisibility(healthbarVisible);

        _requestLootDrop = true;

        if (_brain != null)
            _brain.Target = target;

        if (_health != null) {
            if (_healthAmount == 0)
                _healthAmount = _health.MaximumHealth;
            _health.MaximumHealth = values.GetValueOrDefault(Attribute.Health, 1) * _healthAmount;
            _health.InitialHealth = _health.MaximumHealth;
            _health.ResetHealthToMaxHealth();
            if (_character != null && _character.ConditionState.CurrentState == CharacterStates.CharacterConditions.Dead)
                _health.Revive();
        }

        float powerMultiplier = values.GetValueOrDefault(Attribute.Power, 1);
        StartCoroutine(SetupWeaponCo(powerMultiplier));    

        var damageOnTouchList = GetComponentsInChildren<DamageOnTouchController>();
        damageOnTouchList.ForEach(t => SetupDamageOnTouch(t, powerMultiplier));

        if (weaponManager != null)
            weaponManager.Setup(powerMultiplier);
        _citadelDamageAmount = citadelDamage * powerMultiplier;

        float speedMultiplier = values.GetValueOrDefault(Attribute.Speed, 1);
        if (_movement != null)
            _movement.WalkSpeed = _speed * speedMultiplier;

        if (shadow != null)
            shadow.enabled = true;
        
        if (spine != null)
            spine.PlayAnimation(AnimationState.Appear);
    }

    //This is needed because otherwise enemy weapon won't be accessable inside Setup function
    IEnumerator SetupWeaponCo(float powerMultiplier) {
        yield return null;
        if (_handleWeapon != null && _handleWeapon.CurrentWeapon != null && _handleWeapon.CurrentWeapon.WeaponState != null) {
            _handleWeapon.CurrentWeapon.WeaponState.ChangeState(Weapon.WeaponStates.WeaponIdle);
            if (_handleWeapon.CurrentWeapon is MeleeWeapon meleeWeapon && meleeWeapon.ExistingDamageArea != null)
                SetupDamageOnTouch(meleeWeapon.ExistingDamageArea.GetComponent<DamageOnTouchController>(), powerMultiplier);
            else if (_handleWeapon.CurrentWeapon is EnemyProjectileWeapon projectileWeapon)
                projectileWeapon.Setup(powerMultiplier);
                    
            OnWeaponChanged();
        }
    }

    void SetupDamageOnTouch(DamageOnTouchController controller, float mulptiplier) {
        if (controller != null)
            controller.Setup(mulptiplier);
    }

    protected virtual void OnMovementStateChanged() {
        if (spine != null) {
            var state = _character.MovementState.CurrentState;
            if (state == CharacterStates.MovementStates.Idle)
                spine.PlayAnimation(AnimationState.Idle);
            else if (state == CharacterStates.MovementStates.Walking)
                spine.PlayAnimation(AnimationState.Walk);
        }
    }

    protected virtual void OnWeaponChanged() {
        Weapon currentWeapon = _handleWeapon.CurrentWeapon;            
        currentWeapon.transform.localScale = Vector3.one;
        currentWeapon.WeaponState.OnStateChange -= OnWeaponStateChanged;
        currentWeapon.WeaponState.OnStateChange += OnWeaponStateChanged;
    }

    protected virtual void OnWeaponStateChanged() {
        if (spine != null)
            if (_handleWeapon.CurrentWeapon.WeaponState.CurrentState == Weapon.WeaponStates.WeaponStart)
                spine.PlayAnimation(AnimationState.Attack);
    }
}
