using System;
using Core.Events;
using UnityEngine;

namespace ChainRush.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ChainRushRunInputAdapter : MonoBehaviour, IEventListener<ChainRushRunInputRequestEvent>
    {
        ChainRushRunSnapshot _snapshot;
        public void Initialize(ChainRushRunSnapshot snapshot)
        {
            if (_snapshot != null) throw new InvalidOperationException("Run input is immutable after initialization.");
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }
        void OnEnable() => EventBus.Register<ChainRushRunInputRequestEvent>(this);
        void OnDisable() => EventBus.Unregister<ChainRushRunInputRequestEvent>(this);
        public void OnEvent(ChainRushRunInputRequestEvent request)
        {
            if (_snapshot != null) request.Reply(_snapshot);
        }
    }
}
