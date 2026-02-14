using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides a unified read-only view of the world for orchestrators.
/// Queries the <see cref="OrchestrationRegistry"/> to collect entity snapshots.
/// IMPORTANT: Read-only aggregation only — no gameplay ownership, no Update loop.
/// Typed-only: uses <see cref="FactionRelationTableAsset"/> exclusively.
/// Prefer NonAlloc methods to avoid per-call allocations.
/// </summary>
public sealed class OrchestrationWorldView : MonoBehaviour
{
    [SerializeField] FactionRelationTableAsset typedRelations;

    [Header("Query Buffers")]
    [Tooltip("Safety limit to prevent spikes. Orchestrators can raise via inspector if needed.")]
    [SerializeField] int maxResults = 64;

    bool _warnedMissingRelations;

    /// <summary>
    /// Fills the provided list with snapshots of all registered entities.
    /// PERF: No LINQ; linear scan. Null reporters are lazily removed.
    /// </summary>
    public void CollectAllSnapshotsNonAlloc(List<StateSnapshot> results)
    {
        results.Clear();
        var reporters = OrchestrationRegistry.StateReportersList;

        for (int i = 0; i < reporters.Count; i++)
        {
            if (reporters[i] == null || reporters[i] is Object obj && obj == null)
            {
                reporters.RemoveAt(i);
                i--;
                continue;
            }

            results.Add(reporters[i].ReportState());
            if (results.Count >= maxResults) break;
        }
    }

    /// <summary>
    /// Fills the provided list with snapshots within the given radius of center.
    /// Uses sqrMagnitude to avoid sqrt. Null reporters are lazily removed.
    /// </summary>
    public void CollectSnapshotsNearNonAlloc(Vector2 center, float radius, List<StateSnapshot> results)
    {
        results.Clear();
        var reporters = OrchestrationRegistry.StateReportersList;
        float radiusSqr = radius * radius;

        for (int i = 0; i < reporters.Count; i++)
        {
            if (reporters[i] == null || reporters[i] is Object obj && obj == null)
            {
                reporters.RemoveAt(i);
                i--;
                continue;
            }

            StateSnapshot snap = reporters[i].ReportState();
            if ((snap.Position - center).sqrMagnitude <= radiusSqr)
            {
                results.Add(snap);
                if (results.Count >= maxResults) break;
            }
        }
    }

    /// <summary>
    /// Fills the provided list with snapshots that match the desired faction relation
    /// to the requester, within the given radius.
    /// IMPORTANT: Typed-only. Requires <see cref="typedRelations"/> and a non-null
    /// <paramref name="requesterFaction"/>. Fails closed: returns empty results and
    /// logs a warning once if either is missing. Snapshots with null faction are skipped.
    /// </summary>
    public void CollectByRelationNearNonAlloc(
        FactionAsset requesterFaction,
        Vector2 center,
        float radius,
        FactionRelation relation,
        List<StateSnapshot> results)
    {
        results.Clear();

        if (typedRelations == null || requesterFaction == null)
        {
            if (!_warnedMissingRelations)
            {
                _warnedMissingRelations = true;
                Debug.LogWarning(
                    "[OrchestrationWorldView] typedRelations or requesterFaction is null. " +
                    "Returning empty results (fail-closed). This warning is logged once.", this);
            }
            return;
        }

        var reporters = OrchestrationRegistry.StateReportersList;
        float radiusSqr = radius * radius;

        for (int i = 0; i < reporters.Count; i++)
        {
            if (reporters[i] == null || reporters[i] is Object obj && obj == null)
            {
                reporters.RemoveAt(i);
                i--;
                continue;
            }

            StateSnapshot snap = reporters[i].ReportState();

            if (snap.Faction == null)
                continue;

            if ((snap.Position - center).sqrMagnitude > radiusSqr)
                continue;

            FactionRelation rel = typedRelations.GetRelation(requesterFaction, snap.Faction);

            if (rel == relation)
            {
                results.Add(snap);
                if (results.Count >= maxResults) break;
            }
        }
    }
}
