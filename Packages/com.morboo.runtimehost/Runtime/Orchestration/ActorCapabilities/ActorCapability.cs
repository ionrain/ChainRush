using UnityEngine;

/// <summary>
/// Typed actor capability identity token. Comparison by reference equality.
/// IMPORTANT: Same pattern as FactionAsset — no string matching, no typos.
/// Adding new capabilities = creating new SO asset, no code changes.
/// </summary>
[CreateAssetMenu(fileName = "ActorCapability", menuName = "Game/Orchestration/Actor Capability")]
public sealed class ActorCapability : ScriptableObject { }
