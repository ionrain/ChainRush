using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

public class UnitCard : MonoBehaviour {
    [SerializeField] UnitData data;
    [SerializeField] UnitMergeState state;
    [SerializeField] SkeletonGraphic skeletonGraphic;
    [SerializeField] IconTextItem skillPrefab;
    [SerializeField] Transform skillsRoot;
    [SerializeField] bool autoSetupOnEnable = true;

    void OnEnable() {
        if (autoSetupOnEnable)
            Setup();
    }
    
    public void Setup(UnitData unitData) {
        data = unitData;
        Setup();
    }

    public void Setup() {
        if (data != null && skeletonGraphic != null) {
            var mergeData = data.GetMergeData(state);
            if (mergeData != null) {
                skeletonGraphic.skeletonDataAsset = mergeData.spineData;
                skeletonGraphic.Clear();
                skeletonGraphic.AnimationState.ClearTracks();
                skeletonGraphic.AnimationState.SetAnimation(0, data.animations.GetValueOrDefault(AnimationState.Idle, null)?.name ?? "Idle", true);

                if (skillPrefab != null && skillsRoot != null) {
                    skillsRoot.DestroyChildren();
                    foreach (var skill in data.skills) {
                        if (skill.data != null && skill.data.skillType != SkillType.Ultimate) {
                            var skillItem = Instantiate(skillPrefab, skillsRoot);
                            skillItem.Setup(skill.data.Icon, skill.data.Title);
                        }
                    }
                }
            }
        }
    }

}
