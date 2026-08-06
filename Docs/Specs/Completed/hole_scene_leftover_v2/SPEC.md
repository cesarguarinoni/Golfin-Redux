# SPEC — hole_scene_leftover_v2

**Tier:** 2 — editor tooling only, no runtime/player code. Log + manual-repro gated.
**Priority:** P2 (daily editor friction for Cesar). **Status:** SPEC_READY.
**Figma:** N/A.
**Handoff file:** `Docs/Specs/Active/hole_scene_leftover_v2/SPEC.md`
**Kickoff:** `Use the implementer subagent on "hole_scene_leftover_v2"`

---

## 1. Why

Cesar report (2026-08-06): Hole_06_Geo still pops up alongside every scene, AFTER the 2026-08-05
`hole_scene_leftover` fix (CaptureSceneSetup + wiring into SmokeRunner2e/2f + VersusHudCaptureMenu).

Root cause of the recurrence — **the fix resurrects the leftover it was built to remove**:

1. `CaptureSceneSetup.Capture()` snapshots the CURRENT editor scene setup with **no filter** for
   `Hole_NN_Geo` entries. A leftover Hole_06_Geo (from the pre-fix era) that is open when any fixed
   launcher starts gets recorded as "pre-run setup".
2. On exit, `Restore()` closes staged hole scenes — then `RestoreSceneManagerSetup(payload)`
   **re-opens Hole_06_Geo** because it's in the payload.
3. Next run's `Capture()` snapshots it again → permanent cycle. SmokeRunner2f's defensive
   pre-clean can't break it: the sweep runs AFTER `Capture()` (comment in SmokeRunner2fMenu.cs:33-36
   confirms the order), so it cleans the RUN but the restore puts the leftover back afterwards.

Secondary gaps, same family:

4. **`LoopV2SmokeBotMenu` was never wired** to CaptureSceneSetup at all. `Launch()` opens ShellScene
   (Single) with no restore — the user's pre-run hierarchy is silently replaced by ShellScene after
   every smoke run (not a hole leak, but the same "run eats my hierarchy" defect class).
5. `LoopV2SmokeBotMenu.LaunchDirectLab()` stages LabScaffold(Single) + Hole_NN_Geo(Additive) in edit
   mode and its ExitingPlayMode handler restores nothing — a guaranteed hole-scene leak.
   **Verified 2026-08-06: it currently has ZERO callers** (grep `LaunchDirectLab` → only the
   definition). It is a loaded trap for the next task that calls it, and is very likely how the
   original Hole_06_Geo leftover was born (file declares `Hole06GeoPath`; cup_capture_and_lipout
   clips are Hole-6-calibrated).

---

## 2. Scope

### In
- `CaptureSceneSetup.Capture()`: **exclude `Hole_NN_Geo` scenes from the snapshot** (they are staged
  content by definition; the file's own contract says nothing in it may ever persist one).
- `CaptureSceneSetup.Restore()`: **skip `Hole_NN_Geo` entries when rebuilding the SceneSetup** —
  defence in depth against stale snapshots already sitting in SessionState from before this fix.
- `LoopV2SmokeBotMenu`: wire `CaptureSceneSetup.Capture(Key)` at the top of BOTH `Launch()` and
  `LaunchDirectLab()` (before any OpenScene), and `CaptureSceneSetup.Restore(Key)` in the existing
  `OnPlayModeStateChanged` handler — gated on a SessionState cleanup flag, mirroring
  SmokeRunner2fMenu's pattern (arm the flag at launch, not later).
- Shared helper for the name test: `static bool IsHoleGeoScene(string nameOrPath)` — one
  implementation, used by Capture, Restore, and `CloseStagedHoleScenes` (which currently duplicates
  the StartsWith/EndsWith test).

### Out (do NOT do)
- No change to SmokeRunner2e/2f or VersusHudCaptureMenu wiring (they already Capture/Restore; the
  CaptureSceneSetup filter fixes them transitively).
- Do NOT remove `LaunchDirectLab` (future tasks may need it) — fix it in place.
- Do NOT reorder 2f's defensive sweep vs Capture — with the filter, order no longer matters.
- No runtime (`Golfin.Physics.Viewer` non-Editor) code. No scene file edits, ever — the standing
  contract "nothing may write a Hole_NN_Geo scene" extends to this whole diff.

---

## 3. Grounding (verified this session)

- `Assets/Scripts/Physics/Viewer/Editor/CaptureSceneSetup.cs` — `Capture()` records every entry of
  `EditorSceneManager.GetSceneManagerSetup()` unfiltered; `Restore()` → `CloseStagedHoleScenes()`
  then `RestoreSceneManagerSetup(payload)` re-opens any hole entry in the payload. Hole-name test
  duplicated in `CloseStagedHoleScenes` (`StartsWith("Hole_") && EndsWith("_Geo")`).
- `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` — `Launch()` (ShellScene Single,
  no snapshot/restore), `LaunchDirectLab()` (~line 564; Lab Single + geo Additive, no
  snapshot/restore, zero callers today), `OnPlayModeStateChanged` ExitingPlayMode branch (recorder
  End + DisableSceneReload restore only). `[DidReloadScripts]` re-registration already exists —
  reuse it; do not add a second handler.
- Callers with Capture/Restore already correct: SmokeRunner2eMenu.cs (:58, :79, :184-188),
  SmokeRunner2fMenu.cs (:36, :153-156), VersusHudCaptureMenu.cs (:325, :379, :418, :1239).

---

## 4. Design

### 4.1 CaptureSceneSetup filter

```csharp
static bool IsHoleGeoScene(string nameOrPath)
{
    string n = System.IO.Path.GetFileNameWithoutExtension(nameOrPath ?? "");
    return n.StartsWith("Hole_") && n.EndsWith("_Geo");
}
```

- `Capture()`: `if (IsHoleGeoScene(s.path)) { Debug.Log($"[CaptureSceneSetup] Excluding staged hole scene from snapshot: {s.path}"); continue; }`
  — BEFORE the untitled-scene check consumes the entry. If filtering leaves ZERO entries (user had
  only a hole scene open — degenerate), erase the key and log, same as the empty-setup path today.
- `Restore()`: same skip when rebuilding `setup` from the payload (stale pre-fix snapshots).
- `CloseStagedHoleScenes()`: switch its inline test to `IsHoleGeoScene(s.name)` (behaviour identical).

### 4.2 LoopV2SmokeBotMenu wiring

- `const string SetupKey = "LoopV2SmokeBotMenu.SceneSetup"; const string CleanupKey = "LoopV2SmokeBotMenu.Cleanup";`
- `Launch()` and `LaunchDirectLab()`, FIRST statements after the isPlaying guard:
  `CaptureSceneSetup.Capture(SetupKey); SessionState.SetBool(CleanupKey, true);`
  (arm-at-launch, per SmokeRunner2fMenu's stranded-snapshot rationale).
- `OnPlayModeStateChanged`, `EnteredEditMode` branch (ADD — currently only EnteredPlayMode and
  ExitingPlayMode are handled):
  ```csharp
  else if (state == PlayModeStateChange.EnteredEditMode)
  {
      if (!SessionState.GetBool(CleanupKey, false)) return;
      SessionState.SetBool(CleanupKey, false);
      CaptureSceneSetup.Restore(SetupKey);
      Debug.Log("[LoopV2SmokeBotMenu] Run cleaned up: staged scenes closed, scene setup restored.");
  }
  ```
  Restore at EnteredEditMode (not ExitingPlayMode) — scene operations during ExitingPlayMode are
  unsafe; this matches the pattern the other three launchers already use.
- NOTE: `Restore()` also runs `CloseStagedHoleScenes()` unconditionally, so even a no-snapshot run
  still sweeps leftovers — this retroactively cleans the existing Hole_06_Geo on the first
  smoke run after this ships.

---

## 5. Traps

- **Never save a hole scene.** `CloseScene(s, true)` without save is the only permitted operation on
  them (existing contract in CaptureSceneSetup header). Nothing in this diff calls SaveScene except
  the pre-existing `StripSerializedHost` (untouched).
- **Don't double-restore:** LoopV2's new EnteredEditMode branch must be gated on its OWN CleanupKey —
  the handler fires for every play-mode exit including runs launched by OTHER menus.
- **Untitled-scene snapshots:** Capture() already refuses setups containing untitled scenes. Keep
  that behaviour AFTER the hole filter (a hole entry must be filtered, not trigger the refusal).
- **SessionState keys are per-launcher** — do not reuse SmokeRunner2eMenu's key names.
- `LaunchDirectLab` has no callers today; after wiring, a compile is the only proof it gets — the
  manual gate below uses `Launch()`.

---

## 6. Acceptance / Gates (manual, in-editor — no video gate)

1. **Resurrection cycle broken:** open any user scene + Hole_06_Geo additively (simulate the
   leftover) → run a SmokeRunner2f capture → after exit: Hole_06_Geo is GONE and the user scene is
   restored alone. Repeat once more to prove it stays gone (the old behaviour reproduced on every
   run). Console shows the new "Excluding staged hole scene from snapshot" line on run 1 only.
2. **LoopV2 hierarchy restore:** open any user scene → run "GOLFIN > Smoke > Loop v2 > Settings
   Round Trip" → after exit, the user scene is back (today: ShellScene replaces it). Cleanup log
   line present.
3. **Stale-snapshot defence:** hand-write a SessionState payload containing a Hole_06_Geo entry
   under a launcher's SetupKey → Restore skips it (no hole scene reopened, no warning spam).
4. **git status clean:** zero `.unity` diffs after all of the above.

---

## 7. Handoff

- Touch list: `Assets/Scripts/Physics/Viewer/Editor/CaptureSceneSetup.cs` (filter + shared helper),
  `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` (Capture/Restore wiring + new
  EnteredEditMode branch). Nothing else.
- Kickoff: `Use the implementer subagent on "hole_scene_leftover_v2"`
