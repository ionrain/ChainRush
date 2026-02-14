using UnityEngine;

/// <summary>
/// Holds a unit's orchestration identity: faction and role tag.
/// IMPORTANT: Read-only identity for the orchestration layer. Does not drive gameplay.
/// </summary>
public sealed class UnitOrchestrationIdentity : MonoBehaviour
{
    [Header("Faction")]
    [SerializeField] FactionAsset faction;

    [Header("Role")]
    [SerializeField] string roleTagOverride = "";

    /// <summary>
    /// Returns the typed faction asset, or null if not assigned.
    /// IMPORTANT: Combat orchestration runtime uses this exclusively.
    /// </summary>
    public FactionAsset GetFactionAsset() => faction;

    /// <summary>
    /// Returns the unit's role tag for orchestration state reporting.
    /// Priority: roleTagOverride > UnitData.unitClass > "Unit" fallback.
    /// </summary>
    public string GetRoleTag(Unit unit)
    {
        if (!string.IsNullOrEmpty(roleTagOverride))
            return roleTagOverride;

        if (unit != null && unit.Data != null)
            return unit.Data.unitClass.ToString();

        return "Unit";
    }

    void OnValidate()
    {
        if (faction == null)
            Debug.LogWarning($"[{name}] UnitOrchestrationIdentity has no FactionAsset assigned.", this);
    }
}
