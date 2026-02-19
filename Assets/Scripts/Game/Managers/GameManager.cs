using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;
using UnityEngine.Events;
using Lofelt.NiceVibrations;
using Sirenix.OdinInspector;

public enum EventStage { Start, Process, End }
public enum GameSettingsAction { Save, Load, Reset, TurnOffTutorial }

public abstract class GameSettings : SerializedScriptableObject {
    public virtual void Save(GameData data) {}
    public virtual void Load(GameData data) {}
    public virtual void Reset() {}
}

[System.Serializable]
public class ResourceAmount {
    public ResourceType resource;
    public int amount;

    public ResourceAmount(ResourceType resource, int amount) {
        this.resource = resource;
        this.amount = amount;
    }
}

[System.Serializable]
public class GameData {
    public int currentLocation;
    public long productionStart;
    public List<LocationStateData> locations = new List<LocationStateData>();
    public List<ResourceAmount> resources = new List<ResourceAmount>();
    public List<ItemStateData> items = new List<ItemStateData>();
    public List<UnitStateData> units = new List<UnitStateData>();
    public DailyRewardsStateData dailyRewards = new DailyRewardsStateData();

    public List<ResourceAmount> Resources => resources;
    public void AddResourceAmount(ResourceAmount amount) => resources.Add(amount);
}

public struct GameSettingsEvent {
    public EventStage Stage { get; private set; }
    public GameSettingsAction Action { get; private set; }
    public GameData Data { get; private set; }

    static GameSettingsEvent e;
    public static void Trigger(EventStage stage, GameSettingsAction action, GameData data = null) {
        e.Stage = stage;
        e.Action = action;
        e.Data = data;
        MMEventManager.TriggerEvent(e);
    }
}

public enum VolumeActionType { UpdateMusic, UpdateSound, Save }

public struct VolumeSettingsEvent {
    public VolumeActionType Action { get; private set; }
    public float Value { get; private set; }

    static VolumeSettingsEvent e;
    public static void Trigger(VolumeActionType action, float value = 0) {
        e.Action = action;
        e.Value = value;
        MMEventManager.TriggerEvent(e);
    }
}

public struct VibrationSettingsEvent {
    public bool Active { get; private set; }

    static VibrationSettingsEvent e;
    public static void Trigger(bool active) {
        e.Active = active;
        MMEventManager.TriggerEvent(e);
    }
}

public class GameManager : MMSingleton<GameManager>, MMEventListener<GameSettingsEvent>, MMEventListener<VolumeSettingsEvent>,
                                   MMEventListener<VibrationSettingsEvent> {

    public static float SoundUIVolume => 1f;
    public static float SoundSFXVolume => 1f;
    public static string MusicVolumeParamName => "MusicVolume";
    public static string SoundVolumeParamName => "SoundVolume";
    public static string VibrationParamName => "Vibration";
    const string FIRST_RUN = "FirstRun";

    [SerializeField] bool removeFirstRun;
    [SerializeField] float initialDelay = 1f;

    [Header("Bindings")]
    [SerializeField] List<GameSettings> settingsList = new();
    [SerializeField] MMSoundManager soundManager;
    [SerializeField] HapticReceiver haptics; 

    [Header("Save & Load settings")]
    public MMSaveLoadManagerMethods saveLoadMethod = MMSaveLoadManagerMethods.Json;
    public string fileName = "GameSettings.bin";
    public string folderName = "Settings/";

    [Header("Events")]
    [SerializeField] UnityEvent OnLoad;
    [SerializeField] UnityEvent OnSave;

    protected IMMSaveLoadManagerMethod _saveLoadManagerMethod;
    string _encryptionKey = "AbraShvabraKadabra";

    public void OnMMEvent(VibrationSettingsEvent e) {
        PlayerPrefs.SetInt(VibrationParamName, e.Active ? 1 : 0);
        if (haptics != null)
            haptics.hapticsEnabled = e.Active;
    }

    public void OnMMEvent(VolumeSettingsEvent e) {
        if (soundManager) {
            if (e.Action == VolumeActionType.UpdateMusic) {
                PlayerPrefs.SetFloat(MusicVolumeParamName, e.Value);
                soundManager.SetVolumeMusic(e.Value);
            } else if (e.Action == VolumeActionType.UpdateSound) {
                PlayerPrefs.SetFloat(SoundVolumeParamName, e.Value);
                soundManager.SetVolumeSfx(e.Value * SoundSFXVolume);
                soundManager.SetVolumeUI(e.Value * SoundUIVolume);
            } else {
                soundManager.SaveSettings();
                PlayerPrefs.Save();
            }
        }
    }

    public void OnMMEvent(GameSettingsEvent e) {
        if (e.Action == GameSettingsAction.Reset)
            OnFirstRun();
        else if (e.Action == GameSettingsAction.TurnOffTutorial) {
            PlayerPrefs.SetInt(TutorialManager.TutorialParamName, 0);
            PlayerPrefs.Save();
        } else if (e.Action == GameSettingsAction.Save && e.Stage == EventStage.Start)
            Save();
    }

    void OnFirstRun() {
        PlayerPrefs.SetInt(TutorialManager.TutorialParamName, 1);
        PlayerPrefs.SetInt(VibrationParamName,
        #if UNITY_ANDROID
        0
        #else
        1
        #endif        
        );
        PlayerPrefs.SetInt(TutorialManager.TutorialParamName, 1);
        PlayerPrefs.SetFloat(MusicVolumeParamName, 1);
        PlayerPrefs.SetFloat(SoundVolumeParamName, 1);
        PlayerPrefs.SetInt(FIRST_RUN, 1);
        PlayerPrefs.Save();

        if (soundManager != null) {
            soundManager.SetVolumeUI(SoundUIVolume);
            soundManager.SetVolumeSfx(SoundSFXVolume);
            soundManager.SaveSettings();
        }

        MMSaveLoadManager.DeleteSaveFolder(folderName);
        
        settingsList.ForEach(t => t?.Reset());

        Save();
    }

    protected override void Awake() {
        base.Awake();

        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        CheatsPopup.ClearPlayerPrefs();
        GameSpeedModifier.ClearPlayerPrefs();

#if (UNITY_EDITOR)
        if (removeFirstRun && PlayerPrefs.HasKey(FIRST_RUN))
            PlayerPrefs.DeleteKey(FIRST_RUN);
#endif
        if (!PlayerPrefs.HasKey(FIRST_RUN))
            OnFirstRun(); 
        Load();

        if (soundManager != null) {
            float musicVolume = PlayerPrefs.GetFloat(MusicVolumeParamName, 1);
            soundManager.SetVolumeMusic(musicVolume);
            float soundVolume = PlayerPrefs.GetFloat(SoundVolumeParamName, 1);
            soundManager.SetVolumeSfx(soundVolume * SoundSFXVolume);
            soundManager.SetVolumeUI(soundVolume * SoundUIVolume);            
        }
        
        if (haptics != null)
            haptics.hapticsEnabled = PlayerPrefs.GetInt(VibrationParamName, 0) == 1;
        
        DontDestroyOnLoad(this);
        StartCoroutine(LoadCo());

    }

    IEnumerator LoadCo() {
        yield return new WaitForSeconds(initialDelay);
        OnLoad?.Invoke();
    }

    void OnApplicationQuit() {
        Save();
    }

    void OnApplicationPause(bool paused) {
        if (paused)
            Save();
    }

    void Load() {
        try {
            InitializeSaveLoadMethod();
            GameData data = (GameData)MMSaveLoadManager.Load(typeof(GameData), fileName, folderName);
            GameSettingsEvent.Trigger(EventStage.Process, GameSettingsAction.Load, data);

            if (data != null)
                settingsList.ForEach(t => t?.Load(data));

            GameSettingsEvent.Trigger(EventStage.End, GameSettingsAction.Load, data);
        } catch {
            OnFirstRun();
        }
    }

    void Save() {
        GameData data = new GameData();
        GameSettingsEvent.Trigger(EventStage.Process, GameSettingsAction.Save, data);

        settingsList.ForEach(t => t?.Save(data));

        GameSettingsEvent.Trigger(EventStage.End, GameSettingsAction.Save, data);
        OnSave?.Invoke();

        InitializeSaveLoadMethod();
        MMSaveLoadManager.Save(data, fileName, folderName);
        Debug.Log("GameSettings Save: Completed at path: " + Application.dataPath);
    }

    protected void InitializeSaveLoadMethod() {
        switch (saveLoadMethod) {
            case MMSaveLoadManagerMethods.Binary:
                _saveLoadManagerMethod = new MMSaveLoadManagerMethodBinary();
                break;
            case MMSaveLoadManagerMethods.BinaryEncrypted:
                _saveLoadManagerMethod = new MMSaveLoadManagerMethodBinaryEncrypted();
                (_saveLoadManagerMethod as MMSaveLoadManagerEncrypter).Key = _encryptionKey;
                break;
            case MMSaveLoadManagerMethods.Json:
                _saveLoadManagerMethod = new MMSaveLoadManagerMethodJson();
                break;
            case MMSaveLoadManagerMethods.JsonEncrypted:
                _saveLoadManagerMethod = new MMSaveLoadManagerMethodJsonEncrypted();
                (_saveLoadManagerMethod as MMSaveLoadManagerEncrypter).Key = _encryptionKey;
                break;
        }
        MMSaveLoadManager.SaveLoadMethod = _saveLoadManagerMethod;
    }

    void OnEnable() {
        this.MMEventStartListening<GameSettingsEvent>();
        this.MMEventStartListening<VolumeSettingsEvent>();
        this.MMEventStartListening<VibrationSettingsEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<GameSettingsEvent>();
        this.MMEventStopListening<VolumeSettingsEvent>();
        this.MMEventStopListening<VibrationSettingsEvent>();
    }
}
