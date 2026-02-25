using System;
using UnityEngine;

/// <summary>
/// Read-only configuration snapshot passed to <see cref="ExecutionRouter.Execute"/>.
/// Populated by the arbiter each tick from serialized fields and domain-sourced policy maps.
/// IMPORTANT: Struct — stack-allocated, zero GC.
/// </summary>
public struct ExecutionContext
{
    Type _bindingType0;
    ScriptableObject _bindingAsset0;
    Type _bindingType1;
    ScriptableObject _bindingAsset1;
    Type _bindingType2;
    ScriptableObject _bindingAsset2;
    byte _bindingCount;

    public OrchestrationCommand Command;
    public FactionAsset OrchestratorFaction;
    public FactionRelationTableAsset Relations;
    public Float2 Anchor;
    public float Now;
    public bool DebugLog;

    public bool TryGetBinding<TAsset>(out TAsset asset)
        where TAsset : ScriptableObject
    {
        Type assetType = typeof(TAsset);
        for (int i = 0; i < _bindingCount; i++)
        {
            if (!TryGetSlot(i, out Type slotType, out ScriptableObject slotAsset))
                continue;

            if (slotType != assetType)
                continue;

            asset = slotAsset as TAsset;
            return asset != null;
        }

        asset = null;
        return false;
    }

    public void SetBinding<TAsset>(TAsset asset)
        where TAsset : ScriptableObject
    {
        Type assetType = typeof(TAsset);
        SetBindingRaw(assetType, asset);
    }

    public void SetBindingRaw(Type assetType, ScriptableObject asset)
    {
        if (assetType == null)
            return;

        for (int i = 0; i < _bindingCount; i++)
        {
            if (!TryGetSlot(i, out Type slotType, out _))
                continue;

            if (slotType != assetType)
                continue;

            SetSlotAsset(i, asset);
            return;
        }

        if (_bindingCount >= 3)
            return;

        SetSlot((int)_bindingCount, assetType, asset);
        _bindingCount++;
    }

    bool TryGetSlot(int index, out Type assetType, out ScriptableObject asset)
    {
        switch (index)
        {
            case 0:
                assetType = _bindingType0;
                asset = _bindingAsset0;
                return true;
            case 1:
                assetType = _bindingType1;
                asset = _bindingAsset1;
                return true;
            case 2:
                assetType = _bindingType2;
                asset = _bindingAsset2;
                return true;
            default:
                assetType = null;
                asset = null;
                return false;
        }
    }

    void SetSlot(int index, Type assetType, ScriptableObject asset)
    {
        switch (index)
        {
            case 0:
                _bindingType0 = assetType;
                _bindingAsset0 = asset;
                break;
            case 1:
                _bindingType1 = assetType;
                _bindingAsset1 = asset;
                break;
            case 2:
                _bindingType2 = assetType;
                _bindingAsset2 = asset;
                break;
        }
    }

    void SetSlotAsset(int index, ScriptableObject asset)
    {
        switch (index)
        {
            case 0:
                _bindingAsset0 = asset;
                break;
            case 1:
                _bindingAsset1 = asset;
                break;
            case 2:
                _bindingAsset2 = asset;
                break;
        }
    }
}
