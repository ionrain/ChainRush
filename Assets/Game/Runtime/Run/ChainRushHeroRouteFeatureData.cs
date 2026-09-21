using System;
using System.Collections.Generic;
using Core.Activities;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
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
    [CreateAssetMenu(fileName = "ChainRushHeroRoute", menuName = "ChainRush/Gameplay/Hero Route Feature")]
    public sealed class ChainRushHeroRouteFeatureData : ActivityFeatureData
    {
        [SerializeField] List<ChainRushHeroDefinitionBinding> heroes = new List<ChainRushHeroDefinitionBinding>();
        [SerializeField] FrameworkSkillData movement;
        [SerializeField] HostValueData health;

        public override IActivityFeatureRuntime CreateRuntime() => new Runtime();

        sealed class Runtime : IActivityFeatureRuntime, IEventListener<CapabilityHostUnregisteredEvent>, IEventListener<HostValueChangedEvent>
        {
            ChainRushHeroRouteFeatureData _data;
            ChainRushRunSnapshot _input;
            ActivityId _activity;
            IEconomyAssetOwner _owner;
            CapabilityHostData _heroDefinition;
            EntityId _hero;
            SkillExecutionRef _execution;
            bool _started;

            public ActivityFeatureTickStageType TickStages => ActivityFeatureTickStageType.BeforeObjective;

            public bool Validate(in ActivityFeatureValidationContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                if (context.Bindings.Count != 1 || !(context.Bindings[0].Feature is ChainRushHeroRouteFeatureData data)
                    || data.heroes.Count == 0 || data.movement == null || data.health == null
                    || data.movement.TargetType != SkillTargetType.Position)
                    diagnosticMessage = "Hero route requires hero bindings, health and a position movement skill.";
                return diagnosticMessage == null;
            }

            public bool Open(in ActivityFeatureOpenContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                _data = (ChainRushHeroRouteFeatureData)context.Bindings[0].Feature;
                EventBus.Trigger(new ChainRushRunInputRequestEvent(input =>
                {
                    if (_input != null) throw new InvalidOperationException("More than one run input source is active.");
                    _input = input;
                }));
                if (_input == null || _input.GoalType != LevelGoalType.Distance || context.Bindings[0].Participants.Count != 1)
                { diagnosticMessage = "Hero route requires a Distance run and one participant."; return false; }
                foreach (var binding in _data.heroes)
                    if (binding.ContentId == _input.Hero.ContentId)
                    {
                        if (_heroDefinition != null) { diagnosticMessage = "Hero route definition is ambiguous."; return false; }
                        _heroDefinition = binding.Definition;
                    }
                if (_heroDefinition == null) { diagnosticMessage = "Selected hero has no route definition."; return false; }
                _owner = context.Bindings[0].Participants[0].ParticipantEconomyOwner;
                _activity = context.ActivityId;
                EventBus.Register<CapabilityHostUnregisteredEvent>(this);
                EventBus.Register<HostValueChangedEvent>(this);
                return true;
            }

            public void Tick(in ActivityFeatureTickContext context)
            {
                if (_execution.IsValid && SkillService.TryConsumeExecutionObservation(_execution, out var observation)
                    && observation.IsTerminal)
                {
                    _execution = SkillExecutionRef.Invalid;
                    if (observation.Type == SkillExecutionObservationType.Failed)
                        throw new InvalidOperationException("Hero route failed: " + observation.FailureReason);
                }
                if (_started) return;
                foreach (var host in CapabilityHostService.GetAll())
                    if (host.ActivityId == _activity && _heroDefinition.Matches(host.Definition)
                        && host.Owner?.StableSimulationKey == _owner?.StableSimulationKey)
                    {
                        if (_hero.IsValid && _hero != host.EntityId) throw new InvalidOperationException("More than one selected hero is active.");
                        _hero = host.EntityId;
                    }
                if (!_hero.IsValid || !SpatialService.TryGetPose(_hero, out var pose)
                    || !CapabilityHostService.TryGetHostValue(_hero, _data.health, out var health) || health.CurrentValue <= 0) return;
                var coordinates = pose.Coordinates + Vector3.right * _input.GoalAmount;
                if (!TopologyService.TryResolvePosition(_activity, coordinates, out var destination, out var resolved)
                    || Vector3.Distance(coordinates, resolved) > 0.001f)
                    throw new InvalidOperationException("The authored navigation space does not contain the Distance destination.");
                if (!SkillService.TryResolveId(_data.movement, out var skill))
                    throw new InvalidOperationException("Hero route skill is not registered.");
                _started = true;
                var result = SkillService.TryExecuteAsync(new SkillExecutionRequest(_hero, skill,
                    Core.Skills.SkillTarget.AtPosition(destination))).GetAwaiter().GetResult();
                if (!result.Success) throw new InvalidOperationException("Hero route admission failed: " + result.FailureReason);
                _execution = result.ExecutionRef;
            }

            public void OnEvent(CapabilityHostUnregisteredEvent e)
            {
                if (e.Snapshot.EntityId == _hero) Stop();
            }

            public void OnEvent(HostValueChangedEvent e)
            {
                if (e.EntityId == _hero && _data.health.Matches(e.Value) && e.CurrentRawValue <= 0) Stop();
            }

            public void Close(in ActivityFeatureCloseContext context)
            {
                EventBus.Unregister<CapabilityHostUnregisteredEvent>(this);
                EventBus.Unregister<HostValueChangedEvent>(this);
                Stop();
                _hero = EntityId.Invalid;
                _heroDefinition = null;
                _input = null;
                _owner = null;
                _started = false;
            }

            void Stop()
            {
                if (_hero.IsValid) SkillService.TryInterrupt(_hero, _data.movement);
                if (_execution.IsValid) SkillService.TryConsumeExecutionObservation(_execution, out _);
                _execution = SkillExecutionRef.Invalid;
            }
        }
    }
}
