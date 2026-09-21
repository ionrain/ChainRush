using System;
using System.Collections.Generic;
using Core.GameFlow;
using Core.GameRuntime;
using UnityEngine;

namespace ChainRush.Gameplay
{
    /// <summary>Working model: the captured level chooses an explicitly authored GameFlow.</summary>
    [CreateAssetMenu(fileName = "StartChainRushLevel", menuName = "ChainRush/Gameplay/Start Selected Level")]
    public sealed class StartChainRushLevelActionData : GameStartupActionData
    {
        [SerializeField] List<ChainRushLevelFlowBinding> levels = new List<ChainRushLevelFlowBinding>();
        [SerializeField] string externalKey;

        public IReadOnlyList<ChainRushLevelFlowBinding> Levels => levels;
        public override GameStartupPhase Phase => GameStartupPhase.PostWorld;

        public override void Execute(GameRuntimeContext context)
        {
            if (context == null || !context.TryGetRuntimeValue<ChainRushRunSnapshot>(ChainRushRunSnapshot.RuntimeKey, out var input))
                throw new InvalidOperationException("Level startup requires a captured ChainRush run.");
            GameFlowTemplateData selected = null;
            foreach (var level in levels)
                if (level != null && level.LevelId == input.LevelId && level.HeroId == input.Hero.ContentId)
                {
                    if (selected != null) throw new InvalidOperationException("The selected level has multiple GameFlow bindings.");
                    selected = level.Flow ?? throw new InvalidOperationException("The selected level has no GameFlow.");
                }
            if (selected == null) throw new InvalidOperationException("The selected level has no authored framework content.");
            if (!GameFlowService.Add(new GameFlowAddRequest(selected, context.PrimaryOwner, context.PrimaryOwner, externalKey)).IsValid)
                throw new InvalidOperationException("The selected level GameFlow could not start.");
        }
    }

    [Serializable]
    public sealed class ChainRushLevelFlowBinding
    {
        [SerializeField] string levelId;
        [SerializeField] string heroId;
        [SerializeField] GameFlowTemplateData flow;
        public string LevelId => levelId;
        public string HeroId => heroId;
        public GameFlowTemplateData Flow => flow;

        public ChainRushLevelFlowBinding(string levelId, string heroId, GameFlowTemplateData flow)
        { this.levelId = levelId; this.heroId = heroId; this.flow = flow; }
    }
}
