using UnityEngine;

public class LevelTabUnlockCondition : TabUnlockCondition {
    [SerializeField] LevelData level;
    protected override string PlayerPrefsId => string.Format("LevelTabUnlockCondition{0}", level != null ? level.name : gameObject.name);

    public override void Check() {
        if (level != null && !WasShown)
            InvokeOnCheck(level.State >= LevelState.Passed);
    }
}
