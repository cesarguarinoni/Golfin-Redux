DONE

Approved by Cesar 2026-08-11 ("Done"). Implementation shipped in 4008b7f20.

Task: hole_scene_leftover_v3
Filed: 2026-08-10 (Architect)
iter-1: 2026-08-10 — Layer 1 + Layer 2 built; 11/11 acceptance items passed
iter-1 gates: SELF_REVIEW_PASS -> READY_FOR_REDTEAM -> ARCHITECT_REVIEW_ESCALATE (red-team)
Cesar scope decision: 2026-08-11 — "fix at source + relax guard"
iter-2: 2026-08-11 — implemented and verified

WHY iter-2 EXISTS. The red-team reproduced Cesar's exact symptom on the iter-1 candidate: a clean,
all-green suite run ending with ShellScene + Hole_06_Geo in the hierarchy. Root cause is a THIRD
vector the spec never named — PhysicsLabAutoRestore (Assets/Scripts/Editor/Physics/
PhysicsLabHolePicker.cs) auto-loads EditorPref Golfin.PhysicsLab.CurrentHole (= 6) whenever
LabScaffold is opened, including additively by EditMode fixtures, and never re-validated after its
deferral (observed injecting Hole_06_Geo next to ShellScene with no LabScaffold open at all,
Editor.log:64820-64826). That is why the leftover was ALWAYS Hole_06 and never a random hole from
the 18-hole sweep. Layer 1 remains correct — it just was not the vector producing the symptom.

iter-2 changes (Cesar authorised widening beyond the spec's file list):
  • PhysicsLabHolePicker.cs — restore only on OpenSceneMode.Single (the human / Hole-Picker path,
    not the fixtures' additive open); re-validate LabScaffold is still open after the deferral.
  • StagedHoleSceneGuard.cs — condition (d) narrowed to ShellScene only. LabScaffold + a clean
    non-active hole is the identical shape to a deliberate lab session; the guard closing it was a
    real workflow regression the red-team benched. Fixed at source instead.
  • StagedHoleSceneGuard.cs — editor-load sweep changed from a bare delayCall (which RACES Unity's
    post-InitializeOnLoad scene restore, and was observed LOSING that race, letting a leak survive
    a full kill/relaunch untouched) to an idle-settle pump on EditorApplication.update.

iter-2 verification, all re-derived:
  • guard R1 lab shape closed=0 (protected) / R2 leak shape closed=1 (swept)
  • source fix: additive open injects nothing; RestoreHole with LabScaffold open restores (workflow
    intact); RestoreHole without LabScaffold bails (the observed defect is dead)
  • two back-to-back suites: 1116 total — run 1: 1113 pass / 0 fail; run 2: 1112 pass / 1 fail
    (AudioEmitter flake, fails on any 2nd run of an editor session, pre-existing, filed separately).
    Both dumps: ShellScene only, HOLE_GEO_SCENES_OPEN=0
  • 0 auto-restores and 0 guard activity during the runs; 36 Layer-1 teardown closes (18 x 2)
  • acceptance #7 re-run end-to-end: kill -9 with ShellScene + Hole_06_Geo staged, relaunch, guard
    swept it at load (hook=editor-load) in a verifiably fresh session (log truncates on start)
  • zero .unity diffs throughout

Editor left clean: ShellScene only, not dirty, guard ENABLED, PhysicsLab pref untouched at 6.

iter-2 was approved directly by Cesar without re-running the subagent gate chain.
