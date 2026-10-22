#if UNITY_EDITOR
using System;
using System.IO;
using Core.GameRuntime;
using Core.EditorTools;
using Unity.Profiling;
using UnityEngine;

namespace ChainRush.Diagnostics
{
    internal sealed partial class PerformanceBenchMeasurements
    {
        readonly ProfilerRecorder[] _chain = new ProfilerRecorder[56];
        void OpenChainRecorders()
        {
            _chain[0] = Open(ProfilerCategory.Scripts, "Runtime.Computation.ComponentReads");
            _chain[1] = Open(ProfilerCategory.Scripts, "Runtime.Computation.ComponentWrites");
            _chain[2] = Open(ProfilerCategory.Scripts, "Runtime.Computation.AccessRefreshes");
            _chain[3] = Open(ProfilerCategory.Scripts, "SpatialService.ValidationQueries");
            _chain[4] = Open(ProfilerCategory.Scripts, "SpatialService.Revalidations");
            _chain[5] = Open(ProfilerCategory.Scripts, "Runtime.Computation.RecordReconstructions");
            _chain[6] = Open(ProfilerCategory.Scripts, "Runtime.Consumers.Knowledge");
            _chain[7] = Open(ProfilerCategory.Scripts, "Runtime.Consumers.Fog");
            _chain[8] = Open(ProfilerCategory.Scripts, "Runtime.Consumers.Visibility");
            _chain[9] = Open(ProfilerCategory.Scripts, "Runtime.Consumers.Projection");
            _chain[10] = Open(ProfilerCategory.Scripts, "Runtime.Consumers.SpaceRegion");
            _chain[11] = Open(ProfilerCategory.Scripts, "Runtime.Consumers.Objectives");
            _chain[12] = Open(ProfilerCategory.Scripts, "Runtime.Consumers.Orchestration");
            _chain[13] = Open(ProfilerCategory.Scripts, "Runtime.Consumers.Selection");
            _chain[14] = Open(ProfilerCategory.Scripts, "Runtime.Consumers.Materialization");
            _chain[15] = Open(ProfilerCategory.Scripts, "Runtime.Consumers.Drops");
            _chain[16] = Open(ProfilerCategory.Scripts, "Runtime.Consumers.Navigation");
            _chain[17] = Open(ProfilerCategory.Scripts, "Runtime.Deferred.QueuedWork");
            _chain[18] = Open(ProfilerCategory.Scripts, "Runtime.Deferred.ExecutedWork");
            _chain[19] = Open(ProfilerCategory.Scripts, "Runtime.Deferred.MaximumPending");
            _chain[20] = Open(ProfilerCategory.Scripts, "Runtime.Deferred.PendingWork");
            _chain[21] = Open(ProfilerCategory.Scripts, "KnowledgeBroker.PendingHints");
            _chain[22] = Open(ProfilerCategory.Scripts, "Runtime.Consumers.Analytics");
            _chain[23] = Open(ProfilerCategory.Scripts, "Runtime.Consumers.Forecast");
            _chain[24] = Open(ProfilerCategory.Scripts, "Runtime.Consumers.SpatialArea");
            _chain[25] = Open(ProfilerCategory.Scripts, "Runtime.Computation.StructuralChecks");
            _chain[26] = Open(ProfilerCategory.Scripts, "Runtime.Computation.AccessOpened");
            _chain[27] = Open(ProfilerCategory.Scripts, "Runtime.Computation.AccessClosed");
            _chain[28] = Open(ProfilerCategory.Scripts, "Runtime.Pipeline.Scheduled");
            _chain[29] = Open(ProfilerCategory.Scripts, "Runtime.Pipeline.CompleteForRead");
            _chain[30] = Open(ProfilerCategory.Scripts, "Runtime.Pipeline.CompleteForExternal");
            _chain[31] = Open(ProfilerCategory.Scripts, "Runtime.Pipeline.CompleteForCapacity");
            _chain[32] = Open(ProfilerCategory.Scripts, "Runtime.Pipeline.CompleteForRetirement");
            _chain[33] = Open(ProfilerCategory.Scripts, "Skills.PreparedRanges");
            _chain[34] = Open(ProfilerCategory.Scripts, "Topology.BatchRequests");
            _chain[35] = Open(ProfilerCategory.Scripts, "Topology.BatchRegistrations");
            _chain[36] = Open(ProfilerCategory.Scripts, "Topology.MaterializedPositions");
            _chain[37] = Open(ProfilerCategory.Scripts, "Skills.NumericResults");
            _chain[38] = Open(ProfilerCategory.Scripts, "Skills.ExternalResults");
            _chain[39] = Open(ProfilerCategory.Scripts, "Topology.GroupingReadBoundaries");
            _availability.Add("Continuation and coordination counters require trace diagnostics. Missing counters are unavailable, not zero. Spatial snapshot caching is absent from this candidate.");
            _availability.Add("Access, dependency, coordinate, component, validation and consumer work counts require trace diagnostics; -1 in Normal means not collected. Consumer counts are owner work units, not CPU durations. Listener timings are in the trace event hierarchy. Completion counters count synchronization calls at the named boundaries; they are not elapsed waiting time. Movement Completed, pose ReachedTarget and successful patrol AI states are distinct event counts.");
        }
        void ReadChainRecorders(ref PerformanceBenchFrame row)
        {
            bool enabled = GameRuntimeDiagnostics.OrchestrationTraceEnabled;
            row.coordinateGroupingBoundaries = enabled ? Count(_chain[39]) : -1;
            row.numericSkillResults = enabled ? Count(_chain[37]) : -1;
            row.externalSkillResults = enabled ? Count(_chain[38]) : -1;
            row.structuralVersionChecks = enabled ? Count(_chain[25]) : -1;
            row.accessScopesOpened = enabled ? Count(_chain[26]) : -1;
            row.accessScopesClosed = enabled ? Count(_chain[27]) : -1;
            row.pipelineScheduled = enabled ? Count(_chain[28]) : -1;
            row.completionsForRead = enabled ? Count(_chain[29]) : -1;
            row.completionsForExternal = enabled ? Count(_chain[30]) : -1;
            row.completionsForCapacity = enabled ? Count(_chain[31]) : -1;
            row.completionsForRetirement = enabled ? Count(_chain[32]) : -1;
            row.preparedEffectRanges = enabled ? Count(_chain[33]) : -1;
            row.coordinateBatchRequests = enabled ? Count(_chain[34]) : -1;
            row.coordinateBatchRegistrations = enabled ? Count(_chain[35]) : -1;
            row.coordinateMaterializations = enabled ? Count(_chain[36]) : -1;
            row.componentReads = enabled ? Count(_chain[0]) : -1;
            row.componentWrites = enabled ? Count(_chain[1]) : -1;
            row.componentAccessRefreshes = enabled ? Count(_chain[2]) : -1;
            row.validationQueries = enabled ? Count(_chain[3]) : -1;
            row.revalidations = enabled ? Count(_chain[4]) : -1;
            row.recordReconstructions = enabled ? Count(_chain[5]) : -1;
            row.knowledgeWork = enabled ? Count(_chain[6]) : -1;
            row.fogWork = enabled ? Count(_chain[7]) : -1;
            row.visibilityWork = enabled ? Count(_chain[8]) : -1;
            row.projectionWork = enabled ? Count(_chain[9]) : -1;
            row.regionWork = enabled ? Count(_chain[10]) : -1;
            row.objectiveWork = enabled ? Count(_chain[11]) : -1;
            row.orchestrationWork = enabled ? Count(_chain[12]) : -1;
            row.selectionWork = enabled ? Count(_chain[13]) : -1;
            row.materializationWork = enabled ? Count(_chain[14]) : -1;
            row.dropsWork = enabled ? Count(_chain[15]) : -1;
            row.navigationConsumerWork = enabled ? Count(_chain[16]) : -1;
            row.queuedDeferredWork = enabled ? Count(_chain[17]) : -1;
            row.executedDeferredWork = enabled ? Count(_chain[18]) : -1;
            row.maximumDeferredPending = enabled ? Count(_chain[19]) : -1;
            row.deferredPending = enabled ? Count(_chain[20]) : -1;
            row.knowledgePendingHints = enabled ? Count(_chain[21]) : -1;
            row.analyticsWork = enabled ? Count(_chain[22]) : -1;
            row.forecastWork = enabled ? Count(_chain[23]) : -1;
            row.spatialAreaWork = enabled ? Count(_chain[24]) : -1;
        }
        [Serializable]
        struct ChainFrame
        {
            public int frame, phase;
            public long coordinateGroupingBoundaries;
            public long numericSkillResults, externalSkillResults;
            public long structuralVersionChecks, accessScopesOpened, accessScopesClosed, pipelineScheduled, completionsForRead, completionsForExternal, completionsForCapacity, completionsForRetirement, preparedEffectRanges, coordinateBatchRequests, coordinateBatchRegistrations, coordinateMaterializations;
            public long componentReads, componentWrites, componentAccessRefreshes, validationQueries, revalidations, recordReconstructions, knowledgeWork, fogWork, visibilityWork, projectionWork, regionWork, objectiveWork, orchestrationWork, selectionWork, materializationWork, dropsWork, navigationConsumerWork, queuedDeferredWork, executedDeferredWork, maximumDeferredPending, deferredPending, knowledgePendingHints, analyticsWork, forecastWork, spatialAreaWork;
        }
        void WriteChainFrames(string directory, ProfilerDumpStorageBudget budget)
        {
            using var writer = budget.OpenWriter(Path.Combine(directory, "chain-frames.jsonl"));
            for (int i = 0; i < FrameCount; i++)
            {
                var frame = _frames[i];
                var row = new ChainFrame { frame = frame.frame, phase = frame.phase,
                    coordinateGroupingBoundaries = frame.coordinateGroupingBoundaries,
                    numericSkillResults = frame.numericSkillResults,
                    externalSkillResults = frame.externalSkillResults,
                    structuralVersionChecks = frame.structuralVersionChecks,
                    accessScopesOpened = frame.accessScopesOpened,
                    accessScopesClosed = frame.accessScopesClosed,
                    pipelineScheduled = frame.pipelineScheduled,
                    completionsForRead = frame.completionsForRead,
                    completionsForExternal = frame.completionsForExternal,
                    completionsForCapacity = frame.completionsForCapacity,
                    completionsForRetirement = frame.completionsForRetirement,
                    preparedEffectRanges = frame.preparedEffectRanges,
                    coordinateBatchRequests = frame.coordinateBatchRequests,
                    coordinateBatchRegistrations = frame.coordinateBatchRegistrations,
                    coordinateMaterializations = frame.coordinateMaterializations,
                    componentReads = frame.componentReads,
                    componentWrites = frame.componentWrites,
                    componentAccessRefreshes = frame.componentAccessRefreshes,
                    validationQueries = frame.validationQueries,
                    revalidations = frame.revalidations,
                    recordReconstructions = frame.recordReconstructions,
                    knowledgeWork = frame.knowledgeWork,
                    fogWork = frame.fogWork,
                    visibilityWork = frame.visibilityWork,
                    projectionWork = frame.projectionWork,
                    regionWork = frame.regionWork,
                    objectiveWork = frame.objectiveWork,
                    orchestrationWork = frame.orchestrationWork,
                    selectionWork = frame.selectionWork,
                    materializationWork = frame.materializationWork,
                    dropsWork = frame.dropsWork,
                    navigationConsumerWork = frame.navigationConsumerWork,
                    queuedDeferredWork = frame.queuedDeferredWork,
                    executedDeferredWork = frame.executedDeferredWork,
                    maximumDeferredPending = frame.maximumDeferredPending,
                    deferredPending = frame.deferredPending,
                    knowledgePendingHints = frame.knowledgePendingHints, analyticsWork = frame.analyticsWork,
                    forecastWork = frame.forecastWork, spatialAreaWork = frame.spatialAreaWork };
                writer.WriteLine(JsonUtility.ToJson(row));
            }
        }
    }
}
#endif
