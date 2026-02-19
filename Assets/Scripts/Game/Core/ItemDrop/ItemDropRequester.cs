using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class ItemDropRequester : SerializedMonoBehaviour {
    [Header("Drop Request Params")]
    [SerializeField] protected int channelId = 0;
    [SerializeField] protected int id = 0;
    [SerializeField] protected bool once;

    public int Id => id;
    public int ChannelId => channelId;
    public bool RequestCompleted => _requestCompleted;

    protected bool _requestCompleted;

    public virtual void RequestDrop() {
        if (!once || !_requestCompleted) {
            _requestCompleted = true;
            ItemDropRequestEvent.Trigger(channelId, id, transform.position);
        }
    }
}
