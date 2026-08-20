using System;
using System.Collections.Generic;
using Core.Activities;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Economy;
using Core.Entities;
using Core.Events;
using Core.Production.Authoring;
using Core.Production.Events;
using Core.Projection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Autobattle
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIProjectionContextController))]
    public sealed class ExperienceUIController :
        MonoBehaviour,
        IEventListener<CapabilityHostRegisteredEvent>,
        IEventListener<CapabilityHostUnregisteredEvent>,
        IEventListener<EconomyResourceChangedEvent>,
        IEventListener<ProductionOrderYieldedEvent>
    {
        [Header("Runtime Identity")]
        [SerializeField] CapabilityHostBaseData playerSpawnerDefinition;
        [SerializeField] EconomyAssetData experience;
        [SerializeField] ProductionRecipeData turnTokenRecipe;

        [Header("Progress")]
        [SerializeField] Slider progressBar;
        [SerializeField] TMP_Text valueLabel;

        ActivityId _activityId;
        EntityId _playerSpawnerEntityId;
        string _playerOwnerStableKey;
        long _experienceAmount;
        long _completedTurnTokens;
        UIProjectionContextController _projectionContext;

        void OnEnable()
        {
            _projectionContext = GetComponent<UIProjectionContextController>();
            if (_projectionContext != null)
                _projectionContext.BindingChanged += OnActivityBindingChanged;
            EventBus.Register<CapabilityHostRegisteredEvent>(this);
            EventBus.Register<CapabilityHostUnregisteredEvent>(this);
            EventBus.Register<EconomyResourceChangedEvent>(this);
            EventBus.Register<ProductionOrderYieldedEvent>(this);
            ApplyActivityBinding();
        }

        void OnDisable()
        {
            EventBus.Unregister<CapabilityHostRegisteredEvent>(this);
            EventBus.Unregister<CapabilityHostUnregisteredEvent>(this);
            EventBus.Unregister<EconomyResourceChangedEvent>(this);
            EventBus.Unregister<ProductionOrderYieldedEvent>(this);
            if (_projectionContext != null)
                _projectionContext.BindingChanged -= OnActivityBindingChanged;
            _projectionContext = null;
            ResetRuntimeState(clearActivity: true);
        }

        public void OnEvent(CapabilityHostRegisteredEvent e)
        {
            CapabilityHostSnapshot snapshot = e.Snapshot;
            if (!snapshot.EntityId.IsValid
                || !_activityId.IsValid
                || snapshot.ActivityId != _activityId
                || playerSpawnerDefinition == null
                || snapshot.Definition == null
                || !snapshot.Definition.Matches(playerSpawnerDefinition))
            {
                return;
            }

            if (_playerSpawnerEntityId.IsValid
                && snapshot.EntityId.Value >= _playerSpawnerEntityId.Value)
            {
                return;
            }

            _playerSpawnerEntityId = snapshot.EntityId;
            _playerOwnerStableKey = snapshot.Owner == null
                ? null
                : snapshot.Owner.StableSimulationKey;
            RefreshProgress();
        }

        public void OnEvent(CapabilityHostUnregisteredEvent e)
        {
            if (!_playerSpawnerEntityId.IsValid || e.EntityId != _playerSpawnerEntityId)
                return;

            ResetRuntimeState(clearActivity: false);
        }

        public void OnEvent(EconomyResourceChangedEvent e)
        {
            if (experience == null
                || e.Asset == null
                || !e.Asset.Matches(experience)
                || string.IsNullOrWhiteSpace(_playerOwnerStableKey)
                || e.Owner == null
                || !string.Equals(
                    e.Owner.StableSimulationKey,
                    _playerOwnerStableKey,
                    StringComparison.Ordinal))
            {
                return;
            }

            _experienceAmount = Math.Max(0L, e.NewValue);
            RefreshProgress();
        }

        public void OnEvent(ProductionOrderYieldedEvent e)
        {
            if (!_playerSpawnerEntityId.IsValid
                || e.ProductionEntityId != _playerSpawnerEntityId
                || turnTokenRecipe == null
                || !string.Equals(e.RecipeId, turnTokenRecipe.Id, StringComparison.Ordinal))
            {
                return;
            }

            _completedTurnTokens++;
            RefreshProgress();
        }

        void OnActivityBindingChanged()
        {
            ApplyActivityBinding();
        }

        void ApplyActivityBinding()
        {
            ActivityId nextActivityId = _projectionContext == null
                ? ActivityId.Invalid
                : _projectionContext.ActivityId;
            if (_activityId == nextActivityId)
                return;

            ResetRuntimeState(clearActivity: true);
            _activityId = nextActivityId;
        }

        void ResetRuntimeState(bool clearActivity)
        {
            if (clearActivity)
                _activityId = ActivityId.Invalid;
            _playerSpawnerEntityId = EntityId.Invalid;
            _playerOwnerStableKey = null;
            _experienceAmount = 0L;
            _completedTurnTokens = 0L;
            RefreshProgress();
        }

        void RefreshProgress()
        {
            long targetValue = ResolveNextTargetValue();
            if (progressBar != null)
            {
                progressBar.minValue = 0f;
                progressBar.maxValue = Math.Max(1L, targetValue);
                progressBar.SetValueWithoutNotify(Math.Min(_experienceAmount, targetValue));
            }

            if (valueLabel != null)
            {
                valueLabel.text = string.Concat(
                    _experienceAmount.ToString(),
                    " / ",
                    targetValue.ToString());
            }
        }

        long ResolveNextTargetValue()
        {
            if (turnTokenRecipe == null
                || !turnTokenRecipe.TryResolveInputs(
                    _completedTurnTokens + 1L,
                    out List<Core.Economy.Authoring.EconomyOperationData> inputs,
                    out _)
                || inputs.Count == 0)
            {
                return 1L;
            }

            return Math.Max(1L, inputs[0].Amount);
        }

    }
}
