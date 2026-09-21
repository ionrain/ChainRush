using System;
using Core.Activities;
using Core.Events;

namespace ChainRush.Gameplay
{
    public readonly struct ChainRushEnemyFieldRequestEvent : IEvent
    {
        readonly Action<ChainRushEnemyFieldSnapshot> _reply;
        public ActivityId ActivityId { get; }

        public ChainRushEnemyFieldRequestEvent(ActivityId activityId, Action<ChainRushEnemyFieldSnapshot> reply)
        {
            ActivityId = activityId;
            _reply = reply ?? throw new ArgumentNullException(nameof(reply));
        }

        public void Reply(ChainRushEnemyFieldSnapshot snapshot) => _reply(snapshot);
    }
}
