# System Blueprint Index

Date: 2026-02-20  
Purpose: единый реестр blueprint-документов, обязательных для новых систем и major refactor.

## Status Legend

1. `Ready` — заполнен и можно реализовывать.
2. `Draft` — частично заполнен.
3. `Missing` — blueprint ещё не создан.

## Blueprint Register

1. `Orchestration` -> `Assets/Docs/Architacture/System_Blueprint_Orchestration.md` -> Owner: `Orchestration Platform Owner` -> Status: `Ready`
2. `Kernel Services` -> `TBD` -> Owner: `Kernel Systems Owner` -> Status: `Missing`
3. `Entity Backbone` -> `TBD` -> Owner: `Entity Backbone Owner` -> Status: `Missing`
4. `Engine Anti-Corruption (TDE exit)` -> `TBD` -> Owner: `Engine Adapter Owner` -> Status: `Missing`
5. `Gameplay Modularization` -> `TBD` -> Owner: `Gameplay Domains Owner` -> Status: `Missing`
6. `Project Bridge Composition` -> `TBD` -> Owner: `Experience & Bridge Owner` -> Status: `Missing`

## Governance Rule

1. PR с новой системой или major refactor не принимается без ссылки на `Ready/Draft` blueprint в этом индексе.
