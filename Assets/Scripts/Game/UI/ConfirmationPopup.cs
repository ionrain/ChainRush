using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ConfirmationPopup : Popup<UnityAction> {
    [SerializeField] Button yesButton;

    public override bool Setup(UnityAction data) {
        if (data != null && yesButton != null) {
            yesButton.onClick.RemoveAllListeners();
            yesButton.onClick.AddListener(data);
            return base.Setup(data);
        }
        return false;
    }
}
