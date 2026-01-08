using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

public enum TrapType { Alarm, Debuff, DebuffAll, Damage, DamageAll, Freeze }

public struct TrapEvent {
    public EventStage EventStage { get; private set; }
    public TrapType Type { get; private set; }
    public CellTrap Trap { get; private set;}
    public Vector2 Position => Trap != null ? Trap.transform.position : Vector2.zero;

    static TrapEvent e;
    public static void Trigger(EventStage eventStage, TrapType type, CellTrap trap) {
        e.EventStage = eventStage;
        e.Type = type;
        e.Trap = trap;
        MMEventManager.TriggerEvent(e);
    }
}

[Serializable]
public class CellTrapData {
    [SerializeField] Sprite sprite;
    [SerializeField] MMF_Player feedback;

    public Sprite Sprite => sprite;
    public MMF_Player Feedback => feedback;
}

public class CellTrap : CellItem {
    [SerializeField] MMF_Player blockFeedback;
    [SerializeField] Dictionary<TrapType, CellTrapData> options = new();
    
    public TrapType TrapType { get; private set; }

    public override void Setup(Vector2Int position, object param) {
        base.Setup(position, param);
        TrapType = (TrapType)param;
        name = string.Format("CellTrap_{0}_{1}x{2}", TrapType, position.x, position.y);
        if (options != null) {
            CellTrapData data = options.GetValueOrDefault(TrapType, null);
            if (data != null) {
                if (image != null)
                    image.sprite = data.Sprite;
                activateFeedback = data.Feedback;
            }
        }
    }

    public override void SetVisible(bool value) {
        base.SetVisible(value);
        if (value)
            TrapEvent.Trigger(EventStage.Start, TrapType, this);
    }

    public void Block() {
        blockFeedback?.PlayFeedbacks();
    }
}
