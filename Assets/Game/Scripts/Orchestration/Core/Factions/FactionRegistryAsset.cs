using UnityEngine;

/// <summary>
/// Convenience ScriptableObject holding references to well-known faction assets.
/// <para>
/// NOTE: Currently optional and not referenced at runtime. Provided as a central
/// lookup for editor workflows and future systems that need faction asset references
/// without hardcoding paths. Can be safely ignored until needed.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "FactionRegistry", menuName = "Game/Orchestration/Faction Registry")]
public sealed class FactionRegistryAsset : ScriptableObject
{
    public FactionAsset Player;
    public FactionAsset Enemy;
    public FactionAsset Neutral;
}
