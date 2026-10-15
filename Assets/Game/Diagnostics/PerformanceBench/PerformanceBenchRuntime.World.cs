#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Core.CapabilityHosts.Runtime;
using Core.Events;
using Core.World;
using UnityEngine;

namespace ChainRush.Diagnostics
{
    internal sealed partial class PerformanceBenchRuntime
    {
        void PrepareTargetsAndGeometry()
        {
            int rooms = (int)Math.Ceiling(_settings.population / (double)_settings.visiblePopulation);
            _targets = new TargetPair[rooms];
            var surface = new List<NavigationGeometryShape>();
            for (int room = 0; room < rooms; room++)
            {
                var origin = PerformanceBenchLayout.RoomOrigin(room, _settings.density);
                _targets[room] = new TargetPair
                {
                    Left = CreateTarget(PerformanceBenchLayout.Target(room, false, _settings.density)),
                    Right = CreateTarget(PerformanceBenchLayout.Target(room, true, _settings.density)),
                };
                surface.Add(Rectangle(origin.x - 22 / _settings.density, -17 / _settings.density,
                    origin.x + 22 / _settings.density, 17 / _settings.density));
                if (_settings.scenario != PerformanceBenchScenarioType.Navigation) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    var center = origin + new Vector3(0, side * 8 / _settings.density, 0);
                    if (!TopologyService.TryResolvePosition(_activity, center, out var position, out _))
                        throw new InvalidOperationException("Не удалось разместить препятствие.");
                    var obstacle = CapabilityHostService.Register(new CapabilityHostRegistrationRequest(
                        _assets.obstacleObject, _worldOwner, _activity, Core.CapabilityHosts.CapabilityHostPlacementType.Spatial, position));
                    if (!obstacle.IsValid) throw new InvalidOperationException("Регистрация препятствия не выполнена.");
                    _obstacles.Add(obstacle);
                    var shapes = new List<NavigationGeometryShape>
                    { Rectangle(center.x - .3f, center.y - 4, center.x + .3f, center.y + 4) };
                    EventBus.Trigger(new NavigationGeometryRegistrationRequestedEvent(_activity, obstacle,
                        "performance-bench-obstacle", NavigationGeometryType.Obstacle, Vector3.zero, shapes));
                }
            }
            if (_settings.scenario == PerformanceBenchScenarioType.Navigation)
                EventBus.Trigger(new NavigationGeometryRegistrationRequestedEvent(_activity, _activitySnapshot.ActivityRootEntityId,
                    "performance-bench-surface", NavigationGeometryType.Surface, Vector3.zero, surface));
        }
        Core.Entities.EntityId CreateTarget(Vector3 coordinates)
        {
            if (!TopologyService.TryResolvePosition(_activity, coordinates, out var position, out var resolved)
                || !SpatialMarkerService.TryRegisterTransientMarker(_activity, _activitySnapshot.ActivityRootEntityId,
                    new SpatialPose(position, resolved, Quaternion.identity), out var handle, out var entity, out string failure))
                throw new InvalidOperationException("Не удалось создать Entity цели маршрута.");
            _markers.Add(handle); return entity;
        }
        static NavigationGeometryShape Rectangle(float left, float bottom, float right, float top)
        {
            var world = new List<Vector3> { new(left, bottom, 0), new(right, bottom, 0), new(right, top, 0), new(left, top, 0) };
            var frame = NavigationFrame.Planar(TopologyUpAxisType.Z);
            return NavigationGeometryShape.Polygon(new List<Vector2>
                { new(left, bottom), new(right, bottom), new(right, top), new(left, top) }, world, frame);
        }
        void ReleaseAuxiliaryEntities()
        {
            if (_actors != null) foreach (var actor in _actors) ReleaseActorPlacement(actor);
            foreach (var marker in _placementMarkers) SpatialMarkerService.ReleaseTransientMarker(marker);
            _placementMarkers.Clear();
            foreach (var marker in _markers) SpatialMarkerService.ReleaseTransientMarker(marker);
            _markers.Clear();
            foreach (var obstacle in _obstacles)
            {
                EventBus.Trigger(new NavigationGeometryUnregistrationRequestedEvent(_activity, obstacle,
                    "performance-bench-obstacle", NavigationGeometryType.Obstacle));
                CapabilityHostService.Unregister(obstacle);
            }
            _obstacles.Clear();
            if (_activity.IsValid && _activitySnapshot.ActivityRootEntityId.IsValid)
                EventBus.Trigger(new NavigationGeometryUnregistrationRequestedEvent(_activity, _activitySnapshot.ActivityRootEntityId,
                    "performance-bench-surface", NavigationGeometryType.Surface));
        }
    }
}
#endif
