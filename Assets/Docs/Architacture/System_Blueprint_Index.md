# System Blueprint Index

Date: 2026-02-20  
Purpose: единый реестр blueprint-документов, обязательных для новых систем и major refactor.

## Status Legend

1. `Ready` — заполнен и можно реализовывать.
2. `Draft` — частично заполнен.
3. `Missing` — blueprint ещё не создан.

## Blueprint Register

1. `Orchestration` -> `Assets/Docs/Architacture/System_Blueprint_Orchestration.md` -> Owner: `Orchestration Platform Owner` -> Status: `Ready`
2. `Kernel Services` -> `Assets/Docs/Architacture/System_Blueprint_KernelServices.md` -> Owner: `Kernel Systems Owner` -> Status: `Ready`
3. `Entity Backbone` -> `Assets/Docs/Architacture/System_Blueprint_EntityBackbone.md` -> Owner: `Entity Backbone Owner` -> Status: `Ready`
4. `Engine Anti-Corruption (TDE exit)` -> `TBD` -> Owner: `Engine Adapter Owner` -> Status: `Missing`
5. `Gameplay Modularization` -> `TBD` -> Owner: `Gameplay Domains Owner` -> Status: `Missing`
6. `Project Bridge Composition` -> `TBD` -> Owner: `Experience & Bridge Owner` -> Status: `Missing`
7. `Game Runtime System Decomposition (No UI/Board)` -> `Assets/Docs/Architacture/MasterMigration/00_Program/Game_Runtime_System_Decomposition_Layer_Mapping_2026-02-20.md` -> Owner: `Gameplay Domains Owner` -> Status: `Draft`
8. `Actor System` -> `Assets/Docs/Architacture/System_Blueprint_Actor.md` -> Owner: `Gameplay Domains Owner` -> Status: `Draft`
9. `Orchestrator Pre-Refactor Minimum Contract Blocks` -> `Assets/Docs/Architacture/MasterMigration/04_Phase4_Orchestration/Orchestrator_PreRefactor_Minimum_Contract_Blocks_2026-02-20.md` -> Owner: `Orchestration Platform Owner` -> Status: `Draft`
10. `Actor-Orchestrator Interaction Contract` -> `Assets/Docs/Architacture/System_Interaction_Contract_Actor_Orchestrator.md` -> Owner: `Gameplay Domains Owner` -> Status: `Draft`

## Governance Rule

1. PR с новой системой или major refactor не принимается без ссылки на `Ready/Draft` blueprint в этом индексе.
