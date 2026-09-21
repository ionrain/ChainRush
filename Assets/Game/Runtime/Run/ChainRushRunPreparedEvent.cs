namespace ChainRush.Gameplay
{
    public readonly struct ChainRushRunPreparedEvent : Core.Events.IEvent
    {
        public ChainRushRunSnapshot Snapshot { get; }
        public ChainRushRunPreparedEvent(ChainRushRunSnapshot snapshot) => Snapshot = snapshot;
    }
}
