using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Typed faction relation table using <see cref="FactionAsset"/> references.
/// IMPORTANT: Combat orchestration runtime uses this exclusively (no legacy fallback).
/// </summary>
[CreateAssetMenu(fileName = "FactionRelationTable", menuName = "Game/Orchestration/Faction Relation Table")]
public sealed class FactionRelationTableAsset : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public FactionAsset A;
        public FactionAsset B;
        public FactionRelation Relation;
    }

    [SerializeField] List<Entry> entries;

    /// <summary>
    /// Returns the relation between two factions.
    /// <list type="bullet">
    /// <item>Null faction → <see cref="FactionRelation.Neutral"/>.</item>
    /// <item>Same asset (reference equality) → <see cref="FactionRelation.Friendly"/>.</item>
    /// <item>Linear scan, symmetric match (A,B) or (B,A).</item>
    /// <item>No match → <see cref="FactionRelation.Neutral"/>.</item>
    /// </list>
    /// </summary>
    public FactionRelation GetRelation(FactionAsset a, FactionAsset b)
    {
        if (a == null || b == null)
            return FactionRelation.Neutral;

        if (ReferenceEquals(a, b))
            return FactionRelation.Friendly;

        if (entries == null || entries.Count == 0)
            return FactionRelation.Neutral;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];

            if ((ReferenceEquals(e.A, a) && ReferenceEquals(e.B, b)) ||
                (ReferenceEquals(e.A, b) && ReferenceEquals(e.B, a)))
                return e.Relation;
        }

        return FactionRelation.Neutral;
    }
}
