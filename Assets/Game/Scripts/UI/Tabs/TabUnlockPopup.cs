using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TabUnlockPopup : Popup<Sprite> {
    [Header("Tutorial Unlock Popup")]
    [SerializeField] Image icon;
    [SerializeField] Transform flyTarget;
    [SerializeField] Vector3 flyOffset;

    public override bool Setup(Sprite value) {
        if (base.Setup(value) && icon != null) {
            icon.sprite = data;
            return true;
        }
        return false;
    }

    public void SetFlyToPoisition(Vector3 position) {
        if (flyTarget != null)
            flyTarget.position = position + flyOffset;
    }
}
