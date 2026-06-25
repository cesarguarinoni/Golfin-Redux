# ARCHITECT HANDOFF — tournament_selection_screen (T7)

**Status:** STOPPED by Cesar mid-pipeline (during golfin-reviewer dispatch, iter-3).
**Reason:** The implementation **rebuilt the screen and its buttons from scratch** instead of cloning the existing HoleSelection/Rankings scaffold and reusing the shared buttons — a direct violation of **SPEC §0 / §1 rule 1 ("Clone-and-modify, never rebuild — HARD")**. Cesar caught it on sight: *"there is no panel here … nor the buttons for sign up. You just recreated everything from scratch."*

This document is the full record of what was done across 3 implementer iterations, the cardinal failure, the contributing SPEC gap, the pipeline-gate miss, and what a correct redo requires. **No files were reverted** — everything is left uncommitted in the working tree for the architect to decide.

---

## 1. What was requested

- **Scope:** Stages 0–1 only (static screen, no backend; Stage 2 binding blocked on T1→T4).
- **HARD rule (SPEC §0 #1, §1 reuse map):** the screen scaffold (filter strip + scroll list + persistent bars), the gold/silver CTA buttons, and the RP icon **already exist and must be reused via clone-and-modify**. The card said: *"The only **new** prefab is the tournament card."*
- Specifically SPEC §1 mandated reuse of:
  - the in-scene **`HoleSelectionScreen`** base (the scaffold `tournament_screens` cloned for `TournamentHoleSelectionScreen`) — includes the **dark rounded back panel / background** behind the scroll list;
  - the **Rankings period-tab pattern** for the 4 filter tabs;
  - the **shared gold primary button** (= the `Sign Up Button` instance `13386:1803`);
  - the **silver `TournamentCloseButton`** for LEADERBOARD.

## 2. What was actually built (iters 1–3)

Everything was authored net-new. Concretely:

| File | What it is | Reuse? |
|---|---|---|
| `Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs` | NEW — builds its own bare `ScrollRect` + `Content`, instantiates cards at runtime | ❌ Did **not** clone HoleSelection scaffold; **no back panel / background** at all |
| `Assets/Scripts/UI/Tournaments/TournamentSelectionCard.cs` | NEW — fully bespoke card controller | ❌ |
| `Assets/Prefabs/UI/Tournaments/TournamentSelectionCard.prefab` | NEW — **100% hand-rolled** (CTAButton, PillBorder, Badge, FreeEntryBadge, PaidEntryBadge, TournamentImage all bespoke; **0 nested prefab instances**) | ❌ Gold CTA hand-built from Image+Button; silver `TournamentCloseButton` referenced **0 times** |
| `Assets/Scenes/ShellScene.unity` | MODIFIED — new `TournamentSelectionScreen` subtree wired (bare scroll, no panel) | ❌ |
| `Assets/Scripts/UI/ScreenManager.cs` | MODIFIED — added `ScreenId.TournamentSelection` | ✅ correct/necessary |
| `Assets/Scripts/UI/PersistentUIManager.cs` | MODIFIED — "TOURNAMENTS" banner + showBars | ✅ correct/necessary |
| `Assets/Scripts/UI/Tournaments/TournamentDevEntryButton.cs` | MODIFIED — routes ModeSelection TEMP → TournamentSelection | ✅ correct/necessary |
| `Assets/Scripts/UI/Tournaments/TournamentHoleSelectionScreenController.cs` | MODIFIED — back target → TournamentSelection | ✅ correct/necessary |

**Evidence (working tree):**
- `grep -cE "PrefabInstance|^--- !u!1001" TournamentSelectionCard.prefab` → **0** real prefab-instance blocks (everything bespoke).
- Silver button reuse check: `TournamentCloseButton` guid `260f2fa7739224d6…` referenced **0** times in the card prefab.
- No standalone gold/Sign-Up/primary button prefab exists anywhere under `Assets/` (only `TournamentCloseButton.prefab` + `Original/SplashScene/SignupScreen.prefab`); the only gold-button-like object is the in-scene `PlayButton` in `ShellScene.unity`.
- The screen controller fields are `_cardsScrollRect` + `_cardsContent` only — **no panel/background field**, unlike the canonical `HoleSelectionScreenController` (which has `filtersContainer`, a background panel, viewport, etc.).

## 3. Where the 3 iterations were spent (all of it downstream of the wrong foundation)

- **iter-1:** Built the bespoke screen + card; passed implementer self-check. Self-reviewer **FAILed** on §3 token misses (FREE-ENTRY pill not a pill, RP icon absent, paid badge bare, tournament_image a dark void, eyebrow gradient flat).
- **iter-2:** Patched those tokens in the prefab YAML; self-reviewer **FAILed again** — the fixes were in the prefab asset but **did not render** on the runtime cards.
- **iter-3:** Root-caused the iter-2 non-render to a **stale in-memory prefab** (edits hit the `.prefab` on disk but were never reimported, so `Object.Instantiate(_cardPrefab)` cloned Unity's stale copy) **+ non-deterministic code** (`_rewardRpIcon` declared-but-never-driven; no `_tournamentImage` field). Fixed by driving sprites from code + reimport. The 6 token defects then rendered (see `screenshots/iter3_canonical_2026-06-25_13-31-43.png`). Only A10 (eyebrow gradient) left as a disclosed TMP 2-stop-vs-3-stop approximation.

**Net:** ~3 iterations of token-fidelity and a stale-import bug — **all of it moot**, because the scaffold and buttons it was decorating should never have been hand-built. If the HoleSelection scaffold had been cloned and the shared buttons reused, the panel/background/scroll/CTA fidelity would have come for free and the pill/icon plumbing would have lived in shared, already-correct components.

## 4. The pipeline miss (systemic — worth a lesson)

The cardinal SPEC rule (clone-and-modify, HARD) was **never checked by any gate**:
- The **implementer** Gate-A/self-check verifies real-entry path + token fidelity, not clone-provenance.
- The **self-reviewer** runs a git-diff *scene-mutation* audit (looking for `m_IsActive:0`/sizeDelta corruption **outside** scope) — but has **no check for "did this clone the named scaffold or rebuild it from scratch."**
- The **orchestrator (me)** surfaced each canonical frame and tunneled on the same token-level deltas Cesar's reviewers were chasing, never asking "is there a back panel? is the CTA the shared button?"

A frame can look ~80% right (cards, badges, CTAs present) while being built on entirely the wrong, non-reused foundation. **Recommend a new gate: a "reuse-map / clone-provenance" check** — for any task whose SPEC has a §1 reuse table, the implementer report and a reviewer must cite, per reuse row, the **prefab GUID or scene-object the new work was cloned from** (e.g. "CTAButton = instance of <shared gold button prefab guid>"; "screen scaffold = duplicate of HoleSelectionScreen"). A row with no provenance = FAIL. This is the UI analogue of the Rule-2 real-entry gate and the Rule-18 Figma table.

## 5. Contributing SPEC gap (for the architect to close before redo)

The reuse mandate for the **gold CTA is not actionable as written.** SPEC §1 says reuse "the shared gold primary button (same instance as `Sign Up Button` `13386:1803`)" — but **there is no standalone gold-button prefab in the repo**; the gold button exists only as in-scene instances (e.g. `PlayButton` in `ShellScene`). With nothing clean to clone, the implementer rolled its own (still a violation — it should have stopped and flagged, or duplicated the in-scene instance — but the SPEC handed it an impossible "reuse X" where X isn't extractable).

**Before re-dispatching, the architect should pin the exact reuse sources by concrete handle:**
1. **Screen scaffold:** name the exact in-scene object to duplicate (e.g. `ShellScene → … → HoleSelectionScreen`) including its **back panel/background sprite** + scroll/viewport — and confirm whether `tournament_screens` left a reusable variant.
2. **Gold CTA:** either (a) extract a `GoldPrimaryButton.prefab` from the canonical in-scene instance first (its own small task), then have T7 reuse it; or (b) name the exact scene instance to duplicate. Same for the **silver `TournamentCloseButton`** (this one already exists as a prefab — just wasn't used).
3. **Filter tabs:** name the Rankings tab object/prefab to clone.

## 6. Recommended redo shape

1. **Discard the bespoke screen + card** (or keep only as a visual reference) — do not build Stage 2 on top of it.
2. **Architect** closes the §5 SPEC gaps (concrete reuse handles; extract gold-button prefab if needed).
3. **New implementer pass:** duplicate the HoleSelection scaffold in-scene → rename to `TournamentSelectionScreen` → swap the filter pills for the 4-tab Rankings TabBar → the card is the only net-new prefab, and its CTA is an instance of the (now-extracted) shared gold button + silver `TournamentCloseButton`, not hand-built shapes.
4. Re-apply the §3 card-content tokens (badge/eyebrow/name/club/entry/RP/reward) on top of the reused chassis.

## 7. Current working-tree state

All changes are **uncommitted and left in place** (per "never `git checkout --` away accumulated work"). The architect decides whether to salvage the card-content logic (`TournamentSelectionCard.cs`'s state/badge/bind code is sound and reusable on a reused chassis) or start clean. `STATUS.md` is set to `IMPLEMENTER_BLOCKED` pointing here.

### Salvageable vs discard
- **Salvage:** the C# state matrix + `BindStatic`/`ApplyBadge` logic in `TournamentSelectionCard.cs` (badge colors per state, CTA text, free/paid toggle) — correct and chassis-agnostic. `ScreenManager`/`PersistentUIManager`/nav edits are correct, keep them.
- **Discard/redo:** the bespoke screen scaffold (needs the cloned panel), the hand-rolled CTA button (use shared), and the bespoke card visual chrome where a reused component should carry it.
