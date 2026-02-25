using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven cross-capability matching rules.
/// Determines whether a source (pipeline's actors collectively) can engage a target.
/// IMPORTANT: "Source" and "Target" are neutral terms — orchestrator may assign
/// attack, support, healing, or any other interaction.
/// RATIONALE: Rules are target-centric: "if target has capability X, source must have ≥1 of [A, B, C]."
/// If no rule matches the target's capabilities, engagement is allowed (default: engageable).
/// </summary>
[CreateAssetMenu(fileName = "EngagementPolicy", menuName = "Game/Orchestration/Actor Capability Engagement Policy")]
public sealed class ActorCapabilityEngagementPolicy : ScriptableObject
{
    [SerializeField] List<EngagementRule> rules;

    [Serializable]
    public struct EngagementRule
    {
        [Tooltip("If the target has this capability...")]
        public ActorCapability targetCapability;

        [Tooltip("...the source side must have at least one of these.")]
        public List<ActorCapability> sourceRequiresAny;
    }

    /// <summary>
    /// Returns true if sourceCaps can engage targetCaps.
    /// For each rule: if target has the rule's capability and source has none of the required → false.
    /// PERF: Linear scan per rule. Rules count is small (typically 2–5).
    /// </summary>
    public bool CanEngage(in ActorCapabilitySnapshot sourceCaps, in ActorCapabilitySnapshot targetCaps)
    {
        if (rules == null) return true;
        for (int i = 0; i < rules.Count; i++)
        {
            EngagementRule rule = rules[i];
            if (rule.targetCapability == null) continue;
            if (!targetCaps.Has(rule.targetCapability)) continue;

            // Target has the guarded capability — check source
            if (!sourceCaps.HasAny(rule.sourceRequiresAny))
                return false;
        }
        return true;
    }
}
