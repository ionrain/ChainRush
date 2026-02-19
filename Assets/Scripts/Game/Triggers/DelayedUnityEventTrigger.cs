using System.Collections;
using UnityEngine;

public class DelayedUnityEventTrigger : Triggerable {
    [Header("Delayed Unity Event Trigger")]
    [SerializeField] float delay;
    [SerializeField] bool realtime;

    protected override void OnInvoke() {
        StartCoroutine(OnInvokeCo());
    }

    IEnumerator OnInvokeCo() {
        yield return realtime ? new WaitForSecondsRealtime(delay) : new WaitForSeconds(delay);
        base.OnInvoke();
    }
}
