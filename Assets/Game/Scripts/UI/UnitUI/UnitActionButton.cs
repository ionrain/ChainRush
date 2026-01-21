using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UnitActionButton : MonoBehaviour {
    [SerializeField] UnitDataEvent OnClicked;

    UnitData _data;

    void Awake() {
        Button button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnClick);
    }

    public void Setup(UnitData data) {
        _data = data;
    }

    public void OnClick() {
        OnClicked?.Invoke(_data);
    }
}
