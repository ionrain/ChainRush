using System;
using System.Collections.Generic;
using Core.Events;
using UnityEngine;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Gameplay
{
    public readonly struct ChainRushCombatViewCandidate
    {
        public EntityId EntityId { get; }
        public Vector3 Position { get; }
        public ChainRushCombatViewCandidate(EntityId entityId, Vector3 position)
        { EntityId = entityId; Position = position; }
    }

    public readonly struct ChainRushCombatViewRequestEvent : IEvent
    {
        public IReadOnlyList<ChainRushCombatViewCandidate> Candidates { get; }
        public Action<List<EntityId>> Reply { get; }
        public ChainRushCombatViewRequestEvent(IReadOnlyList<ChainRushCombatViewCandidate> candidates, Action<List<EntityId>> reply)
        { Candidates = candidates; Reply = reply; }
    }
}
