using Core.Events;
using Core.Projection;
using UnityEngine;

namespace ChainRush.Gameplay
{
    // Visual-only anchor for the nonspatial experience collector's existing projection transition.
    [DisallowMultipleComponent]
    public sealed class ChainRushRunProjectionFollowController : MonoBehaviour,
        IProjectionBindingConsumer, IEventListener<ChainRushRunDisplacementEvent>
    {
        ProjectionBindingContext _binding;
        Vector3 _origin;

        void OnEnable() => EventBus.Register<ChainRushRunDisplacementEvent>(this);
        void OnDisable()
        {
            EventBus.Unregister<ChainRushRunDisplacementEvent>(this);
            ResetBinding();
        }

        public void OnProjectionBound(ProjectionBindingContext context)
        { _binding = context; _origin = transform.position; }

        public void OnProjectionUnbound(ProjectionBindingContext context) => ResetBinding();

        public void OnEvent(ChainRushRunDisplacementEvent e)
        {
            if (_binding.Handle.IsValid && _binding.Handle.ActivityId == e.ActivityId)
                transform.position = _origin + (e.IsActive ? e.Displacement : Vector3.zero);
        }

        void ResetBinding()
        {
            if (_binding.Handle.IsValid) transform.position = _origin;
            _binding = default;
        }
    }
}
