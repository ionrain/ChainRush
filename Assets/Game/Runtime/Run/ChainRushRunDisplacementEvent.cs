using Core.Activities;
using Core.Events;
using UnityEngine;

namespace ChainRush.Gameplay
{
    public readonly struct ChainRushRunDisplacementEvent : IEvent
    {
        public ActivityId ActivityId { get; }
        public Vector3 Displacement { get; }
        public bool IsActive { get; }

        public ChainRushRunDisplacementEvent(ActivityId activityId, Vector3 displacement, bool isActive)
        { ActivityId = activityId; Displacement = displacement; IsActive = isActive; }
    }
}
