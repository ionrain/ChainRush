using System.Collections;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Events;

public class OverTimeAction : MonoBehaviour {
    [Header("Action Settings")]
    [SerializeField] protected bool infinite = true;
    [MMCondition("infinite", true, true)]
    [SerializeField] protected bool turnOffOnFinish;
    [MMCondition("infinite", true, true)]
    [SerializeField] protected int totalAmount;
    [SerializeField] protected int oneTimeAmount;
    [SerializeField] protected float initialDelay;
    [SerializeField] protected float intervalBetween;

    [Header("Events")]
    [SerializeField] protected UnityEvent OnBegin;
    [SerializeField] protected UnityEvent OnProgress;
    [SerializeField] protected UnityEvent OnFinish;

    protected int _progress;

    public int OneTimeAmount => oneTimeAmount;
    public int TotalAmount => totalAmount;

    protected virtual void Awake() {
        _progress = 0;
    }    

    public virtual void Activate() {
        StartCoroutine(Action());
    }

    public virtual void SetOneTimeAmount(int value) {
        oneTimeAmount = value;
    }

    public virtual void SetTotalAmount(int value) {
        totalAmount = value;
    }

    protected virtual IEnumerator Action() {
        if (initialDelay > 0)
            yield return new WaitForSeconds(initialDelay);
        OnBegin?.Invoke();
        while (infinite || _progress < totalAmount) {
            bool needWait = true;
            _progress += oneTimeAmount;
            int amount = oneTimeAmount;
            if (!infinite && _progress >= totalAmount) {
                amount = oneTimeAmount - (totalAmount - _progress);
                _progress = totalAmount;
                needWait = false;
            }
            HandleProgress(amount);
            OnProgress?.Invoke();
            if (needWait)
                yield return new WaitForSeconds(intervalBetween);
        }
        OnFinish?.Invoke();
        if (turnOffOnFinish)
            gameObject.SetActive(false);
    }

    protected virtual void HandleProgress(int amount) {

    }

    public void Interrupt() {
        StopCoroutine(Action());
    }
}
