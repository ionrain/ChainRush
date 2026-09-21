using System;
using System.Collections.Generic;
using Core.Activities;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Economy;
using Core.Events;
using Core.World;
using UnityEngine;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Gameplay
{
    [CreateAssetMenu(fileName = "ChainRushRunFollow", menuName = "ChainRush/Gameplay/Run Follow Feature")]
    public sealed class ChainRushRunFollowFeatureData : ActivityFeatureData
    {
        [SerializeField] List<ChainRushHeroDefinitionBinding> heroes = new List<ChainRushHeroDefinitionBinding>();
        [SerializeField] List<CapabilityHostData> spatialFollowers = new List<CapabilityHostData>();

        public override IActivityFeatureRuntime CreateRuntime() => new Runtime();

        sealed class Runtime : IActivityFeatureRuntime, IEventListener<CapabilityHostUnregisteredEvent>
        {
            readonly Dictionary<EntityId, Vector3> _origins = new Dictionary<EntityId, Vector3>();
            ChainRushRunFollowFeatureData _data;
            ActivityId _activity;
            IEconomyAssetOwner _owner;
            CapabilityHostData _heroDefinition;
            EntityId _hero;
            Vector3 _origin;
            bool _captured;

            public ActivityFeatureTickStageType TickStages => ActivityFeatureTickStageType.BeforeObjective;

            public bool Validate(in ActivityFeatureValidationContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                if (context.Bindings.Count != 1 || !(context.Bindings[0].Feature is ChainRushRunFollowFeatureData data)
                    || data.heroes.Count == 0 || data.spatialFollowers.Count == 0 || data.spatialFollowers.Exists(value => value == null))
                    diagnosticMessage = "Run follow requires explicit hero and spatial follower definitions.";
                return diagnosticMessage == null;
            }

            public bool Open(in ActivityFeatureOpenContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                _data = (ChainRushRunFollowFeatureData)context.Bindings[0].Feature;
                ChainRushRunSnapshot input = null;
                EventBus.Trigger(new ChainRushRunInputRequestEvent(value =>
                {
                    if (input != null) throw new InvalidOperationException("More than one run input source is active.");
                    input = value;
                }));
                if (input == null || context.Bindings[0].Participants.Count != 1)
                { diagnosticMessage = "Run follow requires one participant and captured run input."; return false; }
                foreach (var binding in _data.heroes)
                    if (binding.ContentId == input.Hero.ContentId)
                    {
                        if (_heroDefinition != null) { diagnosticMessage = "Run follow hero is ambiguous."; return false; }
                        _heroDefinition = binding.Definition;
                    }
                if (_heroDefinition == null) { diagnosticMessage = "Selected hero has no follow binding."; return false; }
                _owner = context.Bindings[0].Participants[0].ParticipantEconomyOwner;
                _activity = context.ActivityId;
                EventBus.Register<CapabilityHostUnregisteredEvent>(this);
                return true;
            }

            public void Tick(in ActivityFeatureTickContext context)
            {
                if (!_captured)
                {
                    foreach (var host in CapabilityHostService.GetAll())
                        if (BelongsToRun(host.ActivityId, host.Owner) && _heroDefinition.Matches(host.Definition)
                            && SpatialService.TryGetPose(host.EntityId, out var pose))
                        {
                            if (_captured) throw new InvalidOperationException("More than one selected hero is active.");
                            _hero = host.EntityId;
                            _origin = pose.Coordinates;
                            _captured = true;
                        }
                }
                if (!_captured || !SpatialService.TryGetPose(_hero, out var heroPose)) return;
                // Source UnitManager moves deployment areas along X, preserving each area's original offset.
                Vector3 displacement = Vector3.right * (heroPose.Coordinates.x - _origin.x);
                foreach (var host in CapabilityHostService.GetAll())
                {
                    if (!BelongsToRun(host.ActivityId, host.Owner)
                        || !_data.spatialFollowers.Exists(definition => definition.Matches(host.Definition))
                        || !SpatialService.TryGetPose(host.EntityId, out var pose)) continue;
                    if (!_origins.TryGetValue(host.EntityId, out var origin))
                    { origin = pose.Coordinates; _origins.Add(host.EntityId, origin); }
                    Vector3 destination = origin + displacement;
                    if (pose.Coordinates == destination) continue;
                    if (!SpatialService.TrySetPosition(host.EntityId, destination, out _, out var resolved)
                        || Vector3.Distance(resolved, destination) > 0.001f)
                        throw new InvalidOperationException("A run follower left its authored topology.");
                }
                EventBus.Trigger(new ChainRushRunDisplacementEvent(_activity, displacement, true));
            }

            bool BelongsToRun(ActivityId activity, IEconomyAssetOwner owner) =>
                activity == _activity && owner?.StableSimulationKey == _owner?.StableSimulationKey;

            public void OnEvent(CapabilityHostUnregisteredEvent e) => _origins.Remove(e.Snapshot.EntityId);

            public void Close(in ActivityFeatureCloseContext context)
            {
                EventBus.Unregister<CapabilityHostUnregisteredEvent>(this);
                EventBus.Trigger(new ChainRushRunDisplacementEvent(_activity, Vector3.zero, false));
                _origins.Clear();
                _captured = false;
                _hero = EntityId.Invalid;
                _heroDefinition = null;
                _owner = null;
                _data = null;
            }
        }
    }
}
