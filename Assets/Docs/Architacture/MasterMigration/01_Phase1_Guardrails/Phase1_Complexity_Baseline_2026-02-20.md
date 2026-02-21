# Phase 1 Complexity Baseline

Date: 2026-02-20  
Phase: `Phase 1 — Guardrails Baseline`

## Purpose

Зафиксировать стартовые метрики сложности до Phase 4 refactor (`C02..C07`), чтобы измерять снижение file-sprawl и улучшение data-driven onboarding.

## Measured Metrics

1. Orchestration runtime sources:
   - `Packages/com.morboo.integration.strategycombat/Runtime/Orchestration`: `72` `.cs` files
   - `.../Domains`: `14` `.cs` files
   - `.../Domains/Combat`: `7` `.cs` files
   - `.../Domains/Idle`: `6` `.cs` files
   - `.../Domains/Common`: `1` `.cs` file

2. Domain onboarding touchpoint proxy (host/domain coupling token footprint):
   - files matched by key orchestration coupling tokens in `.../Orchestration`: `18` files

3. Engine coupling baseline (game runtime):
   - files with `MoreMountains.TopDownEngine` in `Assets/Scripts/Game/**/*.cs`: `27`
   - files with `MMEventManager|MMEventListener|MMSingleton` in `Assets/Scripts/Game/**/*.cs`: `59`

## Data-vs-Code Variation Baseline

1. Current orchestration variation is mixed, but still significantly code-driven in host pipeline touchpoints.
2. Target for C04A: standard new domain onboarding should be policy/config-first with:
   - `host-runtime touchpoints == 0`
   - `outside-domain touchpoints <= 1`

## Measurement Commands

```bash
find Packages/com.morboo.integration.strategycombat/Runtime/Orchestration -type f -name "*.cs" | wc -l
find Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains -type f -name "*.cs" | wc -l
find Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Combat -type f -name "*.cs" | wc -l
find Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Idle -type f -name "*.cs" | wc -l
find Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Common -type f -name "*.cs" | wc -l
rg -l "ArbitrationInput|OrchestrationArbiterProposals|OrchestrationArbiter|ExecutionContext|ExecutionRouter|OrchestrationDomainKeys|DispatchCombatCommand|DispatchIdleCommand" Packages/com.morboo.integration.strategycombat/Runtime/Orchestration --glob "*.cs" | wc -l
rg -l "MoreMountains\.TopDownEngine" Assets/Scripts/Game --glob "*.cs" | wc -l
rg -l "MMEventManager|MMEventListener|MMSingleton" Assets/Scripts/Game --glob "*.cs" | wc -l
```
