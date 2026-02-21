using System;

[Serializable]
public struct CapabilityEntry
{
    public CapabilityId Id;
    public ParamSet Params;

    public bool TryGetFloat(string key, out float v)
    {
        return Params.TryGetFloat(key, out v);
    }

    public bool TryGetBool(string key, out bool v)
    {
        return Params.TryGetBool(key, out v);
    }
}
