# SPEC — `capture_helper`

> Authoritative spec. Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

See `STATUS.md`. Currently `SPEC_READY`.

## Goal

Eliminate Code's recurring "screenshot timing" failures by replacing the unreliable `ScreenCapture.CaptureScreenshot` async path with a synchronous editor-side helper, and by adding fake-state menu shortcuts so UI verification doesn't need playmode at all. End state: Code never has to think about pause/unpause ordering or `WaitForEndOfFrame` — a single menu item or static method does the right thing in every situation.

## Background — why this exists

Code has shipped multiple visually-broken UI tasks (8.3 attempt 1, parts of 8.4) where the root cause traced to screenshot timing, not code logic. Three Unity-engine constraints conspire:

1. `ScreenCapture.CaptureScreenshot(path)` is **async** — it queues the write for the next end-of-frame. If Code reads the file immediately it gets the previous capture (or nothing).
2. `WaitForEndOfFrame` **does not fire while paused.** Captures queued during pause silently never complete. Confirmed Unity bug; not fixable in user code.
3. Buttons, animations, and event-driven state changes don't tick during pause — so Code can't pause-then-interact-then-capture, but also can't capture-while-running because the state changes too fast.

The fix is tooling, not training. Give Code a deterministic synchronous capture path + state-injection helpers; remove the unreliable path from Code's mental model via a hard rule in CLAUDE.md.

## Reference

- Unity docs: `ScreenCapture.CaptureScreenshotAsTexture` (synchronous, returns `Texture2D` in the same frame the call is made — no `WaitForEndOfFrame` required when called from EditMode after a forced repaint).
- Unity bug: `WaitForEndOfFrame` does not fire when paused (issuetracker — multiple confirmations 2016 → 2022).
- Existing static busses already in `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` — these are the injection points for fake state.

## Architecture context

- **New file:** `Assets/Scripts/Editor/CaptureHelper.cs` — editor-only utility, lives in `Assembly-CSharp-Editor` (no asmdef needed; the `Editor/` folder placement handles it).
- **Asmdef boundaries:** `CaptureHelper` references `Golfin.Gameplay.UI.HUD` namespace (auto-referenced — works because `Golfin.Gameplay.UI` is `autoReferenced: true`).
- **Existing static busses to drive (all in `Golfin.Gameplay.UI.HUD`):**
  - `PlayerContext` — DisplayName, Level, Portrait
  - `HoleContext` — HoleNumber, Par, ChampionshipYards, CourseName, TeeName, GreenCentroidWorld, PinWorld
  - `WindContext` — SpeedMph, DirectionDegrees
  - `GameSession` — TurnCount (call `SetTurn(int)`)
  - `BallContext` — SelectedBallId, SelectedNameLabel, SelectedQuantityDisplay, SelectedThumbnail, SelectedFullSprite, OwnedBalls, SelectedIndex
  - `ClubContext` — (read file before faking; mirror BallContext shape)
  - `ShotModeContext` — (read file before faking)
  - `SpinContext` — (read file before faking)
- **Asset paths for default sprites:**
  - Portrait default: `Resources/Portraits/Thumbnails/Camila.png` (load via `Resources.Load<Sprite>("Portraits/Thumbnails/Camila")`)
  - Hole map default: `Assets/Art/In-Game UI/HoleMaps/Lomond - Hole 1.png` (load via `AssetDatabase.LoadAssetAtPath<Sprite>(...)` — editor-only, that's fine here)
  - Ball thumbnails: TBD when BallContext gets wired with real assets — leave fake to use `null` Sprite for v1, populator widget should fall back to a default.

## Implementation

### Part A — `CaptureHelper.cs` (the synchronous capture path)

Create `Assets/Scripts/Editor/CaptureHelper.cs`:

```csharp
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Synchronous screenshot helper. Replaces ScreenCapture.CaptureScreenshot (async, unreliable).
    /// Use SnapGameView from EditMode or paused playmode. Use SnapAtEndOfFrameAndPause from coroutines.
    /// </summary>
    public static class CaptureHelper
    {
        const string OUT_DIR = "Docs/Diagnostics/_capture";

        // ────────────────────────────────────────────────────────────────────────
        // PRIMARY PATH — call this from EditMode, paused playmode, OR running playmode.
        // Returns the absolute path of the written PNG. Synchronous. Always works.
        // ────────────────────────────────────────────────────────────────────────
        [MenuItem("GOLFIN/Capture/Snap Game View %#&s")] // Ctrl+Shift+Alt+S
        public static string SnapGameView()
        {
            return SnapGameViewWithLabel("snap");
        }

        public static string SnapGameViewWithLabel(string label)
        {
            Directory.CreateDirectory(OUT_DIR);
            string path = $"{OUT_DIR}/{label}_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";

            // Force GameView to repaint — required when paused, harmless when running.
            var gvType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gvType != null)
            {
                var gv = EditorWindow.GetWindow(gvType);
                if (gv != null) gv.Repaint();
            }

            // Synchronous capture from the GameView render target.
            // Unlike ScreenCapture.CaptureScreenshot(path), this does NOT require WaitForEndOfFrame
            // and works while paused.
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.Refresh();
            Debug.Log($"[CaptureHelper] Wrote {path}");
            return Path.GetFullPath(path);
        }

        // ────────────────────────────────────────────────────────────────────────
        // For coroutines that need to capture mid-animation, then freeze.
        // CRITICAL: this captures FIRST, pauses AFTER. Never the other way around.
        // ────────────────────────────────────────────────────────────────────────
        public static IEnumerator SnapAtEndOfFrameAndPause(string label)
        {
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(OUT_DIR);
            string path = $"{OUT_DIR}/{label}_f{Time.frameCount}.png";

            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            EditorApplication.isPaused = true;
            AssetDatabase.Refresh();
            Debug.Log($"[CaptureHelper] Wrote {path} and paused");
        }
    }
}
```

### Part B — Fake state preset menu items

Append to the same file (or split into `CaptureHelper.FakeStates.cs` partial class — Implementer's call). One menu item per scenario. Each one populates ALL relevant contexts and calls `Raise()` on each so subscriber widgets refresh.

**Important rule the Implementer MUST follow when adding new presets in the future:** every `[MenuItem("GOLFIN/Capture/Fake State - X")]` MUST end by writing the populated context values to the Console with a single Debug.Log line, so it's obvious what was injected. Format: `[FakeState:X] Player=CAMILA Lv13 Hole=Lomond#1 Par5 Wind=8mph@270 Turn=5 Ball=GOLFIN Club=Driver Mode=Aim Spin=Center`.

**Preset list — implement all of these for v1:**

```csharp
using Golfin.Gameplay.UI.HUD;

public static partial class CaptureHelper
{
    [MenuItem("GOLFIN/Capture/Fake State - Reset All")]
    public static void FakeReset()
    {
        PlayerContext.Reset();
        HoleContext.Reset();
        WindContext.Reset();
        GameSession.SetTurn(1);
        BallContext.Reset();
        ClubContext.Reset();   // verify method exists; if not, set fields manually + raise
        ShotModeContext.Reset();
        SpinContext.Reset();
        Debug.Log("[FakeState:Reset] All contexts reset to defaults");
    }

    [MenuItem("GOLFIN/Capture/Fake State - Mid Aim (Camila, Lomond H1, Driver, GOLFIN ball)")]
    public static void FakeMidAim()
    {
        PlayerContext.DisplayName = "CAMILA";
        PlayerContext.Level       = 13;
        PlayerContext.Portrait    = Resources.Load<Sprite>("Portraits/Thumbnails/Camila");
        PlayerContext.Raise();

        HoleContext.HoleNumber        = 1;
        HoleContext.Par               = 5;
        HoleContext.ChampionshipYards = 425;
        HoleContext.CourseName        = "LOMOND";
        HoleContext.TeeName           = "REGULAR";
        HoleContext.Raise();

        WindContext.SpeedMph         = 8f;
        WindContext.DirectionDegrees = 270f; // wind from West
        WindContext.Raise();

        GameSession.SetTurn(5);

        // Ball: hardcode GOLFIN selection. Thumbnail/sprite null is OK — widgets fall back to defaults.
        BallContext.SelectedBallId          = "golfin";
        BallContext.SelectedNameLabel       = "GOLFIN";
        BallContext.SelectedQuantityDisplay = "∞";
        BallContext.RaiseSelectedChanged();

        // Club: TODO — once ClubContext exists with stable shape, set Driver here.
        // SetClubFake("driver", "DRIVER");

        // Shot mode: TODO — set to "Aim" once ShotModeContext fields are known.
        // Spin: TODO — set to center (0, 0) once SpinContext fields are known.

        Debug.Log("[FakeState:MidAim] Player=CAMILA Lv13 Hole=Lomond#1 Par5 425y Wind=8mph@270 Turn=5 Ball=GOLFIN");
    }

    [MenuItem("GOLFIN/Capture/Fake State - Putt (Olivia, Lomond H7, Putter)")]
    public static void FakePutt()
    {
        PlayerContext.DisplayName = "OLIVIA";
        PlayerContext.Level       = 7;
        PlayerContext.Portrait    = Resources.Load<Sprite>("Portraits/Thumbnails/Olivia");
        PlayerContext.Raise();

        HoleContext.HoleNumber        = 7;
        HoleContext.Par               = 4;
        HoleContext.ChampionshipYards = 380;
        HoleContext.CourseName        = "LOMOND";
        HoleContext.TeeName           = "REGULAR";
        HoleContext.Raise();

        WindContext.SpeedMph         = 0f;
        WindContext.DirectionDegrees = 0f;
        WindContext.Raise();

        GameSession.SetTurn(3);

        BallContext.SelectedBallId          = "golfin";
        BallContext.SelectedNameLabel       = "GOLFIN";
        BallContext.SelectedQuantityDisplay = "∞";
        BallContext.RaiseSelectedChanged();

        // Club = Putter (TODO when ClubContext stable)
        // ShotMode = Putt (TODO)

        Debug.Log("[FakeState:Putt] Player=OLIVIA Lv7 Hole=Lomond#7 Par4 Turn=3 Wind=0");
    }

    [MenuItem("GOLFIN/Capture/Fake State - Strong Wind (extreme indicator test)")]
    public static void FakeStrongWind()
    {
        WindContext.SpeedMph         = 25f;
        WindContext.DirectionDegrees = 135f; // SE
        WindContext.Raise();
        Debug.Log("[FakeState:StrongWind] Wind=25mph@135");
    }

    // ────────────────────────────────────────────────────────────────────────
    // STANDING RULE: when a new static-bus context is added to HUD/, add:
    //   1. A fake-write block here in FakeMidAim (and FakePutt if relevant)
    //   2. A new dedicated preset if the context has interesting variation worth testing
    //   3. A line in Reset above
    // See "Maintenance protocol" at bottom of this spec for the checklist.
    // ────────────────────────────────────────────────────────────────────────
}
```

**For the TODO blocks (ClubContext, ShotModeContext, SpinContext):** the Implementer reads each context file, identifies the writable static fields and any `Raise*()` methods, then fills in the blocks. If a context has no obvious "neutral" state, leave a `// TODO:` and surface to Architect — do NOT guess.

### Part C — `CLAUDE.md` rule update

Append a new section to `CLAUDE.md`. Find the appropriate spot (likely near other "session rules" or after the Multi-Agent Workflow section).

```markdown
## Screenshots — MANDATORY rules

Code's screenshot history is full of timing failures. These rules eliminate the common ones.

**Hard rules:**

1. **NEVER call `ScreenCapture.CaptureScreenshot(path)`.** It is async, unreliable, and silently fails when paused. Use `CaptureHelper.SnapGameView()` instead — it is synchronous and works in EditMode, paused playmode, and running playmode.

2. **NEVER pause before capturing.** The render loop stops emitting frames during pause, so any queued capture never completes. Always capture-then-pause, never pause-then-capture. `CaptureHelper.SnapAtEndOfFrameAndPause()` does this in the right order.

3. **For UI-only verification, do NOT enter playmode.** Use `GOLFIN > Capture > Fake State - <preset>` from the Editor menu (or call `CaptureHelper.FakeMidAim()` etc. from a `[MenuItem]` script), then `CaptureHelper.SnapGameView()`. The static-bus contexts (PlayerContext, HoleContext, etc.) make this work without any game loop running.

4. **For mid-animation verification,** start a coroutine that runs `yield return CaptureHelper.SnapAtEndOfFrameAndPause("label")`. Do NOT pause first.

5. **Output location.** All captures land in `Docs/Diagnostics/_capture/`. After capture, copy/rename the relevant one(s) into the task's `screenshots/` folder under `Docs/Specs/Active/<task>/screenshots/`. Don't litter the diagnostics folder with task-specific names.

**Quick reference:**

| Situation                              | Tool                                                 |
|----------------------------------------|------------------------------------------------------|
| UI layout check, no playmode needed    | Fake State preset → `SnapGameView()`                 |
| Verify scene contents in EditMode      | `SnapGameView()`                                     |
| Frozen moment from playmode            | `SnapAtEndOfFrameAndPause("label")` in coroutine     |
| Series of frames during animation      | Multiple `SnapGameViewWithLabel("step1"/"step2"/…)`  |
| `ScreenCapture.CaptureScreenshot(path)` | **DO NOT USE — banned by this project**             |

**Adding new fake-state presets:** when a new static-bus context is added under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`, the same task that adds it must (a) extend `CaptureHelper.FakeMidAim` to set sensible values for the new context, (b) extend `CaptureHelper.FakeReset` to call its `Reset()`, and (c) add a dedicated preset if the context has interesting variation. See `Docs/Specs/Active/capture_helper/SPEC.md` § Maintenance protocol.
```

### Part D — Subagent prompt updates (pipeline enforcement)

The `CLAUDE.md` rule covers Code's session-level behavior, but the multi-agent pipeline's reviewer agents have their own prompts that don't inherit it directly. Without these edits, future tasks could ship with `ScreenCapture.CaptureScreenshot` calls or skip the maintenance protocol unnoticed. Edit two files.

#### D.1 — `.claude/agents/golfin-self-reviewer.md`

Find the section titled `## Verification protocol` (it has Steps 1–4). After Step 4, add a new Step 5:

```markdown
### Step 5 — Capture-helper compliance check

Before writing any verdict, verify two compliance items related to `Docs/Specs/Active/capture_helper/SPEC.md`:

1. **Screenshot provenance.** The screenshot in `screenshots/` MUST have been generated via `CaptureHelper.SnapGameView()` or `CaptureHelper.SnapAtEndOfFrameAndPause()`. Check `IMPLEMENTER_REPORT.md` — the report should mention which capture method was used. If the report is silent on this OR cites `ScreenCapture.CaptureScreenshot` directly OR cites a manual OS-level screenshot tool, OVERRIDE-FAIL the screenshot's checklist item with reason "capture method not compliant with CLAUDE.md § Screenshots rules."

2. **Maintenance protocol for new contexts.** If the diff in this task adds ANY new `*Context.cs` file under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` (or any equivalent static-bus context elsewhere), confirm `Assets/Scripts/Editor/CaptureHelper.cs` was extended per `Docs/Specs/Active/capture_helper/SPEC.md` § Maintenance protocol — specifically: (a) `FakeReset` calls the new context's `Reset()`, (b) `FakeMidAim` sets sensible values for it, (c) the closing Debug.Log line in `FakeMidAim` mentions the new context's values. If any of (a)–(c) is missing, OVERRIDE-FAIL with verdict `BACK_TO_IMPLEMENTER` and reason "capture_helper maintenance protocol not followed for new context <name>."

These checks are non-negotiable. Even if every other item passes, missing capture-helper compliance is grounds for routing back.
```

#### D.2 — `.claude/agents/golfin-architect.md`

In Mode 2 (Final review), find the `Verify:` bullet list. Add a fifth bullet:

```markdown
- **Capture-helper compliance:** the self-reviewer should have checked Step 5 (screenshot provenance + maintenance protocol for new contexts). Verify their finding is correct — if they missed a non-compliant capture method or a missing fake-state extension, FAIL the task with reason "capture_helper protocol violation, see SPEC.md § Maintenance protocol." This is a backstop in case the self-reviewer waved it through.
```

Also, in Mode 1 (Spec authoring), find the numbered list of files to read first. Add a new item 4 (renumbering current 4 → 5, current 5 → 6):

```markdown
4. `Docs/Specs/Active/capture_helper/SPEC.md` § Maintenance protocol — if the new task introduces any static-bus context, the spec MUST include an explicit "extend CaptureHelper" implementation step.
```

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item PASS/FAIL with one-sentence justification.

- [ ] `Assets/Scripts/Editor/CaptureHelper.cs` exists, compiles cleanly, no errors in Console.
- [ ] `GOLFIN > Capture > Snap Game View` menu item appears and writes a PNG to `Docs/Diagnostics/_capture/snap_<timestamp>.png` from EditMode (no playmode).
- [ ] Same menu item works while playmode is paused (writes a fresh PNG, does not silently no-op).
- [ ] Same menu item works during running playmode (writes a fresh PNG of the live frame).
- [ ] `GOLFIN > Capture > Fake State - Reset All` resets all 8 contexts and logs the reset line.
- [ ] `GOLFIN > Capture > Fake State - Mid Aim (...)` populates Player/Hole/Wind/Turn/Ball, all subscriber widgets visibly refresh in the Game View, Debug.Log line printed.
- [ ] `GOLFIN > Capture > Fake State - Putt (...)` populates the alternate scenario correctly.
- [ ] `GOLFIN > Capture > Fake State - Strong Wind` updates only Wind context, log line printed.
- [ ] `Ctrl+Shift+Alt+S` shortcut invokes Snap Game View (verify by checking the menu shows the binding).
- [ ] ClubContext / ShotModeContext / SpinContext: either (a) included in fake presets with sensible values + log line entries, or (b) marked `// TODO:` with a clear comment explaining what the Implementer needs from Architect.
- [ ] `CLAUDE.md` updated with the new "Screenshots — MANDATORY rules" section, placed in a logical spot (note where in the report).
- [ ] `.claude/agents/golfin-self-reviewer.md` updated with Step 5 (capture-helper compliance check) inserted after the existing Step 4.
- [ ] `.claude/agents/golfin-architect.md` updated: Mode 2 Verify list gets the new bullet; Mode 1 file-reading list gets the new item 4 with renumbering.
- [ ] Captured PNG from `FakeMidAim → SnapGameView` (in `LabScaffold` scene with the 8.3 widgets present) attached to `screenshots/fake_mid_aim_demo.png` in this task folder, showing PlayerCard reading "CAMILA / Lv 13 / TURN 5" and HoleCard reading "LOMOND / HOLE 1 - REGULAR / PAR 5".
- [ ] Spec deviations (if any) flagged at bottom of report.

## Files this task touches

- `Assets/Scripts/Editor/CaptureHelper.cs` — NEW.
- `Assets/Scripts/Editor/CaptureHelper.cs.meta` — NEW (Unity auto-generates).
- `CLAUDE.md` — append "Screenshots — MANDATORY rules" section.
- `.claude/agents/golfin-self-reviewer.md` — add Step 5 to Verification protocol.
- `.claude/agents/golfin-architect.md` — add Verify bullet (Mode 2) + file-read item (Mode 1).
- `Docs/Diagnostics/_capture/.gitkeep` — NEW empty file so the folder exists pre-commit.
- `Docs/Specs/Active/capture_helper/screenshots/fake_mid_aim_demo.png` — verification artifact.

## Out of scope

- Do NOT modify `BallSimulation.cs` or anything in `Physics/Core/`.
- Do NOT modify any of the existing `*Context.cs` files in HUD/. The fake helper is read-only against them (it writes their public statics, but does not change the type definitions).
- Do NOT add new MCP tools or modify the multi-agent pipeline routing — this is purely an editor-side menu helper.
- Do NOT add fake states for contexts that don't yet exist. If 8.5/8.6/8.7 introduce new contexts later, those tasks own extending the fake presets (per the maintenance protocol below).
- Do NOT delete or rename `Docs/Diagnostics/phase-8/8.3/topbar-diff-v3.png` or other historical captures.

## Maintenance protocol (READ — applies to all future tasks)

When ANY future task introduces a new static-bus context under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` (or any equivalent Context pattern elsewhere), that same task MUST:

1. **Extend `CaptureHelper.FakeReset`** to include the new context's `Reset()` (or equivalent field clears + Raise).
2. **Extend `CaptureHelper.FakeMidAim`** to set sensible non-default values for the new context, so the demo scenario remains visually complete.
3. **Update `CaptureHelper.FakeMidAim`'s closing Debug.Log line** to include the new context's values.
4. **Add a dedicated preset** if the new context has interesting variation worth isolating (e.g., the Strong Wind preset for WindContext).
5. **Note the addition in the task's `IMPLEMENTER_REPORT.md`** under a "Capture helper updates" bullet.

The Architect MUST flag missing fake-state extensions during architect-review when reviewing tasks that add new contexts. This is a standing review checkpoint, not a per-task rule.

## Notes for the Architect (post-implementation)

After Code lands this, update `Docs/Architecture/RUNTIME_BLUEPRINT.md`:

- Add a §5 "Editor tooling" section (or extend §4 Asset Locations) that documents `CaptureHelper.cs` + the fake-state pattern + the maintenance protocol.
- Cross-reference from §2 (where the Context pattern is described) → "see §5 for fake-state injection helper".
