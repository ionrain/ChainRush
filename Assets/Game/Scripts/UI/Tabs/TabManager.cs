using System.Collections.Generic;
using UnityEngine;

public class TabManager : MonoBehaviour {
    List<TabSelectButton> _tabs = new List<TabSelectButton>();

    void Awake() {
        _tabs.AddRange(gameObject.GetComponentsInChildren<TabSelectButton>());
    }

    void OnEnable() {
        _tabs.ForEach(t => t.OnTabSelectButtonPress += OnTabButtonPressed);
    }

    void OnTabButtonPressed(TabSelectButton button) {
        foreach (var tab in _tabs)
            if (tab != null) {
                bool selected = tab == button;
                if (!selected)
                    tab.Deselect();
            }
    }

    public void Refresh(int index) {
        if (index >= 0 && index < _tabs.Count) {
            if (_tabs[index].Active)
                _tabs[index].InvokeSelectEvent();
        }
    }

    void OnDisable() {
        _tabs.ForEach(t => t.OnTabSelectButtonPress -= OnTabButtonPressed);
    }
}
