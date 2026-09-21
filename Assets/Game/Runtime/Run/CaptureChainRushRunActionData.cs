using System;
using Core.Events;
using Core.GameRuntime;
using UnityEngine;

namespace ChainRush.Gameplay
{
    [CreateAssetMenu(fileName = "CaptureChainRushRun", menuName = "ChainRush/Gameplay/Capture Run Startup Action")]
    public sealed class CaptureChainRushRunActionData : GameStartupActionData
    {
        [SerializeField] ChainRushRunSelectionData selection;
        public override GameStartupPhase Phase => GameStartupPhase.PreWorld;

        public override void Execute(GameRuntimeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (selection == null || !context.SessionSeed.HasValue)
                throw new InvalidOperationException("Run capture requires an explicit selection and session seed.");
            if (context.TryGetRuntimeValue<ChainRushRunSnapshot>(ChainRushRunSnapshot.RuntimeKey, out _))
                throw new InvalidOperationException("Run input has already been captured for this runtime.");
            ChainRushRunSnapshot snapshot = selection.Capture(context.SessionSeed.Value);
            context.SetRuntimeValue(ChainRushRunSnapshot.RuntimeKey, snapshot);
            context.HostRoot.gameObject.AddComponent<ChainRushRunInputAdapter>().Initialize(snapshot);
            EventBus.Trigger(new ChainRushRunPreparedEvent(snapshot));
        }
    }
}
