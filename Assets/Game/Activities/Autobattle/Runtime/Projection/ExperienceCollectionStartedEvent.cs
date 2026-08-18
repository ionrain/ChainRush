using Core.Activities;
using Core.Entities;
using Core.Events;
using UnityEngine;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Autobattle
{
    public readonly struct ExperienceCollectionStartedEvent : IEvent
    {
        public ExperienceCollectionStartedEvent(
            ActivityId activityId,
            EntityId dropEntityId,
            Vector2 screenPosition)
        {
            ActivityId = activityId;
            DropEntityId = dropEntityId;
            ScreenPosition = screenPosition;
        }

        public ActivityId ActivityId { get; }
        public EntityId DropEntityId { get; }
        public Vector2 ScreenPosition { get; }
    }
}
