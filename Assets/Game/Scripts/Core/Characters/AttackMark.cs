using System.Collections;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

public class AttackMark : MonoBehaviour {
    [Header("Feedbacks")]
    [SerializeField] MMF_Player activateFeedback;
    [SerializeField] MMF_Player deactivateFeedback;

    [Header("Aiming")]
    [SerializeField] bool customDuration;
    [MMCondition("customDuration", true)]
    [SerializeField] float duration;
    [SerializeField] float aimDuration = 1f;
    [SerializeField] float aimInterval = 0.2f;

    public bool Active => _active;
    public Vector2 LastAimPosition { get; private set; }
    public Quaternion Rotation => transform.rotation;
    public float Duration => duration;
    public bool CustomDuration => customDuration;

    bool _active;
    float _elapsedAimDuration;

    public void SetDuration(float value) {
        duration = value;
    }

    public void Activate(Transform target) {
        if (Duration > 0 && target != null) {
            LastAimPosition = Vector2.zero;
            _elapsedAimDuration = 0;
            activateFeedback?.PlayFeedbacks();
            if (aimDuration > 0 && aimInterval > 0)
                StartCoroutine(Aim(target));
            _active = true;
            StartCoroutine(DeactivateCo());
        } else
            Debug.LogErrorFormat("AttackMark Activate: Target is NULL or Diration = 0 for {0}", gameObject.name);
    }

    IEnumerator Aim(Transform target) {
        while (_elapsedAimDuration < aimDuration) {
            LastAimPosition = target.position;
            transform.rotation = transform.position.RotateToPosition(LastAimPosition);
            _elapsedAimDuration += aimInterval;
            yield return new WaitForSeconds(aimInterval);
        }
    }

    IEnumerator DeactivateCo() {
        yield return new WaitForSeconds(Duration);
        Deactivate();
    }

    public void Deactivate() {
        deactivateFeedback?.PlayFeedbacks();
        _active = false;
    }
}
