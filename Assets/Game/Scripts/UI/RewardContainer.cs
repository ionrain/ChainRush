using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class RewardContainer : SerializedMonoBehaviour {
    [SerializeField] protected RewardListPopup bubble;
    [SerializeField] protected float xPivot = 0.5f;
    [SerializeField] protected Image icon;
    [SerializeField] protected Dictionary<RewardState, Sprite> stateSprites = new Dictionary<RewardState, Sprite>();
    [SerializeField] protected Dictionary<RewardState, ParticleSystem> stateFXs = new Dictionary<RewardState, ParticleSystem>();

    [Header("Events")]
    [SerializeField] UnityEvent OnOpen;
    [SerializeField] UnityEvent OnBubble;

    protected Button _button;
    protected RectTransform _transform;
    protected IRewardItem _data;

    protected void Awake() {
        _transform = GetComponent<RectTransform>();
        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(OnClicked);
    }

    public virtual void Setup(IRewardItem data, UnityAction callback = null) {
        bubble?.SetVisibility(false);

        if (callback != null)
            OnOpen.AddListener(callback);

        _data = data;
        if (_data != null) {
            UpdateIcon();
            UpdateFX();
        }
    }

    protected void UpdateIcon() {
        if (icon != null && stateSprites.ContainsKey(_data.State))
            icon.sprite = stateSprites[_data.State];
    }

    protected void UpdateFX() {
        foreach (var pair in stateFXs) {
            if (pair.Value != null) {
                var fx = pair.Value;
                if (pair.Key != _data.State) {
                    if (fx.isPlaying)
                        fx.Stop();
                    fx.gameObject.SetActive(false);
                } else {
                    fx.gameObject.SetActive(true);
                    if (fx.isStopped)
                        fx.Play();
                }
            }
        }
    }

    protected void OnClicked() {
        if (_data != null) {
            if (_data.State == RewardState.Ready) {
                _data.SetState(RewardState.Taken);
                RewardEvent.Trigger(EventStage.Start, RewardEventType.Transfer, _data);
                OnOpen?.Invoke();
                UpdateIcon();
                UpdateFX();
            } else if (bubble != null && bubble.Setup(_data.Rewards) && _transform != null) {
                OnBubble?.Invoke();
                bubble.SetPosition(_transform.position, xPivot);
                bubble.SetVisibility(true);
            }
        }
    }
}