using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class DamageOnTouchController : MonoBehaviour {
    [SerializeField] DamageOnTouch damageOnTouch;

    public DamageOnTouch DamageOnTouch => damageOnTouch;

    List<float> _initialValues = new List<float>();

    public void Setup(float powerMultiplier) {
        if (damageOnTouch != null) {
            List<TypedDamage> typedDamages = damageOnTouch.TypedDamages;
            int count = typedDamages.Count;
            if (_initialValues.Count == 0)
                for (int i = 0; i < count; i++)
                    _initialValues.Add(typedDamages[i].MinDamageCaused);

            for (int i = 0; i < count; i++) {
                TypedDamage damage = typedDamages[i];
                damage.MinDamageCaused = _initialValues[i] * powerMultiplier;
                damage.MaxDamageCaused = damage.MinDamageCaused;
            }
        }
    }

    public void InitializeWithValues(List<TypedDamage> typedDamages) {
        if (damageOnTouch != null && typedDamages != null) {
            _initialValues.Clear();
            damageOnTouch.TypedDamages.Clear();
            damageOnTouch.TypedDamages.AddRange(typedDamages);
        }
    }

    public void InitializeWithValue(DamageType damageType, float damage) {
        if (damageOnTouch != null && damageType != null) {
            _initialValues.Clear();
            damageOnTouch.TypedDamages.ManageEntry(damageType, damage);
        }
    }
}
