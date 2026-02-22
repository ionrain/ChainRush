using UnityEngine;

/// <summary>
/// Grid formation: places units in a centered grid pattern.
/// If <see cref="columns"/> is 0, auto-derives a roughly square layout.
/// </summary>
[CreateAssetMenu(fileName = "GridFormationPattern", menuName = "Game/Orchestration/Formations/Grid")]
public sealed class GridFormationPattern2DAsset : FormationPattern2DAsset
{
    [Header("Grid")]
    [Tooltip("Spacing between grid cells.")]
    [SerializeField] float spacing = 1.2f;

    [Tooltip("Number of columns. 0 = auto-derive square root.")]
    [SerializeField] int columns;

    protected override Vector2 ComputeSlotOffset(int slotIndex, int slotCount, int seed)
    {
        int cols = columns > 0 ? columns : Mathf.CeilToInt(Mathf.Sqrt(slotCount));
        if (cols < 1) cols = 1;

        int rows = (slotCount + cols - 1) / cols;

        int col = slotIndex % cols;
        int row = slotIndex / cols;

        // Center the grid on (0, 0)
        float offsetX = (col - (cols - 1) * 0.5f) * spacing;
        float offsetY = (row - (rows - 1) * 0.5f) * spacing;

        return new Vector2(offsetX, offsetY);
    }
}
