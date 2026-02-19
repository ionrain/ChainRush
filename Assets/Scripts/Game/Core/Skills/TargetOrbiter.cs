using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetOrbiter : MonoBehaviour {
    [SerializeField] Transform target;
    [SerializeField] bool follow = true;
    [SerializeField] Vector3 offset;
    [SerializeField] float speed;

    bool _ok;
    Vector3 _lastPosition = Vector3.zero;
    Vector3 _position = Vector3.zero;
    float _moveSpeed;

    void Awake() {
        CheckOK();
    }

    void CheckOK() {
        _ok = target != null && speed > 0;
    }

    public void Setup(Transform orbitTarget, float orbitSpeed, float moveSpeed) {
        target = orbitTarget;
        speed = orbitSpeed;
        _moveSpeed = moveSpeed / 10;
        _position = target.position + offset;
        if (orbitTarget != null)
            _lastPosition = target.position;
        CheckOK();
    }

    void LateUpdate() {
        if (_ok) {
            if (follow) {
                _position = target.position + offset;
                Vector3 delta = _position - _lastPosition ;
                _lastPosition = _position;
                transform.position += delta;
            }

            if (_moveSpeed != 0) {
                Vector3 direction = (transform.position - _position).normalized;
                transform.position += direction * _moveSpeed * Time.deltaTime;
            }
            
            transform.RotateAround(_position, Vector3.forward, speed * Time.deltaTime);
        }
    }
}
