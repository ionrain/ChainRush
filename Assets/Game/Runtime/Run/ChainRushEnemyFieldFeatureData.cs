using System;
using System.Collections.Generic;
using Core.Activities;
using Core.Activities.Analytics;
using Core.Economy;
using Core.Economy.Authoring;
using Core.Events;
using Core.Objectives;
using Core.Orchestration;
using Core.Production;
using Core.Taxonomy;
using UnityEngine;

namespace ChainRush.Gameplay
{
    [CreateAssetMenu(fileName = "ChainRushEnemyField", menuName = "ChainRush/Gameplay/Enemy Field Feature")]
    public sealed class ChainRushEnemyFieldFeatureData : ActivityFeatureData
    {
        [SerializeField] EconomyAssetData progress;
        [SerializeField] EconomyOperationOwnerBindingData progressOwner = new EconomyOperationOwnerBindingData();
        [SerializeField] List<TaxonomyTermData> walletTags = new List<TaxonomyTermData>();
        [SerializeField] long progressResolution;
        [SerializeField] List<TaxonomyTermData> enemyTags = new List<TaxonomyTermData>();

        public override IActivityFeatureRuntime CreateRuntime() => new Runtime();

        public override void CollectRequiredFeatureTypes(List<Type> featureTypes)
        {
            featureTypes.Add(typeof(ActivityAnalyticsConfigData));
            featureTypes.Add(typeof(ActivityOrchestrationConfigData));
        }

        sealed class Runtime : IActivityFeatureRuntime, IEventListener<ChainRushEnemyFieldRequestEvent>
        {
            readonly HashSet<ObjectiveRuntimeId> _objectives = new HashSet<ObjectiveRuntimeId>();
            ChainRushEnemyFieldFeatureData _data;
            ActivityTeamFeatureBinding _binding;
            IEconomyAssetOwner _progressOwner;
            ActivityAnalyticsRead _count;
            ChainRushEnemyFieldSnapshot _snapshot;

            public ActivityFeatureTickStageType TickStages => ActivityFeatureTickStageType.AfterProduction;

            public bool Validate(in ActivityFeatureValidationContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                if (context.Bindings.Count != 1 || !(context.Bindings[0].Feature is ChainRushEnemyFieldFeatureData data)
                    || data.progress == null || data.progressOwner == null || data.progressResolution <= 0
                    || data.walletTags.Count == 0 || data.walletTags.Exists(tag => tag == null)
                    || data.enemyTags.Count == 0 || data.enemyTags.Exists(tag => tag == null))
                    diagnosticMessage = "Enemy field observation requires one team, a progress source and enemy tags.";
                return diagnosticMessage == null;
            }

            public bool Open(in ActivityFeatureOpenContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                _binding = context.Bindings[0];
                _data = (ChainRushEnemyFieldFeatureData)_binding.Feature;
                if (_binding.Participants.Count != 1)
                { diagnosticMessage = "Enemy field observation requires one enemy participant."; return false; }
                _progressOwner = _data.progressOwner.Resolve(_binding.Participants[0].ParticipantEconomyOwner);
                if (_progressOwner == null)
                { diagnosticMessage = "Enemy field progress owner is unavailable."; return false; }
                _snapshot = Pending(false, "Waiting for the first simulation observation.");
                EventBus.Register<ChainRushEnemyFieldRequestEvent>(this);
                return true;
            }

            public void Tick(in ActivityFeatureTickContext context)
            {
                bool complete = EconomyService.QueryAmount(new EconomyAmountQuery(_progressOwner,
                    _data.walletTags, _data.progress, EconomyFormType.Stack)) >= _data.progressResolution;
                if (_count == null && !ActivityAnalyticsService.TryPrepareEntityCount(context.RuntimeDomainId,
                    _binding.Participants[0].ParticipantEntityId,
                    new EntityCountActivityAnalyticsQuery(ActivityAnalyticsAggregationScopeType.Participant, null, _data.enemyTags),
                    true, out _count, out string failure))
                { _snapshot = Pending(complete, failure); return; }
                var existing = _count.Read(ActivityAnalyticsMeasureType.ExistingEntities);
                var incoming = _count.Read(ActivityAnalyticsMeasureType.AcceptedOutputs);
                if (existing.State != ActivityAnalyticsReadStateType.Ready || incoming.State != ActivityAnalyticsReadStateType.Ready)
                { _snapshot = Pending(complete, existing.Reason ?? incoming.Reason); return; }
                if (!ActivityService.TryGetSnapshot(context.ActivityId, out var activity)
                    || !AgentService.TryGetAssignmentBoardSnapshot(context.RuntimeDomainId, out var assignments))
                { _snapshot = Pending(complete, "Activity or assignment observation is unavailable."); return; }

                _objectives.Clear();
                foreach (var objective in activity.Objectives)
                    if (objective.TeamEntityId == _binding.TeamEntityId) _objectives.Add(objective.RuntimeId);
                bool pending = false;
                foreach (var assignment in assignments.Assignments)
                    if (_objectives.Contains(assignment.RuntimeId)
                        && (assignment.StatusType == AgentAssignmentStatusType.Queued
                            || assignment.StatusType == AgentAssignmentStatusType.Preparing
                            || assignment.StatusType == AgentAssignmentStatusType.WaitingDependencies
                            || assignment.StatusType == AgentAssignmentStatusType.Running))
                    { pending = true; break; }
                if (!pending)
                    foreach (var production in ProductionService.GetSnapshots(context.RuntimeDomainId))
                        if (production.Owner?.StableSimulationKey == _binding.Participants[0].ParticipantEconomyOwner.StableSimulationKey
                            && (production.QueueCount != 0 || production.ActivePipelineCount != 0))
                        { pending = true; break; }
                bool settled = complete && !pending && incoming.Value.Integer == 0;
                _snapshot = new ChainRushEnemyFieldSnapshot(context.ActivityId, true, complete,
                    settled, settled && existing.Value.Integer == 0, existing.Value.Integer, incoming.Value.Integer, null);
            }

            ChainRushEnemyFieldSnapshot Pending(bool complete, string diagnostic) =>
                new ChainRushEnemyFieldSnapshot(_binding.ActivityId, false, complete, false, false, 0, 0, diagnostic);

            public void OnEvent(ChainRushEnemyFieldRequestEvent e)
            {
                if (e.ActivityId == _binding.ActivityId) e.Reply(_snapshot);
            }

            public void Close(in ActivityFeatureCloseContext context)
            {
                EventBus.Unregister<ChainRushEnemyFieldRequestEvent>(this);
                _count?.Dispose();
                _count = null;
                _objectives.Clear();
                _snapshot = default;
                _binding = default;
                _progressOwner = null;
                _data = null;
            }
        }
    }
}
