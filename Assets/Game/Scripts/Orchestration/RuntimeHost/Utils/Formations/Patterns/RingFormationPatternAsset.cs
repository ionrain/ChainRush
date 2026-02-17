using UnityEngine;

/// <summary>
/// Ring formation: places units evenly around a circle with optional jitter.
/// Fully deterministic via seed.
/// </summary>
[CreateAssetMenu(fileName = "RingFormationPattern", menuName = "Game/Orchestration/Formations/Ring")]
public sealed class RingFormationPatternAsset : FormationPatternAsset
{
    [Header("Ring")]
    [Tooltip("Radius of the ring around the anchor.")]
    [SerializeField] float radius = 2f;

    [Tooltip("Maximum angular jitter in degrees. Converted to radians internally.")]
    [SerializeField] float jitterDegrees = 12f;

    protected override Vector2 ComputeSlotOffset(int slotIndex, int slotCount, int seed)
    {
        float baseAngle = ((float)slotIndex / slotCount) * Mathf.PI * 2f;

        // Deterministic jitter: convert degrees to radians, scale by hash
        float jitterRad = jitterDegrees * Mathf.Deg2Rad;
        float jitter = (Hash01(seed) * 2f - 1f) * jitterRad;

        float angle = baseAngle + jitter;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }
}
