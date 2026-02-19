using System.Collections;
using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using Sirenix.OdinInspector;
using UnityEngine;

public class WeaponActionData {
    public float minDistance;
    public float maxDistance;
    public AttackMark mark;
    public AnimationState animationState;

    public float AnimationDuration { get; set; }
    public float AttackEventTime { get; set; }
    public bool AnimationActive { get; set; }
}

public class WeaponManager : SerializedMonoBehaviour {
    [SerializeField] Character owner;
    [SerializeField] string spineEvent = "Attack";
    [SerializeField] SpineSkeletonModel spineModel;
    [SerializeField] Dictionary<Weapon, WeaponActionData> weapons = new Dictionary<Weapon, WeaponActionData>();

    List<Weapon> _weaponsList = new List<Weapon>();
    Weapon _lastWeapon;

    void Awake() {
        if (weapons != null && weapons.Count > 0 && owner != null && spineModel != null) {
            _weaponsList = new List<Weapon>(weapons.Keys);
            _weaponsList.ForEach(t => {
                t.SetOwner(owner, null);
                t.Initialization();
            });

            foreach (var pair in weapons) {
                WeaponActionData data = pair.Value;
                if (data != null) {
                    Vector2 durations = spineModel.GetDuration(data.animationState, spineEvent);
                    data.AnimationDuration = durations.y;
                    data.AttackEventTime = durations.x;
                    if (data.mark != null) {
                        if (!data.mark.CustomDuration)
                            data.mark.SetDuration(durations.x);
                        else
                            data.AnimationDuration = data.mark.Duration;
                    }
                }else
                    Debug.LogErrorFormat("WeaponManager Awake: WeaponActionData or ActiveMark is NULL for {0}", pair.Key.gameObject.name);
            }
        } else
            Debug.LogError("WeaponManager Awake: weapons list or owner or spineModel is NULL");

    }

    public void Setup(float powerMultiplier) {
        foreach (var pair in weapons) {
            WeaponActionData data = pair.Value;
            if (data != null)
                data.AnimationActive = false;
            
            pair.Key.WeaponState.ChangeState(Weapon.WeaponStates.WeaponIdle);

            MeleeWeapon meleeWeapon = pair.Key as MeleeWeapon;
            if (meleeWeapon != null) {
                DamageOnTouchController damageController = meleeWeapon.ExistingDamageArea.gameObject.GetComponent<DamageOnTouchController>();
                if (damageController != null)
                    damageController.Setup(powerMultiplier);
            } else {
                EnemyProjectileWeapon projectileWeapon = pair.Key as EnemyProjectileWeapon;
                if (projectileWeapon != null)
                    projectileWeapon.Setup(powerMultiplier);
            }
        }
    }

    public bool AreAllWeaponsFinished => weapons != null && _weaponsList.Find(t => IsWeaponActive(t, weapons[t])) == null;

    public bool IsAnyWeaponReady(float distance) {
        return FindReadyWeapon(distance);
    }

    bool IsWeaponIdle(Weapon weapon, WeaponActionData data) {
        return data != null && weapon.gameObject.activeSelf && weapon.WeaponState.CurrentState == Weapon.WeaponStates.WeaponIdle
                && (data.mark == null || !data.mark.Active) && !data.AnimationActive;
    }

    bool IsWeaponActive(Weapon weapon, WeaponActionData data) {
        return data != null && weapon.gameObject.activeSelf && ((weapon.WeaponState.CurrentState != Weapon.WeaponStates.WeaponIdle &&
               weapon.WeaponState.CurrentState != Weapon.WeaponStates.WeaponDelayBetweenUses) ||
               (data.mark != null && data.mark.Active) || data.AnimationActive);
    }

    bool FindReadyWeapon(float distance) {
        List<Weapon> result = new List<Weapon>();
        if (weapons != null) {
            foreach (var pair in weapons) {
                WeaponActionData data = pair.Value;
                if (IsWeaponIdle(pair.Key, data) && distance >= data.minDistance && distance <= data.maxDistance)
                    result.Add(pair.Key);
            }
        }
        _lastWeapon = result.Count > 0 ? result[Random.Range(0, result.Count)] : null;
        return _lastWeapon != null;
    }

    public void Attack(Transform target) {
        Debug.LogFormat("WeaponManager Attack: performing)");
        if (weapons != null && weapons.ContainsKey(_lastWeapon)) {
            WeaponActionData data = weapons[_lastWeapon];
            if (data != null) {
                Interrupt();
                spineModel?.PlayAnimation(data.animationState);
                data.AnimationActive = true;
                StartCoroutine(SetAnimationInactive(data));
                if (data.mark != null) {
                    data.mark.Activate(target);
                    _lastWeapon.transform.rotation = data.mark.Rotation;
                    StartCoroutine(AttackCo(data.mark.Duration, data.mark.transform));
                } else {
                    StartCoroutine(AttackCo(data.AttackEventTime));
                }
            }
        }
    }

    IEnumerator SetAnimationInactive(WeaponActionData data) {
        yield return new WaitForSeconds(data.AnimationDuration);
        data.AnimationActive = false;
    }

    IEnumerator AttackCo(float delay, Transform t = null) {
        if (t != null)
            _lastWeapon.transform.rotation = t.rotation;
        if (delay > 0)
            yield return new WaitForSeconds(delay);
        if (t != null)
            _lastWeapon.transform.rotation = t.rotation;
        _lastWeapon.WeaponInputStart();
        _lastWeapon.WeaponInputStop();
    }

    public void Interrupt() {
        StopAllCoroutines();
        foreach (var pair in weapons)
            if (pair.Value != null)
                pair.Value.AnimationActive = false;
    }
}
