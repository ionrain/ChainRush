using UnityEngine;

/// <summary>
/// Holds an enemy's orchestration identity: faction and role tag.
/// IMPORTANT: Read-only identity for the orchestration layer. Does not drive gameplay.
/// </summary>
public sealed class EnemyOrchestrationIdentity : MonoBehaviour
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
    /// Returns the enemy's role tag for orchestration state reporting.
    /// Priority: roleTagOverride > EnemyType (Normal/Chieftain/Boss) > EnemyName > "Enemy" fallback.
    /// </summary>
    public string GetRoleTag(Enemy enemy)
    {
        if (!string.IsNullOrEmpty(roleTagOverride))
            return roleTagOverride;

        if (enemy != null)
        {
            string typeName = enemy.Type.ToString();
            if (!string.IsNullOrEmpty(typeName))
                return typeName;

            string enemyName = enemy.EnemyName;
            if (!string.IsNullOrEmpty(enemyName))
                return enemyName;
        }

        return "Enemy";
    }

    void OnValidate()
    {
        if (faction == null)
            Debug.LogWarning($"[{name}] EnemyOrchestrationIdentity has no FactionAsset assigned.", this);
    }
}
