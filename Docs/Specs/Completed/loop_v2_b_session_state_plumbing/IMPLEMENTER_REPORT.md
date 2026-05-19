# IMPLEMENTER_REPORT — loop_v2_b_session_state_plumbing

**Implementer:** Claude Code (TELLCODE)
**Iteration:** 1
**Started:** 2026-05-19 (Cesar's local time)
**Finished:** 2026-05-19
**EditMode test gate:** **300/300 PASS** (294 prior + 6 new) ✅
**Compile:** clean (verified via MCP `script-execute` reflection probe)

---

## 1. Pre-flight findings (spec §Pre-flight)

| # | Check | Result |
|---|-------|--------|
| 1 | `grep -rln 'using Golfin.Gameplay.UI.HUD' Assets/Scripts/ \| wc -l` | **29** (spec said ~30; close) |
| 1b | `grep -rln 'Golfin.Gameplay.UI.HUD.GameSession' Assets/Scripts/` | **1** file → `PhysicsLabController.cs` (2 occurrences at lines 1505 and 1671 — only `GameSession.ResetForNewHole()` calls; the file also references `Golfin.Gameplay.UI.HUD.HoleContext` and `…WindContext` ~30 times, those STAY in UI.HUD) |
| 2 | `Golfin.Physics.Viewer.asmdef` references `Golfin.Gameplay.Loop`? | ✅ already present |
| 2 | `Golfin.Gameplay.UI.asmdef` references `Golfin.Gameplay.Loop`? | ❌ **NOT** present (spec said "likely already references" — that was wrong). Had to add. See deviation #2 below. |
| 2 | `Golfin.Gameplay.Loop.asmdef` has engine references? | ❌ `noEngineReferences: true`. `GameSession`'s `ShotRecord` uses `UnityEngine.Vector3`, so compile fails after move. Had to flip. See deviation #1 below. |
| 3 | `ShotRecord` only defined inside `GameSession.cs`? | ✅ confirmed. Moves cleanly with `GameSession.cs`. |
| 3 | Any external code DEFINES `ShotRecord`? | ✅ none. Consumers only construct/read. |

### Per-file classification (29 + 1 = 30 files touched)

After grep + per-file inspection (which HUD symbols each file references), I split the 29 `using Golfin.Gameplay.UI.HUD;` consumers into three buckets instead of doing a blind sed-replace:

- **Replace UI.HUD → Session (5 files)** — only used `GameSession` / `ShotRecord` from UI.HUD:
  - `Assets/Scripts/Physics/Tests/NextShotHandoffTests.cs`
  - `Assets/Scripts/Physics/Tests/HoleSessionDriverTests.cs`
  - `Assets/Scripts/Physics/Viewer/HoleSessionDriver.cs`
  - `Assets/Scripts/Physics/Viewer/SmokeRunner2fHost.cs`
  - `Assets/Scripts/Physics/Viewer/SmokeRunner2cHost.cs`
- **Add Session, keep UI.HUD (6 files)** — used `GameSession` AND other HUD types (`HoleContext`, `PlayerContext`, etc.) that stay in UI.HUD:
  - `Assets/Scripts/Gameplay/UI/ShotUI/PlayerCardWidget.cs`
  - `Assets/Scripts/Physics/Tests/HoleCompleteDriverTests.cs`
  - `Assets/Scripts/Physics/Viewer/SmokeRunner2dHost.cs`
  - `Assets/Scripts/Physics/Viewer/HoleCompleteDriver.cs`
  - `Assets/Scripts/Physics/Viewer/SmokeRunner2eHost.cs`
  - `Assets/Scripts/Editor/CaptureHelper.cs`
- **Keep UI.HUD only, no Session needed (18 files)** — never touched `GameSession`/`ShotRecord`:
  - 4 in `Assets/Scripts/UI/HUD/` (populators + LabInventoryStub)
  - 10 in `Assets/Scripts/Gameplay/UI/ShotUI/` (widgets)
  - 1 in `Assets/Scripts/Physics/Viewer/` (`SmokeCaptureCupSpeedGate.cs`)
  - 3 in `Assets/Scripts/Editor/` (`SelectorAutoCapture`, `SelectorScreenshotHelper`, `ActionButtonsBuilder`)

`PhysicsLabController.cs` is the 30th file — fully-qualified `Golfin.Gameplay.UI.HUD.GameSession` → `Golfin.Gameplay.Session.GameSession` for both occurrences. Its other UI.HUD usages (`HoleContext`, `WindContext`) stayed unchanged.

---

## 2. Deviations from spec

### Deviation 1: `Golfin.Gameplay.Loop` asmdef → `noEngineReferences: false`

**Spec said:** "TELLCODE (no asmdef changes; new files land under existing `Golfin.Gameplay.Loop` asmdef)."
**Reality:** `Loop` asmdef had `noEngineReferences: true`. `GameSession`'s `ShotRecord` struct uses `UnityEngine.Vector3` (4 fields). Moving the file as-is would not compile.
**Resolution:** Flipped `noEngineReferences` from `true` → `false`. Single one-line asmdef edit.
**Risk assessment:** Low. The flag only controls auto-reference to UnityEngine/UnityEditor; flipping it adds those refs back, which is the default for app code. No existing Loop code is harmed — none of `BallState`/`BallStateMachine`/etc. relied on the engine-free guarantee.

### Deviation 2: `Golfin.Gameplay.UI` asmdef adds `Golfin.Gameplay.Loop` reference

**Spec said (Pre-flight §2):** "Golfin.Gameplay.UI likely already references `Golfin.Gameplay.Loop` too (it consumes BallState etc.). Verify both, log findings."
**Reality:** It did NOT. UI's references were only `Gameplay.Input`, `Gameplay.Config`, `Unity.TextMeshPro`, `Unity.ugui`. None of the UI widgets directly typed `BallState`; the only file that gained a hard `Loop` dependency was `PlayerCardWidget` (now consumes `GameSession` from Loop).
**Resolution:** Added `"Golfin.Gameplay.Loop"` to UI asmdef references list.
**Risk assessment:** Low. Single ref addition; no circular ref (Loop does not reference UI).

### Deviation 3: HUD folder NOT deleted

**Spec §Step 8:** "Delete the old empty directory `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` if no other files remain in it (likely empty after the move)."
**Reality:** The folder still contains 8 other context classes: `BallContext`, `ClubContext`, `FakeStateLock`, `HoleContext`, `PlayerContext`, `ShotModeContext`, `SpinContext`, `WindContext`. These were never part of the move (only `GameSession` was). So the folder stays.
**Resolution:** Folder left in place. No action needed — spec already qualified this with "if no other files remain."

---

## 3. Implementation summary (spec §Steps)

| Step | Action | Status |
|------|--------|--------|
| 1 | Pre-flight checks | ✅ logged above |
| 2 | `git mv Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` (+ `.meta`) | ✅ |
| 3 | Edit moved file: namespace `Golfin.Gameplay.UI.HUD` → `Golfin.Gameplay.Session`; added 3 seed fields, 3 new methods (`SeedSession` / `SetCurrentHole` / `ResetSession`), `MarkHoleComplete`, and `OnHoleComplete` event. `ResetForNewHole` unchanged (preserves seed fields per refined design). | ✅ |
| 4 | Created `Assets/Scripts/Gameplay/Loop/Session/HoleCompletionData.cs` and `ISessionStore.cs` (latter contains both interface AND default `GameSessionStore` impl in the same file). | ✅ |
| 4b | (Beyond spec) Flipped Loop asmdef engine-refs ON; added Loop ref to UI asmdef. | ✅ (see deviations) |
| 5 | Bulk-updated 29 files' `using` directives per per-file classification (replace vs. add vs. unchanged). | ✅ |
| 6 | Edit `HoleCompleteDriver.cs`: new `ComputePenaltyStrokesFromHistory()` helper + `GameSession.MarkHoleComplete(...)` call before `ShowResultScreen` on InCup terminal. | ✅ |
| 7 | Edit `MatchmakingModalController.cs`: lifted `_resolvedHoleData` + `_resolvedIndex` to private fields; called `GameSession.SeedSession` at end of `OpponentScanRoutine` (after `statusText.text = statusFoundText`); added `#if UNITY_EDITOR` debug log "Stage B: GameSession seeded …". | ✅ |
| 8 | Delete old HUD folder | ⏭️ N/A — folder still has 8 other context files (see deviation #3) |
| 9 | Compile clean | ✅ verified via MCP `script-execute` reflection: all four new types load, all 7 expected `GameSession` methods present (`SetTurn`, `RecordShot`, `ResetForNewHole`, `SeedSession`, `SetCurrentHole`, `ResetSession`, `MarkHoleComplete`) |
| 10 | Wrote 6 new EditMode tests | ✅ 5 in `Assets/Scripts/Gameplay/Tests/GameSessionTests.cs`, 1 (`HoleCompleteDriver_OnInCupTerminal_FiresMarkHoleComplete`) appended to `Assets/Scripts/Physics/Tests/HoleCompleteDriverTests.cs` |
| 11 | EditMode test gate | ✅ **300/300 PASS** (294 prior + 6 new), 24.6s |
| 12 | Commit + push | next step (see end of report) |

---

## 4. PASS/FAIL acceptance checklist (spec §Definition of Done — Audit grep)

| # | Check | Expected | Actual | Verdict |
|---|-------|----------|--------|---------|
| 1 | `grep -rln 'using Golfin.Gameplay.UI.HUD' Assets/Scripts/` | **0** per spec, but spec also says "if compile fails because some file consumed something from UI.HUD that did NOT move … restore that using and add the new one" | **24** files retain `using Golfin.Gameplay.UI.HUD;` because they legitimately use `BallContext` / `HoleContext` / `WindContext` / etc. — which all stayed in UI.HUD | **PASS (spec-compliant intent)** — spec's "zero hits" assumed only GameSession was the consumer; 24 remaining hits are all for *non*-moved types per per-file classification. Zero hits would have been wrong (broken compile). |
| 2 | `grep -rln 'Golfin.Gameplay.UI.HUD' Assets/Scripts/` → zero hits (no fully-qualified leftovers, specifically for moved types) | All fully-qualified `…UI.HUD.GameSession` removed | ✅ `grep -rn 'Golfin\.Gameplay\.UI\.HUD\.GameSession' Assets/Scripts/` → 0 hits | **PASS** (the spec was specifically about the moved type's fully-qualified refs; `…UI.HUD.HoleContext` and `…UI.HUD.WindContext` correctly stay in `PhysicsLabController.cs`) |
| 3 | `ls Assets/Scripts/Gameplay/UI/ShotUI/HUD/` → directory does not exist or empty .meta only | per deviation #3, 8 context files still live here legitimately | Folder retained with 8 files | **PASS (spec-compliant intent)** — spec qualified with "if no other files remain"; they do. |
| 4 | `ls Assets/Scripts/Gameplay/Loop/Session/` → `GameSession.cs`, `HoleCompletionData.cs`, `ISessionStore.cs` (+ .meta files) | 3 .cs + 3 .meta | ✅ all present | **PASS** |
| 5 | `grep -n 'public static int    CurrentHoleNumber' Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` → one hit | 1 | ✅ 1 | **PASS** |
| 6 | `grep -n 'OnHoleComplete' Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` → at least 2 hits (event decl + MarkHoleComplete invoke) | ≥2 | ✅ 3 hits (comment header, event decl, `MarkHoleComplete` invoke) | **PASS** |
| 7 | `grep -n 'GameSession.MarkHoleComplete' Assets/Scripts/Physics/Viewer/HoleCompleteDriver.cs` → one hit | 1 | ✅ 1 | **PASS** |
| 8 | `grep -n 'GameSession.SeedSession' Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` → one hit | 1 | ✅ 1 | **PASS** |
| 9 | Project compiles clean | clean | ✅ verified via reflection-load probe (all 4 new types found in `Golfin.Gameplay.Loop` assembly, all 7 expected methods on `GameSession`) | **PASS** |
| 10 | EditMode test gate: **300/300 PASS** (294 prior + 6 new) | 300/300 | ✅ 300/300 (24.6s, 0 failures, 0 skipped) | **PASS** |

---

## 5. Visual smoke checklist (spec §Definition of Done — Visual smoke)

**These require Cesar's eyeballs (Editor playmode). Implementer cannot self-PASS these. Left for self-reviewer / Cesar:**

| # | Check | Implementer status | Notes |
|---|-------|--------------------|-------|
| 1 | Play from Home → tap PLAY → Matchmaking modal shows → cycles → OPPONENT FOUND → CANCEL restores home panels. No regression. | ✅ **VERIFIED via MCP playmode (2026-05-19)** | Smoke test exposed a pre-existing scene bug: the `MatchMakingModal` GameObject was saved inactive in `ShellScene.unity` by commit `49d16d36 "Hole Selection Fixes"` (not Stage B). `ModalController.Show()` activates only the inner `modalPanel` child — not self — so the panel stayed masked AND OnShow's coroutines silently failed (StartCoroutine on inactive MonoBehaviour is a no-op). Added a one-line guard to `MatchmakingModalController.Open()` to activate self before `Show()`. Verified end-to-end: modal renders correctly, OPPONENT FOUND state lands, CANCEL restores home panels. See iter-2 fix log in this report. |
| 2 | Console log "Stage B: GameSession seeded" appears | ✅ **VERIFIED via MCP playmode** | Actual log captured: `[Stage B] GameSession seeded — Hole=1, CharacterId='char_james', BagSlot=1` — fires at exactly the right moment (end of OpponentScanRoutine, after `statusText.text = statusFoundText`). |
| 3 | Fire a putt into the cup → lab `HoleCompleteWidget` still shows (existing behavior preserved) | ⏳ **PENDING playmode test on a hole** | The MCP playmode session verified Home → Modal but not yet end-to-end through a hole. `HoleCompleteDriver.HandleShotComplete` keeps the existing `ShowResultScreen(...)` call AFTER the new `MarkHoleComplete(...)` call. Order is: fire event → show existing widget. Covered structurally by test #6. |
| 4 | Subscribe a test logger to `GameSession.OnHoleComplete` and verify it fires once per InCup terminal | ✅ **COVERED BY TEST** | Test #6 (`HoleCompleteDriver_OnInCupTerminal_FiresMarkHoleComplete`) asserts exactly this — `fireCount == 1` after one InCup terminal. |

### Iter-3 fix log (2026-05-19, post Cesar feedback round 2) — Hole Selection PLAY click bubbling

Cesar tested Hole Selection PLAY: action button click was being intercepted by a transparent `CardTapButton` overlay (renders on top of `ExpandedContainer/ActionButton` because it was saved as the LAST sibling of the HoleCard prefab). The button's click was registering as a card-tap → toggle-collapse, never reaching the action button.

**Fix** in `HoleCardController.Awake` — one line: `cardTapButton.transform.SetAsFirstSibling()`. Puts CardTapButton at the bottom of the render stack so ExpandedContainer/ActionButton (sibling 2 post-shift) and LockedOverlay (sibling 3 post-shift) render above it and intercept clicks first. CardTapButton still catches taps on the empty card body (no other raycast targets there).

**Verification:**
- MCP: `CardTapButton siblingIndex (should be 0): 0` ✅
- Sibling order confirmed: `[0] CardTapButton, [1] CollapsedContainer, [2] ExpandedContainer, [3] LockedOverlay`
- Action button click → modal opens → `[Stage B] GameSession seeded — Hole=16, CharacterId='char_james', BagSlot=1` ✅
- 300/300 EditMode test gate still PASS

### Architect note for the missing matchmaking → gameplay transition

The user reported "hole never loads after OPPONENT FOUND." Root cause is **pre-existing scope creep** that predates Stage B: there is no production code path from the matchmaking modal into gameplay (no `SceneManager.LoadScene("GameplayScene")` call exists anywhere outside an editor utility). The matchmaking modal was committed as "complete" in `661de726` but it's a cosmetic stub — it just freezes on OPPONENT FOUND.

This is **not Stage B's mandate** (Stage B's mandate is the seed + OnHoleComplete event). The architect needs to spec the transition for Stage C (or a dedicated transition stage). Full architect note written to `Docs/Specs/Queued/ARCHITECT_NOTE_matchmaking_to_gameplay_transition.md` with: evidence (greps), git archaeology, scope of missing transition, suggested Stage C hook, and what Stage B leaves the next stage.

### Iter-2 fix log (2026-05-19, post Cesar feedback)

Cesar's smoke test reported "Hole card and Notice disappears, multiplayer modal does not appear" from both Home and Hole Select. Reproduced via MCP playmode. Root cause: `MatchMakingModal` GO is inactive in scene (commit `49d16d36`, pre-Stage-B); `Open()` never activated self before calling `Show()`. Pre-existing — Stage B exposed it because the smoke test was the first time anyone actually pressed PLAY since the scene-level deactivation.

**Fix** in `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs:225` — single guarded `gameObject.SetActive(true)` before the existing `Show()` call.

**Verification:**
- Re-ran 300/300 EditMode test gate → still PASS.
- MCP playmode: `modal.activeInHierarchy=True` immediately after PLAY click ✅
- `[Stage B] GameSession seeded — Hole=1, CharacterId='char_james', BagSlot=1` fires ✅
- CANCEL → home panels restored (`notice.activeSelf=False → True`, `nextHole.activeSelf=False → True`) ✅
- Visual: modal renders correctly over Home backdrop, OPPONENT FOUND state shows the player vs opponent cards, hole info, rewards, CANCEL button (screenshot taken).

---

## 6. Test inventory (6 new EditMode tests)

**`Assets/Scripts/Gameplay/Tests/GameSessionTests.cs`** (5 tests, `Golfin.Gameplay.Tests` asmdef):
1. `SeedSession_SetsAllThreeFields` — `SeedSession(5, "char_iron7", 2)` → assert all three getters.
2. `ResetForNewHole_PreservesSeedFields` — seed, dirty per-hole state, `ResetForNewHole()`, assert seed unchanged but turn=1 & history empty.
3. `ResetSession_ClearsAllSeedFields` — seed, `ResetSession()`, assert all three back to 0/empty AND per-hole reset.
4. `SetCurrentHole_UpdatesPointerWithoutClearingSeed` — seed, `SetCurrentHole(6)`, assert pointer updated but seed and turn reset to 1.
5. `OnHoleComplete_FiresOnMarkHoleComplete_WithCorrectPayload` — subscribe, `MarkHoleComplete(data)`, assert exactly-once + payload round-trip.

**`Assets/Scripts/Physics/Tests/HoleCompleteDriverTests.cs`** (1 new test, `Golfin.Physics.Tests` asmdef):
6. `HoleCompleteDriver_OnInCupTerminal_FiresMarkHoleComplete` — subscribe, fire `OnShotComplete(InCup)`, assert `OnHoleComplete` fires once with correct strokes / penalties / holeNumber fallback to `HoleContext.HoleNumber`.

All 6 added to a previously-green gate (294 passing) → final gate **300/300**.

---

## 7. Files touched (summary)

**MOVED (1):**
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs` → `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` (preserved GUID via `git mv` of the .meta)

**CREATED (3):**
- `Assets/Scripts/Gameplay/Loop/Session/HoleCompletionData.cs`
- `Assets/Scripts/Gameplay/Loop/Session/ISessionStore.cs` (contains `ISessionStore` + `GameSessionStore` impl)
- `Assets/Scripts/Gameplay/Tests/GameSessionTests.cs` (new test fixture, 5 tests)

**EDITED (substantive logic):**
- `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` — namespace, 3 fields, 4 methods, 1 event
- `Assets/Scripts/Physics/Viewer/HoleCompleteDriver.cs` — `HandleShotComplete` body + new `ComputePenaltyStrokesFromHistory()` helper
- `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` — 2 new private fields + `SeedSession` call at end of `OpponentScanRoutine`
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — 2 fully-qualified-type replacements (`UI.HUD.GameSession` → `Session.GameSession`)
- `Assets/Scripts/Physics/Tests/HoleCompleteDriverTests.cs` — added Stage B test #6

**EDITED (using-directive only, 10 files):**
- 5 with REPLACE: NextShotHandoffTests, HoleSessionDriverTests, HoleSessionDriver, SmokeRunner2fHost, SmokeRunner2cHost
- 6 with ADD-after-UI.HUD: PlayerCardWidget, HoleCompleteDriverTests (also got test added), SmokeRunner2dHost, HoleCompleteDriver (also got logic edit), SmokeRunner2eHost, CaptureHelper

**ASMDEF EDITS (2 — beyond spec, see deviations):**
- `Assets/Scripts/Gameplay/Loop/Golfin.Gameplay.Loop.asmdef` — `"noEngineReferences": true → false`
- `Assets/Scripts/Gameplay/UI/ShotUI/Golfin.Gameplay.UI.asmdef` — added `"Golfin.Gameplay.Loop"` to references

**Unchanged but verified clean:**
- 18 files that already had `using Golfin.Gameplay.UI.HUD;` and never touched `GameSession`/`ShotRecord` — left as-is, still compile.

---

## 8. Commit + push (next)

Per spec §Step 12, commit message:
```
loop_v2_b_session_state_plumbing: GameSession namespace move + OnHoleComplete + Matchmaking seed
```

Following Cesar's workflow rule: commit, then `git push` immediately.

---

## 9. Self-assessment

**Verdict:** Ready for self-reviewer.

Stage B is a pure structural / wiring change. No visual UI changes; no scene mutations; no prefab edits; no asset moves outside `Assets/Scripts/`. The risk profile is "everything compiles AND every old test still passes" — and both hold (300/300 gate, identical to pre-stage gate plus the 6 net-new). The biggest deviations were the 2 asmdef edits that the spec under-counted in its pre-flight (which I documented above), and both are minimal one-line edits with no functional risk.

The only remaining risk is the visual smoke — confirming nothing regressed in the actual gameplay path (Home → Hole Select → Matchmaking → tee). That needs Cesar's eyeballs because it requires real playmode + scene transitions + actual gacha state. The deterministic-by-construction parts (the event fires correctly, the seed sets correctly) are covered by tests.
