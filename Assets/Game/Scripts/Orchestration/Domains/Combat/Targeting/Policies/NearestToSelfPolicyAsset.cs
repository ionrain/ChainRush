using UnityEngine;

/// <summary>
/// Picks the closest alive candidate from <see cref="CombatTargetSet"/> to the unit's
/// own position. Falls back to <paramref name="primaryTarget"/> if the set is
/// null, expired, empty, or all candidates are null/inactive.
/// <para>
/// PERF: Index loop, no LINQ, no allocations.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "NearestToSelfPolicy", menuName = "Game/Orchestration/Combat/Targeting Policies/Nearest To Self")]
public sealed class NearestToSelfPolicyAsset : CombatTargetingPolicyAsset
{
    public override Transform ChooseTarget(
        Transform self,
        Transform primaryTarget,
        CombatTargetSet targetSet,
        out string debugInfo)
    {
        debugInfo = "NearestToSelf";

        if (self == null || targetSet == null)
            return primaryTarget;

        Transform[] candidates;
        int count;
        if (!targetSet.TryGetTargets(out candidates, out count) || candidates == null || count <= 0)
            return primaryTarget;

        Vector2 selfPos = (Vector2)self.position;
        Transform best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Transform t = candidates[i];
            if (t == null) continue;
            if (!t.gameObject.activeInHierarchy) continue;

            float sqr = ((Vector2)t.position - selfPos).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }

        return best != null ? best : primaryTarget;
    }
}
