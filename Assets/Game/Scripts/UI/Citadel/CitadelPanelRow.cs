using System.Collections.Generic;
using UnityEngine;

public class CitadelPanelRow : MonoBehaviour {
    [SerializeField] List<CitadelPanelItem> items = new();

    public List<CitadelPanelItem> Items => items;
}
