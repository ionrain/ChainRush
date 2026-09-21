using System.Collections.Generic;
using Core.Events;
using UnityEngine;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ChainRushCombatViewAdapter : MonoBehaviour, IEventListener<ChainRushCombatViewRequestEvent>,
        IEventListener<ChainRushRunDisplacementEvent>
    {
        [SerializeField] Camera combatCamera;
        Vector3 _origin;
        Core.Activities.ActivityId _activity;
        bool _following;
        void OnEnable()
        {
            EventBus.Register<ChainRushCombatViewRequestEvent>(this);
            EventBus.Register<ChainRushRunDisplacementEvent>(this);
        }
        void OnDisable()
        {
            EventBus.Unregister<ChainRushCombatViewRequestEvent>(this);
            EventBus.Unregister<ChainRushRunDisplacementEvent>(this);
            if (_following && combatCamera != null) combatCamera.transform.position = _origin;
            _following = false;
        }
        public void OnEvent(ChainRushRunDisplacementEvent e)
        {
            if (combatCamera == null || _following && e.ActivityId != _activity) return;
            if (!_following)
            {
                if (!e.IsActive) return;
                _origin = combatCamera.transform.position;
                _activity = e.ActivityId;
                _following = true;
            }
            combatCamera.transform.position = _origin + (e.IsActive ? e.Displacement : Vector3.zero);
            if (!e.IsActive) _following = false;
        }
        public void OnEvent(ChainRushCombatViewRequestEvent e)
        {
            if (combatCamera == null || !combatCamera.isActiveAndEnabled) return;
            var visible = new List<EntityId>();
            foreach (var candidate in e.Candidates)
            {
                Vector3 point = combatCamera.WorldToViewportPoint(candidate.Position);
                if (point.z >= combatCamera.nearClipPlane && point.z <= combatCamera.farClipPlane
                    && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1)
                    visible.Add(candidate.EntityId);
            }
            e.Reply(visible);
        }
    }
}
