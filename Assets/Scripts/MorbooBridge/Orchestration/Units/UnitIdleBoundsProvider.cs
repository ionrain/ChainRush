using UnityEngine;

/// <summary>
/// Provides idle bounds from a <see cref="Collider2D"/> or fallback size, keyed by <see cref="RoleAsset"/>.
/// Registers itself in <see cref="IdleBoundsRegistry"/> so units can resolve bounds by role at runtime.
/// <para>
/// IMPORTANT — No Update loop. Read-only provider for idle policies/orchestrators.
/// </para>
/// <para>
/// IMPORTANT — One zone per role (MVP). If multiple providers share a role, last-registered wins
/// (deterministic, with warning).
/// </para>
/// </summary>
public sealed class UnitIdleBoundsProvider : MonoBehaviour, IIdleBoundsProvider, IRoleAssetProvider
{
    [Header("Role")]
    [Tooltip("Role this zone provides bounds for. Must match a RoleAsset used by units.")]
    [SerializeField] RoleAsset role;

    [Header("Bounds Source")]
    [Tooltip("Preferred bounds source. Drag a Collider2D or Collider3D from the scene/prefab.")]
    [SerializeField] Collider2D boundsCollider2D;
    [SerializeField] Collider boundsCollider3D;

    [Tooltip("When true, uses boundsCollider.bounds if collider is valid and non-degenerate.")]
    [SerializeField] bool useColliderBounds = true;

    [Tooltip("Fallback size used when collider is missing, disabled, or degenerate.")]
    [SerializeField] Vector3 fallbackSize = new Vector3(6f, 3f, 0);

    [Header("Anchor")]
    [Tooltip("If assigned, anchor is this transform's position. Otherwise anchor = bounds.center.")]
    [SerializeField] Transform anchorOverride;

    // ──────────────────────────────────────────────────────────────────
    //  IRoleAssetProvider
    // ──────────────────────────────────────────────────────────────────

    public RoleAsset GetRoleAsset() => role;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle — registry binding
    // ──────────────────────────────────────────────────────────────────

    void OnEnable() => IdleBoundsRegistry.Register(this);
    void OnDisable() => IdleBoundsRegistry.Unregister(this);

    // ──────────────────────────────────────────────────────────────────
    //  Editor convenience
    // ──────────────────────────────────────────────────────────────────

    void Reset()
    {
        boundsCollider2D = GetComponentInParent<Collider2D>();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (role == null)
            Debug.LogWarning($"[{name}] UnitIdleBoundsProvider2D has no RoleAsset assigned. " +
                             "It will not register in IdleBoundsRegistry.", this);

        if (useColliderBounds && boundsCollider2D == null && boundsCollider3D == null)
            Debug.LogWarning($"[{name}] UnitIdleBoundsProvider2D: useColliderBounds is true but " +
                             "boundsCollider2D and boundsCollider3D are both null. Will use fallbackSize.", this);

        if (boundsCollider2D != null && boundsCollider2D.bounds.size.sqrMagnitude <= 0.01f)
            Debug.LogWarning($"[{name}] UnitIdleBoundsProvider2D: boundsCollider2D has near-zero size. " +
                             "May cause degenerate idle behavior.", this);
        if (boundsCollider3D != null && boundsCollider3D.bounds.size.sqrMagnitude <= 0.01f)
            Debug.LogWarning($"[{name}] UnitIdleBoundsProvider3D: boundsCollider3D has near-zero size. " +
                             "May cause degenerate idle behavior.", this);
    }
#endif

    // ──────────────────────────────────────────────────────────────────
    //  IIdleBoundsProvider
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns idle bounds from collider or fallback. Always returns true.
    /// Falls back to <see cref="fallbackSize"/> centered on this transform when
    /// the collider is null, disabled, or has near-zero size (prevents degenerate
    /// bounds causing "everyone converges" behavior).
    /// </summary>
    public bool TryGetIdleBounds(out Bounds bounds)
    {
        if (useColliderBounds && ((boundsCollider2D != null && boundsCollider2D.enabled) || 
        (boundsCollider3D != null && boundsCollider3D.enabled)))
        {
            Bounds cb = boundsCollider2D != null ? boundsCollider2D.bounds : boundsCollider3D.bounds;
            // Guard against degenerate/zero-size collider bounds
            if (cb.size.sqrMagnitude > 0.01f)
            {
                bounds = cb;
                return true;
            }
        }

        // Fallback: build from transform position + fallbackSize
        bounds = new Bounds(transform.position, new Vector3(fallbackSize.x, fallbackSize.y, fallbackSize.z));
        return true;
    }

    /// <summary>
    /// Returns the idle anchor point. If <see cref="anchorOverride"/> is assigned,
    /// uses its position. Otherwise derives anchor from <see cref="TryGetIdleBounds"/>
    /// to ensure anchor is always consistent with the bounds source.
    /// </summary>
    public bool TryGetIdleAnchor(out Vector3 anchor)
    {
        if (anchorOverride != null)
        {
            anchor = anchorOverride.position;
            return true;
        }

        Bounds b;
        TryGetIdleBounds(out b);
        anchor = b.center;
        return true;
    }
}
