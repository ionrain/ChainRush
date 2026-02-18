using UnityEngine;

/// <summary>
/// Read-only configuration snapshot passed to <see cref="ExecutionRouter.Execute"/>.
/// Populated by the arbiter each tick from serialized fields and domain-sourced policy maps.
/// IMPORTANT: Struct — stack-allocated, zero GC.
/// </summary>
public struct ExecutionContext
{
    public IdleRolePolicyMapAsset IdleRolePolicyMap;
    public CombatRolePolicyMapAsset CombatRolePolicyMap;
    public CombatRoleConstraintsMapAsset CombatRoleConstraintsMap;
    public CombatCommand CombatCommand;
    public FactionAsset OrchestratorFaction;
    public FactionRelationTableAsset Relations;
    public Vector2 Anchor;
    public float Now;
    public bool DebugLog;
}
