using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public class BackgroundScroller : MonoBehaviour, MMEventListener<LevelLoadEvent> {
    [SerializeField] AllLocationsData locations;

    Camera _camera;
    List<Transform> _segments = new();
    float _segmentWidth;

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start)
            Setup();
    }

    void Setup() {
        Clear();

        _camera = Camera.main;
        if (_camera == null) return;

        LocationData location = locations != null ? locations.Current : null;
        if (location == null || location.levelBack == null) return;

        GameObject first = Instantiate(location.levelBack, transform);
        Renderer rend = first.GetComponentInChildren<Renderer>();
        if (rend == null) { Destroy(first); return; }

        _segmentWidth = rend.bounds.size.x;
        if (_segmentWidth <= 0) { Destroy(first); return; }

        float viewportWidth = GetViewportWidth();
        int count = Mathf.CeilToInt(viewportWidth / _segmentWidth) + 2;

        float camX = _camera.transform.position.x;
        float startX = camX - (count * 0.5f - 0.5f) * _segmentWidth;

        first.transform.position = new Vector3(startX, transform.position.y, transform.position.z);
        _segments.Add(first.transform);

        for (int i = 1; i < count; i++) {
            GameObject seg = Instantiate(location.levelBack, transform);
            seg.transform.position = new Vector3(startX + i * _segmentWidth, transform.position.y, transform.position.z);
            _segments.Add(seg.transform);
        }
    }

    void LateUpdate() {
        if (_segments.Count < 2 || _camera == null) return;

        float camX = _camera.transform.position.x;
        float totalWidth = _segmentWidth * _segments.Count;
        float halfTotal = totalWidth * 0.5f;

        for (int i = 0; i < _segments.Count; i++) {
            Vector3 pos = _segments[i].position;
            float diff = pos.x - camX;
            diff = Mathf.Repeat(diff + halfTotal, totalWidth) - halfTotal;
            pos.x = camX + diff;
            _segments[i].position = pos;
        }
    }

    float GetViewportWidth() {
        if (_camera.orthographic)
            return 2f * _camera.orthographicSize * _camera.aspect;

        float distance = Mathf.Abs(_camera.transform.position.z);
        float halfHeight = distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return 2f * halfHeight * _camera.aspect;
    }

    void Clear() {
        foreach (Transform seg in _segments)
            if (seg != null) Destroy(seg.gameObject);
        _segments.Clear();
    }

    void OnEnable() => this.MMEventStartListening<LevelLoadEvent>();
    void OnDisable() => this.MMEventStopListening<LevelLoadEvent>();
}
