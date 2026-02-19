using UnityEngine;

/// <summary>
/// Picks the closest alive candidate from <see cref="CombatTargetSet"/> to the unit's
/// own position. Falls back to <paramref name="primaryTarget"/> if the set is
/// null, expired, empty, or all candidates are dead/missing.
/// <para>
/// PERF: Index loop, no LINQ, no allocations. Uses IWorldQueryBase.TryGetActorPosition
/// for O(1) position lookups per candidate.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "NearestToSelfPolicy", menuName = "Game/Orchestration/Combat/Targeting Policies/Nearest To Self")]
public sealed class NearestToSelfPolicyAsset : CombatTargetingPolicyAsset
{
    public override EntityId ChooseTarget(
        EntityId selfEntityId,
        Float2 selfPosition,
        EntityId primaryTarget,
        CombatTargetSet targetSet,
        float now,
        IWorldQueryBase world,
        out string debugInfo)
    {
        debugInfo = "NearestToSelf";

        if (targetSet == null || world == null)
            return primaryTarget;

        EntityId[] candidates;
        int count;
        if (!targetSet.TryGetTargets(out candidates, out count, now) || candidates == null || count <= 0)
            return primaryTarget;

        EntityId best = EntityId.None;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            EntityId eid = candidates[i];
            if (eid.IsNone) continue;

            Float2 pos;
            if (!world.TryGetActorPosition(eid, out pos))
                continue;

            float sqr = Float2.DistanceSqr(pos, selfPosition);
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = eid;
            }
        }

        return !best.IsNone ? best : primaryTarget;
    }
}
