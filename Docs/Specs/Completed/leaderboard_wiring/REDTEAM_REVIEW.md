# REDTEAM_REVIEW — leaderboard_wiring (Phase 1, iter-10)

- **Reviewer:** golfin-redteam-reviewer (adversarial gate)
- **Date:** 2026-06-15 14:39 CEST
- **Verdict:** **ARCHITECT_REVIEW_PASS** — I genuinely tried to re-break BLOCKER 1/2 (my own iter-9 kill shot) and hunt for a regression introduced by the fix, and came up empty on all three attacks. The `_username` cache is now structurally immune to corruption, the round-trip is proven by an independent fresh runtime log, and the scene diff is clean (no revert needed). This advances to Cesar's final approval.

---

## Prior-rejection replay — my iter-9 BLOCKERS, re-attacked

### BLOCKER 1 — `_username` cache corrupted via ModeSelection; Home center goes blank → **GONE**

Re-traced EVERY writer of `_username` and EVERY caller of `SetUsername`/`UpdateUsername` across `Assets/Scripts/` (grep, not re-read):

- **`_username` write sites (exactly 3, all legitimate):**
  - `PersistentUIManager.cs:22` — field init `= string.Empty`
  - `PersistentUIManager.cs:70` — `Awake()` caches designer "CHOTO"
  - `PersistentUIManager.cs:188` — `UpdateUsername()` (genuine profile change)
  - `SetUsername` (line 169) **no longer writes `_username`** — verified in source; the `_username = username` line is gone, replaced by a comment explaining the decoupling.
- **Callers of `SetUsername` codebase-wide: ZERO non-test callers.** `ModeSelectScreenController` no longer calls it (the `OnEnable`/`OnDisable` poke + `_savedUsernameText` field are fully removed — only an explanatory comment remains; grep confirms zero live `_savedUsernameText` references). So the cache is doubly safe: `SetUsername` is decoupled AND nothing invokes it.
- **Caller of `UpdateUsername`: only `UserProfileSubmenu.cs:151`** (real profile save). Correct — that path SHOULD write the cache.
- **Runtime proof (fresh iter-10 log, new `[RoundTrip]` marker, executes my exact deterministic failure trace):**
  `[RoundTrip] SUMMARY: Home='CHOTO' | ModeSelection='MODE SELECTION' | HomeAfterReturn='CHOTO' | Leaderboard='LEADERBOARD'`
  `HomeAfterReturn='CHOTO'` is the decisive line — the cache survived the ModeSelection round-trip.
- **Visual proof:** `home_after_modeselect_rt_iter10.png` shows "CHOTO" in the top-bar center after the round-trip. GONE.

### BLOCKER 2 — pre-existing "MODE SELECTION" header label → **RESOLVED (intentional)**

`HighlightScreen` now has `case ScreenId.ModeSelection: usernameText.text = "MODE SELECTION";` (PersistentUIManager.cs:265-267), driving the label centrally instead of via the deleted save/restore dance. `modeselect_rt_iter10.png` shows "MODE SELECTION" in the top-bar center. Confirmed intentional per task brief. RESOLVED.

---

## Angle I captured / re-shot (not re-used)

Opened all four iter-10 captures myself at full res (1170×2532):
- `screenshots/home_rt_iter10.png` — Home top-bar "CHOTO" (before round-trip)
- `screenshots/home_after_modeselect_rt_iter10.png` — Home top-bar "CHOTO" (after round-trip)
- `screenshots/modeselect_rt_iter10.png` — top-bar "MODE SELECTION", tee nav highlighted
- `screenshots/leaderboard_canonical_iter10.png` — "LEADERBOARD", DAILY gold, podium 2/1/3 Thumbnails, scroll starts rank 4 (SAMWISE), YOU pinned rank 121, DIAMOND LEAGUE, RESETS IN 11H 34M 55S

**Recycled-evidence audit (the named "labeling missed 3×" risk):** the two Home captures are byte-identical (`7f8cdf70…`). I traced this rather than assuming foul play: the `_capture/` sources are TWO separate fresh files — `home_rt_iter10_f6079.png` (written 14:25:50) and `home_after_modeselect_rt_iter10_f6486.png` (written 14:25:53), distinct `f####` render calls 3s apart. They are byte-identical because the Home top-bar genuinely renders identical "CHOTO" pixels before and after — which is exactly the proof the fix is correct. The independent `[RoundTrip]` log corroborates the round-trip the identical frames represent. Evidence integrity: OK.

## Numbers / facts I re-ran

- **`_username` writers:** 3 (init, Awake, UpdateUsername) — `SetUsername` excluded. ✓
- **`SetUsername` non-test callers:** 0. **`UpdateUsername` non-test callers:** 1 (`UserProfileSubmenu.cs:151`). ✓
- **Test count:** 17 `[Test]` methods in `LeaderboardTests.cs` (14 pre-existing + 3 new regression). The 3 new tests (`PersistentUI_SetUsername_DoesNotCorruptCachedName`, `PersistentUI_Home_ModeSelection_Home_RestoresUsername`, `PersistentUI_UpdateUsername_DoesUpdateCachedName`) are substantive — the `FakePersistentUI` POJO faithfully mirrors the real iter-10 state machine I read in source. Honest caveat: they validate the LOGIC at the POJO level (not the live MonoBehaviour); the PlayMode `[RoundTrip]` log + screenshots cover the real-component path. Acceptable layering.
- **Compile:** Editor.log shows the test file imported/compiled, zero `error CS`, and the `[RoundTrip]` MonoBehaviour ran in play mode (which requires a successful compile). No NullReferenceException near PersistentUI/HighlightScreen.
- **Null-safety:** `HighlightScreen`'s top-bar text switch is wrapped in `if (usernameText != null)` (line 255) — Roster/Inventory/HoleSelection hitting `default` with a null field cannot null-ref.

## New-regression hunt (from removing ModeSelect save/restore) — clean

- `_savedUsernameText` field removed; zero live references (grep).
- ModeSelect `OnEnable`/`OnDisable` retain ALL card logic; only the username poke removed. No other code depended on the saved text.
- **`HomeScreenController.usernameText` is a SEPARATE field, unwired (`fileID: 0` in ShellScene:111294).** Its `OnEnable` `usernameText.text = "Player"` is a dead no-op — it does NOT point at the shared top-bar center (`fileID: 7151700373420156916`, the PersistentUIManager field). This is WHY Home shows "CHOTO" not "Player" — not a fragile ordering coincidence. Pre-existing, not a regression.

## Scene-drift audit — clean, NO close-out revert needed

`git diff Assets/Scenes/ShellScene.unity`:
- **ZERO `m_IsActive: 0` (no disables anywhere).** The iter-9 `ModeSelectionScreen m_IsActive: 1 → 0` capture leak has self-resolved (iter-10 routed through ModeSelection, re-enabling it). **No scene revert is required at close-out.**
- The three `m_IsActive: 1` lines are all on NEW GameObject blocks (LeaderboardButton ×2, LeaderboardManager) — legitimate iter-1 additions, not flips of existing screens.
- The one PrefabInstance `m_IsActive` modification (`value: 1`) enables a RankingsScreen sub-object — part of the legitimate new-screen wiring.
- Only non-layout property mutations: `m_TextStyleHashCode` (TMP internal, benign) + that one enable. No ShotUI/HUD mutation. All 556 insertions are the expected footprint of wiring RankingsScreen into ShellScene.

## Three break-attempts (per protocol)

1. **Geometric/data-flow (re-firing my iter-9 kill shot):** traced every `_username` writer and every `SetUsername`/`UpdateUsername` caller codebase-wide. No path can corrupt the cache — `SetUsername` is decoupled AND has zero live callers. *Attack failed.*
2. **New regression from the fix:** checked whether ModeSelect or any other screen relied on the removed save/restore, and whether HomeScreen's "Player" write fights the top-bar. `_savedUsernameText` gone with no dependents; HomeScreen's field is unwired (dead no-op). *Attack failed.*
3. **Visual + scene-drift:** all four captures fresh and correct, Round 1-6 visual set holds in the canonical, and the ShellScene diff has zero disables / no HUD mutation. *Attack failed.*

All three came up empty.

## Routing & close-out actions

`ARCHITECT_REVIEW_PASS` → Cesar's final approval.

**Remaining close-out actions for Cesar (none are blockers):**
- **NO scene revert needed** — the iter-9 ModeSelectionScreen `m_IsActive` leak self-resolved; current `git diff` has zero `m_IsActive: 0`.
- On close-out commit, run `git status --porcelain --untracked-files=all` per CLAUDE.md Rule 12 — all reported code/data files are in the report's "Files modified or created" table, so commit the code first, then the folder move-to-Completed.
- On-disk `SELF_REVIEW.md` may still be the iter-9 file (noted in brief); not my gate and does not affect this PASS.
