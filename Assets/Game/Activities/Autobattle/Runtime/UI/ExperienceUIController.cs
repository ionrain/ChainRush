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
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using EntityId = Core.Entities.EntityId;

namespace ChainRush.Autobattle
{
    [DisallowMultipleComponent]
    public sealed class ExperienceUIController :
        MonoBehaviour,
        IEventListener<CapabilityHostRegisteredEvent>,
        IEventListener<CapabilityHostUnregisteredEvent>,
        IEventListener<EconomyResourceChangedEvent>,
        IEventListener<ProductionOrderYieldedEvent>,
        IEventListener<ExperienceCollectionStartedEvent>
    {
        sealed class FlyingIcon
        {
            public RectTransform RectTransform;
            public Vector2 StartPosition;
            public float Elapsed;
        }

        [Header("Runtime Identity")]
        [SerializeField] CapabilityHostBaseData playerSpawnerDefinition;
        [SerializeField] EconomyAssetData experience;
        [SerializeField] ProductionRecipeData turnTokenRecipe;

        [Header("Progress")]
        [SerializeField] Slider progressBar;
        [SerializeField] TMP_Text valueLabel;

        [Header("Collection Flight")]
        [SerializeField] RectTransform collectionLayer;
        [SerializeField] RectTransform collectionTarget;
        [SerializeField] RectTransform collectionIconPrefab;
        [Min(0.01f)]
        [SerializeField] float flightDuration = 0.35f;
        [SerializeField] AnimationCurve flightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        readonly List<FlyingIcon> _flyingIcons = new List<FlyingIcon>(4);

        ActivityId _activityId;
        EntityId _playerSpawnerEntityId;
        string _playerOwnerStableKey;
        long _experienceAmount;
        long _completedTurnTokens;

        void OnEnable()
        {
            EventBus.Register<CapabilityHostRegisteredEvent>(this);
            EventBus.Register<CapabilityHostUnregisteredEvent>(this);
            EventBus.Register<EconomyResourceChangedEvent>(this);
            EventBus.Register<ProductionOrderYieldedEvent>(this);
            EventBus.Register<ExperienceCollectionStartedEvent>(this);
            ResetRuntimeState();
        }

        void OnDisable()
        {
            EventBus.Unregister<CapabilityHostRegisteredEvent>(this);
            EventBus.Unregister<CapabilityHostUnregisteredEvent>(this);
            EventBus.Unregister<EconomyResourceChangedEvent>(this);
            EventBus.Unregister<ProductionOrderYieldedEvent>(this);
            EventBus.Unregister<ExperienceCollectionStartedEvent>(this);
            ClearFlyingIcons();
        }

        void Update()
        {
            if (_flyingIcons.Count == 0 || collectionLayer == null || collectionTarget == null)
                return;

            float duration = Mathf.Max(0.01f, flightDuration);
            Vector2 targetPosition = ResolveLocalPosition(
                RectTransformUtility.WorldToScreenPoint(
                    ResolveUICamera(),
                    collectionTarget.position));
            for (int i = _flyingIcons.Count - 1; i >= 0; i--)
            {
                FlyingIcon icon = _flyingIcons[i];
                if (icon == null || icon.RectTransform == null)
                {
                    _flyingIcons.RemoveAt(i);
                    continue;
                }

                icon.Elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(icon.Elapsed / duration);
                float progress = flightCurve == null
                    ? normalizedTime
                    : flightCurve.Evaluate(normalizedTime);
                icon.RectTransform.anchoredPosition = Vector2.LerpUnclamped(
                    icon.StartPosition,
                    targetPosition,
                    progress);
                if (normalizedTime < 1f)
                    continue;

                Destroy(icon.RectTransform.gameObject);
                _flyingIcons.RemoveAt(i);
            }
        }

        public void OnEvent(CapabilityHostRegisteredEvent e)
        {
            CapabilityHostSnapshot snapshot = e.Snapshot;
            if (!snapshot.EntityId.IsValid
                || !snapshot.ActivityId.IsValid
                || playerSpawnerDefinition == null
                || snapshot.Definition == null
                || !snapshot.Definition.Matches(playerSpawnerDefinition))
            {
                return;
            }

            if (_playerSpawnerEntityId.IsValid
                && snapshot.ActivityId == _activityId
                && snapshot.EntityId.Value >= _playerSpawnerEntityId.Value)
            {
                return;
            }

            bool activityChanged = snapshot.ActivityId != _activityId;
            _activityId = snapshot.ActivityId;
            _playerSpawnerEntityId = snapshot.EntityId;
            _playerOwnerStableKey = snapshot.Owner == null
                ? null
                : snapshot.Owner.StableSimulationKey;
            if (activityChanged)
            {
                _experienceAmount = 0L;
                _completedTurnTokens = 0L;
                ClearFlyingIcons();
            }

            RefreshProgress();
        }

        public void OnEvent(CapabilityHostUnregisteredEvent e)
        {
            if (!_playerSpawnerEntityId.IsValid || e.EntityId != _playerSpawnerEntityId)
                return;

            ResetRuntimeState();
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

        public void OnEvent(ExperienceCollectionStartedEvent e)
        {
            if (!_activityId.IsValid
                || e.ActivityId != _activityId
                || !e.DropEntityId.IsValid
                || collectionLayer == null
                || collectionIconPrefab == null)
            {
                return;
            }

            RectTransform icon = Instantiate(collectionIconPrefab, collectionLayer, false);
            icon.gameObject.SetActive(true);
            Vector2 startPosition = ResolveLocalPosition(e.ScreenPosition);
            icon.anchoredPosition = startPosition;
            _flyingIcons.Add(new FlyingIcon
            {
                RectTransform = icon,
                StartPosition = startPosition,
                Elapsed = 0f,
            });
        }

        void ResetRuntimeState()
        {
            _activityId = ActivityId.Invalid;
            _playerSpawnerEntityId = EntityId.Invalid;
            _playerOwnerStableKey = null;
            _experienceAmount = 0L;
            _completedTurnTokens = 0L;
            ClearFlyingIcons();
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

        Vector2 ResolveLocalPosition(Vector2 screenPosition)
        {
            if (collectionLayer == null)
                return screenPosition;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                collectionLayer,
                screenPosition,
                ResolveUICamera(),
                out Vector2 localPosition)
                ? localPosition
                : screenPosition;
        }

        Camera ResolveUICamera()
        {
            if (collectionLayer == null)
                return null;

            Canvas canvas = collectionLayer.GetComponentInParent<Canvas>();
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
        }

        void ClearFlyingIcons()
        {
            for (int i = 0; i < _flyingIcons.Count; i++)
            {
                FlyingIcon icon = _flyingIcons[i];
                if (icon != null && icon.RectTransform != null)
                    Destroy(icon.RectTransform.gameObject);
            }

            _flyingIcons.Clear();
        }
    }
}
