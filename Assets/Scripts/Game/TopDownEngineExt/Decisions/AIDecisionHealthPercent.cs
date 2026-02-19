using System.Collections;
using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class AIDecisionHealthPercent : AIDecisionHealth {
    public override void Initialization() {
        base.Initialization();
        if (_health != null)
            HealthValue = (int)(HealthValue * _health.MaximumHealth / 100);
    }

}
