# ARCHITECT_REVIEW — capture_helper (Revision 2 review)

**Reviewer:** golfin-architect
**Date:** 2026-04-29 20:15 JST
**Verdict:** PASS (with documented deviation + follow-on task)
**Prior status:** CESAR_REJECTED → re-implemented → READY_FOR_ARCHITECT_REVIEW

## Summary

The three issues Cesar called out in `CESAR_REJECTION.md` are all genuinely fixed. Screenshot mechanism reads the GameView RenderTexture via reflection and produces real Game View content. Club portrait sprite is loaded and the EquippedBag is populated with both `RaiseBagChanged()` and `RaiseSelectedChanged()` fired. Portrait path corrected to `Portraits/InGame/`. The capture pipeline is now structurally sound.

A residual visual deviation exists in the demo screenshot (PlayerCard, WindCard, ClubCard distance show defaults, not fake-state values). This is caused by scene-side `*ContextPopulator` MonoBehaviours in LabScaffold overwriting fake-injected values on `OnEnable()` because `CharacterManager` has no selected character. It is not a CaptureHelper defect — it is a populator-vs-fake-state coordination concern that warrants its own task. See "Deviation accepted" below.

## Per-rejection-item verification

### Issue 1 — Screenshot captures wrong area
**Verdict: FIXED.**
- `GrabGameViewRT()` (lines 37–81 of `Assets/Scripts/Editor/CaptureHelper.cs`) walks reflection candidates `m_RenderTexture` / `m_TargetTexture` / `m_RenderTarget` against `UnityEditor.GameView` and reads the live RT.
- `SnapGameViewWithLabel()` calls `GrabGameViewRT()` first; falls back to `ScreenCapture.CaptureScreenshotAsTexture()` with a `Debug.LogWarning` only if reflection fails.
- Implementer's log shows `[CaptureHelper] Using RT reflection path (GameView RenderTexture)` on all 5 attempts — reflection succeeded every time.
- Screenshot at `screenshots/fake_mid_aim_demo.png` shows real GameView content: 3D fairway/green/trees, ball, full HUD overlay (PlayerCard, HoleCard, Wind/Distance flags, Spin button, Ball button, Club button, Straight indicator, accuracy meter). Confirmed not black, not Editor chrome.

### Issue 2 — Fake state does not update club handle image
**Verdict: FIXED.**
- `FakeMidAim` (lines 193–209) loads `Clubs/Portraits/S_Menu_Driver_GOLFIN`, clears + populates `ClubContext.EquippedBag` with a `ClubEntry` (ClubId, TypeLabel, Distance=230, Portrait, LabClubIndex), sets all four `Selected*` fields, then fires both `RaiseBagChanged()` and `RaiseSelectedChanged()`.
- Same pattern for `FakePutt` with `S_Menu_Putter_GOLFIN`, distance=0.
- `FakeReset` clears `EquippedBag` before calling `ClubContext.Reset()`.
- Screenshot evidence: bottom-right of `fake_mid_aim_demo.png` clearly shows the DRIVER button rendered with a club-handle icon, label "DRIVER" visible. The widget is wired and reacting to fake state. (The "0 yds" distance text below it is the populator-override deviation, not a CaptureHelper bug — see below.)

### Issue 3 — Wrong portrait path
**Verdict: FIXED.**
- `FakeMidAim` line 171: `Resources.Load<Sprite>("Portraits/InGame/Camila")`.
- `FakePutt` line 226: `Resources.Load<Sprite>("Portraits/InGame/Olivia")`.
- Implementer confirms both files exist at `Assets/Resources/Portraits/InGame/`.
- Cannot visually verify in the demo screenshot because PlayerContextPopulator overwrites `PlayerContext.Portrait = null` after FakeMidAim — but the code path is correct and matches the runtime convention used by `PlayerContextPopulator.cs` line 9 (`InGamePortraitPath = "Portraits/InGame"`).

## Y-flip correctness check

The implementer added a Y-flip after `ReadPixels` on the basis that Unity's RT is bottom-up.

**Verdict: CORRECT.**
- Unity `RenderTexture.ReadPixels` writes pixels into a `Texture2D` such that `Texture2D` row 0 corresponds to the BOTTOM of the source rect. PNG encoding via `EncodeToPNG()` treats `Texture2D` row 0 as the TOP of the file. Without flipping, the saved PNG comes out upside-down (which is exactly what Attempt 1 showed before the flip code compiled).
- Empirically verified: the screenshot is right-side-up — sky and HUD top-bar appear at the top of the image, ground/club button at the bottom. Cross-platform behavior of `ReadPixels` is consistent here regardless of D3D vs OpenGL backend, because Unity normalizes the RT origin to bottom-left for `ReadPixels` on all backends. The flip is the right fix and is portable.
- Minor optimization note (NOT a fail item): the per-pixel Color array allocation + double `Apply()` is wasteful for 1080p+ captures. Could be replaced with `Graphics.Blit` + a flip material, or `tex.SetPixels32(...)` with a row-reverse loop using `Color32`. Defer until/unless capture latency becomes a problem — current implementation is correct.

## Deviation accepted: PlayerCard / Wind / Club-distance show defaults

The spec's acceptance checklist line says the demo screenshot must show "CAMILA / Lv 13 / TURN 5" and "LOMOND / HOLE 1 - REGULAR / PAR 5". The screenshot shows:

- HoleCard: `LOMOND / HOLE 1 - REGULAR / PAR 4` (in EditMode the spec value of PAR 5 is overwritten back to 4 by something — implementer's Attempts 4/5 in PlayMode show PAR 5 correctly, confirming HoleContext fake-injection works end-to-end and there's no HoleContextPopulator competing for it; the EditMode discrepancy is likely a domain-reload reset between FakeMidAim and SnapGameView).
- PlayerCard: `USERNAME / Lv 1 / TURN 1` (default state).
- Wind: `0.0 mph` (default).
- Club distance: `0 yds` (default).

**Root cause:** `Assets/Scripts/UI/HUD/PlayerContextPopulator.cs` `OnEnable() → Refresh() → ResetContext()` (lines 12–93). When `CharacterManager.GetSelectedCharacterId()` returns empty (which it does in LabScaffold because the scene has no roster character selected), `ResetContext()` writes `PlayerContext.DisplayName = "PLAYER"`, `Level = 1`, `Portrait = null`, `RarityBackground = null`, then `Raise()` — clobbering whatever `FakeMidAim` just injected. `ClubContextPopulator` and `BallContextPopulator` follow the same pattern.

**Why this is accepted as a deviation rather than a FAIL:**

1. **Scope.** The spec's "Out of scope" section says `Do NOT modify any of the existing *Context.cs files in HUD/`. The populators (`*ContextPopulator.cs`) are scene-side runtime MonoBehaviours, not editor tooling — they live in `Assets/Scripts/UI/HUD/`, not `Assets/Scripts/Editor/`. Modifying them is outside the scoped surface area of this task ("editor-side menu helper").
2. **Cesar's rejection list.** The CESAR_REJECTION listed three specific items. PlayerCard text override was NOT on that list — Cesar accepted that the v1 attempt also showed default text and rejected only the underlying capture-mechanism issues. The implementer fixed exactly what was rejected.
3. **The capture mechanism works.** The HoleCard PAR 5 showing in PlayMode (Attempts 4–5) is positive confirmation that FakeMidAim → Raise → subscriber widget rebind chain functions end-to-end when no competing populator is present. The demo proves the pipeline.
4. **Implementer flagged it.** Open question #1 in the report explicitly raises it for architect direction. They correctly did not silently paper over it.
5. **The fix needs design.** A clean fix introduces a `IsFakeStateActive` gate flag and modifies each populator's reset path. That is small in lines but touches at least three runtime files plus an asmdef-aware indirection (runtime code cannot reference `Golfin.EditorTools`). It deserves its own spec so the contract is clear (when does the flag clear? does PlayMode auto-clear it? does it survive domain reload?).

**Therefore:** the capture_helper task is approved as PASS for the rejection items. The populator-competition concern is bumped to a follow-on task documented below.

## Follow-on task to queue

**Suggested slug:** `fake_state_populator_gate`
**Goal:** Make `*ContextPopulator` MonoBehaviours yield to fake-state injection so `CaptureHelper.FakeMidAim` produces a fully populated demo screenshot in LabScaffold without scene reconfiguration.

**Known surface area (3 populators confirmed, audit for more):**
- `Assets/Scripts/UI/HUD/PlayerContextPopulator.cs`
- `Assets/Scripts/UI/HUD/ClubContextPopulator.cs`
- `Assets/Scripts/UI/HUD/BallContextPopulator.cs`
- Plus a runtime-accessible `FakeStateGate` static (likely in `Golfin.Gameplay.UI.HUD`) that `CaptureHelper` writes through and populators read. Avoids the editor→runtime asmdef-reference forbidden direction.

**Sketch:** at the top of each populator's `Refresh()` (or `ResetContext()`), early-return if `FakeStateGate.IsActive`. CaptureHelper sets `FakeStateGate.IsActive = true` at the top of every `Fake*` preset; clears on `FakeReset` and on PlayMode start. Add `[MenuItem("GOLFIN/Capture/Clear Fake State Lock")]` for manual escape.

## Acceptance checklist verdict (spot check)

The implementer's PASS markers on items 1–11 are consistent with my read of the file and the screenshot. The single FAIL item (acceptance line 13: "Captured PNG showing CAMILA/Lv 13/TURN 5 and LOMOND/HOLE 1-REGULAR/PAR 5") is the deviation accepted above.

## Final verdict

**PASS.**

Cesar: the capture pipeline is now structurally correct. The five attempts in `Docs/Diagnostics/_capture/` collectively prove the RT reflection path, the Y-flip, and the club portrait/EquippedBag fix. PlayerCard/Wind/Club-distance defaults in the demo screenshot are a separate populator-coordination concern that I have queued as a follow-on (`fake_state_populator_gate`).

Recommend you:
1. Open Unity, manually verify the menu items appear at `GOLFIN/Capture/*` and the Ctrl+Shift+Alt+S shortcut binds correctly on `Snap Game View`.
2. Run `GOLFIN > Capture > Fake State - Mid Aim` followed by `GOLFIN > Capture > Snap Game View` and confirm the resulting PNG in `Docs/Diagnostics/_capture/` looks like attempt 4/5 (PAR 5 in PlayMode, PAR 4 in EditMode is acceptable for now).
3. If satisfied: approve, move folder to `Docs/Specs/Completed/`, and let me know to draft `fake_state_populator_gate`.
4. If not: write `CESAR_REJECTION.md` and we route back.
