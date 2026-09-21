using System;
using Core.Events;

namespace ChainRush.Gameplay
{
    public readonly struct ChainRushRunInputRequestEvent : IEvent
    {
        readonly Action<ChainRushRunSnapshot> _reply;
        public ChainRushRunInputRequestEvent(Action<ChainRushRunSnapshot> reply) => _reply = reply ?? throw new ArgumentNullException(nameof(reply));
        public void Reply(ChainRushRunSnapshot snapshot) => _reply(snapshot);
    }
}
