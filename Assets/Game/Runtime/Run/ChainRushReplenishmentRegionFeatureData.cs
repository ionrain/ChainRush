using System;
using System.Collections.Generic;
using Core.Activities;
using Core.Economy;
using Core.Economy.Authoring;
using Core.Taxonomy;
using Core.World;
using UnityEngine;

namespace ChainRush.Gameplay
{
    [CreateAssetMenu(fileName = "ChainRushReplenishmentRegion", menuName = "ChainRush/Gameplay/Replenishment Region")]
    public sealed class ChainRushReplenishmentRegionFeatureData : ActivityFeatureData
    {
        [SerializeField] string regionId;
        [SerializeField] Vector3 center;
        [SerializeField] Vector3 size;
        [SerializeField] List<TaxonomyTermData> tags = new List<TaxonomyTermData>();
        [SerializeField] float distance;
        [SerializeField] EconomyAssetData progress;
        [SerializeField] EconomyOperationOwnerBindingData progressOwner = new EconomyOperationOwnerBindingData();
        [SerializeField] List<TaxonomyTermData> walletTags = new List<TaxonomyTermData>();
        [SerializeField] long progressResolution;

        public override IActivityFeatureRuntime CreateRuntime() => new Runtime();

        sealed class Runtime : IActivityFeatureRuntime
        {
            ChainRushReplenishmentRegionFeatureData _data;
            ActivityRuntimeSnapshot _activity;
            IEconomyAssetOwner _progressOwner;
            SpaceRegionGeometryDefinition _geometry;
            SpaceRegionHandle _handle;
            long _publishedProgress;

            public ActivityFeatureTickStageType TickStages => ActivityFeatureTickStageType.BeforeObjective;

            public bool Validate(in ActivityFeatureValidationContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                if (context.Bindings.Count != 1 || !(context.Bindings[0].Feature is ChainRushReplenishmentRegionFeatureData data)
                    || !new SpaceRegionId(data.regionId).IsValid || data.size.x <= 0 || data.size.z <= 0
                    || data.size.y < 0 || !float.IsFinite(data.distance) || data.distance < 0
                    || data.tags.Count == 0 || data.tags.Exists(tag => tag == null)
                    || data.progress == null || data.progressOwner == null || data.walletTags.Count == 0
                    || data.walletTags.Exists(tag => tag == null) || data.progressResolution <= 0)
                    diagnosticMessage = "Replenishment region requires one binding, explicit geometry, tags and progress source.";
                return diagnosticMessage == null;
            }

            public bool Open(in ActivityFeatureOpenContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                _data = (ChainRushReplenishmentRegionFeatureData)context.Bindings[0].Feature;
                if (context.Bindings[0].Participants.Count != 1
                    || !ActivityService.TryGetSnapshot(context.ActivityId, out _activity))
                { diagnosticMessage = "Replenishment region requires its Activity and one participant."; return false; }
                _progressOwner = _data.progressOwner.Resolve(context.Bindings[0].Participants[0].ParticipantEconomyOwner);
                if (_progressOwner == null)
                { diagnosticMessage = "Replenishment progress owner is unavailable."; return false; }
                _geometry = SpaceRegionGeometryDefinition.Volume(new SpaceRegionBounds(Vector3.zero, _data.size));
                _publishedProgress = ReadProgress();
                if (!TryRegistration(_publishedProgress, out var registration, out diagnosticMessage)) return false;
                return SpaceRegionService.TryRegister(registration, out _handle, out diagnosticMessage);
            }

            public void Tick(in ActivityFeatureTickContext context)
            {
                if (_data.distance == 0) return;
                long current = ReadProgress();
                if (current == _publishedProgress) return;
                if (!TryRegistration(current, out var registration, out string failure)
                    || !SpaceRegionService.TryUpdate(_handle, registration, out failure))
                    throw new InvalidOperationException("Replenishment region update failed: " + failure);
                _publishedProgress = current;
            }

            long ReadProgress() => EconomyService.QueryAmount(new EconomyAmountQuery(
                _progressOwner, _data.walletTags, _data.progress, EconomyFormType.Stack));

            bool TryRegistration(long value, out SpaceRegionRegistration registration, out string failure)
            {
                registration = default;
                failure = null;
                if (value < 0 || value > _data.progressResolution)
                { failure = "Run progress is outside its authored range."; return false; }
                Vector3 coordinates = _data.center + Vector3.right * (_data.distance * value / _data.progressResolution);
                if (!TopologyService.TryResolvePosition(_activity.Id, coordinates, out var position, out coordinates))
                { failure = "Replenishment region is outside its Activity topology."; return false; }
                registration = new SpaceRegionRegistration(_activity.Id, _activity.ActivityRootEntityId,
                    new SpaceRegionId(_data.regionId), default, new SpatialPose(position, coordinates, Quaternion.identity),
                    _geometry, _data.tags, SpaceRegionStateType.Active, SpaceRegionPlanningAvailabilityType.Available);
                return true;
            }

            public void Close(in ActivityFeatureCloseContext context)
            {
                if (_handle.IsValid) SpaceRegionService.Release(_handle);
                _handle = default;
                _geometry = null;
                _progressOwner = null;
                _data = null;
                _activity = default;
                _publishedProgress = 0;
            }
        }
    }
}
