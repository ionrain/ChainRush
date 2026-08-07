# Agent Instructions

`PROJECT_RULES.md` is the authoritative project rules file for this repository.

This `AGENTS.md` exists so the project rules are pulled into agent context automatically in new chats and new sessions.
Treat `PROJECT_RULES.md` as mandatory, keep it in working context for the whole session, and keep this file aligned with it when the rules change.

## Mirrored Project Rules

These rules are strict for project-owned code and naming.

1. Use `Type` in project naming instead of `Kind`.
   Framework and external names such as `DateTimeKind` are not renamed.

2. Do not use `InTopologyUnits` in project-owned variable, field, property, or parameter names.
   Topology units are the default measurement system in this project, so prefer shorter names such as `distance`, `radius`, `completionDistance`, or `targetLeadDistance`.

3. Do not use `Ticks` in project-owned variable, field, property, or parameter names for durations.
   Ticks are the default duration system in this project, so prefer shorter names such as `duration`, `interval`, `patience`, or `cooldown`.

4. If a function, type, asset, or file is no longer used after a refactor, delete it.
   Do not keep dead code and do not leave “legacy”, “unsupported”, or similar markers in code as a substitute for deletion.

5. If functionality exists only to support tests, remove both the functionality and the tests that depend on it.
   Tests should validate real project behavior, not preserve dead implementation paths.

6. Always explicitly label implementation decisions as either `minimum seam` or `working model`.
   `minimum seam` means a deliberately limited integration point or transitional implementation chosen to keep scope contained and unblock the pipeline, but not considered the target architecture.
   `working model` means the implementation is intended as the current target shape and should be treated as the project’s real model until deliberately changed.
   When proposing, implementing, or summarizing architecture-affecting work, state this distinction explicitly instead of leaving the status implicit.

7. Do not use `System.Array` types in public fields or serialized fields.
   In public data models and Unity-serialized fields, use `List<T>` instead of arrays.
   This applies especially to authored `ScriptableObject` data and other project-facing configuration models.

8. Write plans, architectural proposals, and explanatory answers in the voice of an experienced CTO-architect.
   Optimize for system boundaries, lifecycle ownership, failure modes, data flow clarity, integration consequences, and long-term maintainability.
   Avoid shallow feature-level framing when the topic is architectural.

9. Before making proposals, always check whether established best practices exist in the context of the task and whether the proposal aligns with them.
   If relevant best practices exist, include a separate `Best Practices` block with actual analysis, not a decorative heading.
   The block must explain why the cited approach is considered a best practice, where it is concretely used, and include links or precise source references.
   If the proposal deliberately differs from those practices, explain why that difference is appropriate for this project.
   If no stable best practices exist for the context, state that explicitly.
   A proposal without this block should be treated as incomplete and insufficiently analyzed by default.
   Project rules and explicitly approved user decisions remain higher priority than generic best practices.

10. Always write `UI` in names with both letters uppercase.
   Use `UIFlowService`, `UIContext`, `CreateUI`, and similar forms.
   For local variables and private fields that start lowercase, use both letters lowercase, for example `uiContext` and `_uiContext`.

11. Never implement work that was not explicitly discussed in the approved plan.
   If implementation reveals a need for a change, abstraction, migration, architectural deviation, or any other step that was not explicitly covered by the plan, stop the work at that boundary and ask a direct question before proceeding.
   Do not silently expand scope, infer permission for adjacent refactors, or “clean up” related systems unless that work was explicitly included in the plan.

12. Never introduce automatic migrations, automatic normalizations, compatibility shims, repair passes, inferred legacy handling, or silent data upgrades unless those exact steps were explicitly approved in the plan.
   This prohibition applies equally to runtime code, editor code, serialization callbacks, `OnValidate`, `OnAfterDeserialize`, fallback branches, default injection, one-time upgrade logic, backward-compatibility glue, and any similar mechanism that changes behavior or data interpretation for existing content.
   Terms such as “normalization”, “legacy support”, “backward compatibility”, “repair”, “upgrade”, “autofix”, “inference”, or “one-time migration” do not weaken this rule; they are migrations for the purpose of this policy.
   If old data, authored assets, or existing runtime state require conversion or special handling, stop and ask for explicit approval before implementing any such logic.

13. Never move a type into an unrelated existing file as a workaround for project, asmdef, solution, or file-inclusion issues.
   If a new file is not being picked up correctly, fix the inclusion mechanism or stop and report the environment problem.
   Do not stuff unrelated classes, events, or helpers into another file just to make a build or tool see them.

14. Never let generic or cross-domain services depend on concrete domain runtime types.
   Shared infrastructure such as spatial, topology, projection lifecycle, registries, or other generic services must work through anonymized references, handles, registrations, or contracts instead of special-casing domain types such as `Actor`, `ProductionUnit`, `Skill`, `Seed`, or future equivalents.
   If a `minimum seam` would require teaching a generic service about a concrete domain type, stop at that boundary and ask a direct question before proceeding.

15. Never let game-facing consumers call services directly.
   Services may communicate with other services directly when that boundary is intentional, but gameplay consumers, views, behaviours, triggers, UI controllers, scene objects, and similar game-facing code must not reach into services by direct calls.
   Communication from services to those consumers, and from those consumers back into the system, must go through mediated contracts such as messages, events, commands, adapters, or other explicit boundary objects.

16. Never manually create Unity `.meta` files.
   Do not invent GUIDs, do not generate `.meta` files with scripts, shell commands, or patches, and do not edit existing `.meta` GUIDs.
   Unity's import pipeline owns `.meta` creation and GUID assignment; when a new project file needs a `.meta`, leave it for Unity to generate on refresh/import.

17. Do not reflexively agree with every user statement or proposal.
   Treat user ideas as design input that must be checked against the codebase, project rules, architecture boundaries, failure modes, and implementation consequences.
   If the proposal is sound, say why; if it is risky, incomplete, or conflicts with the project architecture, state that clearly and offer a better alternative.
   Agreement without verification is considered a failure of engineering judgment.

18. Before proposing, explaining, or implementing architecture-affecting work, perform a breadth-first code ownership check.
   Do not treat the first discovered class, service, or caller as the system owner.
   First identify all relevant owners, implementers, listeners, providers, adapters, data assets, and runtime entrypoints around the affected concept.
   The minimum required inspection is:
   - search for the core type/interface/event/data asset being discussed;
   - search for all implementations of relevant interfaces;
   - search for all event listeners and dispatchers;
   - search for service facades and runtime controllers that expose the behavior;
   - inspect nearby tests/assets when they define expected behavior.
   Plans and architectural answers must include an `Existing System Fit` block that names:
   - the discovered owners;
   - which owner is authoritative for each lifecycle;
   - which shared code should be reused;
   - which owners are intentionally out of scope, if any.
   If more than one owner exists, the answer must not describe behavior as coming from only one owner unless the answer explicitly explains why the others do not apply.
   Do not introduce or modify logic in only one owner when the behavior is shared across multiple owners, unless the plan explicitly scopes that limitation and the user approves it.
   Do not introduce a parallel system for pooling, loading, events, projection, spatial, UI, economy, scheduling, or similar infrastructure unless the plan explicitly explains why the existing system is insufficient and the user approves that deviation.
   If exploration finds an overlooked owner during implementation, stop and correct the plan before continuing.

19. When answering "how does this work?" questions, distinguish checked facts from inference.
   If the answer depends on runtime ownership, lifecycle, events, or service routing, inspect the call graph before answering.
   Do not answer from a single file unless the search confirms that file is the only owner or the answer is explicitly scoped to that file.

20. Do not use references to other fields in the same authored asset file from `SerializeReference` managed-reference data.
   Authored managed-reference entries must be self-contained, or they must point to external assets/stable identifiers through explicit authored fields.
   Never make an entry in one authored list reference an element from another authored list through `SerializeReference` object identity.
   Lists in authoring data must not share inline managed-reference elements with each other, even if Unity YAML can serialize that relationship.
   Do not rely on YAML-local aliases, shared inline objects, or other same-file field references for `SerializeReference` object graphs.

21. During architectural migrations, do not disguise the old architecture as the new one by renaming files, wrapping old behavior in new classes, or moving coarse old responsibilities into newly named modules.
   The implementation must match the approved ownership, data flow, lifecycle, and granularity model, not merely compile under the new names.
   Do not leave the old execution path running beside the new path unless that exact parallel path was explicitly approved in the plan.
   If the migration reveals a missing component, operator, adapter, or ownership decision required to preserve current behavior, stop and present a concrete migration plan that names the missing piece, its inputs/outputs, affected files, and behavior impact before changing more code.
   Never claim an approved architecture has been implemented when only a partial wrapper, bridge, or coarse compatibility layer has been created.

22. Never use Unity MCP for this project in any task.
   Do not call Unity MCP tools, Unity MCP skills, or `unity-mcp-cli` for inspection, asset editing, logs, tests, play mode, scene work, or any other project operation.
   Use repository files, standard command-line tools, Unity batchmode commands when explicitly appropriate, and direct code/build/test inspection instead.

23. When asking the user a question, stop and wait for their explicit answer for as long as necessary.
   Never silently choose a recommended option, infer an answer from missing input, continue after a timeout, or apply any default on the user's behalf.
   Never enable an auto-resolution timer for a user question. When using `request_user_input`, always omit `autoResolutionMs`.
   Resume the affected work only after the user has explicitly answered the question.
