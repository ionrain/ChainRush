using System;
using System.Collections.Generic;
using System.Linq;
using Core.AI;
using Core.Taxonomy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        static AIBrainAnchorPatternData WriteAnchorPattern(UnitData source, UnitAIProfile profile, UnitAIController ai,
            TaxonomyTermData anchor, double step)
        {
            if (profile.formation == null) throw new InvalidOperationException("Included ally requires its source formation profile.");
            var pattern = ChainRushBoardPlannerAuthoring.WriteContentAsset<AIBrainAnchorPatternData>(
                AIRoot + "/" + source.name.Replace("Data", "") + "AnchorPattern.asset", null);
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Game/Scenes/Level.unity");
            try
            {
                var manager = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UnitManager>(true))
                    .Single(value => value.gameObject.activeInHierarchy);
                var areas = GetField<Dictionary<UnitSpeciality, BoxCollider2D>>(manager, "spawnAreas");
                if (!areas.TryGetValue(source.speciality, out var area) || area == null
                    || area.transform.rotation != Quaternion.identity)
                    throw new InvalidOperationException("Included ally requires its unrotated source deployment area: " + source.name);
                var hero = GetField<Transform>(manager, "heroSpawnPoint");
                Vector3 offset = area.transform.TransformPoint(area.offset) - hero.position;
                Vector3 scale = area.transform.lossyScale;
                SetField(pattern, "anchorOffset", new Vector3Int(Coordinate(offset.x), 0, Coordinate(offset.y)));
                SetField(pattern, "roamSize", new Vector3Int(Distance(area.size.x * Mathf.Abs(scale.x)), 0, Distance(area.size.y * Mathf.Abs(scale.y))));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            var formation = profile.formation;
            var rest = GetField<Vector2>(ai, "rallyRestInterval");
            SetField(pattern, "anchorKey", anchor);
            SetField(pattern, "forwardOffset", Coordinate(formation.forwardOffsetX));
            SetField(pattern, "rowBackstep", Distance(formation.rowBackstepX));
            SetField(pattern, "spread", Distance(formation.spreadY));
            SetField(pattern, "maxPerRow", formation.maxPerLine);
            SetField(pattern, "arrivalDistance", Distance(GetField<float>(ai, "_rallyArrivalThreshold")));
            SetField(pattern, "minimumRest", checked((int)ContentDuration(rest.x, step)));
            SetField(pattern, "maximumRest", checked((int)ContentDuration(rest.y, step)));
            SetField(pattern, "formationRetention", checked((int)ContentDuration(GetField<float>(ai, "idleRoamDelayAfterThreat"), step)));
            SetField(pattern, "catchupDistance", Distance(formation.catchupDistance));
            SetField(pattern, "catchupStopDistance", Distance(formation.catchupStopDistance));
            SetField(pattern, "separationRadius", Distance(GetField<float>(ai, "personalSpaceRadius")));
            SetField(pattern, "separationStrength", Distance(GetField<float>(ai, "separationStrength")));
            SetField(pattern, "maximumSeparation", Distance(GetField<float>(ai, "maxSeparationOffset")));
            SetField(pattern, "maximumNeighbors", GetField<int>(ai, "separationOverlapMax"));
            SetField(pattern, "separationInterval", GetField<bool>(ai, "separationUseIntervalUpdate")
                ? checked((int)ContentDuration(GetField<float>(ai, "separationUpdateInterval"), step)) : 1);
            if (!pattern.IsValid) throw new InvalidOperationException("Invalid authored anchor pattern: " + source.name);
            EditorUtility.SetDirty(pattern);
            return pattern;
        }

        static AIBrainAnchorTargetResolverData WriteAnchorTarget(string name, AIBrainAnchorPatternData pattern, bool force)
        {
            var target = ChainRushBoardPlannerAuthoring.WriteContentAsset<AIBrainAnchorTargetResolverData>(AIRoot + "/" + name + ".asset", null);
            SetField(target, "pattern", pattern); SetField(target, "forceFormation", force);
            EditorUtility.SetDirty(target); return target;
        }
    }
}
