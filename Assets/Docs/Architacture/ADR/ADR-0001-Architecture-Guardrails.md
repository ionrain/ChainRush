# ADR-0001: Architecture Guardrails Baseline

Date: 2026-02-20  
Status: Accepted  
Owner: Architecture Program  
Related roadmap phase: Phase 0

## Context

1. Миграция идёт параллельно с развитием игры, поэтому без жёстких guardrails код быстро уходит в bypass-решения.
2. В roadmap и каталоге уже зафиксированы ключевые принципы (`layering`, `architecture-first`, `data-driven-first`, `no direct concrete coupling`), но им нужен статус формального решения.

## Decision

Принять как обязательные program-level guardrails:

1. `architecture-first`: сначала reuse существующих contracts/patterns/extension points.
2. `data-driven-first`: вариативность сначала выражается данными/политиками, а не новыми кодовыми ветками.
3. `typed references`: нетипизированные dependency holders (`GameObject`/`MonoBehaviour`/`Component` как service locator) запрещены в runtime architecture code.
4. `layer boundaries`: `com.morboo.*` пакеты не зависят от project-layer assemblies.
5. `system communication`: межсистемное взаимодействие только через контракты/events/queries, без direct concrete-to-concrete runtime calls.

## Consequences

Positive:

1. Снижается риск бесконечного рефакторинга и file-sprawl.
2. Ускоряется review: исключения сразу видны по ADR/PR policy.

Trade-offs:

1. Больше upfront-дизайна перед кодингом.
2. Иногда нужно писать adapter/contract слой вместо "быстрого" прямого вызова.

## Fitness/Test Impact

1. Архитектурные тесты и PR checklist становятся обязательным gate.
2. Новые bypass-решения допускаются только с ADR и cleanup планом.

## Rollout

1. Phase 0: внедрить ADR, PR template, owner mapping, contract templates.
2. Phase 1+: привязать изменения к fitness tests и phase gates.

## Links

1. `Assets/Docs/Architacture/MasterMigration/00_Program/Master_Migration_Roadmap.md`
2. `Assets/Docs/Architacture/Game_System_Catalog_v2.md`
3. `Assets/Docs/Architacture/New_System_Requirements_Template.md`
