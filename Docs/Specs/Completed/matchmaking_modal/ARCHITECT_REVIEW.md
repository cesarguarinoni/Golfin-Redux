# Architect Review — `matchmaking_modal`

> Final review pass. Reads SPEC.md, IMPLEMENTER_REPORT.md (cumulative iter 1–4), SELF_REVIEW.md (iter-2 PASS), the iter-4 screenshot, and Figma reference. 2026-05-02 JST. Architect: claude-opus-4-7.

## Verdict

`PASS` → ready for Cesar's final approval. STATUS → `ARCHITECT_REVIEW_PASS`.

The implementation is sound, visually faithful (within the spec's placeholder-vs-canonical envelope), and ships with no architectural debt that would bite a follow-up task. The five iter-1 fail items are all resolved with verifiable evidence. Iter-4's home-element hide/restore is correctly implemented with both `OnHide` and `OnDisable` belt-and-suspenders.

I have updated `SPEC.md` to formalize three architectural decisions the pipeline surfaced (see "Lessons captured" + "SPEC.md edits" below).

## Architectural / cross-cutting checks

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries | PASS | Everything stays in `Assembly-CSharp`. No new asmdefs. `MatchmakingModalController` cleanly references `Golfin.Roster`, `Golfin.UI.Modals`, `GolfinRedux.UI` — all already on the same assembly side. |
| Pattern adherence (PATTERNS.md / RUNTIME_BLUEPRINT.md) | PASS | Subclasses `ModalController`, overrides `OnShow`/`OnHide`. Coroutine lifecycle matches the existing modal pattern. Auto-wire mirrors `ItemUseModalAutoWire.cs` structure (helpers, MenuItem path, scene-search). `HomeScreenController` cross-wire mirrors `ItemDetailPanel.useModal` pattern. |
| Reuse of existing utilities | PASS | `CharacterThumbnailCard.InitializeFromTemplate` is added as a sibling method (additive, no behavioural change to existing path). `RarityHelper.GetRarityLabel` / `GetRarityBadgeTextColor` reused. `LocalizationManager.Get` reused. `HoleDatabase.GetHole` + `HoleDatabaseLoader.GetHole` fallback chain mirrors `HomeScreenController.LoadNextHole`. |
| Acceptable duplication | NOTED | `SetupRewardRow`/`HideRewardRow` are duplicated from `HomeScreenController` per spec ("intentional, shared helper is a separate cleanup spec when there are 3+ call sites"). With matchmaking now being a 2nd call site, a `RewardRowBinder` extraction would be a clean follow-up — but not blocking here. |
| Implementation matches spec *intent* | PASS | The "Mac env smoke test" goal is achieved end-to-end: PLAY → modal → cycle → lock → cancel → home. Player data wired from real `CharacterManager`, opponent pool from real `CharacterDatabaseCSV`, hole + rewards from real `HoleDatabase`. No placeholders that pretend to be real. |
| Cross-feature implications | PASS | `HomeScreenController.OnPlayClicked` retains the legacy `screenManager.ShowScreen(ScreenId.Loading)` fallback when `matchmakingModal` is null. Other scenes that don't have the modal in scene aren't affected. `mainPlayButton` on bottom-nav explicitly out-of-scope (per spec) and untouched. |
| Latent bug risk | LOW with one note (see below) | Null guards on every `[SerializeField]` access. Coroutines stopped in `OnHide`. `_opponentPool` rebuilt every `Open()`. Dot-cycle uses fixed-width 3-slot rendering (no horizontal jitter). |
| Capture-helper compliance | PASS | Iter-4 uses `CaptureHelper.SnapGameView()` via `script-execute` with documented `runInBackground=true` to keep the loop driving frames. RT reflection path confirmed in console. No banned `ScreenCapture.CaptureScreenshot` calls. No new `*Context.cs` static-bus files were added in iter 4 (the home-element references are inspector hooks on a UI controller, not a fake-state context), so the maintenance protocol from `Docs/Specs/Active/capture_helper/SPEC.md` § Maintenance protocol is not triggered by this diff. Self-reviewer's N/A finding is correct; no backstop FAIL needed. |

### Latent-issue note (not a fail)

`OnDisable()` on `MatchmakingModalController` unconditionally calls `homeNoticePanel.SetActive(true)` and `homeNextHolePanel.SetActive(true)` (with null guards). This is the right defensive shape for the current scope — if the modal is destroyed mid-cycle (scene change, etc.) the home elements aren't left stuck hidden. The corner case where someone *else* legitimately wants those panels hidden while this modal is also alive doesn't exist today (the modal is the only thing that hides them), so this is fine. If a future feature ever wants per-feature visibility on those panels, refactor to a tracked `_wasNoticeActiveOnShow` cache and restore the *previous* state instead of forcing `true`. Not blocking for this task.

## Visual fidelity verdict

Comparing iter-4 screenshot against `screenshots/figma-reference.png` element-by-element:

| Element | Figma reference | Screenshot iter 4 | Match? |
|---|---|---|---|
| Modal title | "FINDING OPPONENT..." (search state) | "OPPONENT FOUND" (lock state) | YES — spec § 6 step 8 explicitly captures the lock state, structurally identical to search frame minus dots/cycling |
| Two character cards + "Vs." separator | Present, equal sized, centred | Present, equal sized, centred | YES |
| Player username label | "USERNAME" (placeholder) | "YOU" | YES — per spec § 2 "Player username falls back to `\"You\"` until UserData exists" |
| Player rank label | "RANK: #233" (placeholder) | "RANK: #571" | YES — random in `fakeRankRange` per spec § 2 |
| Opponent card | Elizabeth at Lv 7 (placeholder) | Cycling pool result, locked on a real CSV character ("BIRDIE") at Lv 27 | YES — per spec § 2 the opponent pool is the live roster minus player |
| Opponent username | "USERNAME" (placeholder) | "BIRDIE" (6 chars, no clip) | YES — within the 8-char canonical cap |
| Opponent rank | "RANK: #200" (placeholder) | "RANK: #832" | YES — within `fakeRankRange` |
| Hole heading | "HOLE" | "NEXT HOLE" | YES — spec § 3 explicitly accepts `LocalizationManager.Get("HOME_NEXT_HOLE")` |
| Hole subtitle | "Lomond Country Club - Hole 5" | "Lomond Country Club - Hole 5" | YES |
| Reward chips | x10 / x10 / x10 (Figma placeholders) | x100 / x10 / x30 | YES — spec contract is "match home-screen / CSV"; Figma's `x10 x10 x10` are placeholder values per the now-explicit SPEC note |
| Cancel button | Full-width "CANCEL" pill, light grey | Full-width "CANCEL" pill, light grey | YES |
| Backdrop alpha | Visibly dimmed home screen | Visibly dimmed (a=0.85) | YES — iter-1 "too light" defect resolved |
| Home maintenance notice | (n/a — Figma frame doesn't show home behind it) | Absent (hidden by `OnShow`) | YES — iter-4 additive requirement satisfied |
| Home Next Hole panel | (n/a) | Absent (hidden by `OnShow`) | YES — iter-4 additive requirement satisfied |
| League pill ("DIAMOND LEAGE") | Present, passive | Present, passive | YES |

No visible-defect-vs-Figma issues remain. Note the Figma string reads "DIAMOND **LEAGE**" (sic — same in both Figma and the rendered output); that's a pre-existing typo in the prefab's static label and is explicitly out of scope per spec § Out of scope ("not modifying the prefab visually").

## Architect resolution of pipeline-surfaced questions

### 1. `modalPanel → ContentArea` workaround — RESOLVED, promoted to spec convention

I am formalizing this as the canonical convention for any `ModalController` subclass. SPEC.md is updated with:

> wire `modalPanel` to the modal's content sub-tree, NOT to the modal's root GameObject. Reason: `ModalController.Awake()` calls `modalPanel.SetActive(false)` at startup. If `modalPanel` is the root, the controller deactivates itself and any coroutines started later never run.

This is the right call rather than option (c) "push back to implementer with a different fix" because the alternative (refactoring `ModalController` to skip self-deactivation when `modalPanel` is the controller's own GameObject) would change behaviour for every existing modal subclass and is out of scope for a smoke-test task. The convention is a one-line wiring rule that future inheritors can follow without code changes.

**Followup ticket suggestion** (Cesar to triage): if/when a third modal subclass is built and the convention starts to feel implicit, consider a `ModalController.cs` cleanup that detects `modalPanel == this.gameObject` and warns at `Awake`. Not blocking.

### 2. Reward placeholder mismatch (Figma `x10/x10/x10` vs runtime `x100/x10/x30`) — RESOLVED with explicit SPEC note

The runtime values are correct because the spec contract is "match the home screen / `HoleData.rewards`", and `HoleDatabase.asset` was the canonical source even before iter 2's correction. Figma's `x10/x10/x10` are static placeholders; chasing them would mean diverging the modal from the home screen. SPEC.md now explicitly says:

> The Figma-shown values `x10 / x10 / x10` are placeholders only. … Do NOT chase Figma's reward numbers if they disagree with the home-screen / CSV contract.

### 3. Username max length (8 chars) — RESOLVED, formalized in SPEC

Iter 2's empirical 8-char cap (where "GOLFWARR" was clipped from "GolfWarrior") is the right operating value for the current Username TMP rect. SPEC.md now pins this:

> **Username max length: 8 chars** to fit the Username TMP rect without clipping (calibrated iter 2; the 8-char cap is the canonical contract for any future fake-username list — `fakeOpponentUsernames` defaults must obey it).

If Cesar wants longer usernames later, the fix is widening the Username RectTransform / using auto-shrink, which is a separate UI tweak.

### 4. Figma node id discrepancy (`12813:77056` vs `12865:1095`) — RESOLVED

SPEC.md updated to cite `12865:1095` as the canonical node id. The old id is documented as "an earlier draft that was moved/renamed in Figma".

## Specific FAIL items

None.

## Open questions for Cesar

None — all pipeline-surfaced questions resolved by architect.

## Lessons captured (proposed `tasks/lessons.md` additions, after Cesar approves)

- **`ModalController` `modalPanel` wiring rule:** never wire `modalPanel` to the controller's own root GameObject — `Awake()` self-deactivates the panel and breaks coroutines. Wire it to a content sub-tree (e.g. `ContentArea`).
- **Figma placeholder values are NOT canonical for data-driven UI.** When a UI surface mirrors a CSV/database value (rewards, hole names, character stats), the spec must explicitly tag the Figma values as placeholders to prevent the implementer from "fixing" the runtime to match Figma. Add a "placeholder vs canonical content notes" section to every UI spec touching data-driven content.
- **`Application.runInBackground = true` for editor screenshot workflows.** When capturing play-mode screenshots via `script-execute`, the Game View often loses OS focus and the loop stops driving frames (Unity's default `runInBackground=false`). Set `Application.runInBackground=true` at the start of any editor capture session that drives play-mode coroutines.

## SPEC.md edits made by architect this review

1. **Reference § Figma frame** — updated node id from `12813:77056` to `12865:1095` (Cesar's canonical 2026-05-02 link).
2. **Reference § Placeholder vs canonical content notes** — added explicit username 8-char cap; rewrote the reward-line to state Figma's `x10/x10/x10` are placeholders and the home-screen / CSV contract wins.
3. **Reference §** — added "Backdrop alpha" entry pinning `0.85` as the canonical value for any future modal needing to dim a bright backdrop.
4. **Reference §** — added "Home-screen elements hidden while modal is open" entry capturing iter-4's additive requirement.
5. **Architecture context § Existing code referenced** — added the canonical `modalPanel → ContentArea` wiring convention as a sub-bullet under `ModalController`, so future modal specs inherit the rule.

No code, scene, or prefab edits were made by the architect (review only; per agent scoping).

## Cesar's final approval

Cesar fills this section after eyeballing the screenshot one last time.

- [ ] Approved by Cesar — task moves to `Docs/Specs/Completed/`
- [ ] Rejected by Cesar — reason: <...>
