# physics_lab_controller_rename — Rename PhysicsLabController to reflect its production role

> **STATUS:** Queued (filed 2026-05-25 from `live_stat_provider_wiring` ARCHITECT_REVIEW follow-up). Tier 1 (Surgical) or Tier 2 (TellCode).

## One-line

`PhysicsLabController` is misleadingly named — it's the central per-shot controller used in **both** the physics lab scene **and** production gameplay (`LabScaffold` calls it via `BotDriver.PlayHoleToCup` and the auto-revert-to-driver at-rest logic). The name caused real architectural confusion during `live_stat_provider_wiring` Phase 3, where the `SetClub` injection-bypass bug was easy to miss because reviewers assumed "Lab" meant lab-only.

## Why

Three concrete pain-points from the prior task:

1. **Phase 2 review-pass missed the SetClub bypass bug.** The self-reviewer and architect both initially treated `PhysicsLabController.SetClub` as a lab-scoped call. The name primed them to skip it as production-irrelevant. Cost: one full review iteration.
2. **The lab-vs-prod split fix needed an `InjectLabBundleForCurrentClub()` method on `PhysicsLabController` itself** — a method named like that on a class named "Lab" reads as redundant, masking that the controller has a non-lab production path.
3. **Future reviewers / new contributors will hit the same trap.** Renaming once is cheaper than re-explaining the trap on every new spec that touches this class.

## Scope

1. **Rename the class** to a name that reflects its actual responsibility. Candidates (final pick during task):
   - `ShotOrchestrator` — emphasises the per-shot coordination role
   - `ClubShotController` — emphasises the per-club configuration
   - `GameplayShotDirector` — emphasises the gameplay-time orchestration
   - `ShotControllerHost` — emphasises that it hosts/wraps the per-shot state for `ShotController`
2. **Update all callers** — ~16 references across `Assets/Scripts/Physics/Viewer/`, `Assets/Scripts/Gameplay/`, `Assets/Scripts/UI/`. Use Rider/Unity refactor-rename (preserves GUIDs).
3. **Update scene references** — `PhysicsLab_Hole1.unity`, `ShellScene.unity`, `LabScaffold.unity`. Unity refactor-rename should handle these automatically via `m_Script` GUID, but verify each scene loads cleanly after the rename with zero "missing script" warnings.
4. **Update test files** — `PhysicsLabControllerLabVsProdTests.cs` should be renamed to `<NewName>LabVsProdTests.cs` and any `using` statements updated.
5. **Update doc references** — grep `Docs/` for `PhysicsLabController` mentions and update them in-place (postmortems, lessons, AI_CONTEXT, this SPEC's parent reviews).
6. **Update file name** — `PhysicsLabController.cs` → `<NewName>.cs`. Keep the same folder (`Assets/Scripts/Physics/Viewer/`) for now; the folder rename is a separate concern.

## Out of scope

- Splitting the class into separate Lab and Production controllers. The class is too tightly coupled for that to be a half-day task. If the audit reveals genuine separation pressure, file a follow-up.
- Renaming the folder `Assets/Scripts/Physics/Viewer/` (which is also lab-sounding). Folder rename has bigger blast radius on `.meta` files and asmdef paths; separate task.
- Touching `PhysicsLabUI.cs` — it really IS lab-only and the name is correct.

## Hard rules

1. **Behavior unchanged.** Pure rename. No method signature changes, no field changes, no namespace changes (unless the class's current namespace is also "Lab"-prefixed, in which case discuss before changing).
2. **Test gate green at baseline.** Today's baseline is 342/339/0/3 after `live_stat_provider_wiring` completion. Rename must not change this number.
3. **All three scenes load with zero missing-script warnings.** Verified by opening each scene in Unity after the rename.
4. **GUIDs preserved.** Unity refactor-rename keeps the same `m_Script` GUID for the `.cs` file; verify via `git diff` on `.meta` files (should be empty for the renamed file's `.meta`).

## Definition of done

- [ ] Class renamed; file renamed.
- [ ] All callers updated.
- [ ] All three scenes verified clean (no missing scripts, no `m_Script` GUID changes).
- [ ] Tests at or above 342/339/0/3.
- [ ] `Docs/AI_CONTEXT.md` updated with the new name in any references.
- [ ] `tasks/lessons.md` entry (~3 lines) noting the rename and the original naming-confusion postmortem from `live_stat_provider_wiring` Phase 3.
- [ ] Postmortem section in `live_stat_provider_wiring/ARCHITECT_REVIEW.md` cross-referenced to the new name.
