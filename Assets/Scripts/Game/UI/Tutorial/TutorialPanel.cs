using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class TutorialPanel : SerializedMonoBehaviour {
    [SerializeField] Dictionary<int, UnityEvent> substeps = new();
    [SerializeField] UnityEvent OnHide;

    public bool Visible { get; private set; }
    public int Substep { get; private set; }

    public void SetSubstep(int substep) {
        Substep = substep;
    }

    public bool Show() {
        if (!Visible) {
            Visible = true;
            substeps.GetValueOrDefault(Substep)?.Invoke();
            return true;
        }
        return false;
    }

    public void Hide() {
        if (Visible) { 
            Visible = false;
            OnHide?.Invoke();
        }
    }

    public void Forward() {
        if (Substep < substeps.Count - 1) {
            Substep++;
            Visible = false;
            Show();
        }
    }

    public void Backwards() {
        if (Substep > 1) {
            Substep--;
            Visible = false;
            Show();
        }
    }
}
