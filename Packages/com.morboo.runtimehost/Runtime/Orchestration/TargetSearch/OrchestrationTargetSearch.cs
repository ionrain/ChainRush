using System;

/// <summary>
/// Shared selector/collector over generic <see cref="OrchestrationTargetRef"/> candidates.
/// IMPORTANT: Stateless and allocation-free per call (caller provides buffers).
/// RuntimeHost knows nothing about combat-only concepts such as hostility or camera viewport.
/// </summary>
public static class OrchestrationTargetSearch
{
    public static bool TryFindBest(
        IWorldQuery world,
        EntityId seekerEntityId,
        IOrchestrationTargetProvider provider,
        IOrchestrationTargetFilter filter,
        IOrchestrationTargetScorer scorer,
        OrchestrationTargetRef[] scratchCandidates,
        out OrchestrationTargetRef bestTarget)
    {
        bestTarget = OrchestrationTargetRef.None;

        if (provider == null || scorer == null || scratchCandidates == null || scratchCandidates.Length == 0)
            return false;

        int candidateCount = provider.FillCandidates(world, seekerEntityId, scratchCandidates);
        if (candidateCount <= 0)
            return false;

        if (candidateCount > scratchCandidates.Length)
            candidateCount = scratchCandidates.Length;

        bool found = false;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < candidateCount; i++)
        {
            OrchestrationTargetRef candidate = scratchCandidates[i];
            if (candidate.IsNone)
                continue;

            if (filter != null && !filter.Accept(world, seekerEntityId, in candidate))
                continue;

            if (!scorer.TryScore(world, seekerEntityId, in candidate, out float score))
                continue;

            if (float.IsNaN(score))
                continue;

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return found;
    }

    public static int FillTopK(
        IWorldQuery world,
        EntityId seekerEntityId,
        IOrchestrationTargetProvider provider,
        IOrchestrationTargetFilter filter,
        IOrchestrationTargetScorer scorer,
        int maxTargets,
        OrchestrationTargetRef[] outTargets,
        float[] outScores,
        OrchestrationTargetRef[] scratchCandidates)
    {
        if (provider == null || scorer == null || outTargets == null || outScores == null || scratchCandidates == null)
            return 0;

        if (maxTargets <= 0 || outTargets.Length == 0 || outScores.Length == 0 || scratchCandidates.Length == 0)
            return 0;

        int slotLimit = Math.Min(maxTargets, Math.Min(outTargets.Length, outScores.Length));
        for (int i = 0; i < slotLimit; i++)
        {
            outTargets[i] = OrchestrationTargetRef.None;
            outScores[i] = float.NegativeInfinity;
        }

        int candidateCount = provider.FillCandidates(world, seekerEntityId, scratchCandidates);
        if (candidateCount <= 0)
            return 0;

        if (candidateCount > scratchCandidates.Length)
            candidateCount = scratchCandidates.Length;

        int count = 0;
        for (int i = 0; i < candidateCount; i++)
        {
            OrchestrationTargetRef candidate = scratchCandidates[i];
            if (candidate.IsNone)
                continue;

            if (filter != null && !filter.Accept(world, seekerEntityId, in candidate))
                continue;

            if (!scorer.TryScore(world, seekerEntityId, in candidate, out float score))
                continue;

            if (float.IsNaN(score))
                continue;

            InsertSortedDescending(candidate, score, outTargets, outScores, slotLimit, ref count);
        }

        return count;
    }

    static void InsertSortedDescending(
        in OrchestrationTargetRef candidate,
        float score,
        OrchestrationTargetRef[] outTargets,
        float[] outScores,
        int slotLimit,
        ref int count)
    {
        if (slotLimit <= 0)
            return;

        int insertIndex;
        if (count < slotLimit)
        {
            insertIndex = count;
            count++;
        }
        else
        {
            if (score <= outScores[slotLimit - 1])
                return;

            insertIndex = slotLimit - 1;
        }

        while (insertIndex > 0 && score > outScores[insertIndex - 1])
        {
            outScores[insertIndex] = outScores[insertIndex - 1];
            outTargets[insertIndex] = outTargets[insertIndex - 1];
            insertIndex--;
        }

        outScores[insertIndex] = score;
        outTargets[insertIndex] = candidate;
    }
}
