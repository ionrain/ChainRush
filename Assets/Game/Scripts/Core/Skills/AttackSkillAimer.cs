using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;

public struct TargetListRequestEvent {
    public EventStage Stage {get; private set;}
    public string RequesterId { get; private set; }
    public TargetMode TargetMode { get; private set; }
    public Collider2D TargetArea { get; private set; }
    public List<Vector2> Positions{ get; private set; }

    static TargetListRequestEvent e;
    public static void Trigger(EventStage stage, string requesterId, TargetMode targetMode, Collider2D targetArea = null, List<Vector2> positions = null) {
        e.Stage = stage;
        e.RequesterId = requesterId;
        e.TargetMode = targetMode;
        e.TargetArea = targetArea;
        e.Positions = positions;
        MMEventManager.TriggerEvent(e);
    }
}

public class AttackSkillAimer : MonoBehaviour, MMEventListener<TargetListRequestEvent>  {
    [SerializeField] LayerMask enemyMask;

    BoxCollider2D _searchBox;
    ContactFilter2D _contactFilter;
 
    void Start() {
        Setup();
    }

    void Setup() {
        _searchBox = GetComponent<BoxCollider2D>();
        if (_searchBox != null)
            _searchBox.size = Camera.main.GetScreenSize();

        _contactFilter = new ContactFilter2D() { layerMask = enemyMask };
    }

    public void OnMMEvent(TargetListRequestEvent e) {
        if (e.Stage == EventStage.Start && _searchBox != null) {            
            List<Collider2D> found = new List<Collider2D>();
            List<Vector2> result = new List<Vector2>();

            Physics2D.OverlapBox(_searchBox.transform.position, _searchBox.size, 0, _contactFilter, found);

            if (found.Count > 0) {
                if (e.TargetMode == TargetMode.Random)
                    result.Add(found[Random.Range(0, found.Count)].transform.position);
            }
            
            TargetListRequestEvent.Trigger(EventStage.End, e.RequesterId, e.TargetMode, e.TargetArea, result);
        }
    }

    void OnEnable() {
        this.MMEventStartListening<TargetListRequestEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<TargetListRequestEvent>();
    }
}
