# SPEC — `game_polish_b` (content & modal motion)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Notion **2111** `game_polish`, slice **b** of three. Map: `Docs/Specs/Queued/game_polish/MAP.md` (approved 2026-09-03; G3–G8, the result-modal row, Roster/Inventory level-ups, Mode Select front-door stagger, Rankings Top-3 reveal). **Runs AFTER `design_consistency_audit` is DONE and its approved Quick fixes have landed** — if a fix changed a bar from `Filled` to width-driven, or a prefab this task animates, the retrofit targets the fixed prefab, never a stale one. `game_polish_a` (DONE `b2496871d`) is the precedent for every gate here: `GamePolishProbe`, invariants JSON, baseline-vs-instant parity, captioned videos, `check_report_counts.py`.

## Status

See `STATUS.md`.

## Goal

The GAME's **content** moves the way the GPS surface's does: every modal pops (G3), the three hand-rolled motions run on `UiMotion` with a frame-identical result (G4), the numbers that change count instead of snapping (G5), a cold fetch shows shimmer instead of a blank panel (G6), every network CTA shows the `…` pending state (G7), list rows and cards stagger in on a fetch paint (G8), and the result modals get a small choreography. Rest state stays pixel-identical (0 px parity); nothing about what a screen *does* changes. No haptics, no package, no `Animator`.

Cesar's calls, baked in: pop **all** game modals (incl. the result modals, which keep their inner choreography on top); add an optional **`Ease`** parameter to `UiMotion` (GPS behaviour unchanged, `Docs/GPS/GPS_BACKLOG.md` row exists); **move** `ShimmerBlock.prefab` to `Common/` (GPS_BACKLOG row exists); Mode Select cards stagger on the front-door paint; Rankings Top-3 reveal **3 → 2 → 1**.

## Reference

- **No Figma nodes** — motion only. Rest state at HEAD (post-audit-fixes) is the reference; `game_polish_a`'s `baseline` captures + the audit's crop sheets are the parity ground truth.
- **Model:** `Docs/Specs/Completed/gps_polish/` SPEC §D5–§D8 and IMPLEMENTER_REPORT A7/A8/A9/A13; `Docs/Specs/Completed/game_polish_a/` for the probe, the parity method and the report-count discipline.
- **Motion vocabulary:** `Assets/Scripts/UI/Polish/UiMotion.cs` (`Fade`/`Pop`/`Unpop`/`Slide`/`Rise`/`CountUp`/`Bump`/`Tween`/`Stagger`/`Pulse`/`Then`, constants `FadeDur` 0.15 · `PopDur` 0.20 · `CountDur` 0.40 · `StaggerDelay` 0.03 cap 12 · `PulseDur` 0.6), `UiSelection`, `PendingSpend`, `NavSlotHighlight` (a), `LayeredPush` (a).
- **GPS helpers to generalise (no GPS source edits):** `Assets/Scripts/UI/Gps/GpsPaintMotion.cs` (`PaintGate` cache/fetch/repaint gate, `PanelReveal`, `StaggerRise`, `Shimmer(root, site, cold)`), `Assets/Scripts/UI/Gps/ShimmerHost.cs`, `Assets/Prefabs/UI/Gps/ShimmerBlock.prefab`. See §D0.
- **The modals** (measured from the YAML, 2026-09-05 — 13, not 12: `SchemeConfirmModal` landed since the map):

| Modal | Where | `animateShow` today |
|---|---|---|
| `LevelUpModal` (`LevelUpModalController`) | ShellScene `RosterScreen/LevelUpModal` | 0 |
| `ClubLevelUpModal`, `BagSelectionModal`, `BagsClubModal` (`BagClubModalController`), `ItemUseModal` | ShellScene `InventoryScreen/…` | 0 |
| `VersusResultModal` (`VersusResultModalController`) | ShellScene `Canvas/VersusResultModal` | 0 (own pop-in, §D2) |
| `MatchMakingModal` | ShellScene (prefab instance `Matchmaking/MatchMakingModal.prefab`) | 0 |
| `TournamentSignupModal`, `TournamentResultModal`, `GachaRatesModal`, `GachaRevealModal`, `StartingCharacterConfirmModal`, `InGameSettingsModal`, `SchemeConfirmModal`, `HoleCompleteModal` | `Assets/Prefabs/UI/Modals/*.prefab` | unset/0 |

All 13 carry `useAnimation = 1` (the legacy alpha-only `FadeIn`/`FadeOut`, `animationDuration` 0.2). **`HoleCompleteModalController.Show()` is overridden as a no-op** — the `HoleCompleteWidget` (`Assets/Scripts/Gameplay/UI/ShotUI/`) owns visibility via `_root.SetActive`; the flag alone will not pop it (§D1.3).

- **Live-value and CTA sites** (the report's tables enumerate these; add what the grep finds): `PersistentUIManager.SetRewardPoints` (already counts when `ArmRewardPointsCountUp()` was called ≤ 5 s before — GPS arms it today); `PendingSpend` already on `LevelUpModalController`, `StaminaShopDetailScreenController`, `GeneralShopScreenController`, `ClubLevelUpModalController`, `TournamentSignupModalController`, `ModeCardController`; `LevelUpModalController` / `ClubLevelUpModalController` stat bars are `Image.fillAmount` (`bar`, `barPending`, `loftBar`); `GachaHistoryScreenController` now pages 12 rows (`gacha_history_rebuild_stall`, DONE).

## Design

### D0 · Shared helpers move out of `Gps/` (zero GPS source edits)

`git mv Assets/Scripts/UI/Gps/GpsPaintMotion.cs Assets/Scripts/UI/Polish/PaintMotion.cs`, `git mv …/Gps/ShimmerHost.cs …/Polish/ShimmerHost.cs`, `git mv Assets/Prefabs/UI/Gps/ShimmerBlock.prefab Assets/Prefabs/UI/Common/ShimmerBlock.prefab` — **GUIDs preserved, namespaces and class names unchanged** (`Golfin.Gps.UI` stays on the moved classes; renaming would touch every GPS caller — that rename is the GPS session's, GPS_BACKLOG row). The one GPS-folder edit allowed: the `ShimmerBlock` prefab path constant in `GpsPolishBuilder.cs` (already authorised, GPS_BACKLOG). If `Shimmer(root, site, cold)` locates hosts by a GPS-specific site table, add a game table beside it in a NEW file (`Polish/GameShimmerSites.cs`), not by editing the moved file's GPS table.

### D1 · Modals pop (G3)

1. Set `animateShow = 1` on all 13 (scene objects via `SerializedObject` + `RecordPrefabInstancePropertyModifications`; prefabs directly) — from `GamePolishBuilder.ApplyModals()` (extend `Assets/Scripts/UI/Polish/Editor/GamePolishBuilder.cs`), re-runnable, idempotent. **Code default stays `false`** (`GpsScreenTransitionTests` pins it — do not touch).
2. `ModalController`: when `animateShow` is true the legacy `useAnimation` path must not ALSO run (today `Show()` branches `animateShow` first, `Hide()` too — verify both, and that `HideImmediate` runs exactly once). No change to `IsVisible()` / `OpenModalCount` timing (pinned).
3. **HoleComplete:** `HoleCompleteWidget.Show/Hide` runs `Pop`/`Unpop` on `_root`'s panel + `Fade` on its scrim (a `CanvasGroup` on `_root`, added at runtime like a's D-1), same constants, so it matches the other twelve. `TournamentResultModal` and `VersusResultModal` go through the base class like everyone else (Versus after §D2 removes its own pop).
4. **Inner choreography on the result modals** (on top of the pop, starting when the pop completes — `UiMotion.Then`): reward rows `Stagger`-rise; RP / points / score labels `CountUp` (from 0, or from the pre-result value where the modal shows a running total); rank or "WIN"/"LOSE" glyph `Pop`; mission-complete banner (HoleComplete) `Rise` + one `Pulse`. Buttons stay interactable from the first frame (no dead time — a tap during the choreography completes it instantly, `CompleteNow()` pattern).

### D2 · The three retrofits (G4) — frame-identical

| Controller | Today | After | Gate |
|---|---|---|---|
| `VersusResultModalController.PopInScaleRoutine` (0.9→1.0, 0.2 s, ease-out) | own coroutine | delete; `animateShow` pop does the same curve (`Pop` = 0.9→1.0 / `PopDur` 0.20 / ease-out cubic) — confirm the easing function is the same expression; if the old one was a different ease-out, keep the visual by passing the new `Ease` | frame-by-frame scale log old vs new, max |Δscale| ≤ 0.005 per frame |
| `DailyMissionPillController.SlideRoutine` + `SetGlowAlpha` pulse | own coroutines (`_motion`) | `UiMotion.Slide` (same from/to/duration/easeOut flag) + `UiMotion.Pulse` on a `CanvasGroup` over the glow Image (`SetGlowAlpha` becomes the group's alpha) | per-frame `anchoredPosition.x` + glow alpha log old vs new, max Δ ≤ 0.5 px / 0.01 |
| `GachaRevealModalController` `StepEnter` (0.6→1 ease-out-back), `StepPop` (0.25→1 + position, ease-out-back), `StepShake` | own coroutines | `Pop`/`Tween` with the new **`Ease.OutBack`**; `StepShake` keeps its own amplitude/frequency curve but drives through `UiMotion.Tween` (the shake is a signal, not an ease). The rarity tiers, delays and order are UNTOUCHED — this is a primitive swap | per-frame scale/position log per step, max Δ ≤ 0.005 / 0.5 px; video (d) of one x10 reveal beside the pre-change recording |

**`UiMotion.Ease`:** `enum Ease { OutCubic, OutBack, Linear }` + an optional trailing `Ease ease = Ease.OutCubic` parameter on `Pop`, `Unpop`, `Slide`, `Tween`, `Rise` (`OutBack` overshoot factor 1.70158 — the value the gacha code uses today; quote it). Every existing call site compiles unchanged; `UiMotionTests` easing-endpoint tests unchanged and green; new tests pin `OutBack` overshoot and the `Linear` midpoint. This is the ONE `UiMotion` public-API change of the track.

### D3 · Count-ups on game deltas (G5)

`PersistentUIManager.ArmRewardPointsCountUp()` is called immediately before the RP write at: shop purchase confirm (`GeneralShopScreenController`), stamina purchase (`StaminaShopDetailScreenController`), gacha pull result (`GachaRevealModalController` / `GachaPullService` callback — wherever the client applies the server's new balance), mission claim (`HoleCompleteModalController.ClaimMissionRoutine`), hole-complete rewards, tournament result, level-up spend (`LevelUpModalController`, `ClubLevelUpModalController`). `SetRewardPoints` only counts UP today (`points > from`) — add the DOWN direction for spends (`CountUp` works with `to < from`; pin it). Ticket count in the top bar (if it has one — `TryParseTopBarNumber` sites): same arm. Modal-local numbers: level-up modal level `Pop`, stat bars `Tween` `fillAmount` old→new (`CountDur`) — or width if the audit's fix changed them; mission counters on `MissionCard` `CountUp`.

### D4 · Shimmer + fade-in on cold fetches (G6)

Hosts (from `Common/ShimmerBlock.prefab`, placed by `GamePolishBuilder.ApplyShimmer()`, INACTIVE at rest so parity holds): Rankings list ×3 + Top-3 ×3 (`RankingsScreenController`), TournamentSelection cards ×2 (`TournamentSelectionScreenController.RebuildNextFrame` path), TournamentLeaderboard rows ×3, GachaHistory rows ×3 (page 1), GeneralShop cards ×4 (content catalog paint), MissionSelection cards ×2. Rule as GPS §D8: shown ONLY on a cold fetch (`PaintGate.Should(PaintKind.Fetch…)` — the disk-cached first paint counts as CACHE, never shimmer), replaced by rows on paint, hidden on error in favour of the existing empty/error label which `Fade`s in. Log line per site: `paint(cache)` / `paint(fetch)` / `shimmer(cold)`.

### D5 · `PendingSpend` audit (G7)

Table every CTA that awaits `Golfin.Net` / Supabase / the content service: the six already wired + gacha PULL x1/x10 (the tap → modal open window), mission CLAIM, tournament ENTER/CLAIM, Settings sign-out, Rankings/Tournament refresh pulls (if user-triggered). Wire what is missing; one captured `…` frame per newly wired CTA; before/after column per row (gps_polish A7 shape).

### D6 · Staggers and selection (G8 + the slice-b half of G9)

`StaggerRise` on the FETCH paint of: hole cards, mission cards, tournament cards, tournament leaderboard rows, rankings rows, shop cards, gacha history rows (page 1), gacha prizes grid. **Front-door exception (Cesar):** Mode Select cards stagger on EVERY entry paint (local data). **Rankings Top-3 reveal:** `Pop` 3 → 2 → 1, `StaggerDelay × 3` apart, after the list stagger starts. Selection bumps: mode card selected state, hole card selected, `HistoryChip`, mission card tap — `UiSelection` bump + two-Image cross-fade where a sprite pair exists (no tinting).

### D7 · Probe and gates

`GamePolishProbe` (a) gains modes: `modals` (open/close each of the 13 through its REAL trigger, log `IsVisible`/`OpenModalCount` timing, capture one mid-pop frame each), `retrofit` (the three per-frame logs, old vs new — record the OLD logs on the FIRST commit before any change), `shimmer` (cold-fetch frames, cache-hit log), `perf` (as a).

## Localization

No new strings expected. If a "Loading…" fallback is needed, Build rule: `LocalizationText.csv` EN+JA → importer PLAN → APPLY → publish → `--check`; quote the PLAN verdict either way (`add 0` expected).

## Architecture context

- **New:** `Polish/GameShimmerSites.cs` (if needed), tests: `UiMotionEaseTests`, `RetrofitParityTests` (the per-frame logs compared in EditMode where the routine can be stepped), `ModalPopTests` (all 13 prefabs/scene objects have `animateShow` true — read from the assets; `HoleCompleteWidget` pop present), `CountDownTests` (`SetRewardPoints` decreasing while armed).
- **Moved (D0):** `GpsPaintMotion.cs` → `Polish/PaintMotion.cs`, `ShimmerHost.cs` → `Polish/`, `ShimmerBlock.prefab` → `Common/` (git mv, GUIDs kept).
- **Touched:** `UiMotion.cs` (the `Ease` parameter — the only API change), `ModalController.cs` (D1.2 guard only), `HoleCompleteWidget.cs`, `VersusResultModalController.cs`, `DailyMissionPillController.cs`, `GachaRevealModalController.cs`, `PersistentUIManager.cs` (count-down + arm sites), the screen controllers listed in D3–D6, `GamePolishBuilder.cs` + the modal prefabs / scene objects it flags, `GpsPolishBuilder.cs` (path constant only).
- **Untouched:** `FadeController`, `LayeredPush`, `NavSlotHighlight`, everything else under `Gps/`, every GPS prefab except the moved one.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] **A1 · Modals:** table of the 13 — asset path, `animateShow` read back from the asset, real trigger used, mid-pop frame path, `IsVisible` true from frame 0 of Show / false from frame 0 of Hide (log). `HoleCompleteWidget` pop captured from a real hole completion (or the `HoleCompleteModal` demo recorder if one exists — name it).
- [ ] **A2 · Retrofit parity:** three per-frame logs OLD (first commit) vs NEW, max Δ quoted per routine within the D2 gates; video (d) of one x10 reveal.
- [ ] **A3 · Rest parity 0 px** on every screen and every modal at rest vs the `game_polish_a` baselines (or the post-audit baselines — say which); shimmer hosts inactive at rest.
- [ ] **A4 · Videos** (≥ 50 KB, captioned, one still each): (a) Roster level-up with bars + count-up; (b) shop purchase → RP counts down in the top bar → Inventory; (c) Rankings cold open (shimmer → Top-3 3→2→1 → rows); (d) gacha x10 reveal (after); (e) hole complete → rewards choreography → mission claim; (f) tournament signup → result modal; (g) Mode Select entry + card select.
- [ ] **A5 · Count-up table:** every site in D3 — before (snap) / after (count), one still each; `SetRewardPoints` decreasing case pinned by test.
- [ ] **A6 · Shimmer:** one cold frame per site (how the cache was cleared, quoted), one `paint(cache)` log line proving the cached path skipped it, error path frame for one site.
- [ ] **A7 · Pending table** (D5), before/after per CTA, one `…` frame per newly wired CTA.
- [ ] **A8 · Stagger:** one mid-stagger frame per D6 site; log line distinguishing fetch vs cache paint per site; Mode Select stagger on a CACHE paint (the exception) shown.
- [ ] **A9 · `UiMotion` API:** `git diff` of `UiMotion.cs` shows only the `Ease` addition; every GPS test suite green unchanged; `Docs/GPS/GPS_BACKLOG.md` row references this task.
- [ ] **A10 · D0 moves:** `git log --follow` shows the three moves; `.meta` GUIDs identical before/after (quoted); `git diff --stat -- Assets/Scripts/UI/Gps` lists ONLY `GpsPolishBuilder.cs` (one line) — nothing else under `Gps/`.
- [ ] **A11 · Lint** delta zero on every prefab the builder touched.
- [ ] **A12 · EditMode** full sweep green + new suites by name.
- [ ] **A13 · Perf:** isolated ≤ 32 B/frame for `Pop(OutBack)` and `Tween` (tests); in-situ upper bound over video (c) and (d) quoted; the GachaHistory arrival re-measured after `gacha_history_rebuild_stall` (should now be < 50 ms).
- [ ] **A14 · Report counts** regenerated by `check_report_counts.py` from the JSONs (a's rule), `check_report_citations.py` 0 unresolved.
- [ ] **A15 · Deviations** flagged with justification.

## Smoke evidence

Videos (a)–(g) + stills, the A1/A5/A7 tables, the retrofit logs, the invariants/perf JSONs.

## Out of scope (do NOT do these)

- Scroll feel, safe area, `ButtonPressFeedback` sweep, Rubik Medium — **`game_polish_c`**.
- Haptics; any Settings toggle for motion; new screens; GPS source edits beyond the one path constant; renaming the moved classes' namespace (GPS session).
- Changing what any modal DOES, any reward maths, any fetch size.
