# MolarMap agent guide

## Mission

Build an offline Windows tool that derives unfound NG+ Milk Molar markers from authoritative Grounded save records. Never infer a spawn or state from landmarks, icon colors, proximity, or candidate-list subtraction.

## Non-negotiable invariants

- Reconstruct selected spawns before resolving collection state.
- Use spawn GUIDs as identity and parse GUID plus X/Y/Z atomically.
- Unproven state is `Unknown`; unproven formats are `Unsupported`.
- Normal rendering requires `Validated` confidence and `Uncollected` state.
- Keep decoding, parsing, state resolution, projection, and UI separate.
- Never commit private save fixtures.

## Working loop: plan, act, verify

### Plan

1. Read `docs/BUILD_SPEC.md`, `docs/ROADMAP.md`, and the relevant entries in `docs/DECISIONS.md`. Inspect the affected code, tests, and working-tree changes before choosing an implementation. Preserve unrelated user changes.
2. State the requested outcome and observable acceptance criteria. Separate proven facts from assumptions and identify which invariants the change touches. For format work, identify the authoritative record evidence; missing evidence must retain `Unknown` or `Unsupported` behavior.
3. Share a short, ordered plan naming the affected components, next concrete action, and verification needed. Keep planning proportional: a small edit can use one sentence; multi-step work should track pending, active, and completed steps. Ask for clarification only when an unresolved choice prevents correct progress; continue independent work when possible.
4. Identify independent work that can run in parallel and apply the subagent rules below. Include ownership and integration dependencies in multi-agent plans.

### Act

1. Execute bounded plan steps in dependency order; independent steps may run in parallel under the subagent rules below. For each understood binary structure, first add a regression check and confirm it fails for the expected reason. Use synthetic fixtures or locally supplied private fixtures; never commit private saves or decoded payloads.
2. Implement the smallest evidence-backed change while preserving the separation of decoding, parsing, state resolution, projection, and UI. Do not widen accepted signatures merely to make a failing fixture pass.
3. Inspect the resulting diff and run the focused behavioral checks appropriate to that step. Update plan status with observed evidence, not intended behavior.
4. If evidence contradicts the plan or a check fails, identify the cause, revise the remaining steps, and repeat the affected checks after the fix. Do not repeat an unchanged failing action without new evidence. Resolve unrelated failures separately and report any that remain.

### Verify and finish

1. Run `pwsh -File scripts/Test-Repo.ps1` after changes. Never invoke `dotnet` directly for the standard verification loop: the script isolates NuGet and .NET state inside the workspace so restricted environments do not read the user-level NuGet configuration. Include visual or interactive verification when UI behavior changes.
2. Compare the final diff and verification results with every acceptance criterion and the non-negotiable invariants. Distinguish passed checks, failed checks, and skipped or unavailable private-fixture checks; a skipped check is not evidence of success. Build success alone is not regression success.
3. Record newly proven format decisions in `docs/DECISIONS.md` and update `docs/ROADMAP.md` when milestone status changes. Do not record assumptions as proven format facts.
4. Report the outcome, material changes, verification results, and remaining limitations. Mark work complete only when the acceptance criteria are met; otherwise state the exact remaining blocker and the evidence or action needed to resolve it.

Planning does not introduce an approval gate. Continue authorized, reversible implementation and verification without waiting for plan approval.

## Parallel subagent work

- Use subagents proactively when a task contains two or more independent, meaningful workstreams and parallel work will reduce completion time or improve review coverage. Keep small or tightly coupled changes with one agent. Useful splits include independent code investigation, implementation in separate components with agreed interfaces, and read-only review of invariants or regression coverage.
- The primary agent owns the plan, task boundaries, integration, and final report. Give each subagent a bounded objective, relevant context and invariants, explicit file ownership or read-only scope, dependencies, acceptance criteria, and expected verification. All agents follow this guide and preserve unrelated changes.
- Respect available concurrency limits. Keep dependent steps sequential and continue useful independent work while subagents run. Do not delegate the same implementation to multiple agents or recursively expand the team without agreement from the primary agent.
- Agents share a working tree. Assign one writer per file at a time, especially `tests/GroundedMolar.Tests/Program.cs`, `AGENTS.md`, and shared documentation. Read-only review may overlap implementation, but reviewers must identify the revision or file state reviewed. Coordinate ownership transfers before editing; never overwrite or revert another agent's changes.
- Serialize builds and tests that share outputs. The primary agent schedules `scripts/Test-Repo.ps1`, which uses the shared `artifacts/verification` directory; subagents must coordinate before running it or other commands that mutate shared build state. Read-only inspection can run in parallel.
- Require each subagent to return changed files, findings with evidence, checks run and their results, assumptions, and unresolved issues. A subagent's completion message alone does not prove correctness. The primary agent inspects the integrated diff, resolves conflicting findings against authoritative evidence, and runs final verification after all contributing edits finish.
- If workstreams collide, an interface changes, or evidence invalidates a task, pause or redirect the affected work and revise the plan before continuing. Finish or stop delegated work before reporting completion; include any remaining blockers and skipped checks in the final report.

## Verification rule

- Always check your work after making changes. Run the relevant build, tests, static checks, and visual or behavioral verification appropriate to the change before reporting completion.

## Current boundary

Grounded World profile v1 is implemented and fixture-validated for selected NG+ molar records and persistent actor state. The WPF app is limited to save picking, recursive folder monitoring, image preview, and manual refresh. Any changed record signature must fail closed as `Unsupported` until a new profile is proven.
