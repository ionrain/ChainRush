using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level pipeline composition owner (C04B).
/// Holds the arbiter, Faction-first identity, and ordered domain orchestrators for one pipeline.
/// </summary>
public sealed class OrchestrationPipelineComponent : MonoBehaviour
{
    [Header("Pipeline")]
    [Tooltip("Arbiter instance for this pipeline.")]
    [SerializeField] OrchestrationArbiter arbiter;

    [Header("Faction-First Scope Identity")]
    [Tooltip("Faction identity for this pipeline (C04B Faction-first start).")]
    [SerializeField] FactionAsset orchestratorFaction;

    [Header("Domains")]
    [Tooltip("Ordered domain orchestrators for this pipeline (single source-of-truth domain composition for this pipeline).")]
    [SerializeField] DomainOrchestrator[] domainOrchestrators;

    public OrchestrationArbiter Arbiter => arbiter;
    public FactionAsset OrchestratorFaction => orchestratorFaction;
    public IReadOnlyList<DomainOrchestrator> ConfiguredDomainOrchestrators => domainOrchestrators ?? Array.Empty<DomainOrchestrator>();

    public DomainOrchestrator[] GetConfiguredDomainArrayOrEmpty()
        => domainOrchestrators ?? Array.Empty<DomainOrchestrator>();
}
