using UnityEngine;

/// <summary>
/// Read-only provider of idle allowed area; used by Idle policies/orchestrators.
/// <para>
/// IMPORTANT — Implementations must not allocate per call. Bounds/anchor
/// should be derived from existing colliders, transforms, or serialized data.
/// </para>
/// </summary>
public interface IIdleBoundsProvider
{
    /// <summary>
    /// Returns the idle bounds for this provider.
    /// </summary>
    /// <param name="bounds">The axis-aligned bounding box defining the allowed idle area.</param>
    /// <returns>True if bounds are valid and usable.</returns>
    bool TryGetIdleBounds(out Bounds bounds);

    /// <summary>
    /// Returns the idle anchor point (preferred center of idle area).
    /// If not available, caller should use <c>bounds.center</c> from
    /// <see cref="TryGetIdleBounds"/>.
    /// <para>
    /// Anchor is always inside or at center of bounds for debuggability.
    /// </para>
    /// </summary>
    /// <param name="anchor">The 3D anchor point.</param>
    /// <returns>True if anchor is valid.</returns>
    bool TryGetIdleAnchor(out Vector3 anchor);
}
