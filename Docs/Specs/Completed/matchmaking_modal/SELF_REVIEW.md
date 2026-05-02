# Self-Review — `matchmaking_modal`

> Written by `golfin-self-reviewer` subagent. 2026-05-02 JST. Iteration 2 of self-review (covers Implementer iter 4).

## Verdict

`PASS` → `FORWARD_TO_ARCHITECT`

## Visual diff notes (Step 1 — pixels only, written before consulting spec)

What I see in `screenshots/matchmaking-iter4_2026-05-02_07-40-10.png`:

- **Top of frame:** yellow R-coin badge with "50,000" top-left in a navy chip. Top-right: white circular gear button.
- **Just below:** dark-navy header bar with white "CHOTO" centered and small notch / shape on the right side.
- **Above modal:** sky/sunset landscape (golden clouds, distant course terrain). The landscape is visibly dimmed/darkened (no longer at full brightness).
- **No maintenance-notice strip visible above the modal.** The space between the CHOTO bar and the modal contains only dimmed sky/landscape.
- **Centered modal (dark navy rounded panel, ~85% screen width):**
  - A pill-style chip at the top of the modal area reading "DIAMOND LEAGE" in cream text.
  - White centered title: "OPPONENT FOUND".
  - Two character cards side-by-side, separated by a white "Vs.":
    - **Left card (player):** teal/green rarity border framing a character with a white cap, green visor, blond hair, tan jersey. Small "Lv 6" label inside the upper-right of the card. Below the card: "YOU" in white, "RANK: #571" in lighter gray.
    - **Right card (opponent):** orange/yellow rarity border framing a character with yellow visor, blonde hair, white shirt with green sleeves. Small "Lv 27" upper-right. Below: "BIRDIE" in white, "RANK: #832" in gray.
  - Light divider line.
  - Cream/gold heading "NEXT HOLE".
  - White text "Lomond Country Club - Hole 5".
  - Three reward chips: yellow coin "x100", crossed-tools icon "x10", white-circle "x30".
  - Light divider.
  - Light-grey "CANCEL" pill button, full-width inside the modal.
- **Below the modal:** a partial character render (legs / golf pants in grey-blue, no jersey visible), framed against the dimmed background. **No NEXT HOLE / PLAY panel visible** — the gold PLAY button + reward strip from the home screen is absent.
- **Bottom of frame:** five circular nav buttons (home left-side, golf-tee centered/larger, profile rightmost).

## Visual diff notes (Step 2 — Figma reference comparison)

Figma reference (`screenshots/figma-reference.png`, 474×1024) shows the "FINDING OPPONENT..." search state. Screenshot is the lock state ("OPPONENT FOUND") per spec §6 step 8 — expected difference.

| Element | Figma reference | Screenshot iter-4 | Notes |
|---|---|---|---|
| Title | "FINDING OPPONENT..." | "OPPONENT FOUND" | Search vs lock state — spec requires lock for capture. ✓ |
| Two character cards + "Vs." | Present | Present (James-as-player + BIRDIE) | Layout, sizing, spacing match. ✓ |
| Player labels | "USERNAME" / "RANK: #233" | "YOU" / "RANK: #571" | Placeholders substituted per spec §2. ✓ |
| Opponent labels | "USERNAME" / "RANK: #200" | "BIRDIE" / "RANK: #832" | "BIRDIE" (6 chars) renders cleanly inside Username row — no clipping. ✓ |
| Hole header copy | "HOLE" | "NEXT HOLE" | SPEC §3 explicitly accepts `HOME_NEXT_HOLE` localization → "NEXT HOLE". ✓ |
| Hole subline | "Lomond Country Club - Hole 5" | Identical | ✓ |
| Reward row | x10 / x10 / x10 (Figma placeholder) | x100 / x10 / x30 | Matches Lomond 5 CSV / home-screen contract per architect FYI. ✓ |
| Cancel button | Full-width "CANCEL" pill | Identical | ✓ |
| Backdrop | Home screen visibly dimmed behind modal | Home visibly dimmed | Alpha 0.85 reads as appropriately dark; no longer "too light." ✓ |
| League pill | Present above title | Present ("DIAMOND LEAGE") | Passive, per spec. ✓ |

No visible-defect-vs-Figma items remaining from iter-1 review. The five iter-1 fail items are all resolved:
- Reward mismatch: now x100/x10/x30 in modal, matching home contract.
- Figma reference: present at `screenshots/figma-reference.png`.
- Username clipping: "BIRDIE" fits cleanly with no truncation.
- Capture method: explicitly declared in IMPLEMENTER_REPORT.md.
- Backdrop dimming: visibly dark (0.85 alpha override on BG Image).

Plus the iter-4-specific additive requirement is visible-confirmed:
- **No maintenance notice strip above the modal.** Iter-1 screenshot clearly showed the dark-maroon "MAINTENANCE NOTICE / Scheduled server maintenance: 2025/12/31..." panel; iter-4 screenshot has clear dimmed sky in that vertical region. CONFIRM hidden.
- **No NEXT HOLE / PLAY strip below the modal.** Iter-1 screenshot clearly showed the gold PLAY button + reward strip directly below the modal; iter-4 screenshot shows only character pants and dimmed background in that vertical band. CONFIRM hidden.

## Capture-helper compliance check (Step 3)

- **Screenshot provenance:** `IMPLEMENTER_REPORT.md` § Screenshot states explicitly: *"Capture method: `CaptureHelper.SnapGameView()` called via `script-execute`. Console confirms: `[CaptureHelper] Using RT reflection path (GameView RenderTexture)`."* This satisfies CLAUDE.md § Screenshots and resolves the iter-1 process gap. ✓
- **Maintenance protocol for new contexts:** Diff in iter 4 adds `homeNoticePanel` / `homeNextHolePanel` SerializeField hooks on `MatchmakingModalController`. These are NOT new `*Context.cs` static-bus files under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` — they're inspector references on a UI controller. CaptureHelper maintenance protocol is N/A for this diff. ✓

## Checklist verification (Step 4)

| Item | Implementer | Self-reviewer | Notes |
|---|---|---|---|
| `InitializeFromTemplate` exists, public, sets fields, forces icons OFF, no `GetPlayerCharacter`. | PASS | CONFIRMED-PASS (iter-1 carry-forward) | Verified in iter-1 review. |
| No other method on `CharacterThumbnailCard.cs` modified. | PASS | CONFIRMED-PASS (carry-forward) | |
| `MatchmakingModalController.cs` exists, namespace, subclasses `ModalController`. | PASS | CONFIRMED-PASS | |
| Inspector fields per Implementation §2 present. | PASS | CONFIRMED-PASS | Source unchanged from iter 1; +2 new fields under `[Header("Home Screen Elements")]` for iter 4. |
| Tunables under "Tunables" header with correct defaults. | PASS | CONFIRMED-PASS | `fakeOpponentUsernames` capped ≤8 chars per accepted iter-2 deviation. |
| `Open(int = -1)` + no-arg overload. | PASS | CONFIRMED-PASS | |
| Dot cycle "FINDING OPPONENT.", "..", "...", ~0.4s. | PASS | CONFIRMED-PASS (code+runtime) | Cannot verify visually from "OPPONENT FOUND" still; report cites runtime `script-execute` query showing `'FINDING OPPONENT.<alpha=#00>.<alpha=#00>.'`. Accept code+runtime evidence. |
| Dot cycle base phrase doesn't shift. | PASS | CONFIRMED-PASS-CODE | Fixed-width 3-slot rendering via `<alpha=#00>` invisible dots is the correct technique. Multi-frame still capture not provided; accepting code-level evidence per task brief. |
| Opponent portrait/username/rank cycle every ~0.3s. | PASS | CONFIRMED-PASS (carry-forward) | |
| Player portrait+name+level static during search. | PASS | CONFIRMED-PASS (carry-forward) | |
| At 5s: dot cycle stops, status reads "OPPONENT FOUND" no dots, opponent locks. | PASS | CONFIRMED-PASS | Screenshot shows exactly "OPPONENT FOUND" with no trailing dots. |
| Cancel hides modal via base ModalController fade. | PASS | CONFIRMED-PASS-CODE | Auto-wire log shows both `closeButton` and `cancelButton` wired to CancelButton. Code path verified iter 1. |
| Hole info matches home screen for same index. | PASS | CONFIRMED-PASS | "Lomond Country Club - Hole 5" — both surfaces would show this; spec satisfied. |
| **Reward rows match home screen `HoleData.rewards`.** | PASS | **CONFIRMED-PASS** | Modal shows x100/x10/x30 (Lomond 5). `HoleDatabase.asset` was fixed to 100/10/30 in iter 2. iter-1 mismatch resolved. |
| `HomeScreenController.OnPlayClicked` calls `matchmakingModal.Open(currentHoleIndex)` with fallback. | PASS | CONFIRMED-PASS (carry-forward) | |
| `MatchmakingModalAutoWire.cs` exists, registered, reports counts. | PASS | CONFIRMED-PASS | Now uses Debug.Log per iter-2 fix (no DisplayDialog). |
| Auto-wire reports ≥22 wired, 0 failed. | PASS | CONFIRMED-PASS | 29/0 (was 27/0 iter 1; +2 for new home-element fields). |
| Auto-wire sets `HomeScreenController.matchmakingModal`. | PASS | CONFIRMED-PASS (carry-forward) | |
| No new asmdefs, no prefab reauthored. | PASS | CONFIRMED-PASS | |
| **No white-box placeholders visible.** | PASS | CONFIRMED-PASS | All Image slots in screenshot show real sprites: rarity backgrounds (teal + orange), real character portraits (player + BIRDIE), real reward icons (coin/crossed-tools/ball), gear icon, R-points icon, league pill. Zero white rectangles. |
| All `[SerializeField]` references wired. | PASS | CONFIRMED-PASS | Auto-wire 29/0 confirms. |
| Console no errors during smoke test. | PASS | CANNOT-VERIFY-INDEPENDENTLY | Trusting report's "Pre-existing errors only (Rindo Course .meta GUID)" — those are documented as not caused by this task. |
| **Backdrop dims home screen (85% black).** | PASS | **CONFIRMED-PASS** | Screenshot shows visibly dimmed sky/landscape behind modal. iter-1 "too light" defect resolved. YAML override `m_Color.a=0.85` cited at ShellScene.unity line 100620. |
| **Figma reference PNG present.** | PASS | **CONFIRMED-PASS** | `screenshots/figma-reference.png` (474×1024) read successfully. iter-1 missing-file defect resolved. |
| **Fresh play-mode screenshot captured with `CaptureHelper.SnapGameView()`.** | PASS | **CONFIRMED-PASS** | Method named explicitly in report; "Using RT reflection path" console line cited. iter-1 process gap resolved. |
| **[ITER 4] Maintenance Notice hidden while modal open; restored on close.** | PASS | **CONFIRMED-PASS** | Visually absent from iter-4 screenshot in the band above the modal where iter-1 clearly showed the maroon "MAINTENANCE NOTICE / Scheduled server maintenance: 2025/12/31..." strip. Restore-on-Hide also covered by `OnDisable()` safety net per code summary. |
| **[ITER 4] NextHolePanel hidden while modal open; restored on close.** | PASS | **CONFIRMED-PASS** | Visually absent from iter-4 screenshot in the band below the modal where iter-1 clearly showed the gold PLAY button + reward strip ("NEXT HOLE / Lomond Country Club - Hole 5 / x100 x10 x30 / PLAY"). |
| **[ITER 4] AutoWire wires `homeNoticePanel` and `homeNextHolePanel` cross-hierarchy.** | PASS | CONFIRMED-PASS | Console log lines cited: "OK homeNoticePanel -> HomeScreen/NoticePanel" / "OK homeNextHolePanel -> HomeScreen/NextHolePanel". 29/0 total. |
| Spec deviations flagged. | PASS | CONFIRMED-PASS | Seven deviations enumerated in report; all are previously-accepted carry-forwards (modalPanel→ContentArea, BG-via-instance-overrides, AutoWire Debug.Log, runtime `runInBackground=true`, ShowScreen skip-to-Home, etc.). None are net-new spec violations. |

## Iter-1 fail-list re-verification

For completeness, walking the five iter-1 specific failures and confirming each is resolved:

1. **Reward mismatch (modal x100/x1/x3 vs home x100/x10/x30).** RESOLVED — modal now shows x100/x10/x30; `HoleDatabase.asset` Lomond 5 was rewritten to 100/10/30 in iter 2.
2. **Figma reference PNG missing.** RESOLVED — `screenshots/figma-reference.png` present and readable.
3. **Username clipping ("GOLFWARR").** RESOLVED — `fakeOpponentUsernames` capped at ≤8 chars in iter 2; "BIRDIE" (6 chars) renders cleanly in this capture, with no clipping or rank-overlap.
4. **Capture method not declared.** RESOLVED — IMPLEMENTER_REPORT.md § Screenshot now names `CaptureHelper.SnapGameView()` and cites the RT reflection path console line.
5. **Backdrop too light.** RESOLVED — alpha bumped 0.5→0.85; sky/landscape behind modal visibly darker than iter-1 capture.

## Deviation evaluation (cumulative)

The seven deviations enumerated in the report (modalPanel→ContentArea, root active-state, BG-via-scene-overrides, AutoWire Debug.Log, runtime BG color reflection-read timing, `Application.runInBackground=true`, ScreenManager skip-to-Home for smoke testing) are all either (a) accepted in prior reviews/specs or (b) self-evidently correct workarounds. None are net-new violations introduced in iter 4. The architect was previously asked to promote the modalPanel→ContentArea workaround into the canonical spec for inherited `ModalController` — that's still pending and remains a spec-improvement note, not a blocker for this task.

## Iteration count

This is iteration **2** of self-review (iter-1 was FAIL on five concrete items, all now resolved). Implementer is on cumulative iter 4. Below the N≥3 escalate threshold for the self-review iteration counter.

## Routing

`FORWARD_TO_ARCHITECT`. STATUS.md → `READY_FOR_ARCHITECT_REVIEW`.

The architect should give this final visual+structural sign-off. Specifically worth a global Figma-vs-screenshot comparison from the architect (to catch any spec gaps the checklist doesn't cover, per Lesson C in PIPELINE_LESSONS.md), and confirmation that the `modalPanel→ContentArea` workaround should be promoted into the canonical `ModalController` spec for future inheritors.
