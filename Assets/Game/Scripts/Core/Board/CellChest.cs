using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

public class CellChest : CellItem {
    [SerializeField] Dictionary<PartyBoosterType, MMF_Player> boosterPrefabs = new();

    PartyBoosterType _booster;

    public override void Setup(Vector2Int position, object param) {
        base.Setup(position, param);
        _booster = (PartyBoosterType)param;
        name = string.Format("CellChest_{0}_{1}x{2}", _booster, position.x, position.y);
    }

    public override void SetVisible(bool value) {
        base.SetVisible(value);
        if (value) {
            MMF_Player prefab = boosterPrefabs.GetValueOrDefault(_booster);
            if (prefab != null) {
                Instantiate(prefab, transform.position, Quaternion.identity);
                PartyBoostEvent.Trigger(EventStage.Start, _booster, PartyBoosterSource.Board, transform.position);
            }
        }
    }
}
