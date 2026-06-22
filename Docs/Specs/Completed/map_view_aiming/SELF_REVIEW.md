# Self-Review — `map_view_aiming` (Order 352) — iter-15

**Reviewer:** golfin-self-reviewer
**Iteration:** 15
**Date:** 2026-06-19 ~12:35 JST
**Verdict:** **FORWARD_TO_ARCHITECT** (READY_FOR_ARCHITECT_REVIEW)

iter-14 was a full PASS except for a single architect-flagged blocker: the flag-pin spawn used `UnityEditor.AssetDatabase.LoadAssetAtPath` wrapped in `#if UNITY_EDITOR`, which silently strips the flag in player builds (re-introducing Defect 6). iter-15's stated scope is exactly that one fix. This review is therefore SCOPED to (a) verifying the fix is correct and runtime-safe, (b) confirming the flag is still visible, (c) confirming nothing regressed.

## Visual diff notes (Step 1 — pixels only)

**Canonical still (`screenshots/iter15/canonical_map_open_iter15.png`, 1170×2532):** I am looking at a top-down-tilted aerial map of a golf hole. The fairway curves down-left to up-right; the background outside the hole is filled with dense forest (clusters of tree crowns on the left and bottom-right). A bright **cyan guide line** runs from the lower-left edge upward to the upper-left, terminating at a small white-ball icon near a pale-green oval (the green) in the upper-left quadrant — the line is clearly bent (Fade/Draw armed). Three roughly horizontal **semi-transparent grey-white ground bands** lie across the fairway between the ball start (off-bottom) and the green — these are foreshortened ovals consistent with ground-conforming annuli viewed at the hero tilt; their labels read **"80%"**, **"100%"**, **"120%"** in white text from bottom to top, sitting on the fairway. In the **lower-right quadrant**, very close to the "100%" label, a clearly visible **vertical red-and-white striped pole** stands upright on the fairway — this is the flag pin (Flag.fbx instance, NOT a disc or sphere). A **dark navy "SHOOT" button** with the diamond club icon sits in the bottom-right corner; no other Shot-UI chrome is visible (action-row hidden as spec'd). No upside-down elements, no torn UI, no white-box placeholders.

## Step 2 — Reference comparison

No Figma reference (SPEC §0: "No Figma — reference is the previous-implementation screenshot `reference_old_ui.jpg`"). Comparison is against (a) the iter-14 canonical still and the architect's iter-14 PASS verdict for everything except the runtime-safe-flag blocker, and (b) the spec's acceptance criteria 1–10. Nothing in this visual differs from iter-14 except for the underlying flag spawn mechanism, which is what was being fixed.

## Step 3 — Spec checklist walk (scoped)

Per architect instruction this is a scoped re-verification of the one fix + a no-regression check, NOT a full re-litigation.

| Item | Status | Notes |
|---|---|---|
| **1. Runtime-safe flag (THE FIX)** | **CONFIRM-PASS** | See Step 4 below — full mechanism quoted + verified. |
| **2. Flag visible (red+white striped pole, NOT disc)** | **CONFIRM-PASS** | Canonical still: red-and-white striped pole clearly visible in lower-right quadrant adjacent to the "100%" label. Pole is vertical, multi-stripe (≥3 visible alternating segments), consistent with Flag.fbx geometry scaled 18×. Not a disc, not a sphere. |
| **3. Y-flip 0/957 in captioned video** | **CONFIRM-PASS** | Independent L2 detector across all 957 frames flagged 2 candidates (frames 127, 417); on visual inspection both are **scene-transition cuts** (logo→black at 127, "NOW LOADING" pro-tip→hole-loaded at 417), not actual vertical-mirror flips. Detector false-positives are expected on hard cuts where one frame is near-uniform. **Effective y-flips: 0/957.** Implementer's repair step worked. |
| 4a. Map continuous (no flip in main content) | CONFIRM-PASS | Frames 700, 900 sampled — clean upright HUD, no flip. |
| 4b. Fire shown / ball flies / heat blob / bent guide / white labels / semi-transparent rings / guide-on-top | CONFIRM-PASS | All visible in canonical (cyan bent guide ON TOP of ground bands, white 80/100/120% labels, semi-transparent ground bands). |
| 4c. Heading delta 0° (criterion 5) | CONFIRM-PASS | `history.log` cites `CRITERION 5b FINAL DELTA: 0.00 deg — PASS`. Not re-litigated this iter — architect accepted in iter-14. |
| **5a. Physics tripwire (HARD GATE)** | **CONFIRM-PASS** | `git diff --stat HEAD -- Assets/Scripts/Physics/` returns only `PhysicsLabController.cs` (54 ++/--, all listed in iter-15 HEARTBEAT kickoff baseline DIRTY block). No iter-15-introduced edits under `Assets/Scripts/Physics/`. |
| **5b. Rule 11 ButtonPressFeedback on HoleMap (HARD GATE)** | **CONFIRM-PASS** | `git diff Assets/Scenes/Physics/LabScaffold.unity` includes `+m_EditorClassIdentifier: Assembly-CSharp::Golfin.UI.Polish.ButtonPressFeedback` alongside the new HoleMap interaction MonoBehaviour. |
| **5c. LabScaffold additive-only (HARD GATE)** | **CONFIRM-PASS** | `git diff --stat Assets/Scenes/Physics/LabScaffold.unity` = `120 ++, 0 --` (pure additive, 1 file changed). No `m_IsActive: 0` flips, no `sizeDelta`/position drift on existing GameObjects. |
| **5d. 15 EditMode tests (HARD GATE)** | **CONFIRM-PASS** | `grep -cE '^\s*\[Test\]' MapViewAimingTests.cs` = 15. Implementer reports 15/15 PASS, 0.104s. Not re-run this iter (no code under test changed; only the runtime-safe flag spawn path which has no test). |
| **5e. Real-input capture (HARD GATE)** | **CONFIRM-PASS** | Capture is ShellScene → LabScaffold additive → Hole_01_Geo additive, real bot input, no bespoke `*Gate` scenario per architect waiver carried from iter-14. |
| **6. Housekeeping — only iter-15 captioned canonical + raw/repaired in videos/** | **CONFIRM-PASS** | `ls videos/` shows exactly 3 files: `map_view_aiming_iter15_captioned.mp4`, `map_view_iter15_raw.mp4`, `map_view_iter15_repaired.mp4`. iter-13 and iter-14 captioned MP4s are gone. (Stale `screenshots/iter3..iter14/` subdirs remain — not architect-asked to clean, not blocking.) |

## Step 4 — Runtime-safe flag verification (the heart of this iter)

### Grep audit

```
$ grep -n "UnityEditor\|AssetDatabase\|#if UNITY_EDITOR" Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs
409:                // iter-15 RUNTIME-SAFE FLAG (fixes #if UNITY_EDITOR player-build gap):
410:                // Instead of AssetDatabase.LoadAssetAtPath (editor-only), we find the in-scene
1347:        // any AssetDatabase or #if UNITY_EDITOR dependency — works in player builds.

$ grep -n "using UnityEditor" Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs
(no matches)

$ grep -n "using UnityEngine.SceneManagement" Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs
7:using UnityEngine.SceneManagement;
```

All three `UnityEditor`/`AssetDatabase`/`#if UNITY_EDITOR` hits are inside `//` comment lines (409–410 describing the iter-15 fix; 1347 documenting the helper). **Zero runtime code references the UnityEditor namespace.** The `using UnityEditor;` import is removed. The new `using UnityEngine.SceneManagement;` import is present (line 7). This compiles in player builds.

### Runtime spawn mechanism (lines 414–425)

```csharp
GameObject inSceneFlagGO = null;
for (int si = 0; si < SceneManager.sceneCount; si++)
{
    Scene s = SceneManager.GetSceneAt(si);
    if (!s.IsValid() || !s.isLoaded) continue;
    foreach (var root in s.GetRootGameObjects())
    {
        var found = FindDescendantByName(root.transform, "Flag");
        if (found != null) { inSceneFlagGO = found.gameObject; break; }
    }
    if (inSceneFlagGO != null) break;
}

if (inSceneFlagGO != null && _flagWorldPos != Vector3.zero)
{
    var flagGO = GameObject.Instantiate(inSceneFlagGO, markerRoot.transform);
    flagGO.name = "FlagMarker";
    flagGO.transform.position   = _flagWorldPos;
    flagGO.transform.localScale = Vector3.one * 18f;
    flagGO.SetActive(true);
    flagGO.layer = 0;
    foreach (var child in flagGO.GetComponentsInChildren<Transform>(true))
        child.gameObject.layer = 0;
    _flagMarker = flagGO.transform;
}
```

Mechanism breakdown:
- `SceneManager.sceneCount` + `GetSceneAt(i)` — iterates all loaded scenes (ShellScene, LabScaffold, Hole_NN_Geo additively-loaded). Player-build-safe.
- `Scene.GetRootGameObjects()` — runtime API. Player-build-safe.
- `FindDescendantByName(root.transform, "Flag")` — recursive name walk (helper at line 1348, mirrors PhysicsLabController.FindDescendantByName pattern). Player-build-safe.
- `GameObject.Instantiate(inSceneFlagGO, markerRoot.transform)` — clones the live in-scene Flag GO. Player-build-safe.
- 18× scale + position at `_flagWorldPos` (= `HoleContext.PinWorld`) — same visual size that worked in iter-14.
- Destroyed on close: the entire `markerRoot` is destroyed on `Close()`, so this child goes with it. (Cleanup path unchanged from iter-14.)

**No editor-only API anywhere on the runtime path.** The fix is correct.

### Flag visible — frame citation

- **Canonical still:** `screenshots/iter15/canonical_map_open_iter15.png` — red+white striped vertical pole clearly visible in the lower-right quadrant of the map, adjacent to the "100%" white label. Multiple alternating red/white stripe segments are individually resolvable. This is the Flag.fbx geometry, NOT a placeholder sphere or yellow disc. (The pale-green oval near the top-left is the putting-green grass, not a marker.)
- **Video frame:** approximately t≈24s in the captioned video (the "map open + bent guide" caption segment) — same visual content as the canonical still.

## Step 5 — Capture-helper compliance check

Not applicable in the strict sense: this task ships a video deliverable via `MapViewCaptureBotMenu`, which is a task-specific capture driver carried over from earlier iters and architect-accepted in iter-14. No new `*Context.cs` was added in iter-15 — the only code change is the flag spawn body inside `MapViewController.cs` + the `FindDescendantByName` helper + import swap. Capture-helper maintenance protocol does not apply.

## Step 6 — Bbox geometry verification

No new containment claims in iter-15 (no "X inside Y" claim was made in the implementer report). The visible-pole verification in Step 4 is a single-element visibility check, not a containment check. Bbox check not required.

## Step 7 — Scene-mutation audit

```
$ git diff --stat HEAD -- Assets/Scenes/Physics/LabScaffold.unity
 Assets/Scenes/Physics/LabScaffold.unity | 120 ++++++++++++++++++++++++++++++++
 1 file changed, 120 insertions(+)
```

**Pure additive (120 +, 0 −).** No `m_IsActive: 0` flips. No `sizeDelta` or position changes on existing GameObjects. Captured frames 700 and 900 (post-map-close and next-turn) show full Shot-UI restored — no leftover deactivations, no scene corruption.

## Step 8 — Production-flow capture check

Capture is over a real loaded hole via `ShellScene → BeginGameplayLoad → LabScaffold additive → Hole_01_Geo additive` (real boot path), with real bot input. No `*Gate` scenario, no direct LabScaffold loadSceneAsync, no camera-fighting. Architect explicitly waived the bespoke-scenario gate in iter-14 (carried for this iter — the implementer did not change the capture path).

## Summary of overrides

None. Every implementer-claimed PASS was independently verified or accepted under architect's iter-14 carry. The only items that needed live independent verification were (a) the runtime-safe flag (Step 4 — verified by grep + code read), (b) the y-flip count (Step 3 row 3 — verified by independent L2 detector + visual disambiguation of the 2 false-positives), and (c) the hard gates (Step 3 rows 5a–e — verified by git diff stat + grep).

## Verdict

**FORWARD_TO_ARCHITECT.** The single architect-flagged iter-14 blocker (editor-only flag spawn) is fixed correctly: no `UnityEditor` / `AssetDatabase` / `#if UNITY_EDITOR` on the runtime path, in-scene-copy via `SceneManager` + `FindDescendantByName` + `GameObject.Instantiate`, builds in player. The flag is visible (red+white striped pole, lower-right of canonical still). Independent L2 detector confirms 0 effective y-flips across all 957 frames. No regressions detected (map continuous, fire shown, heat blob, bent guide, white labels, ground rings, guide-on-top, heading delta 0°, 15 EditMode tests, hard gates all green, additive-only scene diff, real-input capture).

Set STATUS to `READY_FOR_ARCHITECT_REVIEW`.
