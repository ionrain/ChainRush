using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New AllLocationsData", menuName = "Game/AllLocationsData", order = 16)]
public class AllLocationsData : GameSettings {
    public List<LocationData> locations = new List<LocationData>();
    public int currentIndex;

    public bool HasLocations => locations != null && locations.Count > 0;
    public bool HasPrevious => HasLocations && currentIndex > 0;
    public bool HasNext => HasLocations && currentIndex < locations.Count - 1;
    public int CompletedLevelsCount => locations.FindAll(t => t != null && t.CompletedLevelsCount > 0).Count;
    public int CompletedCount => HasLocations ? locations.FindAll(t => t != null && t.State == LocationState.Completed).Count : 0;
    public LocationData Current => currentIndex >= 0 && currentIndex < locations.Count ? locations[currentIndex] : null;


    public override void Reset() {
        currentIndex = 0;
        locations.ForEach(t => {
            if (t != null)
                t.Reset();
        });
        if (locations.Count > 0)
            locations[0].SetState(LocationState.Unlocked);
    }

    public void SetupLevelIndecies() {
        int totalLevels = 0;
        for (int i = 0; i < locations.Count; i++) {
            List<LevelData> levels = locations[i].levels;
            for (int j = 0; j < levels.Count; j++) {
                levels[j].Index = totalLevels;
                totalLevels++;            
            }
        }
    }
    
    public void UpdateState() {
        if (HasLocations)
            locations.ForEach(t => t.UpdateLevelStates());
    }

    public override void Load(GameData data) {
        SetupLevelIndecies();
        foreach (LocationStateData stateData in data.locations) {
            LocationData location = locations.Find(t => t != null && t.name.Equals(stateData.location));
            if (location != null)
                location.SetStateData(stateData);
        }
        currentIndex = locations[data.currentLocation] != null && locations[data.currentLocation].State != LocationState.Locked ? 
                       data.currentLocation : locations.FindLastIndex(t => t != null && t.State != LocationState.Locked);
    }

    public override void Save(GameData data) {
        data.currentLocation = currentIndex;
        locations.ForEach(t => { 
            if (t != null)
                data.locations.Add(t.GetStateData());
        });
    }

    public bool MoveForward() {
        if (currentIndex < locations.Count - 1) {
            currentIndex++;
            return true;
        }
        return false;
    }

    public bool MoveBackward() {
        if (currentIndex > 0) {
            currentIndex--;
            return true;
        }
        return false;
    }
}
