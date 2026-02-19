using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SafeAreaLayoutOffset : MonoBehaviour {
    [SerializeField] LayoutGroup layout;
    [SerializeField] int offset;
    [SerializeField] UnityEvent<int> OnRefreshed;

    public void OnRefresh(RectOffset rect) {
        if (layout != null && rect != null && rect.top > 0)
            layout.padding.top = offset + rect.top;
        OnRefreshed?.Invoke(layout.padding.top);
    }
}
