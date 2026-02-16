using UnityEngine;

/// <summary>
/// Describes how a unit's target is outside the allowed region (leash radius around anchor).
/// <para>
/// IMPORTANT: <see cref="OutOfBoundsMode"/> applies only when the target is outside leash.
/// Crowd spreading activates only under <see cref="OutOfBoundsMode.SpreadNearBoundary"/>.
/// </para>
/// </summary>
public enum OutOfBoundsMode
{
    /// <summary>Unit holds position (clears target) when target is outside leash.</summary>
    Hold = 0,

    /// <summary>Unit moves to the closest point on the leash boundary.</summary>
    ClosestAllowedPoint = 1,

    /// <summary>
    /// Unit moves to a spread point near the leash boundary, avoiding crowding.
    /// Uses <see cref="CombatMoveConstraintsAsset"/> spread parameters.
    /// </summary>
    SpreadNearBoundary = 2
}

/// <summary>
/// Pure-data ScriptableObject defining per-role movement constraints for combat.
/// Assigned via <see cref="CombatRoleConstraintsMapAsset"/> and injected into executors
/// by the arbiter each tick.
/// <para>
/// IMPORTANT: This asset is pure data. It does NOT depend on arbiter context or runtime state.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "CombatMoveConstraints", menuName = "Game/Orchestration/Combat/Move Constraints")]
public sealed class CombatMoveConstraintsAsset : ScriptableObject
{
    // ──────────────────────────────────────────────────────────────────
    //  Leash
    // ──────────────────────────────────────────────────────────────────

    [Header("Leash")]
    [Tooltip("Maximum distance from anchor. 0 = no leash (unconstrained, default).")]
    public float leashRadius;

    [Tooltip("What to do when target is outside leash radius.")]
    public OutOfBoundsMode outOfBoundsMode = OutOfBoundsMode.ClosestAllowedPoint;

    [Tooltip("Stop distance when moving to a constrained waypoint (world units).")]
    public float stopDistance = 0.5f;

    // ──────────────────────────────────────────────────────────────────
    //  Spread (only used when OutOfBoundsMode == SpreadNearBoundary)
    // ──────────────────────────────────────────────────────────────────

    [Header("Spread (SpreadNearBoundary only)")]
    [Tooltip("Minimum spacing between units (hard crowd penalty threshold).")]
    public float personalSpace = 1f;

    [Tooltip("Radius to check for nearby friendlies during crowd scoring.")]
    public float crowdCheckRadius = 2f;

    [Tooltip("Number of radial samples to test for spread positions.")]
    public int samples = 8;

    [Tooltip("Max radial offset from boundary point (world units). " +
             "Samples are placed at distances between personalSpace and this value.")]
    public float maxSpreadOffset = 1.5f;

    [Tooltip("Weight for preferring positions closer to anchor (0–1). " +
             "1 = strongly prefer closer to anchor.")]
    [Range(0f, 1f)]
    public float anchorDistanceWeight = 0.3f;

    // ──────────────────────────────────────────────────────────────────
    //  Anti-thrash
    // ──────────────────────────────────────────────────────────────────

    [Header("Anti-Thrash")]
    [Tooltip("Minimum time between re-pathing to a new constrained waypoint (seconds).")]
    public float minRepathInterval = 0.3f;

    [Tooltip("Minimum Hold duration before switching to Waypoint mode (seconds).")]
    public float holdMinTime = 0.2f;

    [Tooltip("Minimum time at a constrained waypoint before accepting a new target (seconds).")]
    public float waypointMinTime = 0.4f;
}
