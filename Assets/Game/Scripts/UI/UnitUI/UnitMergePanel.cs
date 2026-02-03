using UnityEngine;

public class UnitMergePanel : MonoBehaviour {
    [SerializeField] Transform itemsRoot;
    [SerializeField] SpineTextItem prefab;

    public void ShowMergeInfo(UnitData data) {
        if (data != null && itemsRoot != null && prefab != null) {
            itemsRoot.DestroyChildren();
            int count = data.MergeStatesCount;
            for (int i = 0; i < count; i++) {
                SpineTextItem item = Instantiate(prefab, itemsRoot);
                MergeStateData mergeData = data.GetMergeData(i);
                if (mergeData != null)
                    item.Setup(mergeData.spineData, mergeData.Description);
            }
        }    
    }
}
