using System;
using System.Collections.Generic;
using Core.Economy;
using Core.Events;
using Core.Orchestration;
using UnityEngine;

namespace ChainRush.Gameplay
{
    public enum ChainRushBoardRosterContentType { Unit, HeroSkill }

    [Serializable]
    public sealed class ChainRushBoardRosterContentBinding
    {
        public EconomyAssetData Output;
        public string ContentId;
        public ChainRushBoardRosterContentType Type;
    }

    [CreateAssetMenu(fileName = "ChainRushBoardRosterEligibility", menuName = "ChainRush/Gameplay/Board Roster Eligibility")]
    public sealed class ChainRushBoardRosterEligibilityData : PopulationContentEligibilityData
    {
        [SerializeField] List<ChainRushBoardRosterContentBinding> bindings = new List<ChainRushBoardRosterContentBinding>();

        public override bool TryValidate(out string failure)
        {
            failure = null;
            var outputs = new HashSet<string>(StringComparer.Ordinal);
            if (bindings.Count == 0) { failure = "Board roster eligibility requires explicit output bindings."; return false; }
            foreach (var binding in bindings)
                if (binding == null || binding.Output == null || !outputs.Add(binding.Output.Id)
                    || string.IsNullOrWhiteSpace(binding.ContentId) || !Enum.IsDefined(typeof(ChainRushBoardRosterContentType), binding.Type))
                { failure = "Board roster bindings require unique outputs and valid source identities."; return false; }
            return true;
        }

        public override bool TryEvaluate(IEconomyAssetOwner owner, EconomyAssetData output, out bool eligible, out string failure)
        {
            eligible = false; failure = null;
            if (owner == null || output == null) { failure = "Board roster eligibility requires a participant and output."; return false; }
            ChainRushRunSnapshot input = null;
            bool ambiguous = false;
            EventBus.Trigger(new ChainRushRunInputRequestEvent(snapshot =>
            {
                ambiguous |= input != null;
                input = snapshot;
            }));
            if (input == null || ambiguous) { failure = "Board roster eligibility requires exactly one immutable run input."; return false; }
            foreach (var binding in bindings)
            {
                if (!binding.Output.Matches(output)) continue;
                if (binding.Type == ChainRushBoardRosterContentType.Unit)
                    foreach (var unit in input.Units) eligible |= unit.ContentId == binding.ContentId;
                else
                    foreach (string skill in input.Hero.AcquiredSkills) eligible |= skill == binding.ContentId;
                return true;
            }
            failure = "Board output has no explicit roster binding: " + output.Id;
            return false;
        }
    }
}
