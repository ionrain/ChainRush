using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class AIDecisionCharacterHit : AIDecision  {
    [SerializeField] Health health;

    bool _changed;

    void OnEnable() {
        if (health != null)
            health.OnHit += OnHit;
    }

    void OnDisable() {
        if (health != null)
            health.OnHit -= OnHit;
    }

    void OnHit() {
        _changed = true;
    }

    public override bool Decide() {
        bool changed = _changed;
        _changed = false;
        return changed;
    }
}
