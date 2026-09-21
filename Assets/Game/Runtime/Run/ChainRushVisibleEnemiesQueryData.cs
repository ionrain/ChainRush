using System;
using System.Collections.Generic;
using Core.CapabilityHosts.Runtime;
using Core.Diplomacy;
using Core.Events;
using Core.HostValues;
using Core.Skills;
using Core.World;
using UnityEngine;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Gameplay
{
    [CreateAssetMenu(fileName = "ChainRushVisibleEnemies", menuName = "ChainRush/Gameplay/Visible Enemies Query")]
    public sealed class ChainRushVisibleEnemiesQueryData : SkillCarrierTargetQueryData
    {
        [SerializeField] HostValueData health;
        public override bool TryValidate(out string failure)
        { failure = health == null ? "Visible enemies require an explicit health value." : null; return failure == null; }

        public override bool TryCollect(SkillExecutionContext context, List<EntityId> candidates, out string failure)
        {
            candidates.Clear();
            failure = null;
            if (!TryValidate(out failure) || !CapabilityHostService.TryGet(context.OwnerEntityId, out var owner))
            { failure = failure ?? "The visible-enemy query owner is unavailable."; return false; }
            var eligible = new List<ChainRushCombatViewCandidate>();
            var allowed = new HashSet<EntityId>();
            foreach (var host in CapabilityHostService.GetAll())
                if (host.ActivityId == owner.ActivityId && host.EntityId != owner.EntityId
                    && CapabilityHostService.TryGetHostValue(host.EntityId, health, out var value) && value.CurrentValue > 0
                    && DiplomacyService.GetRelation(owner.ActivityId, owner.EntityId, host.EntityId,
                        DiplomacyChannelType.Military).Disposition == DiplomacyDispositionType.Hostile
                    && SpatialService.TryGetPose(host.EntityId, out var pose))
                { eligible.Add(new ChainRushCombatViewCandidate(host.EntityId, pose.Coordinates)); allowed.Add(host.EntityId); }
            eligible.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
            List<EntityId> visible = null;
            EventBus.Trigger(new ChainRushCombatViewRequestEvent(eligible, result =>
            {
                if (visible != null) throw new InvalidOperationException("More than one combat view adapter is active.");
                visible = result ?? throw new InvalidOperationException("Combat view returned no candidate list.");
            }));
            if (visible == null) { failure = "The integration combat view is missing."; return false; }
            foreach (var entity in visible)
            {
                if (!allowed.Contains(entity)) { failure = "Combat view returned an ineligible candidate."; return false; }
                if (!candidates.Contains(entity)) candidates.Add(entity);
            }
            candidates.Sort();
            return true;
        }
    }
}
