using UnityEngine;

/// <summary>
/// Read-only world view for orchestration domains and policies.
/// Composed from sub-interfaces for clean future Framework extraction.
/// <para>
/// IMPORTANT — Must NOT expose Unity scene objects (<c>Transform</c>, <c>GameObject</c>,
/// <c>Component</c>). Unity value types (<c>Vector2</c>, <c>Bounds</c>) are acceptable —
/// they are serializable data with no scene reference.
/// </para>
/// <para>
/// IMPORTANT — All data is snapshotted once per tick by
/// <see cref="OrchestrationWorldCache"/>. Index-based access, no copying, no allocations.
/// </para>
/// </summary>

// ──────────────────────────────────────────────────────────────────
//  IWorldQueryBase — Anchor, timing, and actor snapshot access
// ──────────────────────────────────────────────────────────────────

/// <summary>
/// Core world state: anchor point, tick time, and index-based actor snapshot.
/// Actors = all alive entities with a non-null faction (friendly + hostile).
/// </summary>
public interface IWorldQueryBase
{
    Vector2 Anchor { get; }
    float Now { get; }

    int ActorCount { get; }
    EntityId GetActorEntityId(int index);
    Vector2 GetActorPosition(int index);
    FactionAsset GetActorFaction(int index);
    bool GetActorIsAlive(int index);
}

// ──────────────────────────────────────────────────────────────────
//  ICrowdQuery — Friendly crowd positions for spacing/scoring
// ──────────────────────────────────────────────────────────────────

/// <summary>
/// Friendly crowd snapshot for crowd-aware scoring (idle fill, combat spread).
/// Only entities that implement <see cref="IEntityIdProvider"/> are included.
/// </summary>
public interface ICrowdQuery
{
    int CrowdCount { get; }
    Vector2 GetCrowdPosition(int index);
    EntityId GetCrowdEntityId(int index);
}

// ──────────────────────────────────────────────────────────────────
//  IRoleQuery — Per-entity role lookup
// ──────────────────────────────────────────────────────────────────

/// <summary>
/// Resolves the <see cref="RoleAsset"/> for an entity by <see cref="EntityId"/>.
/// Only populated for entities that have a role assignment.
/// </summary>
public interface IRoleQuery
{
    bool TryGetRole(EntityId entityId, out RoleAsset role);
}

// ──────────────────────────────────────────────────────────────────
//  IIdleBoundsQuery — Per-role idle bounds
// ──────────────────────────────────────────────────────────────────

/// <summary>
/// Resolves idle bounds for a <see cref="RoleAsset"/>.
/// Populated from <see cref="IdleBoundsRegistry"/> once per tick.
/// </summary>
public interface IIdleBoundsQuery
{
    bool TryGetIdleBounds(RoleAsset role, out Bounds bounds);
}

// ──────────────────────────────────────────────────────────────────
//  ICombatTargetSetQuery — Shared target candidates
// ──────────────────────────────────────────────────────────────────

/// <summary>
/// Provides access to the resolved <see cref="CombatTargetSet"/> for this tick.
/// May return null if no target set is available.
/// </summary>
public interface ICombatTargetSetQuery
{
    CombatTargetSet GetCombatTargetSet();
}

// ──────────────────────────────────────────────────────────────────
//  IWorldQuery — Composite interface
// ──────────────────────────────────────────────────────────────────

/// <summary>
/// Composite world query interface. Domains and policies access world state
/// exclusively through this interface.
/// <para>
/// IMPORTANT — Implemented by <c>OrchestrationWorldCache</c> (RuntimeHost).
/// Core contracts depend only on this interface, not the concrete type.
/// </para>
/// </summary>
public interface IWorldQuery : IWorldQueryBase, ICrowdQuery, IRoleQuery, IIdleBoundsQuery, ICombatTargetSetQuery
{
}
