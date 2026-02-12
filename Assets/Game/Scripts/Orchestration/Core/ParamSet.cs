using System;
using System.Collections.Generic;
using UnityEngine;

public enum ParamType
{
    Bool,
    Int,
    Float,
    String,
    Vector2,
    Object
}

[Serializable]
public struct Param
{
    public string Key;
    public ParamType Type;
    public bool BoolValue;
    public int IntValue;
    public float FloatValue;
    public string StringValue;
    public Vector2 Vector2Value;
    public UnityEngine.Object ObjectValue;
}

[Serializable]
public struct ParamSet
{
    public List<Param> Items;

    public static readonly ParamSet Empty = default;

    // --- Getters (null-safe, no allocations, no LINQ) ---

    public bool TryGetBool(string key, out bool value)
    {
        value = default;
        if (Items == null || Items.Count == 0) return false;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Key == key && Items[i].Type == ParamType.Bool)
            {
                value = Items[i].BoolValue;
                return true;
            }
        }
        return false;
    }

    public bool TryGetInt(string key, out int value)
    {
        value = default;
        if (Items == null || Items.Count == 0) return false;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Key == key && Items[i].Type == ParamType.Int)
            {
                value = Items[i].IntValue;
                return true;
            }
        }
        return false;
    }

    public bool TryGetFloat(string key, out float value)
    {
        value = default;
        if (Items == null || Items.Count == 0) return false;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Key == key && Items[i].Type == ParamType.Float)
            {
                value = Items[i].FloatValue;
                return true;
            }
        }
        return false;
    }

    public bool TryGetString(string key, out string value)
    {
        value = default;
        if (Items == null || Items.Count == 0) return false;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Key == key && Items[i].Type == ParamType.String)
            {
                value = Items[i].StringValue;
                return true;
            }
        }
        return false;
    }

    public bool TryGetVector2(string key, out Vector2 value)
    {
        value = default;
        if (Items == null || Items.Count == 0) return false;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Key == key && Items[i].Type == ParamType.Vector2)
            {
                value = Items[i].Vector2Value;
                return true;
            }
        }
        return false;
    }

    public bool TryGetObject(string key, out UnityEngine.Object value)
    {
        value = default;
        if (Items == null || Items.Count == 0) return false;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Key == key && Items[i].Type == ParamType.Object)
            {
                value = Items[i].ObjectValue;
                return true;
            }
        }
        return false;
    }

    // --- Setters (find-or-add, lazy list init, no closure allocations) ---

    public void SetBool(string key, bool value)
    {
        int idx = FindOrAdd(key, ParamType.Bool);
        var p = Items[idx]; p.BoolValue = value; Items[idx] = p;
    }

    public void SetInt(string key, int value)
    {
        int idx = FindOrAdd(key, ParamType.Int);
        var p = Items[idx]; p.IntValue = value; Items[idx] = p;
    }

    public void SetFloat(string key, float value)
    {
        int idx = FindOrAdd(key, ParamType.Float);
        var p = Items[idx]; p.FloatValue = value; Items[idx] = p;
    }

    public void SetString(string key, string value)
    {
        int idx = FindOrAdd(key, ParamType.String);
        var p = Items[idx]; p.StringValue = value; Items[idx] = p;
    }

    public void SetVector2(string key, Vector2 value)
    {
        int idx = FindOrAdd(key, ParamType.Vector2);
        var p = Items[idx]; p.Vector2Value = value; Items[idx] = p;
    }

    public void SetObject(string key, UnityEngine.Object value)
    {
        int idx = FindOrAdd(key, ParamType.Object);
        var p = Items[idx]; p.ObjectValue = value; Items[idx] = p;
    }

    int FindOrAdd(string key, ParamType type)
    {
        if (Items == null) Items = new List<Param>(4);

        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Key == key)
            {
                var p = Items[i];
                p.Type = type;
                Items[i] = p;
                return i;
            }
        }

        Items.Add(new Param { Key = key, Type = type });
        return Items.Count - 1;
    }
}
