using Core.Activities;
using Core.Entities;
using Core.Events;
using Core.Projection;
using UnityEngine;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Autobattle
{
    [DisallowMultipleComponent]
    public sealed class ExperienceDropView : MonoBehaviour, IProjectionBindingConsumer
    {
        [SerializeField] GameObject visualRoot;

        ActivityId _activityId;
        EntityId _entityId;
        bool _collectionPublished;

        public void OnProjectionBound(ProjectionBindingContext context)
        {
            _activityId = context.Handle.ActivityId;
            _entityId = context.Handle.EntityId;
            _collectionPublished = false;
            SetVisualActive(true);
        }

        public void OnProjectionUnbound(ProjectionBindingContext context)
        {
            if (context.Handle.EntityId != _entityId)
                return;

            _activityId = ActivityId.Invalid;
            _entityId = EntityId.Invalid;
            _collectionPublished = false;
            SetVisualActive(true);
        }

        public void PublishCollectionStarted()
        {
            if (_collectionPublished || !_activityId.IsValid || !_entityId.IsValid)
                return;

            Camera worldCamera = Camera.main;
            if (worldCamera == null)
                return;

            _collectionPublished = true;
            Vector3 screenPosition = worldCamera.WorldToScreenPoint(transform.position);
            EventBus.Trigger(new ExperienceCollectionStartedEvent(
                _activityId,
                _entityId,
                new Vector2(screenPosition.x, screenPosition.y)));
            SetVisualActive(false);
        }

        void SetVisualActive(bool active)
        {
            if (visualRoot != null)
                visualRoot.SetActive(active);
        }
    }
}
