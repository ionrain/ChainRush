using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using MoreMountains.Tools;

public enum LevelIconDirection { Previous = -1, Current = 0, Next = 1 }

public class BattlePanel : SerializedMonoBehaviour, MMEventListener<RewardEvent>, MMEventListener<BalanceResourcesEvent> {
    [SerializeField] AllLocationsData locations;
    [SerializeField] AllUnitsData units;

    [Header("Main UI")]
    [SerializeField] TextMeshProUGUI locationLabel;
    [SerializeField] GameObject lockedLocation;
    [SerializeField] GameObject buttonsRoot;
    [SerializeField] GameObject playButton;
    [SerializeField] Button nextButton;
    [SerializeField] Button previousButton;
    [SerializeField] BattlePopup popup;
    [SerializeField] float autoChangeDelay = 3f;

    [Header("Location")]
    [SerializeField] RectTransform progressRoot;
    [SerializeField] Transform backRoot;
    [SerializeField] Transform iconRoot;
    [SerializeField] Transform markerRoot;
    [SerializeField] Progressbar progressbar;

    [Header("Markers")]
    [SerializeField] Dictionary<RewardType, IconTextItem> rewardItems = new();
    [SerializeField] Dictionary<LevelState, GameObject> markerPrefabs = new();
    [SerializeField] GameObject activeMarkerPrefab;

    bool _enoughEnergy;
    bool _useEnergy = true;
    LocationData _location;
    LocationIcon _currentLocationIcon;
    float _locationDistance;
    float _markerSpacing;

    void Awake() {
        _locationDistance = Screen.width;
        if (markerRoot != null) {
            HorizontalLayoutGroup layout = markerRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
                _markerSpacing = layout.spacing;
        }
        if (_useEnergy)
            BalanceResourcesEvent.Trigger(EventStage.Start, new List<ResourceType>() { ResourceType.Energy });
        Setup();
        CreateLocationIcon();
    }

    public void Setup() {
        if (locations != null && locations.Current != null && _location != locations.Current) {
            locations.UpdateState();
            _location = locations.Current;

            if (locationLabel != null) {
                locationLabel.text = _location.Title;
                locationLabel.gameObject.SetActive(_location.HasLevels);
            }

            if (backRoot != null) {
                backRoot.DestroyChildren();
                if (_location.backPrefab != null) {
                    Instantiate(_location.backPrefab, backRoot);
                    UpdateUI();
                }
            }
        }
    }

    public void StartLevel() {
        if (!_useEnergy || _enoughEnergy) {
            if (popup != null && units != null) {
                popup.UseEnergy(_useEnergy);
                List<UnitData> unlocked = units.Get(UnitListType.Unlocked);
                if (unlocked.Count <= 1)
                    popup.LoadLevel();
                else if (popup.Setup())
                    popup.SetVisibility(true);
            }
        } else {
            BankEvent.Trigger(EventStage.Start, BankEventType.Request, ResourceType.Energy);
        }
    }

    void OnEnable() {
        this.MMEventStartListening<RewardEvent>();
        this.MMEventStartListening<BalanceResourcesEvent>();
        //if (locations != null && locations.Current != null && _location != locations.Current)
        //    Setup();
    }

    public void NextLocation(bool showUnlocking = false) {
        if (locations != null && locations.MoveForward()) {
            Setup();
            LocationIcon icon = CreateLocationIcon(LevelIconDirection.Next);
            if (showUnlocking)
                icon?.Unlock();
        }
    }

    public void PreviousLocation() {
        if (locations != null && locations.MoveBackward()) {
            Setup();
            CreateLocationIcon(LevelIconDirection.Previous);
        }
    }
    
    void MoveIcon(Transform target, LevelIconDirection direction, bool destroy) {
        target.DOMoveX(target.position.x + (int)direction * _locationDistance * -1, 0.5f).onComplete += () => { 
            if (destroy)
                Destroy(target.gameObject);
        };
    }

    void UpdateUI() {
        if (previousButton != null)
            previousButton.gameObject.SetActive(locations.HasPrevious);

        if (nextButton != null)
            nextButton.gameObject.SetActive(locations.HasNext);
        
        if (buttonsRoot != null)
            buttonsRoot.SetActive(_location.State != LocationState.Locked);

        if (_location.State != LocationState.Locked) {
            if (progressRoot != null)
                progressRoot.gameObject.SetActive(true);
            CreateMarkers();
        } else if (progressRoot != null) 
            if (progressRoot != null) 
                progressRoot.gameObject.SetActive(false);
        
        if (lockedLocation != null)
            lockedLocation.SetActive(_location.HasLevels && _location.State == LocationState.Locked);
    }

    void CreateMarkers() {
        if (markerRoot != null && markerPrefabs != null && markerPrefabs.Count > 0 && _location.HasLevels && rewardItems != null) {
            progressRoot?.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _markerSpacing * (_location.LevelsCount - 1));
            
            markerRoot.DestroyChildren();

            List<LevelData> levels = _location.levels;
            LevelData currentLevel = _location.Current;
            levels.ForEach(t => {
                GameObject marker = null;
                if (t != currentLevel || t.State == LevelState.Locked) {
                    GameObject prefab = markerPrefabs.GetValueOrDefault(t.State);
                    if (prefab != null) {
                        marker = Instantiate(prefab, markerRoot);
                        Button button = marker.GetComponent<Button>();
                        if (button != null) {
                            button.onClick.AddListener(() => {
                                _location.SetCurrentIndex(levels.IndexOf(t));
                                CreateMarkers();
                            });
                        }

                        if (t.State == LevelState.Passed) {
                            RewardContainer container = marker.GetComponentInChildren<RewardContainer>();
                            if (container != null && t.IsRewardValid)
                                container.Setup(t.GetReward(), () => { t.SetCompleted(); });
                        }
                    }
                } else
                    marker = Instantiate(activeMarkerPrefab, markerRoot);

                Reward reward = t.GetRewardByType(RewardType.Unit);
                if (t.State == LevelState.Locked && reward is UnitReward unitReward && unitReward.Unit != null
                    && unitReward.Unit.State == UnitState.Locked && rewardItems.TryGetValue(RewardType.Unit, out IconTextItem unitRewardItemPrefab)) {
                    IconTextItem unitRewardItem = Instantiate(unitRewardItemPrefab, marker.transform);
                    unitRewardItem.Setup(unitReward.Unit.Icon, unitReward.Unit.Title);
                }
            });

            if (progressbar != null) {
                progressbar.Setup();
                progressbar.SetTotal(_location.LevelsCount - 1);
                progressbar.SetValue(_location.PassedCount);
            }
        }
    }

    IEnumerator EnableMoveButtons() {
        yield return new WaitForSeconds(0.5f);
        ToggleMoveButtons(true);
    }
    
    void ToggleMoveButtons(bool enable) {
        if (nextButton != null)
            nextButton.interactable = enable;
        if (previousButton != null)
            previousButton.interactable = enable;
    }

    LocationIcon CreateLocationIcon(LevelIconDirection direction = LevelIconDirection.Current) {
        if (_location != null && _location.iconPrefab != null && iconRoot != null) {
            LocationIcon locationIcon = Instantiate(_location.iconPrefab, iconRoot);
            locationIcon.SetLocked(_location.State == LocationState.Locked);
            locationIcon.transform.position += new Vector3(_locationDistance, 0, 0) * (int)direction;
            if (_currentLocationIcon != null) {
                ToggleMoveButtons(false);
                MoveIcon(_currentLocationIcon.transform, direction, true);
                MoveIcon(locationIcon.transform, direction, false);
                StartCoroutine(EnableMoveButtons());
            }
            _currentLocationIcon = locationIcon;
            return locationIcon;
        }
        return null;
    }

    public void OnMMEvent(RewardEvent e) {
        if (_location != null && _location.LastLevel != null && _location.LastLevel.State == LevelState.Passed &&
            e.Stage == EventStage.Start && e.Type == RewardEventType.Transfer && e.Item != null && e.Item.Type == RewardItemType.LevelReward)
            StartCoroutine(AutoChangeLocation());
    }

    IEnumerator AutoChangeLocation() {
        if (locations.HasNext) {
            yield return new WaitForSeconds(autoChangeDelay);
            NextLocation(true);
        }
    }

    public void OnMMEvent(BalanceResourcesEvent e) {
        if (e.Stage == EventStage.End && units != null && e.Balance.ContainsKey(ResourceType.Energy) && _useEnergy)
            _enoughEnergy = e.Balance[ResourceType.Energy].Value >= units.energyPrice;
    }

    public void UseEnergy(bool value) {
        _useEnergy = value;
    }

    void OnDisable() {
        this.MMEventStopListening<RewardEvent>();
        this.MMEventStopListening<BalanceResourcesEvent>();
    }
}