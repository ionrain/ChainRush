using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TinySaucePreloader : MonoBehaviour {
    [SerializeField] UnityEvent OnFinish;

    void Start() {
        //TinySauce.SubscribeOnInitFinishedEvent(OnInitFinished);
    }

    void OnInitFinished(bool adConsent, bool trackingConsent) {
       OnFinish?.Invoke();
    }
}
