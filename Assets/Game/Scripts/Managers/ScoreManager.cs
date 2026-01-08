using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public enum ScoreBonusMultiplier { x1 = 0, x2 = 25, x3 = 50, x4 = 75, x5 = 100 }

public struct ScoreData {
    public static Dictionary<float, ScoreBonusMultiplier> ScoreMultipliers = new Dictionary<float, ScoreBonusMultiplier> {
        { 0.0f, ScoreBonusMultiplier.x1 },
        { 0.5f, ScoreBonusMultiplier.x2 },
        { 0.75f, ScoreBonusMultiplier.x3 },
        { 0.9f, ScoreBonusMultiplier.x4 },
        { 0.95f, ScoreBonusMultiplier.x5 }
    };

    public int CellOpenRate { get; private set; }
    public int CellsOpened { get; private set; }
    public int TotalCells { get; private set; }
    public int EnemyKillRate { get; private set; }
    public int EnemiesKilled { get; private set; }
    public int TrapRevealRate { get; private set; }
    public int TrapsRevealed { get; private set; }
    public int TrapTiggerRate { get; private set; }
    public int TrapsTriggered { get; private set; }
    public int TotalTraps { get; private set; }
    public int BoosterFindRate { get; private set; }
    public int BoostersFound { get; private set; }
    public int TotalBoosters { get; private set; }
    public int UnitLoseRate { get; private set; }
    public int UnitsLost { get; private set; }
    public int HintUseRate { get; private set; }
    public int HintsUsed { get; private set; }

    public ScoreData(int cellOpenRate, int cellsOpened, int totalCells, int enemyKillRate, int enemiesKilled, int trapRevealRate,
                     int trapsRevealed, int trapTriggerRate, int trapsTriggered, int totalTraps, int boosterFindRate, int boostersFound,
                     int totalBoosters, int unitsLost, int unitLoseRate, int hintUseRate, int hintsUsed) {
        CellOpenRate = cellOpenRate;
        CellsOpened = cellsOpened;
        TotalCells = totalCells;
        EnemyKillRate = enemyKillRate;
        EnemiesKilled = enemiesKilled;
        TrapRevealRate = trapRevealRate;
        TrapsRevealed = trapsRevealed;
        TrapTiggerRate = trapTriggerRate;
        TrapsTriggered = trapsTriggered;
        TotalTraps = totalTraps;
        BoosterFindRate = boosterFindRate;
        BoostersFound = boostersFound;
        TotalBoosters = totalBoosters;
        UnitsLost = unitsLost;
        UnitLoseRate = unitLoseRate;
        HintUseRate = hintUseRate;
        HintsUsed = hintsUsed;
    }

    public int TotalScore => Mathf.Max(0, BoosterScore + TrapRevealScore + EnemyScore + CellScore + UnitScore + TrapTriggerScore + HintUseScore);
    public int CellScore => CellsOpened * CellOpenRate;
    public int TrapTriggerScore => -TrapTiggerRate * TrapsTriggered;
    public int TrapRevealScore => TrapRevealRate * TrapsRevealed;
    public int UnitScore => -UnitLoseRate * UnitsLost;
    public int HintUseScore => -HintUseRate * HintsUsed;
    public int EnemyScore => EnemyKillRate * EnemiesKilled;
    public int BoosterScore => BoosterFindRate * BoostersFound;
    public int MaxScore => EnemyScore + TotalCells * CellOpenRate + TrapRevealRate * TotalTraps + BoosterFindRate * TotalBoosters;
    public float Progress => TotalScore / (float)MaxScore;

    public float GetRewardMultiplier() {
        float progress = Progress;
        var keys = new List<float>(ScoreMultipliers.Keys);
        for (int i = ScoreMultipliers.Count - 1; i >= 0; i--) {
            float key = keys[i];
            if (progress >= key)
                return 1 + (float)ScoreMultipliers[key] / 100f;
        }
        return 1;
    }
}

public class ScoreManager : MonoBehaviour, MMEventListener<CellEvent>, MMEventListener<TrapEvent>, MMEventListener<PartyBoostEvent>, 
                            MMEventListener<LevelLoadEvent>, MMEventListener<PartyUnitEvent>, MMEventListener<EnemyDeathEvent>, 
                            MMEventListener<BoardEvent> {

    [SerializeField] int cellOpenRate = 1;
    [SerializeField] int enemyKillRate = 1;
    [SerializeField] int trapRevealRate = 1;
    [SerializeField] int trapTriggerRate = 1;
    [SerializeField] int boosterFindRate = 1;
    [SerializeField] int unitLoseRate = 1;
    [SerializeField] int hintUseRate = 1;

    int _totalCells;
    int _openedCells;
    int _triggeredTraps;
    int _revealedTraps;
    int _enemiesKilled;
    int _boostersFound;
    int _unitsLost;
    int _totalTraps;
    int _totalBoosters;
    int _hintsUsed;

    public ScoreData Score => new ScoreData(cellOpenRate, _openedCells, _totalCells, enemyKillRate, _enemiesKilled, trapRevealRate,
                                            _revealedTraps, trapTriggerRate, _triggeredTraps, _totalTraps, boosterFindRate, _boostersFound,
                                            _totalBoosters, unitLoseRate, _unitsLost, hintUseRate, _hintsUsed);

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start && e.Data != null) {
            _totalTraps = e.Data.GetTotalTrapsCount();
            _totalBoosters = e.Data.GetTotalBoostersCount();
            _openedCells = 0;
            _triggeredTraps = 0;
            _revealedTraps = 0;
            _enemiesKilled = 0;
            _boostersFound = 0;
            _unitsLost = 0;
            _hintsUsed = 0;
        }
    }

    public void OnMMEvent(CellEvent e) {
        if (e.Type == CellEventType.Open && e.Cell.Type != CellType.Trap)
            _openedCells++;
        else if (e.Type == CellEventType.Reveal && e.Cell != null && e.Cell.Type == CellType.Trap)
            _revealedTraps++;
    }

    public void OnMMEvent(TrapEvent e) {
        if (e.EventStage == EventStage.Start)
            _triggeredTraps++;
    }

    public void OnMMEvent(PartyBoostEvent e) {
        if (e.EventStage == EventStage.Start && e.Source == PartyBoosterSource.Board)
            _boostersFound++;
    }

    public void OnMMEvent(PartyUnitEvent e) {
        if (e.Type == PartyUnitEventType.Death && e.EventStage == EventStage.End)
            _unitsLost++;
    }

    public void OnMMEvent(EnemyDeathEvent e) {
        if (e.Stage == EventStage.End && e.Enemy != null)
            _enemiesKilled++;
    }

    public void OnMMEvent(BoardEvent e) {
        if (e.Type == BoardEventType.UsedHint)
            _hintsUsed++;
        else if (e.Type == BoardEventType.SetupItems)
            _totalCells = e.Board != null ? e.Board.CellsCount : 0;
    }  

    void OnEnable() {
        this.MMEventStartListening<CellEvent>();
        this.MMEventStartListening<TrapEvent>();
        this.MMEventStartListening<PartyBoostEvent>();
        this.MMEventStartListening<LevelLoadEvent>();
        this.MMEventStartListening<PartyUnitEvent>();
        this.MMEventStartListening<EnemyDeathEvent>();
        this.MMEventStartListening<BoardEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<CellEvent>();
        this.MMEventStopListening<TrapEvent>();
        this.MMEventStopListening<PartyBoostEvent>();
        this.MMEventStopListening<LevelLoadEvent>();
        this.MMEventStopListening<PartyUnitEvent>();
        this.MMEventStopListening<EnemyDeathEvent>();
        this.MMEventStopListening<BoardEvent>();
    }

}
