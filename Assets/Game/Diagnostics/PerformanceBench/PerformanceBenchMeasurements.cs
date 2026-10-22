#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace ChainRush.Diagnostics
{
    [Serializable]
    public struct PerformanceBenchFrame
    {
        public int frame, phase, steps, entities, bound, visible, pending;
        public int moved, waiting, traffic, routesBuilt, routesFailed, created, deleted, rented, expanded;
        public int helpers, occupancyChecks, occupancyViolations, blocked;
        public int movementCompleted, poseReachedTarget, patrolSucceeded;
        public long allocatedBytes, nativeCapacityBytes;
        public int screenWidth, screenHeight, firstStepAfterReady, lastStepAfterReady;
        public int jobPipelines, jobIntents, jobCandidates, jobDistanceChecks, jobPreparedPoints, jobNeighbors, appliedMovementResults;
        public double jobPrepareMs, jobScheduleMs, jobCompleteMs, jobReadResultsMs, jobDistanceCpuMs, jobUnionCpuMs, jobNeighborsCpuMs;
        public int aiBatches, aiPrograms, skillBatches, skillExecutions, navigationAdvances, aStarSearches;
        public long aiTicks, aiActions, aiManagedCommands, aiInvalidations, skillActions, navigationExpanded, navigationGeometryChecks, navigationOccupancyChecks, bypassCandidates, bypassSnapshotClear;
        public double navigationPrepareMs, navigationScheduleMs, navigationCompleteMs, navigationApplyMs, kinematicsMs, kinematicsCompleteMs, movementApplyMs, swarmCpuMs, bypassPrepareMs, candidateScoresCpuMs;
        public double structuralCreateMs, structuralRemoveMs;
        public long computationRecordsCreated, computationRecordsRemoved, spatialNativeCapacityBytes, trafficRegistrationCapacityBytes, skillEffectCapacityBytes;
        public long aiNativeCapacityBytes, skillNativeCapacityBytes, navigationNativeCapacityBytes, kinematicsNativeCapacityBytes;
        public long coordinateGroupingBoundaries;
        public long numericSkillResults, externalSkillResults;
        public long structuralVersionChecks, accessScopesOpened, accessScopesClosed, pipelineScheduled, completionsForRead, completionsForExternal, completionsForCapacity, completionsForRetirement, preparedEffectRanges, coordinateBatchRequests, coordinateBatchRegistrations, coordinateMaterializations;
        public long componentReads, componentWrites, componentAccessRefreshes, validationQueries, revalidations, recordReconstructions;
        public long knowledgeWork, fogWork, visibilityWork, projectionWork, regionWork, objectiveWork, orchestrationWork, selectionWork, materializationWork, dropsWork, navigationConsumerWork;
        public long analyticsWork, forecastWork, spatialAreaWork;
        public long queuedDeferredWork, executedDeferredWork, maximumDeferredPending, deferredPending, knowledgePendingHints;
        public bool phaseBoundary;
        public double wallMs, mainThreadMs, schedulingMs, aiMs, movementMs, skillsMs, benchMs, observationMs;
    }
    [Serializable]
    public struct PerformanceBenchOperation
    {
        public int operation, batch, phase, slot, entity, action, epoch, placementAttempts;
        public long token, handle;
        public double admittedAt, entityAt, readyAt, removedAt, releasedAt, cpuMs, helperCpuMs;
        public long allocatedBytes, helperAllocatedBytes;
        public bool success;
    }
    [Serializable]
    internal struct PerformanceBenchBatch
    {
        public int batch, phase, action, requested;
        public double admittedAt;
    }
    [Serializable]
    public sealed class PerformanceBenchResult
    {
        public int run, frameCount, measuredFrames, simulationSteps, created, deleted;
        public bool interrupted, incomplete, lifecycleSucceeded, statisticsSufficient;
        public PerformanceBenchCaptureType captureType;
        public bool profilerDetailsTruncated;
        public string profilerExportMessage;
        public PerformanceBenchStopReasonType stopReason;
        public string stopMessage;
        public int measuredStepCount, simulationFrames, noSimulationFrames;
        public double meanCompleteStepMs = -1;
        public bool stepIntervalComplete, stepIntervalRequired;
        public int requiredMeasuredFrames = 1000;
        public string failure, outputDirectory;
        public PerformanceBenchSettings settings;
        public double meanMs, medianMs, maxMs, meanStepFrameMs, meanNoStepFrameMs;
        public double p95Ms = -1, p99Ms = -1;
        public double above8Percent, above16Percent, above33Percent, stepsPerSecond;
        public long allocatedBytes;
        public int requestedCreates, requestedDeletes, completedCreateRequests, completedDeleteRequests, failedOperations;
        public int routesBuilt, routesFailed, poolExpansions, occupancyChecks, occupancyViolations, maximumPending;
        public double measuredSeconds, createdPerSecond, deletedPerSecond, requestedCreatesPerSecond, requestedDeletesPerSecond;
        public double meanMovementMsPerStep = -1, meanSkillsMsPerStep = -1;
        public double meanSchedulingMsPerStep = -1, meanAIFrameMs = -1, meanBenchFrameMs = -1, meanObservationFrameMs = -1;
        public double meanObservedCreateCpuMs = -1, meanObservedDeleteCpuMs = -1, meanReadyLatencyMs = -1, meanRemovalLatencyMs = -1;
        public double meanMovingPerStepFrame, meanWaitingPerStepFrame;
        public long createAllocationBytes, deleteAllocationBytes;
    }

    // minimum seam: bounded diagnostic storage. No writes, sorting or growing lists during measurement.
    internal sealed partial class PerformanceBenchMeasurements : IDisposable
    {
        const int FrameCapacity = 240000;
        const int OperationCapacity = 500000;
        const int BatchCapacity = 32768;
        readonly PerformanceBenchFrame[] _frames = new PerformanceBenchFrame[FrameCapacity];
        readonly PerformanceBenchOperation[] _operations = new PerformanceBenchOperation[OperationCapacity];
        readonly PerformanceBenchBatch[] _batches = new PerformanceBenchBatch[BatchCapacity];
        int _batchCount;
        readonly List<ProfilerRecorder> _recorders = new List<ProfilerRecorder>();
        readonly List<string> _availability = new List<string>();
        ProfilerRecorder _computationCreated, _computationRemoved, _spatialCapacity, _trafficCapacity, _effectCapacity, _structuralCreate, _structuralRemove;
        ProfilerRecorder _main, _allocation, _scheduling, _ai, _movement, _skills, _bench, _observation;
        ProfilerRecorder _jobPrepare, _jobSchedule, _jobComplete, _jobRead, _jobDistance, _jobUnion, _jobNeighbors;
        ProfilerRecorder _navigationPrepare, _navigationSchedule, _navigationComplete, _navigationApply, _kinematics, _kinematicsComplete, _movementApply, _swarm, _bypassPrepare, _candidateScores;
        public int FrameCount { get; private set; }
        public int OperationCount { get; private set; }
        public bool Overflow { get; private set; }
        public bool ThreadAllocationAvailable { get; }
        public IReadOnlyList<string> Availability => _availability;

        public PerformanceBenchMeasurements()
        {
            // These buffers have already allocated on this thread. A zero lifetime counter
            // means this backend does not supply thread allocation data, not zero work.
            ThreadAllocationAvailable = GC.GetAllocatedBytesForCurrentThread() > 0;
            _computationCreated = Open(ProfilerCategory.Scripts, "Runtime.Computation.RecordsCreated");
            _computationRemoved = Open(ProfilerCategory.Scripts, "Runtime.Computation.RecordsRemoved");
            _spatialCapacity = Open(ProfilerCategory.Scripts, "SpatialService.NumericCapacity");
            _trafficCapacity = Open(ProfilerCategory.Scripts, "MovementTraffic.RegistrationCapacity");
            _effectCapacity = Open(ProfilerCategory.Scripts, "Skills.EffectCapacity");
            _structuralCreate = Open(ProfilerCategory.Scripts, "Runtime.Computation.CreateRecord");
            _structuralRemove = Open(ProfilerCategory.Scripts, "Runtime.Computation.DestroyRecord");
            OpenChainRecorders();
            _main = Open(ProfilerCategory.Internal, "Main Thread");
            _allocation = Open(ProfilerCategory.Memory, "GC Allocated In Frame");
            _scheduling = Open(ProfilerCategory.Scripts, "GameRuntimeHost.PumpRuntime.SchedulingUpdate");
            _ai = Open(ProfilerCategory.Scripts, "AIBrainService.HandleTick");
            _movement = Open(ProfilerCategory.Scripts, "MovementService.PumpRuntime");
            _skills = Open(ProfilerCategory.Scripts, "InMemorySkillRuntime.AdvanceEntitySteps");
            _bench = Open(ProfilerCategory.Scripts, "PerformanceBench.Work");
            _observation = Open(ProfilerCategory.Scripts, "PerformanceBench.Observe");
            _jobPrepare = Open(ProfilerCategory.Scripts, "MovementTraffic.Jobs.PrepareInputs");
            _jobSchedule = Open(ProfilerCategory.Scripts, "MovementTraffic.Jobs.Schedule");
            _jobComplete = Open(ProfilerCategory.Scripts, "MovementTraffic.Jobs.Complete");
            _jobRead = Open(ProfilerCategory.Scripts, "MovementTraffic.Jobs.ReadResults");
            _jobDistance = Open(ProfilerCategory.Scripts, "SpatialService.Jobs.RadiusQuery");
            _jobUnion = Open(ProfilerCategory.Scripts, "MovementTraffic.Jobs.Union");
            _jobNeighbors = Open(ProfilerCategory.Scripts, "MovementTraffic.Jobs.Neighbors");
            _navigationPrepare = Open(ProfilerCategory.Scripts, "NavigationService.Programs.Prepare");
            _navigationSchedule = Open(ProfilerCategory.Scripts, "NavigationService.Programs.Schedule");
            _navigationComplete = Open(ProfilerCategory.Scripts, "NavigationService.Programs.Complete");
            _navigationApply = Open(ProfilerCategory.Scripts, "NavigationService.Programs.Apply");
            _kinematics = Open(ProfilerCategory.Scripts, "MovementService.Programs.Kinematics");
            _kinematicsComplete = Open(ProfilerCategory.Scripts, "MovementService.Programs.Complete");
            _movementApply = Open(ProfilerCategory.Scripts, "MovementService.Programs.Apply");
            _swarm = Open(ProfilerCategory.Scripts, "MovementTraffic.Jobs.Swarm");
            _bypassPrepare = Open(ProfilerCategory.Scripts, "NavigationService.Programs.PrepareTraffic");
            _candidateScores = Open(ProfilerCategory.Scripts, "MovementTraffic.Jobs.CandidateScores");
            _availability.Add("Phase times are inclusive; summed worker CPU overlaps the main thread and other jobs. Do not add these columns as frame shares.");
            _availability.Add("Computation records count internal execution/search records, not gameplay Entity. New counters may be unavailable in baseline. Spatial/traffic/effect capacities are last observed owner capacities; traffic registration is included in total traffic capacity, and must not be added twice.");
            _availability.Add("Native capacities are sampled execution, route, kinematic and traffic storage capacities; allocator headers, other idle preparation buffers and Unity Profiler native capture memory are excluded. -1 means no capacity sample for that phase/frame.");
        }
        ProfilerRecorder Open(ProfilerCategory category, string name)
        {
            var recorder = ProfilerRecorder.StartNew(category, name, 1, ProfilerRecorderOptions.Default
                | ProfilerRecorderOptions.SumAllSamplesInFrame);
            _recorders.Add(recorder);
            _availability.Add(name + (recorder.Valid ? ": recorded" : ": unavailable"));
            return recorder;
        }
        static long Count(ProfilerRecorder recorder) => recorder.Valid && recorder.Count > 0 ? recorder.LastValue : -1;
        static double Ms(ProfilerRecorder recorder) => recorder.Valid && recorder.Count > 0 ? recorder.LastValue / 1000000d : -1;
        public void Frame(PerformanceBenchFrame row)
        {
            if (FrameCount == FrameCapacity) { Overflow = true; return; }
            ReadChainRecorders(ref row);
            row.computationRecordsCreated = Count(_computationCreated); row.computationRecordsRemoved = Count(_computationRemoved);
            row.spatialNativeCapacityBytes = Count(_spatialCapacity); row.trafficRegistrationCapacityBytes = Count(_trafficCapacity);
            row.skillEffectCapacityBytes = Count(_effectCapacity); row.structuralCreateMs = Ms(_structuralCreate); row.structuralRemoveMs = Ms(_structuralRemove);
            row.mainThreadMs = Ms(_main); row.schedulingMs = Ms(_scheduling); row.aiMs = Ms(_ai); row.benchMs = Ms(_bench);
            row.observationMs = Ms(_observation);
            row.movementMs = Ms(_movement); row.skillsMs = Ms(_skills);
            row.jobPrepareMs = Ms(_jobPrepare); row.jobScheduleMs = Ms(_jobSchedule); row.jobCompleteMs = Ms(_jobComplete); row.jobReadResultsMs = Ms(_jobRead);
            row.jobDistanceCpuMs = Ms(_jobDistance); row.jobUnionCpuMs = Ms(_jobUnion); row.jobNeighborsCpuMs = Ms(_jobNeighbors);
            row.navigationPrepareMs = Ms(_navigationPrepare);
            row.navigationScheduleMs = Ms(_navigationSchedule);
            row.navigationCompleteMs = Ms(_navigationComplete);
            row.navigationApplyMs = Ms(_navigationApply);
            row.kinematicsMs = Ms(_kinematics);
            row.kinematicsCompleteMs = Ms(_kinematicsComplete);
            row.movementApplyMs = Ms(_movementApply);
            row.swarmCpuMs = Ms(_swarm);
            row.bypassPrepareMs = Ms(_bypassPrepare);
            row.candidateScoresCpuMs = Ms(_candidateScores);
            if (row.mainThreadMs <= 0) row.mainThreadMs = -1;
            row.allocatedBytes = _allocation.Valid && _allocation.Count > 0 ? _allocation.LastValue : -1;
            _frames[FrameCount++] = row;
        }
        public void Operation(PerformanceBenchOperation row)
        {
            if (OperationCount == OperationCapacity) { Overflow = true; return; }
            _operations[OperationCount++] = row;
        }
        public void Batch(PerformanceBenchBatch row)
        {
            if (_batchCount == BatchCapacity) { Overflow = true; return; }
            _batches[_batchCount++] = row;
        }
        public PerformanceBenchResult Summarize(int run, PerformanceBenchSettings settings, bool interrupted, string failure)
        {
            var result = new PerformanceBenchResult { run = run, settings = settings.Copy(), frameCount = FrameCount,
                interrupted = interrupted, incomplete = Overflow, failure = failure };
            SummarizeSteps(result);
            var timings = new List<double>(FrameCount);
            double stepFrames = 0, noStepFrames = 0, elapsedMs = 0;
            int stepCount = 0, noStepCount = 0, above8 = 0, above16 = 0, above33 = 0;
            double scheduling = 0, ai = 0, bench = 0, observation = 0, moved = 0, waiting = 0;
            double movement = 0, skills = 0; int movementSteps = 0, skillSteps = 0;
            int schedulingCount = 0, aiCount = 0, benchCount = 0, observationCount = 0;
            int allocationCount = 0;
            for (int i = 0; i < FrameCount; i++)
            {
                var row = _frames[i];
                if (row.phase != (int)PerformanceBenchPhaseType.Measuring || row.phaseBoundary) continue;
                timings.Add(row.wallMs); elapsedMs += row.wallMs;
                result.simulationSteps += row.steps; result.created += row.created; result.deleted += row.deleted;
                result.routesBuilt += row.routesBuilt; result.routesFailed += row.routesFailed; result.poolExpansions += row.expanded;
                result.occupancyChecks += row.occupancyChecks; result.occupancyViolations += row.occupancyViolations;
                result.maximumPending = Math.Max(result.maximumPending, row.pending);
                if (row.steps > 0 && row.schedulingMs >= 0) { scheduling += row.schedulingMs; schedulingCount += row.steps; }
                if (row.steps > 0 && row.movementMs >= 0) { movement += row.movementMs; movementSteps += row.steps; }
                if (row.steps > 0 && row.skillsMs >= 0) { skills += row.skillsMs; skillSteps += row.steps; }
                if (row.aiMs >= 0) { ai += row.aiMs; aiCount++; }
                if (row.benchMs >= 0) { bench += row.benchMs; benchCount++; }
                if (row.observationMs >= 0) { observation += row.observationMs; observationCount++; }
                if (row.steps > 0) { moved += row.moved; waiting += row.waiting; }
                if (row.allocatedBytes >= 0) { result.allocatedBytes += row.allocatedBytes; allocationCount++; }
                if (row.steps > 0) { stepFrames += row.wallMs; stepCount++; }
                else { noStepFrames += row.wallMs; noStepCount++; }
                if (row.wallMs > 8.33) above8++;
                if (row.wallMs > 16.67) above16++;
                if (row.wallMs > 33.33) above33++;
            }
            result.meanMovementMsPerStep = movementSteps > 0 ? movement / movementSteps : -1;
            result.meanSkillsMsPerStep = skillSteps > 0 ? skills / skillSteps : -1;
            result.measuredFrames = timings.Count;
            result.statisticsSufficient = timings.Count >= result.requiredMeasuredFrames;
            if (allocationCount == 0) result.allocatedBytes = -1;
            for (int i = 0; i < _batchCount; i++)
                if (_batches[i].phase == (int)PerformanceBenchPhaseType.Measuring)
                { if (_batches[i].action == 0) result.requestedCreates += _batches[i].requested; else result.requestedDeletes += _batches[i].requested; }
            double createCpu = 0, deleteCpu = 0, readyLatency = 0, removalLatency = 0;
            bool createBytesAvailable = true, deleteBytesAvailable = true;
            for (int i = 0; i < OperationCount; i++)
            {
                var op = _operations[i];
                if (op.phase != (int)PerformanceBenchPhaseType.Measuring) continue;
                if (!op.success) { result.failedOperations++; continue; }
                if (op.action == 0)
                { result.completedCreateRequests++; createCpu += op.cpuMs; if (op.allocatedBytes >= 0) result.createAllocationBytes += op.allocatedBytes; else createBytesAvailable = false; readyLatency += (op.readyAt - op.admittedAt) * 1000; }
                else
                { result.completedDeleteRequests++; deleteCpu += op.cpuMs; if (op.allocatedBytes >= 0) result.deleteAllocationBytes += op.allocatedBytes; else deleteBytesAvailable = false; removalLatency += (Math.Max(op.removedAt, op.releasedAt) - op.admittedAt) * 1000; }
            }
            if (!createBytesAvailable) result.createAllocationBytes = -1;
            if (!deleteBytesAvailable) result.deleteAllocationBytes = -1;
            if (result.completedCreateRequests > 0) { result.meanObservedCreateCpuMs = createCpu / result.completedCreateRequests; result.meanReadyLatencyMs = readyLatency / result.completedCreateRequests; }
            if (result.completedDeleteRequests > 0) { result.meanObservedDeleteCpuMs = deleteCpu / result.completedDeleteRequests; result.meanRemovalLatencyMs = removalLatency / result.completedDeleteRequests; }
            if (timings.Count == 0) return result;
            timings.Sort();
            result.meanMs = elapsedMs / timings.Count;
            result.medianMs = Quantile(timings, .5);
            if (result.statisticsSufficient) { result.p95Ms = Quantile(timings, .95); result.p99Ms = Quantile(timings, .99); }
            result.maxMs = timings[timings.Count - 1];
            result.simulationFrames = stepCount; result.noSimulationFrames = noStepCount;
            result.meanStepFrameMs = stepCount == 0 ? -1 : stepFrames / stepCount;
            result.meanNoStepFrameMs = noStepCount == 0 ? -1 : noStepFrames / noStepCount;
            result.above8Percent = above8 * 100d / timings.Count; result.above16Percent = above16 * 100d / timings.Count;
            result.above33Percent = above33 * 100d / timings.Count;
            result.stepsPerSecond = elapsedMs <= 0 ? 0 : result.simulationSteps * 1000 / elapsedMs;
            result.measuredSeconds = elapsedMs / 1000;
            result.createdPerSecond = result.created / result.measuredSeconds; result.deletedPerSecond = result.deleted / result.measuredSeconds;
            result.requestedCreatesPerSecond = result.requestedCreates / result.measuredSeconds; result.requestedDeletesPerSecond = result.requestedDeletes / result.measuredSeconds;
            result.meanSchedulingMsPerStep = schedulingCount == 0 ? -1 : scheduling / schedulingCount;
            result.meanAIFrameMs = aiCount == 0 ? -1 : ai / aiCount;
            result.meanBenchFrameMs = benchCount == 0 ? -1 : bench / benchCount;
            result.meanObservationFrameMs = observationCount == 0 ? -1 : observation / observationCount;
            result.meanMovingPerStepFrame = stepCount == 0 ? 0 : moved / stepCount;
            result.meanWaitingPerStepFrame = stepCount == 0 ? 0 : waiting / stepCount;
            return result;
        }
        static double Quantile(List<double> values, double quantile)
        {
            double position = (values.Count - 1) * quantile;
            int lower = (int)Math.Floor(position), upper = (int)Math.Ceiling(position);
            return values[lower] + (values[upper] - values[lower]) * (position - lower);
        }
        static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        public void Write(string directory, Core.EditorTools.ProfilerDumpStorageBudget budget)
        {
            Directory.CreateDirectory(directory);
            using (var writer = budget.OpenWriter(Path.Combine(directory, "frames.csv")))
            {
                writer.WriteLine("frame,phase,steps,entities,bound,visible,pending,wallMs,mainThreadInclusiveMs,schedulingInclusiveMs,aiInclusiveMs,benchInclusiveMs,observationInclusiveMs,allocatedBytes,moved,waiting,traffic,routesBuilt,routesFailed,created,deleted,rented,expanded,helpers,occupancyChecks,occupancyViolations,blocked,phaseBoundary,movementMs,skillsMs,screenWidth,screenHeight,firstStepAfterReady,lastStepAfterReady,jobPipelines,jobIntents,jobCandidates,jobDistanceChecks,jobPreparedPoints,jobNeighbors,appliedMovementResults,nativeCapacityBytes,jobPrepareMs,jobScheduleMs,jobCompleteMs,jobReadResultsMs,jobDistanceCpuMs,jobUnionCpuMs,jobNeighborsCpuMs,aiBatches,aiPrograms,skillBatches,skillExecutions,navigationAdvances,aStarSearches,aiTicks,aiActions,aiManagedCommands,aiInvalidations,skillActions,navigationExpanded,navigationGeometryChecks,navigationOccupancyChecks,bypassCandidates,bypassSnapshotClear,navigationPrepareMs,navigationScheduleMs,navigationCompleteMs,navigationApplyMs,kinematicsMs,kinematicsCompleteMs,movementApplyMs,swarmCpuMs,bypassPrepareMs,candidateScoresCpuMs,aiNativeCapacityBytes,skillNativeCapacityBytes,navigationNativeCapacityBytes,kinematicsNativeCapacityBytes,computationRecordsCreated,computationRecordsRemoved,spatialNativeCapacityBytes,trafficRegistrationCapacityBytes,skillEffectCapacityBytes,structuralCreateMs,structuralRemoveMs,movementCompleted,poseReachedTarget,patrolSucceeded");
                for (int i = 0; i < FrameCount; i++)
                {
                    var f = _frames[i];
                    writer.WriteLine(string.Join(",", f.frame, f.phase, f.steps, f.entities, f.bound, f.visible, f.pending,
                        f.wallMs.ToString("R", Culture), f.mainThreadMs.ToString("R", Culture), f.schedulingMs.ToString("R", Culture),
                        f.aiMs.ToString("R", Culture), f.benchMs.ToString("R", Culture), f.observationMs.ToString("R", Culture), f.allocatedBytes,
                        f.moved, f.waiting, f.traffic, f.routesBuilt, f.routesFailed, f.created, f.deleted, f.rented, f.expanded,
                        f.helpers, f.occupancyChecks, f.occupancyViolations, f.blocked, f.phaseBoundary, f.movementMs.ToString("R", Culture), f.skillsMs.ToString("R", Culture),
                        f.screenWidth, f.screenHeight, f.firstStepAfterReady, f.lastStepAfterReady, f.jobPipelines, f.jobIntents, f.jobCandidates,
                        f.jobDistanceChecks, f.jobPreparedPoints, f.jobNeighbors, f.appliedMovementResults, f.nativeCapacityBytes,
                        f.jobPrepareMs.ToString("R", Culture), f.jobScheduleMs.ToString("R", Culture), f.jobCompleteMs.ToString("R", Culture),
                        f.jobReadResultsMs.ToString("R", Culture), f.jobDistanceCpuMs.ToString("R", Culture), f.jobUnionCpuMs.ToString("R", Culture), f.jobNeighborsCpuMs.ToString("R", Culture),
                        f.aiBatches, f.aiPrograms, f.skillBatches, f.skillExecutions, f.navigationAdvances, f.aStarSearches, f.aiTicks, f.aiActions, f.aiManagedCommands, f.aiInvalidations, f.skillActions, f.navigationExpanded, f.navigationGeometryChecks, f.navigationOccupancyChecks, f.bypassCandidates, f.bypassSnapshotClear,
                        f.navigationPrepareMs.ToString("R", Culture), f.navigationScheduleMs.ToString("R", Culture), f.navigationCompleteMs.ToString("R", Culture), f.navigationApplyMs.ToString("R", Culture), f.kinematicsMs.ToString("R", Culture), f.kinematicsCompleteMs.ToString("R", Culture), f.movementApplyMs.ToString("R", Culture), f.swarmCpuMs.ToString("R", Culture), f.bypassPrepareMs.ToString("R", Culture), f.candidateScoresCpuMs.ToString("R", Culture), f.aiNativeCapacityBytes, f.skillNativeCapacityBytes, f.navigationNativeCapacityBytes, f.kinematicsNativeCapacityBytes, f.computationRecordsCreated, f.computationRecordsRemoved, f.spatialNativeCapacityBytes, f.trafficRegistrationCapacityBytes, f.skillEffectCapacityBytes, f.structuralCreateMs.ToString("R", Culture), f.structuralRemoveMs.ToString("R", Culture), f.movementCompleted, f.poseReachedTarget, f.patrolSucceeded));
                }
            }
            using (var writer = budget.OpenWriter(Path.Combine(directory, "operations.csv")))
            {
                writer.WriteLine("operation,batch,phase,slot,entity,token,handle,epoch,action,admittedAt,entityAt,readyAt,removedAt,releasedAt,observedCpuMs,observedAllocatedBytes,success,helperCpuMs,helperAllocatedBytes,placementAttempts");
                for (int i = 0; i < OperationCount; i++)
                {
                    var o = _operations[i];
                    writer.WriteLine(string.Join(",", o.operation, o.batch, o.phase, o.slot, o.entity, o.token, o.handle, o.epoch, o.action,
                        o.admittedAt.ToString("R", Culture), o.entityAt.ToString("R", Culture), o.readyAt.ToString("R", Culture),
                        o.removedAt.ToString("R", Culture), o.releasedAt.ToString("R", Culture), o.cpuMs.ToString("R", Culture), o.allocatedBytes, o.success,
                        o.helperCpuMs.ToString("R", Culture), o.helperAllocatedBytes, o.placementAttempts));
                }
            }
            using (var writer = budget.OpenWriter(Path.Combine(directory, "preparation-frames.jsonl")))
                for (int i = 0; i < FrameCount; i++)
                    if (_frames[i].phase == (int)PerformanceBenchPhaseType.Preparing) writer.WriteLine(JsonUtility.ToJson(_frames[i]));
            using (var writer = budget.OpenWriter(Path.Combine(directory, "deletions.jsonl")))
                for (int i = 0; i < OperationCount; i++)
                    if (_operations[i].action == 1) writer.WriteLine(JsonUtility.ToJson(_operations[i]));
            WriteBatches(directory, budget);
            WriteSteps(directory, budget);
            WriteChainFrames(directory, budget);
            var availability = new List<string>(_availability);
            for (int i = 0; i < _recorders.Count; i++)
                if (_recorders[i].Valid && _recorders[i].Count == 0)
                    availability[i] = _availability[i].Replace(": recorded", ": unavailable (no samples collected)");
            availability.Add("Per-operation thread allocation bytes: " + (ThreadAllocationAvailable ? "recorded"
                : "unavailable (backend lifetime counter returned zero after measurement buffers were allocated); use frame GC counter and profiler allocation samples"));
            budget.WriteText(Path.Combine(directory, "counter-availability.txt"), string.Join("\n", availability));
        }
        void WriteBatches(string directory, Core.EditorTools.ProfilerDumpStorageBudget budget)
        {
            var operations = new Dictionary<int, List<PerformanceBenchOperation>>();
            for (int i = 0; i < OperationCount; i++)
            {
                var op = _operations[i];
                if (!operations.TryGetValue(op.batch, out var list)) operations.Add(op.batch, list = new());
                list.Add(op);
            }
            using var writer = budget.OpenWriter(Path.Combine(directory, "batches.csv"));
            writer.WriteLine("batch,phase,action,requested,recordedOperations,successful,observedCpuMs,observedAllocatedBytes,completionLatencyMs");
            for (int i = 0; i < _batchCount; i++)
            {
                var batch = _batches[i]; int successful = 0, recorded = 0; long allocated = 0; bool bytesAvailable = true; double cpu = 0, last = batch.admittedAt;
                if (operations.TryGetValue(batch.batch, out var list)) foreach (var op in list)
                { recorded++; if (op.success) successful++; if (op.allocatedBytes >= 0) allocated += op.allocatedBytes; else bytesAvailable = false; cpu += op.cpuMs; last = Math.Max(last, Math.Max(op.readyAt, Math.Max(op.removedAt, op.releasedAt))); }
                if (!bytesAvailable) allocated = -1;
                writer.WriteLine(string.Join(",", batch.batch, batch.phase, batch.action, batch.requested, recorded, successful,
                    cpu.ToString("R", Culture), allocated, ((last - batch.admittedAt) * 1000).ToString("R", Culture)));
            }
        }
        public void Dispose() { foreach (var recorder in _recorders) recorder.Dispose(); _recorders.Clear(); }
    }
}
#endif
