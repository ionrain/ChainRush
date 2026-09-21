using System;
using System.Collections.Generic;
using Core.Activities;
using Core.Activities.Selection;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Economy;
using Core.Events;
using Core.HostValues;
using Core.Skills;
using UnityEngine;
using FrameworkSkillData = Core.Skills.SkillData;

namespace ChainRush.Gameplay
{
    [CreateAssetMenu(fileName = "ChainRushHeal", menuName = "ChainRush/Gameplay/Heal Feature")]
    public sealed class ChainRushHealFeatureData : ActivityFeatureData
    {
        [SerializeField] ActivityData board;
        [SerializeField] CapabilityHostData cell;
        [SerializeField] HostValueData health;
        [SerializeField] List<CapabilityHostData> recipients = new List<CapabilityHostData>();
        [SerializeField] List<FrameworkSkillData> chainSkills = new List<FrameworkSkillData>();
        public override IActivityFeatureRuntime CreateRuntime() => new Runtime();

        sealed class Runtime : IActivityFeatureRuntime, IEventListener<SelectionResultEvent>
        {
            ChainRushHealFeatureData _data;
            ActivityId _activity;
            IEconomyAssetOwner _owner;
            readonly HashSet<(ActivityId, SelectionRequestId)> _handled = new HashSet<(ActivityId, SelectionRequestId)>();
            public ActivityFeatureTickStageType TickStages => ActivityFeatureTickStageType.None;

            public bool Validate(in ActivityFeatureValidationContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                if (context.Bindings.Count != 1 || !(context.Bindings[0].Feature is ChainRushHealFeatureData data)
                    || data.board == null || data.cell == null || data.health == null || data.recipients.Count == 0
                    || data.recipients.Exists(value => value == null) || data.chainSkills.Count == 0
                    || data.chainSkills.Exists(value => value == null || value.TargetType != SkillTargetType.Self))
                    diagnosticMessage = "Healing requires Board, cell, health, eligible definitions and all chain skills.";
                return diagnosticMessage == null;
            }

            public bool Open(in ActivityFeatureOpenContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                if (context.Bindings[0].Participants.Count != 1)
                { diagnosticMessage = "Healing requires one run participant."; return false; }
                _data = (ChainRushHealFeatureData)context.Bindings[0].Feature;
                _activity = context.ActivityId;
                _owner = context.Bindings[0].Participants[0].ParticipantEconomyOwner;
                EventBus.Register<SelectionResultEvent>(this);
                return true;
            }

            public void OnEvent(SelectionResultEvent e)
            {
                if (e.Type != SelectionResultType.Committed || e.SelectedEntityIds.Count == 0
                    || !ActivityService.TryGetSnapshot(e.ActivityId, out var activity) || !_data.board.Matches(activity.Definition)
                    || !CapabilityHostService.TryGet(e.SelectedEntityIds[0], out var first) || !_data.cell.Matches(first.Definition)
                    || first.Owner?.StableSimulationKey != _owner?.StableSimulationKey) return;
                foreach (var entity in e.SelectedEntityIds)
                    if (!CapabilityHostService.TryGet(entity, out var selected) || !_data.cell.Matches(selected.Definition)
                        || selected.ActivityId != e.ActivityId || selected.Owner?.StableSimulationKey != _owner?.StableSimulationKey)
                        throw new InvalidOperationException("Heal selection contains mismatching cells.");
                if (!_handled.Add((e.ActivityId, e.RequestId))) return;
                if (e.SelectedEntityIds.Count > _data.chainSkills.Count)
                    throw new InvalidOperationException("Heal chain exceeds its authored board capacity.");
                var skill = _data.chainSkills[e.SelectedEntityIds.Count - 1];
                if (!SkillService.TryResolveId(skill, out var skillId)) throw new InvalidOperationException("Heal skill is unregistered.");
                var hosts = CapabilityHostService.GetAll();
                hosts.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
                foreach (var host in hosts)
                {
                    if (host.ActivityId != _activity || host.Owner?.StableSimulationKey != _owner?.StableSimulationKey
                        || !_data.recipients.Exists(definition => definition.Matches(host.Definition))
                        || !CapabilityHostService.TryGetHostValue(host.EntityId, _data.health, out var health) || health.CurrentValue <= 0) continue;
                    var result = SkillService.TryExecuteAsync(new SkillExecutionRequest(host.EntityId, skillId,
                        Core.Skills.SkillTarget.Self())).GetAwaiter().GetResult();
                    if (result.ExecutionRef.IsValid) SkillService.TryConsumeExecutionObservation(result.ExecutionRef, out _);
                    if (!result.Success || result.Status != SkillExecutionStatus.Completed)
                        throw new InvalidOperationException("Instant heal failed: " + result.FailureReason);
                }
            }

            public void Tick(in ActivityFeatureTickContext context) { }
            public void Close(in ActivityFeatureCloseContext context)
            {
                EventBus.Unregister<SelectionResultEvent>(this);
                _handled.Clear(); _owner = null; _data = null;
            }
        }
    }
}
