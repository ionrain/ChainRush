## Phase / Owner

1. `Target phase`:
2. `System owner`:
3. `Related backlog item(s)`:

## Scope

1. `System goal` (one sentence):
2. `Invariants touched`:
3. `Behavior impact`: none | controlled | expected

## Architecture Checklist (Required)

- [ ] Linked `System_Blueprint_<SystemName>.md` (for new system/major refactor)
- [ ] Architecture-first note added:
  - which existing contracts/patterns/extension points were reused
  - if bypassed: ADR link + cleanup phase
- [ ] Data-driven note added:
  - what variation is data/config-driven
  - what required new code branches and why
- [ ] Fan-out/onboarding note added:
  - touched files count
  - budget exceeded? if yes, mitigation or ADR
- [ ] Typed-reference check passed:
  - no new `GameObject`/`MonoBehaviour`/`Component` service-locator refs
  - or justified with removal plan/ADR

## Tests

1. `Architecture tests updated`:
2. `Behavior/regression tests updated`:
3. `Manual smoke checks`:

## Rollback

1. `Rollback-safe checkpoint`:
2. `Revert plan`:

## ADR

1. `ADR required?` yes/no
2. `ADR link` (if yes):
