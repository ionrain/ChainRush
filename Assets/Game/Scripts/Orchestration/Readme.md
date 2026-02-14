# Orchestration System

Goal: provide a **domain-agnostic orchestration layer** that can coordinate entities (units, heroes, NPCs, orchestrators) across multiple domains (Combat, Economy, etc.) without hardcoding domain vocabulary into Core.

This folder contains only **code contracts + planners + adapters**. Gameplay systems (TopDownEngine brains, UnitAIController, etc.) are integrated via **Executors** and bridges.

---

## Architecture at a Glance

**Core (domain-agnostic)**
- Identity: `FactionAsset` (typed), faction relations via `FactionRelationTableAsset`
- Intent layer: `Intent` (high-level "what we want")
- Instruction layer: `Instruction` (more actionable "what to do next")
- State: `StateSnapshot` (reporting), `ParamSet` (typed key-value)
- Capabilities: `CapabilityId/Set/Snapshot`, `CapabilitiesProfile`

**Domains (domain-specific vocabularies + compilation)**
- Example: `Domains/Combat`
  - Typed vocab: `CombatGoalId`, `CombatActionId`
  - Builders: `CombatIntentBuilders`, `CombatInstructionBuilders`
  - Domain command/state: `CombatCommand`, `CombatState`
  - Adapter: compile Core `Instruction` -> domain command (`CombatAdapter`)

**Execution (gameplay integration)**
- Executors apply domain commands to actual gameplay controllers (AIBrain, movement, weapons).
- Core must NOT depend on Domains; Domains may depend on Core.

---

## Key Concepts

### Factions
- `FactionAsset` (ScriptableObject) is the typed identity used by orchestration.
- Hostility is **data-driven** via `FactionRelationTableAsset` (Friendly/Neutral/Hostile).
- All faction identity is typed via `FactionAsset`. No string IDs or legacy `FactionKey`.
- Do NOT hardcode "Player vs Enemy" logic in planners.

### Intent vs Instruction
- **Intent**: high-level goal ("defend this", "harvest here"), may be long-lived.
- **Instruction**: short-lived actionable step; can be re-planned often.
- Core does NOT define domain vocabularies (no `Hold/Eliminate/MoveTo` helpers in Core).

### Capabilities
- Capabilities declare what an entity *can do* (Walk, Fly, Harvest, Melee, etc.).
- Use hierarchical string IDs (e.g. `Move.Walk`, `Combat.Melee`, `Econ.Harvest`).
- Capabilities may have params (reach, speed mode, etc.) via `ParamSet`.

---

## Conventions & Rules (IMPORTANT)

1) **No combat vocabulary in Core**
   - Core should not contain `Combat.*` action/goal lists.
   - Domain-specific enums/builders live under `Domains/<DomainName>/`.

2) **Dependency direction**
   - `Core` must not reference `Domains`.
   - `Domains` may reference `Core`.
   - Executors may reference both (they bridge orchestration to gameplay).

3) **ParamSet**
   - No dictionaries, no LINQ, no allocations in query methods.
   - Keys are case-sensitive; avoid normalization in getters.

4) **Keep compilation green**
   - Changes should be incremental and compile at every step.
   - Avoid refactors that temporarily delete types without providing replacements.

---

## How to Add a New Domain (template)
1) Create `Domains/<Domain>/` folder.
2) Add typed vocab enums (e.g. `<Domain>GoalId`, `<Domain>ActionId`).
3) Add builders to create Core `Intent/Instruction` with `DomainId.<Domain>`.
4) Add adapter to compile Core `Instruction` -> `<Domain>Command`.
5) Add executor to apply `<Domain>Command` to gameplay systems.

---

## Integration Strategy
We migrate legacy AI by adding **bridges/executors** first, then gradually moving planning logic out of monolithic controllers into domain planners.
Legacy gameplay remains untouched until a new vertical slice is proven.

---

### Documentation (Code Comments)
- Public Core contracts (FactionAsset, Intent, Instruction, StateSnapshot, Capability*) must have short XML summaries describing meaning and invariants.
- Adapters/Executors must explicitly document **ownership** (who controls targets/movement) and any anti-conflict rules.
- Use tags in code comments where it matters: `IMPORTANT:` (invariants), `RATIONALE:` (non-obvious heuristics), `PERF:` (no LINQ/no allocations, linear scans by design).
- Avoid commenting trivial code; prefer documenting intent, invariants, and reasons.