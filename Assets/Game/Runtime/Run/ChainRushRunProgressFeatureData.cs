using System;
using System.Collections.Generic;
using Core.Activities;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Economy;
using Core.Economy.Authoring;
using Core.Events;
using Core.Taxonomy;
using Core.World;
using UnityEngine;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Gameplay
{
    /// <summary>Activity-owned adapter from the immutable run input to the shared progress resource.</summary>
    [CreateAssetMenu(fileName = "ChainRushRunProgress", menuName = "ChainRush/Gameplay/Run Progress Feature")]
    public sealed class ChainRushRunProgressFeatureData : ActivityFeatureData
    {
        [SerializeField] EconomyAssetData progress;
        [SerializeField] List<TaxonomyTermData> walletTags = new List<TaxonomyTermData>();
        [SerializeField] long progressResolution;
        [SerializeField] float distanceScale;
        [SerializeField] List<ChainRushHeroDefinitionBinding> heroes = new List<ChainRushHeroDefinitionBinding>();

        public override IActivityFeatureRuntime CreateRuntime() => new Runtime();

        sealed class Runtime : IActivityFeatureRuntime, IEventListener<CapabilityHostRegisteredEvent>,
            IEventListener<CapabilityHostUnregisteredEvent>
        {
            ChainRushRunProgressFeatureData _data;
            ChainRushRunSnapshot _input;
            IEconomyAssetOwner _owner;
            ActivityId _activity;
            CapabilityHostData _heroDefinition;
            EntityId _hero;
            bool _hasOrigin;
            float _origin;
            double _elapsed;
            long _published;

            public ActivityFeatureTickStageType TickStages => ActivityFeatureTickStageType.BeforeObjective;

            public bool Validate(in ActivityFeatureValidationContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                if (context.Bindings.Count != 1 || !(context.Bindings[0].Feature is ChainRushRunProgressFeatureData data)
                    || data.progress == null || data.walletTags.Count == 0 || data.progressResolution <= 0
                    || !(data.distanceScale > 0) || data.heroes.Count == 0)
                    diagnosticMessage = "ChainRush progress requires one team binding, resource, wallet, resolution, distance scale and hero definitions.";
                return diagnosticMessage == null;
            }

            public bool Open(in ActivityFeatureOpenContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                _data = (ChainRushRunProgressFeatureData)context.Bindings[0].Feature;
                var request = new ChainRushRunInputRequestEvent(snapshot =>
                {
                    if (_input != null) throw new InvalidOperationException("More than one ChainRush input source is active.");
                    _input = snapshot;
                });
                EventBus.Trigger(request);
                if (_input == null || context.Bindings[0].Participants.Count != 1)
                { diagnosticMessage = "ChainRush progress requires captured input and one participant."; return false; }
                foreach (ChainRushHeroDefinitionBinding hero in _data.heroes)
                    if (hero.ContentId == _input.Hero.ContentId)
                    {
                        if (_heroDefinition != null) { diagnosticMessage = "Hero definition mapping is ambiguous."; return false; }
                        _heroDefinition = hero.Definition;
                    }
                if (_heroDefinition == null)
                { diagnosticMessage = "Selected hero has no framework definition."; return false; }
                _activity = context.ActivityId;
                _owner = context.Bindings[0].Participants[0].ParticipantEconomyOwner;
                long existing = 0;
                foreach (EconomySelectionQueryItem item in EconomyService.Query(new EconomySelectionQuery(
                    _owner, _data.walletTags, EconomyFormType.Stack, _data.progress)).Items)
                    existing = checked(existing + item.Balance);
                if (existing != 0)
                { diagnosticMessage = "The run progress wallet must start empty."; return false; }
                EventBus.Register<CapabilityHostRegisteredEvent>(this);
                EventBus.Register<CapabilityHostUnregisteredEvent>(this);
                foreach (CapabilityHostSnapshot host in CapabilityHostService.GetAll()) Observe(host);
                return true;
            }

            public void Tick(in ActivityFeatureTickContext context)
            {
                if (_published == _data.progressResolution) return;
                _elapsed += context.Tick.DeltaTime;
                double fraction;
                if (_input.GoalType == LevelGoalType.Survive) fraction = _elapsed / _input.GoalAmount;
                else
                {
                    if (!_hero.IsValid || !SpatialService.TryGetPose(_hero, out SpatialPose pose)) return;
                    if (!_hasOrigin) { _origin = pose.Coordinates.x; _hasOrigin = true; }
                    fraction = (pose.Coordinates.x - _origin) / (_data.distanceScale * _input.GoalAmount);
                }
                long value = (long)Math.Floor(Math.Max(0, Math.Min(1, fraction)) * _data.progressResolution);
                Publish(value);
            }

            void Publish(long value)
            {
                long delta = value - _published;
                if (delta == 0) return;
                EconomyMutationResult result = EconomyService.TryExecute(new EconomySelectionOperationRequest(
                    _owner, _data.walletTags, delta > 0 ? EconomyOperation.Issue : EconomyOperation.Consume,
                    _data.progress, EconomyFormType.Stack, Math.Abs(delta), EconomyTransactionTraceContext.None));
                if (!result.Success) throw new InvalidOperationException("Run progress Economy update failed.");
                _published = value;
            }

            void Observe(CapabilityHostSnapshot host)
            {
                if (host.ActivityId == _activity && _heroDefinition.Matches(host.Definition)
                    && host.Owner?.StableSimulationKey == _owner?.StableSimulationKey)
                {
                    if (_hero.IsValid && _hero != host.EntityId)
                        throw new InvalidOperationException("A run has more than one selected hero.");
                    _hero = host.EntityId;
                    if (!_hasOrigin && SpatialService.TryGetPose(_hero, out SpatialPose pose))
                    { _origin = pose.Coordinates.x; _hasOrigin = true; }
                }
            }

            public void OnEvent(CapabilityHostRegisteredEvent e) => Observe(e.Snapshot);
            public void OnEvent(CapabilityHostUnregisteredEvent e)
            {
                if (e.Snapshot.EntityId == _hero) _hero = EntityId.Invalid;
            }

            public void Close(in ActivityFeatureCloseContext context)
            {
                EventBus.Unregister<CapabilityHostRegisteredEvent>(this);
                EventBus.Unregister<CapabilityHostUnregisteredEvent>(this);
                if (_published != 0) Publish(0);
                _owner = null;
                _input = null;
                _hero = EntityId.Invalid;
                _heroDefinition = null;
                _hasOrigin = false;
                _elapsed = 0;
            }
        }
    }

    [Serializable]
    public sealed class ChainRushHeroDefinitionBinding
    {
        [SerializeField] string contentId;
        [SerializeField] CapabilityHostData definition;
        public string ContentId => contentId;
        public CapabilityHostData Definition => definition;
    }
}
