using UnityEngine;

/// <summary>
/// Typed role identity token for orchestration.
/// Drag-drop in inspector to assign roles to units and policy maps.
/// IMPORTANT: Routing uses ReferenceEquals — each distinct role must be a separate asset.
/// <see cref="Id"/> is for debug/metrics display only, never for routing.
/// </summary>
[CreateAssetMenu(fileName = "Role", menuName = "Game/Orchestration/Role")]
public sealed class RoleAsset : ScriptableObject
{
    [SerializeField] string id = "Role";

    /// <summary>
    /// Display/log identifier. Falls back to asset name if <see cref="id"/> is empty.
    /// RATIONALE: Used for debug logging only, never for behavior selection or routing.
    /// </summary>
    public string Id => string.IsNullOrEmpty(id) ? name : id;
}
