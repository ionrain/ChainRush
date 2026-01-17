using System;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class GateHealthBar : MonoBehaviour {
    [SerializeField] Progressbar progressbar;
    [SerializeField] Health health;

    void Start() {
        if (health != null && progressbar != null) {
            health.OnHit += OnHit;
            health.OnDeath += OnDeath;
            progressbar.Setup();
            progressbar.SetTotal(health.MaximumHealth);
            progressbar.SetValue(health.CurrentHealth);
        }
    }

    void OnHit() {
        progressbar?.SetValue(health.CurrentHealth);
    }

    void OnDeath() {
        LevelActionEvent.Trigger(EventStage.Start, LevelActionType.Fail);
    }
}
