using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

public class UnitMergePanel : MonoBehaviour {
    [SerializeField] GameObject mergeInfoRoot;
    [SerializeField] LocalizedString mergeLoc;
    [SerializeField] List<TextMeshProUGUI> mergeTitles = new();
    [SerializeField] List<TextMeshProUGUI> mergeLabels = new();

    void Start() {
        if (mergeTitles != null && mergeLoc != null && !mergeLoc.IsEmpty) {
            string mergePattern = mergeLoc.GetLocalizedString();
            for (int i = 0; i < mergeTitles.Count; i++) {
                if (mergeTitles[i] != null)
                    mergeTitles[i].text = string.Format(mergePattern, i + 1);
            }
        }
    }

    public void ShowMergeInfo(UnitData data) {
        if (data == null) return;

        if (mergeInfoRoot != null)
            mergeInfoRoot.SetActive(data.Unlocked);
        if (mergeLabels != null) {
            for (int i = 0; i < mergeLabels.Count; i++) {
                if (mergeLabels[i] != null) {
                    MergeStateData mergeData = data.GetMergeData((UnitMergeState)(i + 1));
                    if (mergeData != null)
                        mergeLabels[i].text = mergeData.Description;
                }
            }
        }    
    }
}
