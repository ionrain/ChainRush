using UnityEngine;

public class CheatModeEventTrigger : Triggerable {
    public override void Trigger() {
        #if CHEATS || UNITY_EDITOR
        base.Trigger();
        #endif
    }
}
