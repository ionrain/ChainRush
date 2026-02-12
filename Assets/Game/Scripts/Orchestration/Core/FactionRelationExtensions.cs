public static class FactionRelationExtensions
{
    public static bool IsHostileTo(this FactionRelationsAsset relations, FactionKey a, FactionKey b)
    {
        return relations.GetRelation(a, b) == FactionRelation.Hostile;
    }

    public static bool IsFriendlyTo(this FactionRelationsAsset relations, FactionKey a, FactionKey b)
    {
        return relations.GetRelation(a, b) == FactionRelation.Friendly;
    }

    public static bool IsNeutralTo(this FactionRelationsAsset relations, FactionKey a, FactionKey b)
    {
        return relations.GetRelation(a, b) == FactionRelation.Neutral;
    }
}
