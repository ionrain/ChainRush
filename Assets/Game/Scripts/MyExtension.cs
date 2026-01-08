using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using MoreMountains.TopDownEngine;

public enum MathAction { Set, Add, Substract, Multiply, Divide }
public enum MathValueType { Absolute, Relative }

public static class MyExtension {

    public static string To_MMSS_Time(this int seconds) {
        TimeSpan t = TimeSpan.FromSeconds(seconds);
        return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
    }

    public static float ToTime(this int seconds) {
        if (seconds < 60)
            return seconds;
        if (seconds < 3600)
            return (float)seconds / 60;
        return (float)seconds / 3600;
    }

    public static string ToShortString(this int value) {
        if (value >= 10000000)
            return string.Format("{0:f2}M", (float)value / 10000000);
        if (value >= 1000)
            return string.Format("{0:f2}K", (float)value / 1000);
        return value.ToString();
    }

    public static string ToShortString(this float value) {
        if (value >= 10000000)
            return string.Format("{0:f2}M", value / 10000000);
        if (value >= 1000)
            return string.Format("{0:f2}K", value / 1000);
        return value.ToString();
    }

    public static void DestroyChildren(this Transform transform) {
        foreach (Transform child in transform)
            GameObject.Destroy(child.gameObject);
    }

    /// <summary>
    /// If entry exists, fills with values, if not creates a new one and adds to the list. Removes entry if exists and damage = 0.
    /// Does nothing if damageType is null.
    /// </summary>
    public static void ManageEntry(this List<TypedDamage> list, DamageType damageType, float damage) {
        if (damageType != null) {
            TypedDamage typedDamage = list.Find(t => t.AssociatedDamageType == damageType);
            if (damage > 0) {
                if (typedDamage == null) {
                    typedDamage = new TypedDamage();
                    list.Add(typedDamage);
                }
                typedDamage.AssociatedDamageType = damageType;
                typedDamage.MaxDamageCaused = damage;
                typedDamage.MinDamageCaused = damage;
            } else if (typedDamage != null)
                list.Remove(typedDamage);
        }
    }

    public static void Assign(this TypedDamage damage, DamageType damageType, float amount) {
        damage.AssociatedDamageType = damageType;
        damage.MinDamageCaused = amount;
        damage.MaxDamageCaused = amount;
       
        if (damageType != null && amount <= 0) {
            damage.MinDamageCaused = 0;
            damage.MaxDamageCaused = 0;
        }
    }

    public static Quaternion RotateToPosition(this Vector3 origin, Vector3 target) {
        Vector3 direction = target - origin;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return Quaternion.AngleAxis(angle, Vector3.forward);
    }

    public static Element RandomIfAnyExcept(this Element element, List<Element> exceptions = null) {
        if (element != Element.Any)
            return element;

        Array elements = Enum.GetValues(typeof(Element));
        if (exceptions == null || exceptions.Count == 0)
            return (Element)(UnityEngine.Random.Range(0, elements.Length) + 1);

        List<Element> options = new List<Element>();
        foreach (Element e in elements)
            if (e != Element.Any && !exceptions.Contains(e))
                options.Add(e);
        if (options.Count > 0)
            return options[UnityEngine.Random.Range(0, options.Count)];

        return Element.Physical;
    }

    public static int ApplyMathAction(this int target, MathAction action, int value) {
        switch (action) {
            case MathAction.Add:
                target += value;
                break;
            case MathAction.Set:
                target = value;
                break;
            case MathAction.Substract:
                target -= value;
                break;
            case MathAction.Multiply:
                target *= value;
                break;
            case MathAction.Divide:
                target = (int)((float)target / value);
                break;
            default:
                break;
        }
        return target;
    }

    public static float ApplyMathAction(this float target, MathAction action, float value) {
        switch (action) {
            case MathAction.Add:
                target += value;
                break;
            case MathAction.Set:
                target = value;
                break;
            case MathAction.Substract:
                target -= value;
                break;
            case MathAction.Multiply:
                target *= value;
                break;
            case MathAction.Divide:
                target /= value;
                break;
            default:
                break;
        }
        return target;
    }

    public static Vector2 GetScreenSize(this Camera camera) {
        float frustumHeight = 2.0f * 60.0f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * ((float)Screen.width / Screen.height);
        return new Vector2(frustumWidth, frustumHeight);
    }

    public static void Shuffle<T>(this IList<T> list) {
		var count = list.Count;
		var last = count - 1;
		for (var i = 0; i < last; ++i) {
			var r = UnityEngine.Random.Range(i, count);
			var tmp = list[i];
			list[i] = list[r];
			list[r] = tmp;
		}
	}

    public static bool ContainsLayer(this LayerMask mask, int layer) {
        return ((1 << layer) & mask) != 0;
    }

    public static void Accumulate(this IList<float> list) {
        int count = list.Count;
        if (count > 1)
            for (int i = 1; i < count; i++)
                list[i] = list[i] + list[i - 1];
    }
}
