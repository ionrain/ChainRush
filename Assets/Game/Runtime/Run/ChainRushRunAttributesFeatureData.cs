using System;
using System.Collections.Generic;
using Core.Activities;
using Core.Activities.Selection;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Economy;
using Core.Economy.Authoring;
using Core.Events;
using Core.HostValues;
using Core.Runtime;
using Core.Skills;
using Core.Taxonomy;
using UnityEngine;
using EntityId = Core.Entities.EntityId;
using FrameworkAttributeData = Core.Attributes.AttributeData;

namespace ChainRush.Gameplay
{
    [Serializable]
    public sealed class ChainRushRunAttributeBinding
    {
        public global::Attribute Attribute;
        public Element Element;
        public FrameworkAttributeData Definition;
        public float FormMultiplier;
    }

    [Serializable]
    public sealed class ChainRushRunCharacterBinding
    {
        public string ContentId;
        public CapabilityHostData Definition;
        public List<ChainRushRunAttributeBinding> Attributes = new List<ChainRushRunAttributeBinding>();
        public List<ChainRushRunSkillBinding> Skills = new List<ChainRushRunSkillBinding>();
    }

    [Serializable]
    public sealed class ChainRushRunSkillBinding
    {
        public string ContentId;
        public Core.Skills.SkillData Definition;
    }

    [Serializable]
    public sealed class ChainRushRunBuffBinding
    {
        public CapabilityHostData Cell;
        public global::Attribute Attribute;
        public List<float> Grades = new List<float>();
    }

    /// <summary>Run-owned development and additive buffs projected into the existing attribute wallets.</summary>
    [CreateAssetMenu(fileName = "ChainRushRunAttributes", menuName = "ChainRush/Gameplay/Run Attributes Feature")]
    public sealed class ChainRushRunAttributesFeatureData : ActivityFeatureData
    {
        [SerializeField] ActivityData board;
        [SerializeField] HostValueData health;
        [SerializeField] List<TaxonomyTermData> unitWalletTags = new List<TaxonomyTermData>();
        [SerializeField] List<ChainRushRunCharacterBinding> characters = new List<ChainRushRunCharacterBinding>();
        [SerializeField] List<ChainRushRunBuffBinding> buffs = new List<ChainRushRunBuffBinding>();

        public override IActivityFeatureRuntime CreateRuntime() => new Runtime();

        sealed class Runtime : IActivityFeatureRuntime, IEventListener<CapabilityHostRegisteredEvent>,
            IEventListener<CapabilityHostUnregisteredEvent>, IEventListener<SelectionResultEvent>,
            IEventListener<HostValueChangedEvent>
        {
            sealed class Character
            {
                public CapabilityHostSnapshot Host;
                public ChainRushCharacterSnapshot Input;
                public ChainRushRunCharacterBinding Binding;
                public FrameworkAttributeData HealthAttribute;
                public long MaximumHealth;
                public long? PendingHealth;
                public bool Dead;
            }

            ChainRushRunAttributesFeatureData _data;
            ChainRushRunSnapshot _input;
            ActivityId _activity;
            IEconomyAssetOwner _owner;
            readonly Dictionary<EntityId, Character> _characters = new Dictionary<EntityId, Character>();
            readonly Dictionary<global::Attribute, double> _buffs = new Dictionary<global::Attribute, double>();
            readonly HashSet<(ActivityId, SelectionRequestId)> _selections = new HashSet<(ActivityId, SelectionRequestId)>();

            public ActivityFeatureTickStageType TickStages => ActivityFeatureTickStageType.BeforeObjective;

            public bool Validate(in ActivityFeatureValidationContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                if (context.Bindings.Count != 1 || !(context.Bindings[0].Feature is ChainRushRunAttributesFeatureData data)
                    || data.board == null || data.health == null || data.unitWalletTags.Count == 0 || data.characters.Count == 0)
                { diagnosticMessage = "Run attributes require one participant binding, Board, health and explicit character mappings."; return false; }
                var definitions = new HashSet<CapabilityHostData>();
                foreach (var character in data.characters)
                {
                    if (character == null || string.IsNullOrWhiteSpace(character.ContentId) || character.Definition == null
                        || !definitions.Add(character.Definition) || character.Attributes.Count == 0)
                    { diagnosticMessage = "Run character definitions must be unique and have explicit attributes."; return false; }
                    var attributes = new HashSet<FrameworkAttributeData>();
                    bool hasHealth = false;
                    foreach (var attribute in character.Attributes)
                    {
                        if (attribute == null || attribute.Definition == null || !attributes.Add(attribute.Definition)
                            || float.IsNaN(attribute.FormMultiplier) || float.IsInfinity(attribute.FormMultiplier)
                            || attribute.FormMultiplier < 0)
                        { diagnosticMessage = "Run attribute mappings require unique definitions and finite form multipliers."; return false; }
                        hasHealth |= attribute.Attribute == global::Attribute.Health && attribute.Element == Element.Any;
                    }
                    if (!hasHealth) { diagnosticMessage = "A run character must declare its maximum health attribute."; return false; }
                    foreach (var skill in character.Skills)
                        if (skill == null || string.IsNullOrWhiteSpace(skill.ContentId) || skill.Definition == null)
                        { diagnosticMessage = "Run skills require source identities and framework definitions."; return false; }
                }
                var cells = new HashSet<CapabilityHostData>();
                foreach (var buff in data.buffs)
                {
                    if (buff == null || buff.Cell == null || !cells.Add(buff.Cell) || buff.Grades.Count == 0)
                    { diagnosticMessage = "Run buffs require unique cells and explicit grade values."; return false; }
                    foreach (float grade in buff.Grades)
                        if (float.IsNaN(grade) || float.IsInfinity(grade))
                        { diagnosticMessage = "Run buff values must be finite."; return false; }
                }
                return true;
            }

            public bool Open(in ActivityFeatureOpenContext context, out string diagnosticMessage)
            {
                diagnosticMessage = null;
                _data = (ChainRushRunAttributesFeatureData)context.Bindings[0].Feature;
                EventBus.Trigger(new ChainRushRunInputRequestEvent(input =>
                {
                    if (_input != null) throw new InvalidOperationException("More than one run input source is active.");
                    _input = input;
                }));
                if (_input == null || context.Bindings[0].Participants.Count != 1)
                { diagnosticMessage = "Run attributes require captured input and one participant."; return false; }
                _activity = context.ActivityId;
                _owner = context.Bindings[0].Participants[0].ParticipantEconomyOwner;
                EventBus.Register<CapabilityHostRegisteredEvent>(this);
                EventBus.Register<CapabilityHostUnregisteredEvent>(this);
                EventBus.Register<SelectionResultEvent>(this);
                EventBus.Register<HostValueChangedEvent>(this);
                foreach (var host in CapabilityHostService.GetAll()) Observe(host);
                return true;
            }

            public void Tick(in ActivityFeatureTickContext context)
            {
                foreach (var character in _characters.Values)
                {
                    if (!character.PendingHealth.HasValue || character.Dead || !CapabilityHostService.TryGetEffectiveAttribute(
                        character.Host.EntityId, new Core.Attributes.AttributeSelectorData(character.HealthAttribute), out long maximum)
                        || maximum != character.MaximumHealth) continue;
                    if (!CapabilityHostService.TryGetHostValue(character.Host.EntityId, _data.health, out var current)) continue;
                    long target = Math.Max(0, Math.Min(character.MaximumHealth, character.PendingHealth.Value));
                    character.PendingHealth = null;
                    if (!CapabilityHostService.TryApplyHostValueDelta(character.Host.EntityId, _data.health,
                        target - current.CurrentValue, new RuntimeMutationContext(character.Host.EntityId,
                            character.Host.EntityId, "chainrush.run.attributes", _activity.ToString())))
                        throw new InvalidOperationException("Run health adjustment failed.");
                }
            }

            void Observe(CapabilityHostSnapshot host)
            {
                if (host.ActivityId != _activity || host.Owner?.StableSimulationKey != _owner?.StableSimulationKey
                    || _characters.ContainsKey(host.EntityId)) return;
                foreach (var binding in _data.characters)
                {
                    if (!binding.Definition.Matches(host.Definition)) continue;
                    ChainRushCharacterSnapshot input = _input.Hero.ContentId == binding.ContentId ? _input.Hero : null;
                    foreach (var unit in _input.Units) if (unit.ContentId == binding.ContentId) input = unit;
                    if (input == null) throw new InvalidOperationException("A materialized character is absent from the run roster.");
                    var character = new Character { Host = host, Input = input, Binding = binding };
                    if (!CapabilityHostService.TryGetHostValue(host.EntityId, _data.health, out var health))
                        throw new InvalidOperationException("Run character has no initialized health value.");
                    character.MaximumHealth = health.CurrentValue;
                    _characters.Add(host.EntityId, character);
                    Apply(character);
                    foreach (var skill in binding.Skills)
                    {
                        bool acquired = false;
                        foreach (string id in input.AcquiredSkills) acquired |= id == skill.ContentId;
                        if (!SkillService.TryResolveId(skill.Definition, out var skillId)
                            || !SkillService.TrySetEnabled(host.EntityId, skillId, acquired))
                            throw new InvalidOperationException("Run skill development could not be applied.");
                    }
                    return;
                }
            }

            void Apply(Character character)
            {
                foreach (var binding in character.Binding.Attributes)
                {
                    if (!character.Input.Attributes.TryGetValue(new ElementalAttribute(binding.Element, binding.Attribute), out float amount))
                        throw new InvalidOperationException("The immutable run input is missing an authored character attribute.");
                    _buffs.TryGetValue(binding.Attribute, out double buff);
                    // The source weapon finishes a nonpositive delay immediately. SkillSpeed is its duration multiplier.
                    double multiplier = binding.Attribute == global::Attribute.SkillSpeed ? Math.Max(0, 1 + buff) : 1 + buff;
                    long target = checked((long)Math.Round(amount * binding.FormMultiplier * multiplier
                        * Math.Pow(10, binding.Definition.Precision), MidpointRounding.AwayFromZero));
                    if (target < 0) throw new InvalidOperationException("Run attributes cannot resolve to negative amounts.");
                    if (binding.Attribute == global::Attribute.Health && binding.Element == Element.Any)
                    {
                        if (!CapabilityHostService.TryGetHostValue(character.Host.EntityId, _data.health, out var current))
                            throw new InvalidOperationException("Run character lost its health value.");
                        character.HealthAttribute = binding.Definition;
                        character.PendingHealth = checked((character.PendingHealth ?? current.CurrentValue)
                            + target - character.MaximumHealth);
                        character.MaximumHealth = target;
                    }
                    long existing = 0;
                    foreach (var item in EconomyService.Query(new EconomySelectionQuery(character.Host.SelfEconomyOwner,
                        _data.unitWalletTags, EconomyFormType.Stack, binding.Definition)).Items)
                        existing = checked(existing + item.Balance);
                    long delta = checked(target - existing);
                    if (delta != 0)
                    {
                        var result = EconomyService.TryExecute(new EconomySelectionOperationRequest(character.Host.SelfEconomyOwner,
                            _data.unitWalletTags, delta > 0 ? EconomyOperation.Issue : EconomyOperation.Consume,
                            binding.Definition, EconomyFormType.Stack, Math.Abs(delta), EconomyTransactionTraceContext.None));
                        if (!result.Success) throw new InvalidOperationException("Run attribute projection failed.");
                    }
                }
            }

            public void OnEvent(SelectionResultEvent e)
            {
                if (e.Type != SelectionResultType.Committed || e.SelectedEntityIds.Count == 0
                    || !ActivityService.TryGetSnapshot(e.ActivityId, out var activity) || !_data.board.Matches(activity.Definition)
                    || !CapabilityHostService.TryGet(e.SelectedEntityIds[0], out var cell)
                    || cell.Owner?.StableSimulationKey != _owner?.StableSimulationKey) return;
                foreach (var buff in _data.buffs)
                {
                    if (!buff.Cell.Matches(cell.Definition)) continue;
                    foreach (var entity in e.SelectedEntityIds)
                        if (!CapabilityHostService.TryGet(entity, out var selected) || !buff.Cell.Matches(selected.Definition)
                            || selected.ActivityId != e.ActivityId || selected.Owner?.StableSimulationKey != _owner?.StableSimulationKey)
                            throw new InvalidOperationException("A committed buff selection contains mismatching cells.");
                    if (!_selections.Add((e.ActivityId, e.RequestId))) return;
                    _buffs.TryGetValue(buff.Attribute, out double accumulated);
                    _buffs[buff.Attribute] = accumulated + buff.Grades[Math.Min(e.SelectedEntityIds.Count - 1, buff.Grades.Count - 1)];
                    foreach (var character in _characters.Values) Apply(character);
                    return;
                }
            }

            public void OnEvent(CapabilityHostRegisteredEvent e) => Observe(e.Snapshot);
            public void OnEvent(CapabilityHostUnregisteredEvent e) => _characters.Remove(e.Snapshot.EntityId);
            public void OnEvent(HostValueChangedEvent e)
            {
                if (e.Value != _data.health || !_characters.TryGetValue(e.EntityId, out var character)) return;
                if (e.CurrentRawValue <= 0) { character.Dead = true; character.PendingHealth = null; }
                else if (character.PendingHealth.HasValue)
                    character.PendingHealth = checked(character.PendingHealth.Value + e.DeltaRawValue);
            }

            public void Close(in ActivityFeatureCloseContext context)
            {
                EventBus.Unregister<CapabilityHostRegisteredEvent>(this);
                EventBus.Unregister<CapabilityHostUnregisteredEvent>(this);
                EventBus.Unregister<SelectionResultEvent>(this);
                EventBus.Unregister<HostValueChangedEvent>(this);
                _characters.Clear(); _buffs.Clear(); _selections.Clear();
                _input = null; _data = null; _owner = null; _activity = default;
            }
        }
    }
}
