using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class Citadel : SerializedMonoBehaviour {
    public delegate void CitadelEvent();
    public event CitadelEvent OnDeath;

    [SerializeField] CitadelData data;
    [SerializeField] LayerMask enemyMask;
    [SerializeField] Dictionary<Vector2Int, AttackSkill> turrets = new();
    

    [Header("Feedbacks")]
    [SerializeField] Transform cellPosition;
    [SerializeField] MMF_Player healFeedback;
    [SerializeField] float healDelay = .75f;
    [SerializeField] MMF_Player bombFeedback;
    [SerializeField] float bombDamageDelay = 1.5f;
    
    Health _health;
    MMHealthBar _healthBar;

    void Start() {
        _health = GetComponent<Health>();
        if (_health != null) {
            _health.MaximumHealth = data.HP;
            _health.InitialHealth = data.HP;
            _health.ResetHealthToMaxHealth();
            _health.OnDeath += OnDead;
        }

        _healthBar = GetComponent<MMHealthBar>();
        
        StartCoroutine(Setup());
    }

    public void SetHealthbarVisibility(bool value) {
        if (_healthBar != null)
            _healthBar.SetCompleteVisibility(value);
    }
    
    void OnDead() {
        foreach (var turret in turrets)
            if (turret.Value != null)
                turret.Value.StopAttack();
        OnDeath?.Invoke();
    }

    IEnumerator Setup() {
        yield return null;
        
        if (data != null && turrets != null) {
            foreach (var turret in turrets) {
                if (turret.Value != null) {
                    if (turret.Value.Weapon == null) {
                        CharacterHandleWeapon characterHandleWeapon = turret.Value.GetComponent<CharacterHandleWeapon>();
                        if (characterHandleWeapon != null && characterHandleWeapon.CurrentWeapon != null)
                            turret.Value.SetWeapon(characterHandleWeapon.CurrentWeapon);
                        else
                            Debug.LogErrorFormat("Citadel Start: CharacterHandleWeapon or CurrentWeapon is NULL for {0}", turret.Key);
                    }

                    TurretData turretData = data.Find(turret.Key.x, turret.Key.y);
                    if (turretData != null)
                        turret.Value.Setup(null, null, new Dictionary<Element, float>() { { turretData.element, turretData.Damage} }, 1, data.IsRowAvailable(turret.Key.x));
                    else
                        Debug.LogErrorFormat("Citadel Start: TurretData is NULL for {0}", turret.Key);
                } else
                    Debug.LogErrorFormat("Citadel Start: CharacterHandleWeapon is NULL for {0}", turret.Key);
            }
        } else
            Debug.LogError("Citadel Start: CitadelData or ElementsData or TurretList is NULL");
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (enemyMask.ContainsLayer(other.gameObject.layer)) {
            Enemy enemy = other.GetComponent<Enemy>();
            if (enemy != null) {
                _health.Damage(enemy.CitadelDamage, other.gameObject, 0, 0, Vector3.zero);
                enemy.Kill();
            }
        }
    }

    void PlayCellFeedback(MMF_Player feedback, Vector2 position) {
         if (cellPosition != null)
            cellPosition.position = position;
        feedback?.PlayFeedbacks();       
    }

    public void Heal(float value, Vector2 position) {
        _health.ReceiveHealth(value, null);
        PlayCellFeedback(healFeedback, position);
    }
}
