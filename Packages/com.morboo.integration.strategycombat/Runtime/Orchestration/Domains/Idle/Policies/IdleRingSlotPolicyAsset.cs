using UnityEngine;

/// <summary>
/// Idle policy that assigns each unit a deterministic slot on a ring around the anchor.
/// <para>
/// When called via the arbiter (per-role path), slot angle derives from
/// <c>roleSeed ^ (entitySeed * FNV_PRIME)</c>, producing unique positions per unit.
/// When called via legacy path, delegates with <c>roleSeed=0, entitySeed=GetInstanceID()</c>.
/// </para>
/// <para>
/// IMPORTANT — This asset is stateless. No per-unit timers or last-positions are stored here.
/// Future stability features (recalc intervals, anchor drift) belong in per-unit components.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "IdleRingSlotPolicy", menuName = "Game/Orchestration/Idle/Policies/Ring Slot")]
public sealed class IdleRingSlotPolicy2DAsset : IdlePolicyAsset
{
    // TODO: Migrate radius/angle logic to FormationPattern2DAsset (RingFormationPattern2DAsset).
    [Header("Ring")]
    [SerializeField] float radius = 2.0f;
    [SerializeField] float angleJitterDegrees = 12f;
    [SerializeField] float stopDistance = 0.25f;

    [Header("Stability (Step R1.1 — declared, not enforced this step)")]
    [SerializeField] float recalcInterval = 0.8f;
    [SerializeField] float anchorMoveRecalcDistance = 0.5f;
    [SerializeField] float maxStepPerRecalc = 0.8f;

    // RATIONALE: FNV prime used to combine roleSeed + entitySeed for a well-distributed seed.
    const int FnvPrime = 16777619;

    /// <summary>
    /// Legacy path (no arbiter). Delegates to the roleSeed+entitySeed overload
    /// with <c>roleSeed=0</c> and <c>entitySeed=self.GetInstanceID()</c> to ensure
    /// identical determinism in both code paths.
    /// </summary>
    public override OrchestrationCommand ChooseCommand(Transform self, Vector3 anchor, float now, out string debugInfo)
    {
        int entitySeed = self != null ? self.GetInstanceID() : 0;
        return ChooseCommand(self, anchor, now, 0, entitySeed, out debugInfo);
    }

    /// <summary>
    /// Per-role dispatch path (arbiter). Computes a deterministic ring slot using
    /// a combined seed from roleSeed and entitySeed.
    /// PERF: No allocations. Pure math.
    /// </summary>
    public override OrchestrationCommand ChooseCommand(
        Transform self, Vector3 anchor, float now,
        int roleSeed, int entitySeed,
        out string debugInfo)
    {
        // Combine role and entity seeds for a deterministic, unique-per-unit slot.
        int seed = roleSeed ^ (entitySeed * FnvPrime);

        float baseAngle = Hash01(seed) * 360f;
        float jitter = (Hash01(seed * 48611) * 2f - 1f) * angleJitterDegrees;
        float angleRad = (baseAngle + jitter) * Mathf.Deg2Rad;

        Float3 dir = new Float3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0);
        Float3 point = anchor.ToFloat3() + dir * radius;

        debugInfo = null;
        return BuildPointCommand(self, point);
    }

    public override OrchestrationCommand ChooseCommand(
        Float3 selfPosition, EntityId selfId,
        Float3 anchor, float now,
        int roleSeed, int entitySeed,
        IWorldQuery world,
        out string debugInfo)
    {
        int seed = roleSeed ^ (entitySeed * FnvPrime);

        float baseAngle = Hash01(seed) * 360f;
        float jitter = (Hash01(seed * 48611) * 2f - 1f) * angleJitterDegrees;
        float angleRad = (baseAngle + jitter) * Mathf.Deg2Rad;

        Float3 dir = new Float3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0);
        Float3 point = anchor + dir * radius;

        debugInfo = null;

        EntityId pointTargetId = OrchestrationTargetPointRegistry.SetOperatorPointTarget(selfId, point);
        if (pointTargetId.IsNone)
            return OrchestrationCommand.Cancel();

        return OrchestrationCommand.Engage(OrchestrationTargetRef.Point(pointTargetId));
    }

    /// <summary>
    /// Simple integer hash -> [0, 1] range. Deterministic, no allocations.
    /// </summary>
    static float Hash01(int x)
    {
        unchecked
        {
            uint u = (uint)x;
            u ^= u >> 16;
            u *= 0x7feb352d;
            u ^= u >> 15;
            u *= 0x846ca68b;
            u ^= u >> 16;
            return (u & 0x00FFFFFF) / 16777215f;
        }
    }

    static OrchestrationCommand BuildPointCommand(Transform self, in Float3 point)
    {
        if (self == null)
            return OrchestrationCommand.Cancel();

        IEntityIdProvider idp = self.GetComponent<IEntityIdProvider>();
        EntityId selfId = idp != null ? idp.GetEntityId() : EntityId.None;
        if (selfId.IsNone)
            return OrchestrationCommand.Cancel();

        EntityId pointTargetId = OrchestrationTargetPointRegistry.SetOperatorPointTarget(selfId, point);
        if (pointTargetId.IsNone)
            return OrchestrationCommand.Cancel();

        return OrchestrationCommand.Engage(OrchestrationTargetRef.Point(pointTargetId));
    }
}
