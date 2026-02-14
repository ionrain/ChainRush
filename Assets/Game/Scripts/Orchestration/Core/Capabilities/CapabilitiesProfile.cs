using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CapabilitiesProfile", menuName = "Game/Orchestration/Capabilities Profile")]
public class CapabilitiesProfile : ScriptableObject
{
    public string ProfileId;
    public List<CapabilityEntry> Capabilities;

    /// <summary>
    /// Returns a snapshot with Base.Items referencing this profile's Capabilities list.
    /// The snapshot wraps the list by reference — do not mutate via snapshot.
    /// Add is left empty for runtime composition.
    /// </summary>
    public CapabilitySnapshot ToSnapshot()
    {
        return new CapabilitySnapshot
        {
            Base = new CapabilitySet { Items = Capabilities }
        };
    }
}
