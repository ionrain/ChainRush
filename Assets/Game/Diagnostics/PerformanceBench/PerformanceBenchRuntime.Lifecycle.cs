#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Core.Activities;
using Core.AI;
using Core.CapabilityHosts.Runtime;
using Core.Economy;
using Core.Entities;
using Core.Events;
using Core.Projection;
using Core.World;
using Unity.Profiling;
using UnityEngine;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Diagnostics
{
    internal sealed partial class PerformanceBenchRuntime
    {
        static readonly ProfilerMarker<long, long, long> CreateMarker = new("PerformanceBench.Create", "Run", "Operation", "Slot");
        static readonly ProfilerMarker<long, long, long> DeleteMarker = new("PerformanceBench.Delete", "Run", "Operation", "Entity");
        static readonly ProfilerMarker<long, long, long> MaterializeMarker = new("PerformanceBench.Materialize", "Run", "Operation", "Token");
        static readonly ProfilerMarker<long, long, long> BatchMarker = new("PerformanceBench.Batch", "Run", "Batch", "Requested");
        static readonly ProfilerMarker<long, long, long> PlacementMarker = new("PerformanceBench.PlacementHelpers", "Run", "Operation", "Stage");
        readonly List<SpatialTransientMarkerHandle> _placementMarkers = new();
        static double ClockNow => Time.realtimeSinceStartupAsDouble;

        void AdmitCreate(int requested)
        {
            if (requested <= 0 || _admitting || _stop) return;
            _admitting = true;
            int batch = ++_nextBatch;
            _measurements.Batch(new PerformanceBenchBatch { batch = batch, phase = (int)_phase, action = 0, requested = requested, admittedAt = ClockNow });
            try
            {
                using (BatchMarker.Auto(_run, batch, requested))
                for (int n = 0, visited = 0; n < requested && visited < _actors.Length; visited++)
                {
                    var actor = _actors[_cursor++ % _actors.Length];
                    if (actor.Entity.IsValid || actor.Token.IsValid || actor.Creating || actor.Deleting || actor.Pending.IsValid) continue;
                    actor.Life++; actor.Batch = batch; SetCreating(actor, true); actor.Ready = false;
                    actor.EntityRemoved = false; actor.ViewReleased = false; actor.OrderQueued = false;
                    actor.LastMovedFrame = actor.LastWaitingFrame = -1;
                    actor.Operation = NewOperation(actor, 0);
                    long allocated = GC.GetAllocatedBytesForCurrentThread(); long started = Stopwatch.GetTimestamp();
                    using (CreateMarker.Auto(_run, actor.Operation.operation, actor.Slot))
                    {
                        var request = new EconomyOutputRequest(_worldOwner, _assets.worldWallet.Tags,
                            new List<EconomyAssetAmountEntry> { new(_definition, 1, EconomyFormType.Token) });
                        if (!EconomyService.TryRegisterOperation(request, out var handle, out var failure))
                        {
                            SetCreating(actor, false); actor.Operation.success = false;
                            AccumulateOperation(actor, started, allocated); _measurements.Operation(actor.Operation);
                            Fail(failure.Message); break;
                        }
                        actor.Pending = handle; _pending.Add(actor);
                        actor.Operation.handle = handle.Value;
                        actor.Operation.epoch = handle.Epoch;
                        AccumulateOperation(actor, started, allocated);
                        EconomyService.TryExecuteOperation(handle, out _, out _);
                    }
                    n++;
                }
            }
            finally { _admitting = false; }
        }
        void AdmitDelete(int requested)
        {
            if (requested <= 0 || _admitting) return;
            _admitting = true; int batch = ++_nextBatch;
            _measurements.Batch(new PerformanceBenchBatch { batch = batch, phase = (int)_phase, action = 1, requested = requested, admittedAt = ClockNow });
            try
            {
                using (BatchMarker.Auto(_run, batch, requested))
                for (int n = 0, visited = 0; n < requested && visited < _actors.Length; visited++)
                {
                    var actor = _actors[_cursor++ % _actors.Length];
                    if (!actor.Token.IsValid || actor.Creating || actor.Deleting || actor.Pending.IsValid) continue;
                    actor.Batch = batch; SetDeleting(actor, true); actor.OrderQueued = false;
                    actor.Operation = NewOperation(actor, 1);
                    long allocated = GC.GetAllocatedBytesForCurrentThread(); long started = Stopwatch.GetTimestamp();
                    using (DeleteMarker.Auto(_run, actor.Operation.operation, actor.Entity.Value))
                    {
                        var request = new EconomyEntryOperationRequest(new List<EconomyEntryHandle> { actor.Token },
                            EconomyOperation.Consume, 1, EconomyTransactionTraceContext.None);
                        if (!EconomyService.TryRegisterOperation(_worldOwner, request, out var handle, out var failure))
                        {
                            SetDeleting(actor, false); actor.Operation.success = false;
                            AccumulateOperation(actor, started, allocated); _measurements.Operation(actor.Operation);
                            Fail(failure.Message); break;
                        }
                        actor.Pending = handle; _pending.Add(actor);
                        actor.Operation.handle = handle.Value;
                        actor.Operation.epoch = handle.Epoch;
                        AccumulateOperation(actor, started, allocated);
                        EconomyService.TryExecuteOperation(handle, out _, out _);
                    }
                    n++;
                }
            }
            finally { _admitting = false; }
        }
        PerformanceBenchOperation NewOperation(Actor actor, int action) => new()
        {
            operation = ++_nextOperation, batch = actor.Batch, phase = (int)_phase, slot = actor.Slot,
            entity = actor.Entity.Value, token = actor.Token.IsValid ? actor.Token.TokenId.Value : 0,
            action = action, admittedAt = ClockNow, entityAt = -1, readyAt = -1, removedAt = -1, releasedAt = -1,
            allocatedBytes = _measurements.ThreadAllocationAvailable ? 0 : -1,
            helperAllocatedBytes = _measurements.ThreadAllocationAvailable ? 0 : -1,
        };
        static void AccumulateOperation(Actor actor, long started, long allocated)
        {
            actor.Operation.cpuMs += (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
            if (actor.Operation.allocatedBytes >= 0) actor.Operation.allocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
        }
        void CompletePending()
        {
            // Reverse traversal remains valid when materialization synchronously publishes callbacks.
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var actor = _pending[i];
                if (!EconomyService.TryGetOperation(actor.Pending, out var snapshot))
                    throw new InvalidOperationException("Принятая операция исчезла до завершения.");
                if (!snapshot.IsTerminal) continue;
                _pending.RemoveAt(i);
                FinalizeOperation(actor, snapshot, materialize: _retirement == null && !_hasFailed);
            }
        }
        void FinalizeOperation(Actor actor, EconomyOperationSnapshot snapshot, bool materialize)
        {
            var handle = actor.Pending; actor.Pending = default;
            if (EconomyService.TryGetOperationWorkMeasurement(handle, out double cpuMs, out long bytes))
            {
                actor.Operation.cpuMs += cpuMs;
                if (actor.Operation.allocatedBytes >= 0) actor.Operation.allocatedBytes += bytes;
            }
            try
            {
                if (!snapshot.IsCommitted)
                {
                    SetCreating(actor, false); SetDeleting(actor, false); actor.Operation.success = false;
                    _measurements?.Operation(actor.Operation); Fail(snapshot.Failure.Message); return;
                }
                if (actor.Deleting) { FinishDeletion(actor); return; }
                if (snapshot.IssuedEntries == null || snapshot.IssuedEntries.Count != 1)
                    throw new InvalidOperationException("Выпуск одного объекта должен вернуть ровно один Token.");
                var entry = snapshot.IssuedEntries[0];
                actor.Token = entry.Handle; actor.Operation.token = actor.Token.TokenId.Value;
                if (!materialize)
                {
                    // The Activity owns the issued token even when stop prevents materialization.
                    SetCreating(actor, false); actor.Operation.success = false;
                    _measurements?.Operation(actor.Operation); return;
                }
                Materialize(actor, entry);
            }
            finally { EconomyService.TryCloseOperation(handle); }
        }
        void Materialize(Actor actor, EconomyIssuedEntryData entry)
        {
            try
            {
                var data = PrepareMaterialization(actor, entry);
                long allocated = GC.GetAllocatedBytesForCurrentThread(); long started = Stopwatch.GetTimestamp();
                _materializing = actor;
                try
                {
                    using (MaterializeMarker.Auto(_run, actor.Operation.operation, actor.Token.TokenId.Value))
                    {
                        if (!ActivityService.ApplyMaterialization(data, out EntityId entity, out _, out _, out string failure))
                            throw new InvalidOperationException(failure);
                        if (!actor.Entity.IsValid) RegisterActorEntity(actor, entity);
                    }
                }
                finally { AccumulateOperation(actor, started, allocated); }
                FinishCreation(actor);
            }
            catch
            {
                SetCreating(actor, false); actor.Operation.success = false;
                ReleaseActorPlacement(actor);
                _measurements?.Operation(actor.Operation); throw;
            }
            finally { _materializing = null; }
        }
        ActivityMaterializationData PrepareMaterialization(Actor actor, EconomyIssuedEntryData entry)
        {
            using var sample = PlacementMarker.Auto(_run, actor.Operation.operation, 0);
            long started = Stopwatch.GetTimestamp(), allocated = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                if (!CapabilityHostService.TryResolveSpatialPlacementRequirement(_definition, _activity, out var requirement, out string failure))
                    throw new InvalidOperationException(failure);
                if (!TopologyService.TryResolvePosition(_activity, PerformanceBenchLayout.Position(actor.Slot, _settings), out var position, out var coordinates))
                    throw new InvalidOperationException("Не удалось разрешить позицию создания.");
                int attempts = _settings.scenario == PerformanceBenchScenarioType.Churn && actor.Life > 1 ? 64 : 1;
                string reservationOwner = "performance-bench:" + _run + ":" + actor.Operation.operation;
                for (int attempt = 0; attempt < attempts; attempt++)
                {
                    actor.Operation.placementAttempts++;
                    if (attempt > 0 && !TopologyService.TryResolvePosition(_activity,
                        PerformanceBenchLayout.ReplacementPosition(actor.Slot, actor.Life, attempt, _settings), out position, out coordinates))
                        throw new InvalidOperationException("Не удалось разрешить позицию повторного создания.");
                    if (!SpatialService.TryReservePosition(_activity, reservationOwner, position, requirement.Footprint,
                        requirement.Spacing, 0, SpatialOccupancyMobilityType.Mobile, requirement.OccupancyTags,
                        out var reservation, out failure)) continue;
                    actor.Reservation = reservation;
                    break;
                }
                if (!actor.Reservation.IsValid) throw new InvalidOperationException(failure);
                if (!SpatialMarkerService.TryRegisterTransientMarker(_activity, _activitySnapshot.ActivityRootEntityId,
                    new SpatialPose(position, coordinates, Quaternion.identity), out var markerHandle, out var markerEntity, out failure))
                    throw new InvalidOperationException(failure);
                actor.PlacementMarker = markerHandle; _placementMarkers.Add(markerHandle);
                if (!SpatialMarkerService.TryGetMarker(markerEntity, out var marker)
                    || !SpatialMarkerService.TryAcquireMarkerSelection(_activity, _activitySnapshot.ActivityRootEntityId,
                        null, marker.ProviderId, marker, out var lease, out failure))
                    throw new InvalidOperationException(failure ?? "Не удалось удержать маркер создания.");
                actor.MarkerLease = lease;
                return new ActivityMaterializationData(_activitySnapshot.DomainId, _worldOwner, entry.Handle.Address,
                    _assets.worldWallet.Tags, entry.Asset, entry.Handle, entry.RuntimeTags, _activitySnapshot.ActivityRootEntityId,
                    null, marker, actor.Reservation, lease);
            }
            finally
            {
                actor.Operation.helperCpuMs += (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                if (actor.Operation.helperAllocatedBytes >= 0) actor.Operation.helperAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
            }
        }
        void ReleaseActorPlacement(Actor actor)
        {
            using var sample = PlacementMarker.Auto(_run, actor.Operation.operation, 1);
            long started = Stopwatch.GetTimestamp(), allocated = GC.GetAllocatedBytesForCurrentThread();
            if (actor.MarkerLease.IsValid) SpatialMarkerService.ReleaseMarkerSelection(actor.MarkerLease);
            if (actor.Reservation.IsValid) SpatialService.ReleaseReservation(actor.Reservation);
            if (actor.PlacementMarker.IsValid)
            { SpatialMarkerService.ReleaseTransientMarker(actor.PlacementMarker); _placementMarkers.Remove(actor.PlacementMarker); }
            actor.MarkerLease = default; actor.Reservation = default; actor.PlacementMarker = default;
            actor.Operation.helperCpuMs += (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
            if (actor.Operation.helperAllocatedBytes >= 0) actor.Operation.helperAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
        }
        void RegisterActorEntity(Actor actor, EntityId entity)
        {
            actor.Entity = entity; actor.Operation.entity = entity.Value; actor.Operation.entityAt = ClockNow;
            _byEntity.Add(entity.Value, actor); _population++;
        }
        public void OnEvent(CapabilityHostRegisteredEvent e)
        {
            using var observation = ObserverMarker.Auto();
            if (_materializing == null || e.Snapshot.ActivityId != _activity
                || e.Snapshot.OwnerLevel != EconomyOwnerLevelType.Instance || !e.Snapshot.Definition.Matches(_definition)) return;
            RegisterActorEntity(_materializing, e.Snapshot.EntityId);
        }
        public void OnEvent(ProjectionLifecycleEvent e)
        {
            using var observation = ObserverMarker.Auto();
            if (_disposed || e.Handle.ActivityId != _activity) return;
            if (!_byEntity.TryGetValue(e.Handle.EntityId.Value, out var actor))
                return;
            if (e.EventType == ProjectionLifecycleEventType.BoundReady)
            {
                if (actor.Deleting || actor.EntityRemoved || actor.Ready) return;
                actor.Binding = e.Handle; actor.Ready = true; _bound++;
                actor.Renderer = e.ViewTransform.GetComponent<Renderer>(); actor.Operation.readyAt = ClockNow;
                ref var work = ref WorkFor(Time.frameCount); work.Rented++;
                if (_materializing != actor) _readyCreations.Enqueue(new ActorContinuation(actor, _run, _activity));
            }
            else if (e.EventType == ProjectionLifecycleEventType.Removed && actor.Binding.Equals(e.Handle))
            {
                actor.Operation.releasedAt = ClockNow; actor.ViewReleased = true;
                if (actor.Ready) { actor.Ready = false; _bound--; }
                actor.Renderer = null;
                if (!actor.Pending.IsValid && actor.Deleting) FinishDeletion(actor);
            }
        }
        void FinishCreation(Actor actor)
        {
            if (!actor.Creating || !actor.Ready) return;
            ReleaseActorPlacement(actor);
            SetCreating(actor, false); actor.Operation.success = true; _measurements.Operation(actor.Operation);
            ref var work = ref WorkFor(Time.frameCount); work.Created++;
            if (_settings.Moves && _phase != PerformanceBenchPhaseType.Preparing)
            {
                actor.Right = PerformanceBenchLayout.InitialDirection(actor.Slot, _settings.seed);
                QueuePatrol(actor);
            }
        }
        void CompleteReadyCreations()
        {
            // BoundReady is published before Projection finishes committing its marker lease.
            // Release our placement ownership after that synchronous callback has returned.
            while (_readyCreations.Count > 0)
            {
                var continuation = _readyCreations.Dequeue(); var actor = continuation.Actor;
                if (continuation.Run != _run || continuation.Activity != _activity || continuation.Life != actor.Life
                    || !continuation.Binding.Equals(actor.Binding)) continue;
                FinishCreation(actor);
            }
        }
        void OnEntityRemoved(EntityId entity)
        {
            using var observation = ObserverMarker.Auto();
            if (!_byEntity.TryGetValue(entity.Value, out var actor)) return;
            actor.EntityRemoved = true; actor.Operation.removedAt = ClockNow; _population--;
            if (!actor.Binding.IsValid) actor.ViewReleased = true;
            if (actor.Deleting && !actor.Pending.IsValid) FinishDeletion(actor);
        }
        void FinishDeletion(Actor actor)
        {
            if (!actor.Deleting || !actor.EntityRemoved || !actor.ViewReleased) return;
            SetDeleting(actor, false); actor.Operation.success = true; _measurements?.Operation(actor.Operation);
            _byEntity.Remove(actor.Entity.Value); actor.Entity = default; actor.Token = default; actor.Binding = default;
            ref var work = ref WorkFor(Time.frameCount); work.Deleted++;
        }
        void QueuePatrol(Actor actor)
        {
            if (_stop || actor.Deleting || !actor.Ready || actor.OrderQueued) return;
            actor.OrderQueued = true; _patrolOrders.Enqueue(new ActorContinuation(actor, _run, _activity));
        }
        void DrainPatrolOrders()
        {
            while (_patrolOrders.Count > 0)
            {
                var order = _patrolOrders.Dequeue(); var actor = order.Actor;
                if (order.Life != actor.Life || order.Run != _run || order.Activity != _activity || !order.Binding.Equals(actor.Binding)) continue;
                actor.OrderQueued = false;
                if (_stop || actor.Deleting || !actor.Ready || actor.EntityRemoved) continue;
                var pair = _targets[PerformanceBenchLayout.Room(actor.Slot, _settings.visiblePopulation)];
                EventBus.Trigger(new AIBrainStateOrderEvent(new AIBrainStateOrder(actor.Entity,
                    _assets.controlNode, _assets.moveState, actor.Right ? pair.Right : pair.Left)));
            }
        }
        public void OnEvent(AIBrainStateCompletedEvent e)
        {
            using var observation = ObserverMarker.Auto();
            if (_byEntity.TryGetValue(e.OwnerEntityId.Value, out var actor) && e.State == _assets.moveState
                && e.ResultType == AIBrainStateResultType.Success)
            { actor.Right = !actor.Right; QueuePatrol(actor); }
        }
    }
}
#endif
