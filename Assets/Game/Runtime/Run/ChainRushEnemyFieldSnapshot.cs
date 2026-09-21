using Core.Activities;

namespace ChainRush.Gameplay
{
    /// <summary>Observed emission and field facts; these do not decide the outcome of the run.</summary>
    public readonly struct ChainRushEnemyFieldSnapshot
    {
        public ActivityId ActivityId { get; }
        public bool IsReady { get; }
        public bool ProgressComplete { get; }
        public bool EmissionSettled { get; }
        public bool FieldCleared { get; }
        public long Existing { get; }
        public long Incoming { get; }
        public string Diagnostic { get; }

        public ChainRushEnemyFieldSnapshot(ActivityId activityId, bool isReady, bool progressComplete,
            bool emissionSettled, bool fieldCleared, long existing, long incoming, string diagnostic)
        {
            ActivityId = activityId;
            IsReady = isReady;
            ProgressComplete = progressComplete;
            EmissionSettled = emissionSettled;
            FieldCleared = fieldCleared;
            Existing = existing;
            Incoming = incoming;
            Diagnostic = diagnostic;
        }
    }
}
