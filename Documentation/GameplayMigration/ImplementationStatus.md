# Gameplay migration, part 1

Implementation decision: **working model**. Code and authored content for stages 1–7 have been delivered. The final revision has **not** passed the complete validation matrix; implementation delivery is distinct from verified completion of part 1.

## Current delivery and validation status

This section supersedes the historical progress notes below.

- Included content: Survive 300 and Distance 150; four player definitions, six enemy prefabs and four source skills. Loc001Lvl03–05 remain excluded.
- Board composition: Gold 30%; Unit, HeroSkill, Buff and Heal 17.5% each. Heal cooldown is 450 simulation steps (15 seconds). Bomb and SlowTime remain excluded.
- The final explicit authoring command completed: `/tmp/chainrush-part1-complete-authoring.log`. Unity imported the scripts, saved the assets and generated their metadata. The source registry reported zero source-reference errors. This is authoring evidence, not runtime validation.
- **Latest user instruction:** no further tests while finishing the implementation. No tests were launched after that instruction. Final gameplay, load/pool and cleanup regressions for the delivered revision remain unverified.
- Earlier framework baseline: **886/886 passed**, `/tmp/morboo-part1-combined-common.xml`; `FullUnitChain` **1/1 passed**, `/tmp/morboo-part1-combined-fullchain.xml`. Both precede the final AIBrain/force changes.
- Earlier game baseline: **39/42 passed**, `/tmp/chainrush-part1-combined-game.xml`. It is not a passing result for the final content.
- Latest pre-stop focused evidence: melee area damage and the original Gate collision scenario passed in `/tmp/chainrush-part1-source-deployment-before2.xml`; the two deployment cases failed against the old integration placement. The independent weapon change allowed Cola combat and all five buff scenarios to pass in `/tmp/chainrush-part1-melee-weapon-after.xml`.
- AIBrain scenarios passed in `/tmp/morboo-part1-radial-before.xml`; that combined run's sole failure was the newly authored radial-force reproducer, before its implementation. Radial/displacement force and the final deployment correction have not been exercised after implementation.
- Main launch, final run completion/results/HUD, external meta rewards and removal of the old main execution path remain part 2.

### Final implementation changes

- Ally movement and weapon execution use separate existing AIBrain nodes. Waiting in one node does not prevent later nodes from receiving the simulation step. The movement node owns following, support, chase and return; the weapon node owns attack execution and interruption. Replaced `AttackFirst`/`AttackNext` state assets and their installer references were removed.
- Player melee attacks create a short-lived contact area using the source radius and active duration. A single activation can hit each hostile in that area; damage/power, protection and carrier cleanup remain within Skills.
- Water and Cola use separate ordinary Production hosts/catalogs and Spatial providers authored from the original speciality areas. Their placements are relative to the explicit hero spawn socket's topology coordinates. The run-follow feature moves these providers with the hero. The existing player producer retains experience-to-turn production.
- Radial melee knockback, projectile displacement-based knockback and directional enemy weapon knockback use the existing Skills effect and Movement force lifecycle. The six enemy definitions now contain the source dynamic-body mass and force response. Static/kinematic heroes retain their source force response.
- Projectile velocity uses the original `Projectile.Movement` conversion (`Speed / 10`), followed by the integration simulation-step conversion. Skill-authored Dagger and MightyBlow knockback amounts are included, rather than reading only the prefab's zero-valued default.
- Unity explicitly regenerated the level/hero variants, deployment providers, catalogs, movement definitions, skills, installer references and content registry. The temporary authoring command and two abandoned runner scenes were removed.

Existing System Fit: Production still owns recipe admission, reservations and output settlement; Activity owns seed/materialization lifetime; Spatial owns authored placement providers; the run-follow adapter supplies the level-specific displacement. AIBrain owns independent node execution, Skills owns contact/damage/force application, and Movement owns force integration. No alternate production, wave, event, pool or movement manager was introduced. Implementation decision: **working model**.

### Corrections to the verification setup

Two failures were caused by invalid test assumptions, and do not justify runtime changes:

- The first Tabasco/Survive scenario attempted to place a hostile inside the Gate body and required damage outside MightyBlow's source reach. A scenario using the original controller and original colliders establishes that the Gate stops an approaching enemy outside the base radius of two units. Source geometry and radius were preserved; the migration does not silently rebalance Tabasco in Survive.
- After melee became a carried area skill, the scenario filtered damage by the attacking unit as the immediate mutation source. Skills reports the carrier there; ownership is available on `SkillCarrierLifecycleEvent`. The trace records two successful hits. The scenario now correlates carrier ownership, target and damage instead of imposing the direct-hit implementation.

The initial expectation for the independent node's entry-frame count and an explicit skill activation during its existing reload interval were also fixture issues. No runtime exception was added for either. These mistakes caused unnecessary iterations; historical failing totals must not be presented as confirmed product defects.

### Registration, contact query and pause evidence

Literal buff symptom: `Newly deployed units must inherit the same run buff.` **Verified:** new units hold Health=110000, Speed=3300 or SkillSpeed=900 in their wallets, but their effective projection remains 100000/3000/1000. Owner identities match. `CapabilityHostService.Register → CapabilityHostSkillController.TryAttach → EnsureWalletWatches` defers watch installation during outer event dispatch, then `CapabilityHostRegisteredEvent → RunAttributes.Apply` commits before those watches exist. The independent nested-registration case fails while direct registration passes (`/tmp/morboo-registration-projection-before2.xml`), ruling out missing issue/owner mismatch. The deferred watch boundary now reads the committed graph and rebuilds the projection. Both registration cases and Production regressions pass in the 136 passing cases above.

Literal combat symptom: `Perfume did not land a carried-skill hit.` **Verified:** `/tmp/chainrush-part1-carrier-cause.log` records repeated successful attack activation and moving carriers, but no successful contact range query. `AdvanceCarrierAreaContact` requested Interaction geometry without any interaction tags; `WorldTargetResolutionService.TryCollectRangeResults` rejects that request before querying targets. Missing attack activation and projectile time conversion were ruled out (carrier velocity is integrated per simulation step). `SkillCarrierAreaContactData` now explicitly authors interaction tags, validates them for Interaction/sweeping contacts, and passes them to both the broad query and exact sweep. Player/enemy projectile and enemy contact authoring use the existing combat-body tag. No fallback or data inference was added; explicit authoring updated assets.

Literal pause symptom: Survive progress changes from 333 to 444 after pausing. **Verified:** the authored realtime schedule allows one catch-up step per frame; `RealtimeScheduleRuntime.Update(0)` drained previously accumulated time. The independent scheduler test dispatches one step before pause and two during zero-delta updates (`/tmp/morboo-registration-after-pause-before.xml`), ruling out the progress feature itself or an extra Unity frame as the whole explanation. Zero-delta realtime updates now preserve the backlog until positive time resumes. Calendar and manually advanced turn schedules retain their own contracts.

Existing System Fit: CapabilityHost owns wallet observation and effective state; Economy owns the committed amount; RunAttributes owns the participant's buff total. Skills owns contact execution and carrier lifetime; WorldTargetResolution and InteractionGeometry own candidate geometry and exact intersections. Scheduling owns realtime elapsed time and pause behavior. Corrections remain within those owners. Implementation decision: **working model**.

### Projection and level-variant evidence

Literal navigation symptom: `Free straight-line goal projector returned a position rejected by its matcher.` Call path: Skills movement effect → Movement approach request → WorldTargetResolutionService → EntityApproachPositionMatcher → FreeStraightLineNavigationProvider → Topology quantization → matcher validation. The independent offset-box test failed from an unobstructed position: the exact surface gap 25.1665 was rounded to 25166; projecting with that rounded distance left a gap of 1 instead of 0 (or 1001 instead of 1000). The diagnostic shows identical projected and resolved coordinates, ruling out topology registration as that instance's cause; geometry queries succeed before and after. Reports: `/tmp/morboo-offset-approach-before.xml`, `/tmp/morboo-offset-approach-diagnostic2.xml`. Projection now uses the geometric gap and validates the quantized point; only when rounding leaves it outside does it project one topology unit farther inward. The acceptance threshold is unchanged. All 360 approach directions at both thresholds and 11 nearby scenarios pass.

Literal Distance launch symptom: `Team 0 contains duplicate activity feature type ChainRush.Gameplay.ChainRushHealFeatureData.` The Activity authoring cloned Survive's existing feature then appended Distance's. The four-variant configuration test also identifies old hero references in progress and healing. Explicit authoring now writes one healing feature for each level, seeds healing skills after creating the body variants, and binds Distance progress to the Distance hero definitions. Before: `/tmp/chainrush-part1-variant-edit-before2.xml`; after: all 55 configuration tests passed. No runtime interpretation or repair was added.

### Contact and hero-body evidence

Literal failures: a living enemy spawned a second contact area; the hero held one body definition in its wallet but had no registered interaction geometry. Both failed in `/tmp/chainrush-part1-contact-geometry-before2.xml` and passed in the matching after report.

**Verified:** the contact node ran its spawn action on every tick; successful action execution does not complete that state. It now uses the existing OnEnter action lifecycle. The hero body was issued by an Activity feature after `CapabilityHostAssembler` had already called `CapabilityHostInteractionController.TryAttach`; the registration event comes later and cannot retroactively change that attachment. Missing wallet data was ruled out by the same test's successful amount assertion. Explicit authoring now seeds the body before registration: GateHero for Survive and per-character NormalHero geometry for Distance. Existing finite level/hero Activity variants select the corresponding definition. The obsolete issuing feature and its assets were deleted through Unity, along with the temporary authoring cleanup code.

Existing System Fit: AIBrain owns one-time state entry; Skills owns the persistent contact carrier and expiry; Economy seeds definitions; CapabilityHost owns interaction attachment/detachment; InteractionGeometry owns registered shapes. No dynamic geometry refresh service or parallel lifecycle was introduced.

## Scope

- Included: Location001 Level01 (Survive 300) and Level02 (Distance 150).
- User explicitly excluded Loc001Lvl03–05 from part 1. Their source assets are unchanged.
- Main launch stays unchanged. Work targets `ChainRushFrameworkIntegration`.
- The generated content inventory is `ContentRegistry.json`; its readable index is `ContentRegistry.md`.

## Implemented and checked

- Explicit editor authoring for the two run selections, immutable level/hero/unit/development snapshot, capture before GameFlow startup, and the event adapter supplying Activity features.
- Activity-owned Survive and Distance resource publication. Simulation pause freezes Survive. Distance observes the authoritative Spatial pose relative to hero registration; 75 units yields half of Distance 150.
- Runtime-owned progressive Objective numeric thresholds, Economy observation, immutable target binding snapshots and resolved values supplied to decomposition. Authored thresholds are unchanged.
- Population full/partial policy, admission closure separated from accepted work settlement, region ownership yield while keeping frozen reads, fixed count limit, and ordinary Orchestration no-progress exclusion/applicability reuse.
- Grid first uses free positions, then full policy preserves tentative blocked remainder for admission. Spatial full policy also keeps placement choices while Production remains responsible for current occupancy admission.
- Production owns finite release identities, admitted volume and policy, correlated orders, deduplicated committed outputs, settlement, cancellation and cleanup. A confirmed output remains part of the historical result after consumption or death.
- Objective owns the release requirement and runtime binding. Extraction, fact matching, planning payloads and invalidation resolve that binding. Reset creates a new identity; delayed events from the previous release cannot satisfy it.
- Population correlates ordinary materialization orders with the release. Production Agent has explicit finite volume and completion policy and uses the same release result through ordinary production orders. Its decomposition endpoint requires an Agent executor: planning cannot admit a release directly.
- The registry now includes decoded enemy and board parameters in addition to raw serialized source evidence. Both included levels have no authored progress or signal waves. Their replenishment compositions change at 0, 0.25, 0.5 and 0.75; source count curves range from 2 to 20 and 4 to 20, with simultaneous limits of 20.
- Population content availability has explicit external definitions with initial elapsed duration and progressive cooldown. AgentService owns participant/source state through domain closure; Orchestration observes readiness changes. Availability is frozen before budgeted share redistribution. Full empty passes wait without acquiring a region; partial empty passes close admission without issuing orders. Confirmed production yields reset each used source once per assignment.
- Catalogs support deterministic selection of distinct assets. Each asset has one seeded score regardless of the number of producers exposing it; the chosen asset stays fixed while waiting for production. Board authoring enables this mode and writes Unit/Buff/HeroSkill cooldowns of 240/150/180 simulation steps (8/5/6 seconds at the authored 30 Hz schedule), with initial readiness matching the sources; Gold has no cooldown.
- Gold now uses the existing production input-consumption decomposition and a recipe consuming one selected Gold cell for ten RunGold. The consume-only Gold operator was removed by explicit authoring. Gold recipe orders, Economy transactions and consumed input tokens provide confirmation and replay protection; no game-facing manager issues rewards separately.

## Verification already completed

- Initial ChainRush EditMode baseline: 43/43.
- ChainRush run input/assets EditMode: 48/48 (`/tmp/chainrush-part1-run-assets.xml`).
- Objective package tests: 74/74 (`/tmp/morboo-part1-threshold-tests.xml`).
- Population/Production/Agent/Orchestration regression tests: 360/360 (`/tmp/morboo-part1-policy-expanded.xml`).
- Framework FullUnitChain passed after threshold work and again after Population/close changes (`/tmp/morboo-part1-policy-fullchain.xml`).
- ChainRush progress PlayMode: 2/2 (`/tmp/chainrush-part1-progress-after.xml`).
- Finite release, Production, Population, Objective and Agent regression suites: 393/393 (`/tmp/morboo-part1-release-integrated-after.xml`).
- New release scenarios include Grid and spatial materialization with budgets 1 and 256, death during release, full/partial resource shortage, preserved historical outputs, matching/decomposition, and Objective reset isolation. Targeted checks: 7/7 (`/tmp/morboo-part1-release-focused.xml`).
- Activity closure now checks both ordinary and finite Population orders awaiting materialization: 2/2 (`/tmp/morboo-part1-release-close.xml`), including compensation and removal of the release identity.
- Framework FullUnitChain passed after finite release changes (`/tmp/morboo-part1-release-fullchain.xml`).
- Complete ChainRush Activity composition PlayMode regression: 22/22 (`/tmp/chainrush-part1-release-regression-play.xml`), including selection chains 1–16, merge/deployment, progress, combat/experience, Board turn sequencing and Activity closure. These tests cover the current integration content; they do not establish completion of the still-unported effects or level rules.
- Content availability and catalog work: 401/401 package regressions (`/tmp/morboo-part1-content-regression.xml`), then 454/454 including source observation, Economy progress and process retry suites (`/tmp/morboo-part1-source-final-edit.xml`). FullUnitChain passed after the final source observation wiring (`/tmp/morboo-part1-source-final-fullchain.xml`).
- ChainRush content/run authoring EditMode: 21/21 (`/tmp/chainrush-part1-content-edit.xml`). This run preceded Gold recipe authoring; Gold configuration and final game regressions are being checked separately.

## Diagnostic evidence

### Early Activity close

Literal symptom: `producer projection was removed before output materialization completed`.

Verified: Activity close reached Production with an order in `AwaitingOutput`; the domain was detached before pending materialization could reject the output. Focused test `ActivityClose_SettlesUnconfirmedOutputBeforeReleasingProduction` failed with input compensation expected 1, actual 0 (`/tmp/morboo-part1-close-before.xml`). Normal cancellation followed by confirmation still passed, distinguishing this from an ordinary cancellation failure.

The materialization owner now rejects pending and bound unconfirmed outputs on `ActivityClosing`, while Production and Economy still exist. The same test and 302 adjacent tests passed (`/tmp/morboo-part1-close-after.xml`); the ChainRush early-close progress test also passed. No accepted-order confirmation semantics were changed.

### Distance identity and scale

Literal symptom: 75 units of displacement produced 0 rather than 500000 progress units. Trace showed the same definition ID, same Activity and same owner, but unequal Unity object references (`/tmp/chainrush-part1-distance-identity.log`). The feature now uses semantic definition identity. The topology source confirms that `SpatialPose.Coordinates` are Unity coordinates; the explicit authored scale is 1. The same Distance scenario passed. Temporary diagnostic logging was removed.

### Release test fixture corrections

The new Grid release fixture initially supplied a Free topology and an offset shape that did not fit its cellular region. The algorithm reported its topology validation failure; after a Grid topology and an explicit single-cell usage were supplied, the same historical-release scenario passed at budgets 1 and 256. No runtime change was made for these fixture failures.

Production release work scenarios passed independently, but the expanded suite exposed control claims surviving between tests that recycled Entity IDs. The fixture now resets EntityControlAuthority along with the other session services. The expanded suite subsequently passed all 393 cases. This is test isolation, not a claimed gameplay runtime fix.

## Still in progress

- Level replenishment is authored and game scenarios now exercise initial counts, controlled deaths, composition transitions and the progress admission cutoff. Final materialization cleanup and full combat/load verification remain.
- Availability/cooldown and distinct-content selection are connected. Complete board shape/content selection from the player snapshot, buffs, hero skills, complete combat content, projection effects, and full registry validation remain.
- Latest user decision supersedes the earlier four-category allocation: Gold 30%; Unit, HeroSkill, Buff and Heal each 17.5%. Heal cooldown is 15 seconds. Unavailable categories redistribute their shares before placement; total volume is unchanged. Bomb and SlowTime remain excluded.
- Final integrated level scenarios, resource/pool load checks, and the final suite reruns remain.

No part-2 work (main-game cutover, final outcome/meta reward/HUD/result screen) is included.

## Additional evidence from content integration

- Applicable Population methods originally notified readiness changes without supplying a process observation. A focused test verified both notifications but failed the retained-observation assertion (`/tmp/morboo-part1-source-observation-before.xml`). The Agent operator now preserves the observation alongside executable options, allowing the existing no-progress exclusion to wake through decomposition. The same test plus applicability/process suites passed: 205/205 (`/tmp/morboo-part1-source-observation-after.xml`).
- Two producer-selection fixtures exhausted their old 100-operation loop at budget 1. Increasing only the test's waiting bound preserved the same product assertion and passed all five variants (`/tmp/morboo-part1-content-budget.xml`); no production-admission behavior was changed for this.
- New availability fixtures initially used Free topology with the Grid algorithm. The diagnostic reported that exact validation failure; supplying Grid topology and a fitting single-cell usage fixed the fixture. A catalog comparison originally let the second execution materialize into the first execution's positions; input probes were ready, while the competing output occupied those positions. The revised scenario settles the first release before comparing the second execution's choices. All three focused scenarios passed (`/tmp/morboo-part1-content-fixtures-after.xml`).
- Literal game failure: `[DropAIBrainAction] Drop failed ... Reason='Projection materialization failed.'` in the Cola 7–12 chain scenario. Production/Drop requests reached Projection, then Pool.TryRent reached the capacity limit. The reproducing trace showed 32 live experience hosts, 32 active views, zero free views and max capacity 32 (`/tmp/chainrush-part1-gold-and-pool.xml`), ruling out orphaned rented views in that sample. Explicit run authoring raises the experience pool limit to 64. The same scenario and the deliberate pool-exhaustion scenario passed in the complete 23/23 PlayMode run (`/tmp/chainrush-part1-source-gold-play.xml`). This is not a claim that all real-level load budgets have been verified.
- The Gold refresh fixture originally counted only standalone Economy operations, so its consumption counter remained zero even though the three production recipe orders ran and removed the selected cells. Gold checks now observe the credited resource, selected-cell removal, destruction of the 13 unselected cells, payment order and duplicate selection completion. All 23 scenarios passed (`/tmp/chainrush-part1-source-gold-play.xml`), including Gold chains of 3 and 16, credit before turn payment, and replay protection. Gold configuration also passed the final 21/21 EditMode suite (`/tmp/chainrush-part1-gold-final-edit-after.xml`).

## Count admission and shared progress

- Literal symptom: `Cooldown waiting cannot postpone capture of the assignment's count limit.` The focused four-case test failed in both count-goal variants before the change (`/tmp/morboo-part1-availability-count-before.xml`). **Verified:** Analytics was reviewed before the tick, but the empty-source branch returned before the initial count read; both coverage variants passed. The count read now captures the fixed limit before content availability waits. The same four cases and the expanded package suites passed, 458/458 (`/tmp/morboo-part1-availability-count-after.xml`). Production still checks existing plus incoming before accepting each order, and closes or settles through its original lifecycle.
- Economy applicability now accepts the existing authored/context owner binding. This lets an enemy participant observe the player's shared progress without duplicating the resource. The new test distinguishes the two owners, checks admission at completion, and verifies subscription cleanup. Agent tests: 138/138 (`/tmp/morboo-part1-applicability-owner.xml`).
- The final Gold asset check initially expected 16 Board operators after the consume-only Gold operator had been deleted. The test expectation now reflects the 15 authored operators; the recipe and selected-token assertions remain. This was a stale configuration assertion, not a runtime failure.

## Level configuration integration in progress

- Selected-level startup now resolves an explicit GameFlow from the immutable input. Level01 keeps the Survive flow; Level02 has its own Distance Activity and flow. The fixed Autobattle startup asset was removed through Unity authoring. The 21 configuration checks and both progress PlayMode cases passed before replenishment was connected (`/tmp/chainrush-part1-level-flow-edit.xml`, `/tmp/chainrush-part1-level-flow-play.xml`).
- Authoring now writes level-specific Population Agents, count Objectives, Knowledge/Analytics features and six one-output production recipes. Count curves are represented by existing dependent progression intervals at the published progress resolution; both curves matched the source at all 1,000,001 values. Count limits stay on the Agent, content shares change at the source intervals, and Economy applicability stops admission at full progress. Asset checks: 23/23 (`/tmp/chainrush-part1-level-population-edit.xml`).
- This wiring does not yet establish real enemy combat parity: the six definitions currently reuse the integration combat setup while their source visuals and population identities are connected. Health, attack, projectile, protection, movement and drop differences remain part of stage 7. Distance region following and integrated replenishment still require verification.
- The source spawn box is centered at (22.4, 6.2), sized 10 by 20 in the source XY plane. The integration maps it to XZ through SpaceRegionController. The navigation floor was expanded to contain this box and the Distance route.

### Replenishment startup evidence

Literal symptom: `Activities did not start.` Both new level scenarios failed (`/tmp/chainrush-part1-level-population-play-after.xml`). **Verified:** Activity preparation reached space phase Ready, then Objective admission rejected `ResetOnConditions` without reset conditions. This distinguishes Objective configuration from a space-loading failure. Authoring now supplies a separately constructed progressive `count < target` reset condition. The repeated scenario gets past startup and reaches the planner (`/tmp/chainrush-part1-level-population-reset-play.xml`).

### Category-count matching evidence

Literal symptom after startup: initial enemy counts expected 2/4, actual 0; the replenishment process reported `Process planning produced no executable candidates.` **Verified:** the Population factory already accepts category count goals, but both shared Agent match entrypoints reject a missing concrete asset before comparing the category. The focused two-entrypoint test failed before the change (`/tmp/morboo-part1-category-match-before.xml`). Matching now permits the same explicit nonempty tag selector on both sides; it preserves the existing distinction between an exact asset and a category. Both initial-count game cases passed (`/tmp/chainrush-part1-level-category-play.xml`), with 461 package regressions and FullUnitChain passing (`/tmp/morboo-part1-category-match-after.xml`, `/tmp/morboo-part1-category-fullchain.xml`).

Existing System Fit: Objective owns progressive count/reset thresholds; AgentObjectiveMatchRegistry is shared by decision selection, Agent decomposition and Population shape invalidation; PopulationAgentFactory owns count-goal support; Analytics owns existing plus accepted counts; Production still owns admission and output settlement. The correction belongs in the shared matcher, not a ChainRush-specific bypass.


## Replenishment lifecycle and moving region

Implementation decision: **working model**.

- The Distance region now follows the shared progress resource along the authored route; Survive keeps the initial region. `ChainRushReplenishmentRegionFeatureData` is owned by Activity and publishes its region to the existing SpaceRegionService. It retains one geometry definition when moving the pose. Unity authoring removes the prior static replenishment controller from the space prefab.
- Existing System Fit: Activity owns opening/closing the game adapter; the Economy progress resource remains authoritative; SpaceRegionService owns region registration, frozen reads and publication; Population and Production retain placement and admission ownership. Source camera following is represented as simulation route displacement, without making gameplay depend on a projected camera transform. The initial source box and its XZ mapping are preserved.
- The before test expected region X=97.4 after 75 units of Distance progress, but read 22.4 (`/tmp/chainrush-part1-region-before.xml`). Progress itself was 500000, so an incorrect progress source was ruled out. The repeated test passed, including stable geometry revision, the old pose retained by an existing read, and invalidation on Activity closure (`/tmp/chainrush-part1-level-lifecycle-after.xml`, the Distance progress case).
- The replenishment test controls deaths by disabling the hero's combat skills through their existing runtime contract. This separates count/composition assertions from the ongoing combat tested elsewhere. Its longer bound accommodates sequential accepted production and materialization. It passes all three source composition transitions and the no-new-spawns condition at full progress (`/tmp/chainrush-part1-markers-before.xml`, the lifecycle case). This does not establish a final performance budget.

### Changes during bounded work

Literal symptom: after the initial four enemies died during accepted replenishment, four replacements appeared but the target of eight remained unmet; the process reported `Process exhausted all candidates for the current fact revision.` (`/tmp/chainrush-part1-replenishment-controlled.xml`).

**Verified:** the process compared only initial and final fact values. The test `ProcessRuntime_ChangedFactDuringBoundedWorkRetriesWithUnchangedNetCount` reproduces the 4 → 0 → 4 sequence, with target eight, and failed before the change (`/tmp/morboo-part1-changed-fact-before.xml`). A mere revision change with an unchanged value is a competing case covered by the existing next-candidate test. The first broad revision-based implementation failed that existing case and was replaced with observation of actual fact value/satisfaction changes during the active attempt. No revision-only exception remains.

The process now allows ordinary decomposition after an observed world change during its bounded work. A subsequent unchanged zero-result attempt remains blocked. All 409 affected package checks passed (`/tmp/morboo-part1-observed-fact-after.xml`). This uses the existing fact resolver, process state and admission lifecycle; no Population retry manager was added.

### Placement marker settlement

Literal symptom: `Materialized marker lease could not be committed`, with `Marker provider 'transient-marker:1' has no registered usage policy.` The focused Spatial test confirms the marker and lease are still present, then fails at commit (`/tmp/morboo-part1-marker-commit-before.xml`). Both initial-count game cases reproduce this warning after otherwise reaching the correct count (`/tmp/chainrush-part1-markers-before.xml`). The transient placement marker commit now releases its held selection without entering an authored provider's selection cycle. All 437 affected package checks passed (`/tmp/morboo-part1-marker-commit-after.xml`); both game initial-count cases passed without this warning.

A separate `Spatial marker selection lease was not found` warning occurred on asynchronous prefab completion. **Verified:** materialization 8 was an experience drop; removing the dead source enemy unbound `enemy-drop-position`, and `SpatialMarkerService.RemoveMarker` released lease 5 before projection finished (`/tmp/chainrush-part1-drop-lease-trace.log`). The nearest competing explanation, release by production confirmation or Drop cleanup, is ruled out by the release stack. A trial deferral of projection-bound events did not fix the warning and was fully reverted.

Implementation decision: **working model**. Existing System Fit: Drop transfers accepted placement ownership to Activity materialization; Spatial owns the pending record and lease settlement; Projection supplies completion; provider unbinding owns only its published markers. Staging now explicitly transfers the lease, provider removal detaches an accepted lease, and commit/rollback/Activity closure settle it. Ordinary unaccepted leases are still invalidated by provider removal. A recreated provider has its own selection cycle, and duplicate transfer is rejected.

The focused three-case test failed before this correction at the retained-lease assertion (`/tmp/morboo-part1-drop-handoff-before.xml`). The game replenishment lifecycle now passes, including all composition thresholds, the admission cutoff and absence of materialization-lease warnings (`/tmp/chainrush-part1-drop-handoff-after.xml`). Final package verification also covers a recreated provider, duplicate transfer and late completion: 440/440 passed (`/tmp/morboo-part1-drop-handoff-final.xml`). FullUnitChain passed once after this shared block (`/tmp/morboo-part1-replenishment-fullchain.xml`). Temporary trace logging was removed.

### Validation cadence

**Historical instruction:** first implement the entire remaining approved scope, then run validation together. The current instruction and actual execution status are recorded at the top of this document. The framework host may have no corresponding product-code diff because both Unity projects load the separate MorbooFrameworkPackage repository.

### Completed progress admission cutoff

Literal symptom: `Completed Distance progress must stay complete when the hero moves back`, expected 1000000, actual 500000 (`/tmp/chainrush-part1-progress-finish-before.xml`). **Verified:** the shared resource reached completion at 150 units and then returned to half at 75; the source `LevelManager.FixedUpdate` only updates while `_progress < 1`. The earlier half-distance and region checks pass, ruling out scale/origin errors. The Activity progress adapter now stops publishing progress changes at completion, while close still clears the resource. The same Distance scenario, Survive pause/cleanup and level Population scenarios passed, 5/5 (`/tmp/chainrush-part1-progress-finish-after.xml`).

## Combat attribute implementation in progress (not yet authored or validated)

Implementation decision: **working model**. New source files have been written, but the authoring command and tests have deliberately not run, following the latest user instruction. This section is not a completion claim.

- `ChainRushRunAttributesFeatureData` owns per-participant run buffs, immutable input lookup, idempotent host registration, attribute wallet projection, and pending health adjustments that preserve lost health. It observes committed Selection results before the existing selected-token consumption and Board turn chain. It clears subscriptions and run state on Activity close.
- `ChainRushAutobattleVerticalSliceAuthoring.Attributes.cs` creates explicit attribute definitions and feature mappings for Water/Cola forms and Perfume, authors buff grades, skill speed mappings, a health cap, and begins six-enemy stat/contact authoring. The run authoring entrypoint now calls it after level Population authoring.
- Combat quantities use 3 decimal places, including Health and Defense; formulas subtract raw values at the same precision. Power and movement use owner attributes, and skill timing uses the existing property mappings.
- Common Skills now support an explicitly selected Attribute operand, an explicitly authored missing-attribute policy, minimum result magnitude, Carrier as a formula parameter source, and host-value parameters captured on carriers at spawn. These keep projectile damage independent of later caster attribute changes.
- Existing System Fit: Activity owns the game feature lifecycle; Selection owns the committed request; Economy owns attribute quantities; CapabilityHost/Skills own effective attributes, health caps, execution and carrier state. No old Unit/Enemy runtime dependency has been added to Core. Source managers are read only for behavior and authoring.
- Best Practices: immutable launch input and per-run state preserve the approved authored-data boundary; the existing `DropRuntime.TryMaterialize` ownership transfer and `SkillCarrierRuntimeRecord` lifetime illustrate the same explicit ownership rule for accepted runtime state. Formula execution and estimation share `SkillHostValueFormulaEvaluator`, so the new operand must work identically in both.

Outstanding in this block: attach carrier damage authoring to the new parameter; complete enemy weapons/projectiles and source contact geometry, enemy level modifiers/drop amounts, Tabasco/hero selection and LightningBolt levels; player roster eligibility, source board forms, movement/target/anchor lifecycle, all required effect/projection wiring. New tests and the final combined validation are still pending.

Source correction: the actual current Perfume source and both integration selection assets include **both** TurretKettleSkill and SkillLightningBolt as acquired defaults. Any earlier inference that only Turret was acquired was incorrect. Roster filtering is still required for alternate heroes or an explicitly reduced acquired-skill snapshot.

## Remaining-scope implementation, before combined validation

Implementation decision: **working model**. Everything listed here is source-code work only: Unity import, asset authoring, compilation and tests have **not** been run for this block. Earlier passing results above do not validate these changes.

- Added Tabasco launch variants, snapshot-based Board roster eligibility, source pattern weights, six LightningBolt levels, captured projectile damage, temporary damage protection, per-chain Heal skills and explicit run skill interruption/observation cleanup.
- Heal restores 10% of maximum health per selected cell. Source `UnitManager.heroPrefabs` uses GateHero for Survive (`canBeHealed = false`) and NormalHero for Distance (`true`); ordinary units are healable. Authoring preserves these recipient differences.
- Distance hero movement uses the existing Skills/Movement route lifecycle through an explicit position target. Activity-owned follow data moves the deployment provider through Spatial and publishes displacement for the camera and the nonspatial experience collector's visual anchor. Views only consume events. Closing the Activity restores the camera/visual origin.
- Enemy weapon authoring reads the actual `CharacterHandleWeapon.InitialWeapon`, `WeaponAutoAim` and `WeaponAutoShoot` on Green/Purple sources. Their independent weapon node shares the existing AIBrain; it does not use the unrelated `WeaponManager` path.
- Added persistent area contacts and swept projectile contacts. Circular enemy projectiles and rectangular player projectiles carry explicit source dimensions. InteractionGeometry supplies a generic oriented-box sweep; Skills owns contact filtering, deterministic first-hit order, carried effects, lives and expiry. Neither Spatial nor InteractionGeometry depends on Skill or game types.
- Source experience drops are now read explicitly from active droppers in a preview of `Level.unity`. The active ExperienceDropper produces 3 XP for requester 0 (small enemies) and 6 XP for requester 1 (medium enemies). The other ItemDropper is inactive and is not included. Existing Drop/container/materialization ownership is reused.
- Allied AIBrain authoring includes scored enemy search, acquisition-origin leash, explicit attack completion/interruption, anchor return and recovery. Recent health-decrease observations in AIBrain support hero defense, ally defense and assistance to an engaged ally. Observation state belongs to the brain owner record and disappears with it; time windows use its simulation schedule.
- Added an Activity-owned enemy-field observation contract. It combines the shared progress resource, the enemy team's Objective runtime identities and active Agent assignments, Production queues/pipelines, and complete Analytics existing/incoming counts. Emission settlement and field clearance remain separate facts; the adapter does not decide the run outcome. Its mediated request listener and Analytics read are released on close. The real-level replenishment scenario now asserts these facts and listener cleanup; it has not run yet.
- Carrier target queries now collect current eligible targets at each action, including later LightningBolt shots. Execution and estimation share candidate filtering and ordering; estimation does not consume randomness.
- Explicit registry export now maps both levels, all six enemy identities, player forms, referenced skills, board availability/shapes, attributes and AI profiles to their existing target assets. It checks mapped target object references once per asset. The generated registry files have not yet been refreshed, so they still reflect the previous authoring run.
- Added unrun scenarios for same-hit elemental damage protection, protection expiry on simulation steps, oriented box sweeps, Heal replay/recipient behavior, and buffs on existing/future units with repeated registration.

Existing System Fit: immutable launch data remains game-owned; Activity owns run features and subscriptions; AIBrain owns target selection and local observations; Skills owns admitted attacks/carriers/protection and Movement requests; Spatial and InteractionGeometry own positions and geometry; Production/Drop own materialization and compensation; Projection owns pooled views. Old gameplay executors remain confined to the original scene and source authoring references.

Best Practices: the approved separation between simulation and presentation is retained (`EntitySpatialPoseChangedEvent` → `ProjectionServiceCore`, game displacement event → view adapter). Box sweep uses continuous separating-axis intervals against the existing `WorldTargetGeometryKernel.Box`, preserving the shared geometry owner rather than adding projectile-specific geometry in a view. Per-owner recent-value observations follow the existing `AIBrainService.OwnerRecord` lifetime and `SchedulingService` clock rather than a separate wall-clock monitor.

At this historical point, source movement/contact/formation and knockback implementation, authoring and final validation were still outstanding. Refer to the current delivery section for the implemented revision and its unverified scenarios.

Source geometry assessment (read-only, not a bug-cause claim): the ordinary Unit root has a dynamic Rigidbody2D with mass 10 and a 1.4 × 1.5 BoxCollider2D offset by (0, 0.25); NormalHero has the same box but a kinematic body; GateHero has a static trigger box 7.0785265 × 27.291588, offset (0.7942624, −1.3922105). Enemy roots use circles of distinct radii. These source dimensions are not yet represented by the common 1 × 1 integration combat body. Formation/rally settings are present on Warrior/Range profiles. Existing shared GroupMoveFormation owns coordinated group movement and forced member control; it must not be duplicated or silently substituted for independent ally behavior.

## Contact bodies and independent anchor behavior — awaiting combined validation

Implementation decision: **working model**.

- InteractionGeometry now authors Box or Sphere primitives; the shared geometry kernel resolves box/sphere continuous contact and surface distance. The new Sphere path preserves the enemy radii. Existing Box segment behavior has not been changed without a reproducer.
- Explicit game authoring writes Unit, NormalHero, GateHero and six enemy body definitions from their source colliders. Activity features issue the selected player body through Economy. The feature owns subscriptions; the capability-host wallet owns geometry lifetime. Source offsets use signed coordinates, including GateHero's negative offset.
- Enemy contact is a persistent Skills carrier, independent of pursuit and independent weapon actions. It follows its living owner, uses hostile recipient geometry, and repeats according to the source protection interval. Its invisible pooled projection has an explicit prefab and capacity of 64, so preload uses the existing Projection contract.
- AIBrain owns stable formation membership, anchor-relative idle points, resting, threat retention and catchup hysteresis. Existing Skills position targets and Movement remain the movement executors. Independent per-member destinations do not acquire coordinated GroupMoveFormation control.
- Registry export includes source player bodies, independent enemy weapon/projectile dependencies and the source level scene. For large player prefabs, it records the gameplay components rather than embedded rendering data; dependency hashes and references still cover the whole asset.
- Added unrun tests for stable formation slots/catchup and detached anchors, simulation-clock carrier movement/lifetime/reset, sphere contacts, and a source-controller probe distinguishing movement-owned steps from free force displacement.

Existing System Fit: InteractionGeometry owns primitive geometry; AIBrain owns independent destination selection and per-owner state; Skills owns contact effects and carrier lifecycle; Movement owns character displacement; Economy/CapabilityHost own issued body definitions; Projection owns the corresponding pooled objects. The source controller is used only by an isolated behavior probe, not by the integration runtime.

Validation has started with Unity import and explicit authoring. Initial compilation found an ambiguous `EntityId` import in the new anchor runtime; the reference was qualified. No gameplay tests from this block have completed yet. Carrier pause and source knockback are diagnostic questions, not established runtime causes.

### First combined validation

- Authoring completed (`/tmp/chainrush-part1-content-authoring-final8.log`). The exported registry contains 48 source entries with no reference errors. The only unmapped entry is the empty location with no included levels.
- Framework affected suites: 846/852 passed (`/tmp/morboo-part1-content-combined-before3.xml`). Formation/catchup, sphere sweep, temporary protection and the existing Population/Production/Objective suites passed.
- Literal carrier failure: `Pumping without a simulation step must not expire a carrier.` Both ordinary and area carriers failed. Verified call path: `SkillService.PumpRuntime → InMemorySkillRuntime.AdvanceSkillCarriers → AdvanceSkillCarrier/AdvanceCarrierAreaContact → DecrementCarrierLifetime → ExpireSkillCarrier`. The reproducer never dispatches a simulation step, and the target is twenty units away, outside the three-step lifetime; ordinary hit/movement regression cases still pass. This rules out a hit as the cause of the premature expiry. Carrier stepping now consumes captured schedule events; idle pumping only performs cleanup, and expiry/reset unsubscribes the captured clock. The same scenarios still require their after run.
- Four formula scenarios stopped on a test-provider issue: `SetAttribute` used an attribute-plus-qualifier fingerprint while the real projection lookup keys by attribute and qualifier-only fingerprint. Corrected the fixture key; no formula runtime change was made. The captured-flight fixture also now supplies explicit velocity and dispatches a simulation step.
- ChainRush EditMode: 47/53 passed. Four configuration assertions still described the old prototype (generic enemy brain, cardinal-only selection, no hero movement capability, and no skill-speed mapping installer); they now check the authored model. The source movement probe requires a local physics scene, so it was moved to the PlayMode suite. Its behavior result is still unverified.

### Follow-up evidence and current validation

Implementation decision: **working model**. This section supersedes the unvalidated status of the corresponding blocks above; the entire part is still awaiting the game suites.

- Carrier clock and formula checks passed. The two carrier-clock fixtures were corrected to assert movement magnitude, since the navigation frame determines the signed axis. `/tmp/morboo-part1-carrier-clock-final.xml`: 2/2; the following combined run also covers these cases.
- Literal integration failure: `Run buff values must be finite and nonnegative.` The Activity launch trace reaches `ChainRushRunAttributesFeatureData.Validate`, where the real negative SkillSpeed grades are rejected. The source path is `UnitManager.ApplySupportMultipliers → Unit.OnSupportSkillBuff → SetupSkillSpeed → AttackSkill.SetSkillSpeed`: SkillSpeed multiplies weapon duration and burst interval. It is not a rate. The generic mapping now explicitly selects Rate or Duration, and ChainRush authors Duration only for its attack skills and those two timing properties. Movement and other synthetic skills are outside that mapping. A nonpositive source weapon delay completes immediately; the run projects its duration multiplier to zero at that bound. Buff data remains signed and authored assets remain immutable at runtime.
- The focused timing tests failed before the resolver change (800/1000 gave 37 instead of 24 steps for an authored duration of 30; zero rejected execution) and pass afterward. `/tmp/morboo-selection-timing-before.xml`, `/tmp/morboo-selection-timing-after.xml`.
- Literal FullUnitChain failure: `The authored RTS chain did not build Barracks after the Gold worker became available.` The diagnostic observes both workers in `WorkerGoldSelectDropoffState`, with `SelectEntityTargetByQueryAIBrainActionData returned Failed`, after successful mining. A focused test uses identical candidates and diplomacy before and after Unity serialization: the direct instance succeeds; the serialized instance has four candidates and an unintended, invalid recent-value filter and fails. This rules out missing geometry/candidates and demonstrates the rejecting filter branch. The optional filter now uses `SerializeReference`, which preserves absence, instead of inline serialization. The same direct/serialized scenario passes afterward. No compatibility repair or runtime inference was added. Reports: `/tmp/morboo-part1-content-fullchain-diagnostic.xml`, `/tmp/morboo-selection-timing-before.xml`, `/tmp/morboo-selection-timing-after.xml`. FullUnitChain is being repeated against that correction.
- Source force probe confirmed additive displacement: force 50, mass 10, a 0.02-second physics step adds 0.002 units even while ordinary movement owns the same step. The competing interpretation that `MovePosition` erases force was disproved. Both moving/stationary source cases pass in `/tmp/chainrush-part1-startup-physics-final.xml`. That run's remaining failure is the obsolete assertion that the shared hero definition lacks MovementOwner; launch and hero materialization now succeed.
- Movement now owns optional authored mass, force decay and integration interval. It consumes the existing simulation clock, combines the resulting offset with ordinary movement through Spatial reservations, retains sub-coordinate precision, and releases its subscription on detach/reset. The force comes from the existing Skills carrier's direction. Game authoring copies the actual merge masses (3, 6, 9, 12), and uses the original 0.02-second integration interval and `TopDownController2D.ApplyImpact` decay/cutoff. NormalHero/GateHero retain their source lack of dynamic force response. Source merge collider sizes/offsets are authored per form, including the movable hero's merge geometry.
- Latest common-suite result: **858/858 passed**, `/tmp/morboo-part1-combat-final.xml`. Explicit authoring succeeded, `/tmp/chainrush-part1-content-authoring-final11.log`; registry reports two levels, four player definitions, six enemies, four skills and zero source errors. The full game PlayMode suite and FullUnitChain are running; no completion or load-budget claim is made yet.

Existing System Fit: AIBrain owns target eligibility and its optional observations; Skills owns timing resolution, carrier direction and admitted effects; Movement owns force state and the only resulting movement request; Spatial owns reservation/commit; CapabilityHost movement attachment owns the response registration and cleanup; Activity owns the simulation schedule and run feature lifetime. Source Unity controllers are read by authoring and isolated comparison tests, never by the new gameplay runtime.

Best Practices: use the same simulation clock for continuous effects and movement, as already done by `CapabilityHostSkillController.TryResolveTickSource`; use explicit attach/detach ownership, as in `CapabilityHostMovementController`; preserve optional authored object identity with managed references, as in `AIBrainStateData.OnTickActions`. These keep pause, serialization and cleanup behavior in their existing owners. Timing execution and estimation use the same `SkillPropertyResolver`.
