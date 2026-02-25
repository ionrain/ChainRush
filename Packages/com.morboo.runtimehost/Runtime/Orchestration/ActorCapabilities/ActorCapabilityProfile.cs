using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authored data source for a named set of actor capabilities.
/// IMPORTANT: Base list is shared by reference in the snapshot — do not mutate via snapshot.
/// Replaces the old string-based CapabilitiesProfile.
/// </summary>
[CreateAssetMenu(fileName = "ActorCapabilityProfile", menuName = "Game/Orchestration/Actor Capability Profile")]
public sealed class ActorCapabilityProfile : ScriptableObject
{
    [SerializeField] List<ActorCapability> capabilities;

    public ActorCapabilitySnapshot ToSnapshot()
    {
        return new ActorCapabilitySnapshot { Base = capabilities };
    }
}
