using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FactionRelations", menuName = "Game/Orchestration/Faction Relations")]
public class FactionRelationsAsset : ScriptableObject
{
    [Serializable]
    public struct RelationRule
    {
        public FactionKey A;
        public FactionKey B;
        public FactionRelation Relation;
        public bool Symmetric;
    }

    public List<RelationRule> Rules;

    public FactionRelation GetRelation(FactionKey a, FactionKey b)
    {
        if (string.IsNullOrEmpty(a.Id) || string.IsNullOrEmpty(b.Id))
            return FactionRelation.Neutral;

        if (a.Id == b.Id)
            return FactionRelation.Friendly;

        if (Rules == null || Rules.Count == 0)
            return FactionRelation.Neutral;

        for (int i = 0; i < Rules.Count; i++)
        {
            var rule = Rules[i];
            if (rule.A.Id == a.Id && rule.B.Id == b.Id)
                return rule.Relation;

            if (rule.Symmetric && rule.A.Id == b.Id && rule.B.Id == a.Id)
                return rule.Relation;
        }

        return FactionRelation.Neutral;
    }
}
