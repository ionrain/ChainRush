using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class Popup<T> : SerializedMonoBehaviour {
    protected const string ANIMATOR_VISIBLE = "Visible";

    [SerializeField] protected T data;
    [SerializeField] protected Animator animator;
    [SerializeField] protected RectTransform positionContainer;
    [SerializeField] protected float showDelay;
    [SerializeField] protected bool modifyTimeScale;

    [Header("Events")]
    [SerializeField] public UnityEvent OnShow;
    [SerializeField] public UnityEvent OnShown;
    [SerializeField] public UnityEvent OnHide;
    [SerializeField] public UnityEvent OnHidden;

    protected RectTransform _transform;
    protected bool _canBeShown = true;
    float _timeScale = 1f;

    public bool Visible { get; protected set; }

    protected virtual void Awake() {
        _transform = GetComponent<RectTransform>();
    }

    public virtual void SetShowDelay(float value) {
        showDelay = value;
    }

    public void SetTimescale(float value) {
        if (modifyTimeScale)
            _timeScale = value;
    }    

    public virtual bool Setup() {
        return Setup(data);
    }

    public virtual bool Setup(T value) {
        if (data == null || !data.Equals(value))
            data = value;
        return true;
    }

    protected virtual IEnumerator Hidden(float delay) {
        yield return new WaitForSecondsRealtime(delay);
        OnHidden?.Invoke();
    }

    protected virtual IEnumerator Shown(float delay) {
        yield return new WaitForSecondsRealtime(delay);
        OnShown?.Invoke();
    }

    public virtual void SetCanBeShown(bool value) {
        _canBeShown = value;
    }

    public virtual void SetVisibility(bool visible) {
        if (gameObject.activeInHierarchy && (_canBeShown || !visible) && Visible != visible) {
            Visible = visible;
            if (showDelay > 0)
                StartCoroutine(SetVisibilityCo(visible));
            else
                SetVisibilityInternal(visible);
        }
    }

    protected virtual IEnumerator SetVisibilityCo(bool visible) {
        if (visible && showDelay > 0)
            yield return new WaitForSeconds(showDelay); 

        SetVisibilityInternal(visible);
    }

    public virtual void SetVisibilityInternal(bool visible) {
        float delay = 0;
        if (modifyTimeScale)
            Time.timeScale = visible ? 0 : _timeScale;            
        if (animator != null) {
            animator.SetBool(ANIMATOR_VISIBLE, visible);
            delay = animator.GetCurrentAnimatorStateInfo(0).length;
        }

        if (visible) {
            OnShow?.Invoke();
            if (delay > 0)
                StartCoroutine(Shown(delay));
        } else {
            OnHide?.Invoke();
            if (delay > 0)
                StartCoroutine(Hidden(delay));
        }        
    }

    public virtual void SetPosition(Vector2 position, float xPivot = 0.5f) {
        if (_transform != null)
            _transform.position = position;
        if (positionContainer != null) {
            positionContainer.pivot = new Vector2(xPivot, positionContainer.pivot.y);
            positionContainer.anchoredPosition = new Vector2(0, positionContainer.anchoredPosition.y);
        }
    }
}
