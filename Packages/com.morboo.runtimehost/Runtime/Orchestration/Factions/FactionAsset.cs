using UnityEngine;

/// <summary>
/// ScriptableObject representing a faction identity token.
/// Used by <see cref="FactionRelationTableAsset"/> for typed relation lookups.
/// <para>
/// IMPORTANT: Purely an identity token — no runtime behavior, no Update loop.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "Faction", menuName = "Game/Orchestration/Faction")]
public sealed class FactionAsset : ScriptableObject
{
    [SerializeField] string id = "Unknown";

    /// <summary>Faction identifier string. Never null or empty (falls back to "Unknown").</summary>
    public string Id => string.IsNullOrEmpty(id) ? "Unknown" : id;
}
