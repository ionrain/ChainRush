using System.Collections.Generic;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;

public struct ConsoleEvent {
    public string Text { get; private set; }

    static ConsoleEvent e;
    public static void Trigger(string text) {
        #if UNITY_EDITOR || CHEATS
        string old = PlayerPrefs.GetString("ConsoleText");
        e.Text = old + string.Format("<br>{0}", text);
        PlayerPrefs.SetString("ConsoleText", e.Text);
        Debug.Log(text);
        MMEventManager.TriggerEvent(e);
        #endif
    }
}

public class CheatsPopup : Popup<int> {
    [SerializeField] TextMeshProUGUI consoleLabel;

    [Header("Units")]
    [SerializeField] AllUnitsData units;
    [SerializeField] TMP_Dropdown unitUnlockList;
    [SerializeField] TMP_Dropdown unitList;
    [SerializeField] TMP_InputField unitCardInput;

    [Header("Levels")]
    [SerializeField] AllLocationsData locations;
    [SerializeField] TMP_Dropdown levelUnlockList;
    [SerializeField] TMP_Dropdown levelCompleteList;

    [Header("Resources")]
    [SerializeField] ResourcesData resourcesData;
    [SerializeField] List<ResourceType> resources;
    [SerializeField] TMP_Dropdown resourceList;
    [SerializeField] TMP_InputField resourceInput;
    [SerializeField] TMP_InputField productionInput;

    public static void ClearPlayerPrefs() {
        PlayerPrefs.DeleteKey("ConsoleText");
    }

    public override void SetVisibility(bool value) {
        if (value) {
            if (resourceList != null && resources != null) {
                resourceList.options.Clear();
                resources.ForEach(t => resourceList.options.Add(new TMP_Dropdown.OptionData(t.ToString())));
            }
            if (resourceInput != null)
                resourceInput.text = 0.ToString();

            if (unitCardInput != null)
                unitCardInput.text = 0.ToString();
            
            if (productionInput != null && resourcesData != null)
                productionInput.text = ((int)resourcesData.ProductionTimeSpan.TotalMinutes).ToString();

            FillUnitList(unitList);
            FillUnitList(unitUnlockList);

            FillLevelList(levelUnlockList);
            FillLevelList(levelCompleteList);

            if (consoleLabel != null) {
                consoleLabel.text = PlayerPrefs.GetString("ConsoleText");
                consoleLabel.ForceMeshUpdate();
            }
        }
        
        base.SetVisibility(value);
    }

    void FillLevelList(TMP_Dropdown levelList) {
        #if UNITY_EDITOR || CHEATS
        if (levelList != null && locations != null && locations.Current != null) {
            levelList.options.Clear();
            if (locations.Current.HasLevels)
                locations.Current.levels.ForEach(t => levelList.options.Add(new TMP_Dropdown.OptionData(t.name)));
        }
        #endif
    }

    void FillUnitList(TMP_Dropdown unitList) {
        #if UNITY_EDITOR || CHEATS
        if (unitList != null && units != null) {
            unitList.options.Clear();
            units.units.ForEach(t => unitList.options.Add(new TMP_Dropdown.OptionData(t.Title)));
        }
        #endif
    }

    public void UnlockUnit() {
        #if UNITY_EDITOR || CHEATS
        if (unitUnlockList != null && units != null)
            units.units[unitUnlockList.value].SetState(UnitState.ReadyToBeUnlocked);
        #endif
    }

    public void UnlockLevel() {
        #if UNITY_EDITOR || CHEATS
        if (levelUnlockList != null && locations != null && locations.Current != null && locations.Current.HasLevels)
            locations.Current.levels[levelUnlockList.value].TryUnlock(force: true);
        #endif
    }

    public void CompleteLevel() {
        #if UNITY_EDITOR || CHEATS
        if (levelCompleteList != null && locations != null && locations.Current != null) {
            if (locations.Current.HasLevels) {
                locations.Current.levels[levelCompleteList.value].SetPassed();
                locations.Current.MoveForward();
            }
        }
        #endif
    }

    public void SetProductionTime() {
        #if UNITY_EDITOR || CHEATS
        if (productionInput != null && resourcesData != null && int.TryParse(productionInput.text, out int value))
            resourcesData.SetProductionStart(value);
        #endif
    }

    public void AddResource() {
        #if UNITY_EDITOR || CHEATS
        if (resourceList != null && resourceInput != null && resources.Count > 0 && resourceInput.text.Length > 0)
            EarnResourceEvent.Trigger(EventStage.Start, resources[resourceList.value], ResourceSource.Cheats, string.Empty, int.Parse(resourceInput.text));
        #endif
    }

    public void AddCards() {
        #if UNITY_EDITOR || CHEATS
        if (unitList != null && unitCardInput != null && unitCardInput.text.Length > 0) {
            var unit = units.units[unitList.value];
            unit.AddCards(int.Parse(unitCardInput.text));
            UnitEvent.Trigger(EventStage.End, UnitEventType.CardBalanceChange, unit);
        }
        #endif
    }
}
