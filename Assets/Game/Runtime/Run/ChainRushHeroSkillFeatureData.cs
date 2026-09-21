using System;
using System.Collections.Generic;
using Core.Activities;
using Core.Activities.Selection;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Diplomacy;
using Core.Economy;
using Core.Events;
using Core.HostValues;
using Core.Skills;
using Core.World;
using UnityEngine;
using EntityId = Core.Entities.EntityId;
using FrameworkSkillData = Core.Skills.SkillData;

namespace ChainRush.Gameplay
{
    [CreateAssetMenu(fileName = "ChainRushHeroSkill", menuName = "ChainRush/Gameplay/Hero Skill Feature")]
    public sealed class ChainRushHeroSkillFeatureData : ActivityFeatureData
    {
        [SerializeField] ActivityData board;
        [SerializeField] CapabilityHostData hero;
        [SerializeField] CapabilityHostData cell;
        [SerializeField] HostValueData health;
        [SerializeField] ChainRushVisibleEnemiesQueryData targets;
        [SerializeField] string sourceSkillId;
        [SerializeField] List<FrameworkSkillData> levels = new List<FrameworkSkillData>();
        public override IActivityFeatureRuntime CreateRuntime() => new Runtime();

        sealed class Runtime : IActivityFeatureRuntime, IEventListener<SelectionResultEvent>
        {
            ChainRushHeroSkillFeatureData _data;
            ActivityId _activity;
            IEconomyAssetOwner _owner;
            bool _acquired;
            readonly HashSet<(ActivityId, SelectionRequestId)> _handled = new HashSet<(ActivityId, SelectionRequestId)>();
            readonly List<SkillExecutionRef> _executions = new List<SkillExecutionRef>();
            public ActivityFeatureTickStageType TickStages => ActivityFeatureTickStageType.BeforeObjective;

            public bool Validate(in ActivityFeatureValidationContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                if (context.Bindings.Count != 1 || !(context.Bindings[0].Feature is ChainRushHeroSkillFeatureData data)
                    || data.board == null || data.hero == null || data.cell == null || data.health == null || data.targets == null
                    || string.IsNullOrWhiteSpace(data.sourceSkillId) || data.levels.Count == 0 || data.levels.Exists(level => level == null))
                    diagnosticMessage = "Hero skill requires one participant, explicit hero/cell, health and all authored skill levels.";
                return diagnosticMessage == null;
            }

            public bool Open(in ActivityFeatureOpenContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                _data = (ChainRushHeroSkillFeatureData)context.Bindings[0].Feature;
                ChainRushRunSnapshot input = null;
                EventBus.Trigger(new ChainRushRunInputRequestEvent(snapshot =>
                {
                    if (input != null) throw new InvalidOperationException("More than one run input source is active.");
                    input = snapshot;
                }));
                if (input == null || context.Bindings[0].Participants.Count != 1)
                { diagnosticMessage = "Hero skill requires captured run input and one participant."; return false; }
                _activity = context.ActivityId;
                _owner = context.Bindings[0].Participants[0].ParticipantEconomyOwner;
                foreach (string skill in input.Hero.AcquiredSkills) _acquired |= skill == _data.sourceSkillId;
                EventBus.Register<SelectionResultEvent>(this);
                return true;
            }

            public void OnEvent(SelectionResultEvent e)
            {
                if (e.Type != SelectionResultType.Committed || e.SelectedEntityIds.Count == 0
                    || !ActivityService.TryGetSnapshot(e.ActivityId, out var activity) || !_data.board.Matches(activity.Definition)
                    || !CapabilityHostService.TryGet(e.SelectedEntityIds[0], out var first) || !_data.cell.Matches(first.Definition)
                    || first.Owner?.StableSimulationKey != _owner?.StableSimulationKey) return;
                foreach (EntityId id in e.SelectedEntityIds)
                    if (!CapabilityHostService.TryGet(id, out var selected) || !_data.cell.Matches(selected.Definition)
                        || selected.ActivityId != e.ActivityId || selected.Owner?.StableSimulationKey != _owner?.StableSimulationKey)
                        throw new InvalidOperationException("Hero skill selection contains mismatching cells.");
                if (!_handled.Add((e.ActivityId, e.RequestId))) return;
                if (!_acquired) throw new InvalidOperationException("Selected hero skill is absent from captured acquired skills.");
                EntityId caster = EntityId.Invalid;
                foreach (var host in CapabilityHostService.GetAll())
                    if (host.ActivityId == _activity && host.Owner?.StableSimulationKey == _owner?.StableSimulationKey
                        && _data.hero.Matches(host.Definition))
                    {
                        if (caster.IsValid) throw new InvalidOperationException("Hero skill found more than one hero.");
                        caster = host.EntityId;
                    }
                if (!caster.IsValid || !IsAlive(caster)) return;
                var visible = new List<EntityId>();
                if (!_data.targets.TryCollect(new SkillExecutionContext(caster, caster, EntityId.Invalid,
                    Core.Skills.SkillTarget.Self()), visible, out string failure)) throw new InvalidOperationException(failure);
                if (visible.Count == 0) return;
                var skill = _data.levels[Math.Min(e.SelectedEntityIds.Count - 1, _data.levels.Count - 1)];
                if (!SkillService.TryResolveId(skill, out var skillId)) throw new InvalidOperationException("Hero skill is unregistered.");
                var result = SkillService.TryExecuteAsync(new SkillExecutionRequest(caster, skillId,
                    Core.Skills.SkillTarget.Entities(visible))).GetAwaiter().GetResult();
                if (!result.Success) throw new InvalidOperationException("Hero skill admission failed: " + result.FailureReason);
                if (result.ExecutionRef.IsValid) _executions.Add(result.ExecutionRef);
            }

            bool IsAlive(EntityId entity) => CapabilityHostService.TryGetHostValue(entity, _data.health, out var health)
                && health.CurrentValue > 0;

            public void Tick(in ActivityFeatureTickContext context)
            {
                for (int i = _executions.Count - 1; i >= 0; i--)
                {
                    if (!SkillService.TryConsumeExecutionObservation(_executions[i], out var observation) || !observation.IsTerminal) continue;
                    _executions.RemoveAt(i);
                    if (observation.Type == SkillExecutionObservationType.Failed)
                        throw new InvalidOperationException("Hero skill execution failed: " + observation.FailureReason);
                }
            }

            public void Close(in ActivityFeatureCloseContext context)
            {
                EventBus.Unregister<SelectionResultEvent>(this);
                foreach (var execution in _executions)
                {
                    if (SkillService.TryResolve(execution.SkillId, out var skill)) SkillService.TryInterrupt(execution.HostEntityId, skill);
                    SkillService.TryConsumeExecutionObservation(execution, out _);
                }
                _executions.Clear(); _handled.Clear(); _owner = null; _data = null; _acquired = false;
            }
        }
    }
}
