using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

public class EnemyWikiPopup : Popup<AllEnemiesData> {
    [Header("Enemy Wiki Popup")]
    [SerializeField] EnemyList list;
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI descriptionLabel;
    [SerializeField] protected MMF_Player selectSFX;

    public override bool Setup(AllEnemiesData data) {
        list?.Setup();
        return base.Setup(data);
    }

    void OnItemClicked(EnemyItem item) {
        if (item != null && item.Data != null) {
            selectSFX?.PlayFeedbacks();
            EnemyData data = item.Data;
            titleLabel?.SetText(data.Title);
            descriptionLabel?.SetText(data.Description);
        }
    }

    void OnEnable() {
        if (list != null)
            list.OnClick += OnItemClicked;
    }

    void OnDisable() {
        if (list != null)
            list.OnClick -= OnItemClicked;
    }
}
