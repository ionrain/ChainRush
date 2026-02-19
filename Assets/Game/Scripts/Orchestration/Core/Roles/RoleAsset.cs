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

    [Tooltip("Stable integer identity for this role. Must be unique and non-zero. " +
             "Used as RoleId in Framework contracts.")]
    [SerializeField] int contentId;

    /// <summary>
    /// Display/log identifier. Falls back to asset name if <see cref="id"/> is empty.
    /// RATIONALE: Used for debug logging only, never for behavior selection or routing.
    /// </summary>
    public string Id => string.IsNullOrEmpty(id) ? name : id;

    /// <summary>
    /// Stable <see cref="RoleId"/> derived from <see cref="contentId"/>.
    /// IMPORTANT: Used by RuntimeHost and Framework contracts instead of RoleAsset references.
    /// RoleAsset → RoleId conversion happens at the Integration boundary only.
    /// </summary>
    public RoleId RoleId => new RoleId(contentId);

#if UNITY_EDITOR
    void OnValidate()
    {
        if (contentId == 0)
            Debug.LogWarning($"[RoleAsset] '{Id}' has contentId == 0 (maps to RoleId.None). Assign a unique non-zero value.", this);
    }
#endif
}
