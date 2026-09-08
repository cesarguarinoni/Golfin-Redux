# SPEC — `game_polish_c` (the sweep)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Notion **2111** `game_polish`, slice **c** of three (a DONE `b2496871d`, b DONE `9c3ae0daf`). Map: `Docs/Specs/Queued/game_polish/MAP.md`. This is the per-screen sweep the map deferred: the things that are not motion but decide whether the shell *feels* finished — press feedback on every button, one scroll feel, safe area on every screen. Small, mechanical, and every item is a table with a verdict per site (PIPELINE_HARDENING §22 — enumerate, do not sample).

## Status

See `STATUS.md`.

## Goal

Every player-facing button in the shell presses, every list scrolls the same way, no screen's content sits under the notch or the home indicator. No new motion, no new strings, no `UiMotion` API change, nothing under `Gps/`.

## Reference

- **Rule 11** (`CLAUDE.md` Hard rules): every player-facing `Button` carries `Golfin.UI.Polish.ButtonPressFeedback` (`Assets/Scripts/UI/ButtonPressFeedback.cs`, GUID `6fe5cc7c…`, defaults `_pressedScale 0.95`, `_duration 0.12`). Lesson S in `tasks/lessons.md`.
- **Serialized baseline (Architect, 2026-09-08, YAML count — a `Button` script reference per file vs a `ButtonPressFeedback` reference):** ShellScene 183 vs 23; `GeneralShopScreen` 23 vs 6; `HoleCompleteModal` 4 vs 0; `HoleCompleteWidget` 6 vs 3; `HomeScreen.prefab` 7 vs 0; `RankingsScreen` 8 vs 2; `GachaPrizesScreen` 5 vs 0; `TournamentSelectionScreen` 4 vs 0; every Inventory card prefab 1–3 vs 0; `MatchMakingModal` 2 vs 0 … **354 Button references vs 102 feedback references** across the shell scene + non-GPS prefabs. The serialized count over-counts (template/inactive/disabled buttons, `Selectable` subclasses) — the audit is LIVE, per §C1; this is only the reason the sweep exists.
- **Scroll feel — the reference values** (`gps_polish` §D9, applied to every GPS list): `movementType Elastic`, `elasticity 0.1`, `inertia on`, `decelerationRate 0.135`. Serialized today on the game side: 13 `ScrollRect`s in ShellScene split `Clamped` ×11 / `Elastic` ×2 with `scrollSensitivity` ∈ {1, 20, 30, 40}; prefabs `GeneralShopScreen`, `RankingsScreen`, `StaminaShopSelectionScreen`, `TournamentSelectionScreen` `Clamped`/20; `GachaHistoryScreen` `Elastic`/50; the auth screens + `GachaRatesModal` `Elastic`/40.
- **Safe area:** `Assets/Scripts/UI/Core/SafeAreaFitter.cs` (used by `PersistentUIManager` for the top bar — `safe_area_top_bar`; by the GPS builders for their screens). ShellScene carries ONE `SafeAreaFitter` today. Simulator target: iPhone 15 Pro Max (1290×2796, top inset 59 pt, bottom 34 pt) in the Device Simulator; the shell canvas is 1170×2532.
- **Probes to reuse:** `GamePolishProbe` (a) for real navigation + captures; the b probe's modal driver for the modals.

## Design

### C1 · `ButtonPressFeedback` coverage (Rule 11 backfill)

1. **Enumerate LIVE.** In play mode, after REAL navigation to every shell screen (the a probe's route), every Inventory tab, every Settings submenu, and every modal opened through the b probe: every `UnityEngine.UI.Button` under the active canvas hierarchy (including inactive children — list them with `active:false`) → `Docs/Diagnostics/_capture/game_polish_c_buttons.json`: path, prefab/scene, `interactable`, `active`, `hasFeedback`, `playerFacing` (true unless the row's exclusion reason says otherwise), `exclusionReason`.
2. **Exclusions are named, not assumed.** A button is exempt only for one of: it is a template row that is never enabled at runtime (say who disables it), it is a debug/dev control gated by `GOLFIN_DEV`/Editor, it is a `Selectable` that is not tappable by a player (a scroll handle). Anything else without feedback is a defect.
3. **Fix by builder** (`GamePolishBuilder.ApplyPressFeedback()`, re-runnable): add `ButtonPressFeedback` with the default tunables beside every defective `Button` — prefab edit for prefab sources (the Inventory cards, the shop card, `HomeScreen.prefab`, modals), `SerializedObject` + `RecordPrefabInstancePropertyModifications` for scene objects. Runtime-spawned buttons (`GeneralShopCard` clones, rankings rows) get it from their prefab, not from code.
4. **Nav bar + result modals:** already covered by a/b — the table still lists them, verdict `already`.

### C2 · One scroll feel

Every `ScrollRect` a player drags in the shell (lists, grids, the accordion) → the reference values: `Elastic`, `0.1`, `inertia on`, `0.135`. `scrollSensitivity` only affects wheel/trackpad input, so it is unified to **20** for Editor consistency and noted as non-player-facing. **Exclusions (stay as they are, listed with the reason):** carousels with their own snap logic (`ModeCarouselController`, `BagCarouselController`/`ClubCarouselController`/`BallCarouselController`/`ItemCarouselController` — check each: if the controller drives position itself, the ScrollRect's movement type is not what the player feels; if the controller merely reads a plain ScrollRect, it IS in scope), the auth screens (Tier 2, not this track), `GachaRatesModal` (already Elastic). Per-site table: path, before (type/sens), after, reason if excluded. One short clip of a Rankings over-scroll and an Inventory Items over-scroll.

### C3 · Safe area on every screen

Per screen / overlay / modal (the C1 route), a capture on the iPhone 15 Pro Max simulator view with the safe-area overlay drawn (the probe draws `Screen.safeArea` as a translucent rect): verdict per surface — `clear` / `top hit` / `bottom hit` — with the offending element path. Fix = the existing `SafeAreaFitter` on the screen's content layer (the `LayeredPush` content layer from a's map — `Content`/`ContentArea`/`CardsContainer`/`GameScreenContent`), or on the modal panel, never a hand-typed offset; backgrounds stay full-bleed. The bottom nav is already anchored (`PersistentUI`); the Settings overlay and the result modals are the likely hits — measure, don't assume. **Rest parity on a 1170×2532 view stays 0 px** (the fitter is a no-op when the safe area is the full screen) — that is the regression gate.

### C4 · Toast on `UiMotion`

`ToastController.Fade` → `UiMotion.Fade` (same durations; zero visible change; per-frame alpha log old vs new as b's retrofit gate, max Δ ≤ 0.01). Only because the map listed it — 20 lines, one less hand-rolled fade.

### C5 · The report

Three tables (C1 buttons, C2 scroll rects, C3 safe area) with a verdict per site including the ones that were already fine, the counts generated by a script in `Docs/Scripts/` from the JSON (b's `check_report_counts.py` discipline — no typed totals), and a `## Not done` section.

## Localization

No strings.

## Architecture context

- **Touched:** `GamePolishBuilder.cs` (+ `ApplyPressFeedback`, `ApplyScrollFeel`, `ApplySafeArea`), the prefabs and scene objects the tables name, `ToastController.cs`, the a/b probes (a `sweep` mode that walks the route, dumps buttons + scroll rects, captures with the safe-area overlay).
- **Untouched:** `UiMotion` API, `ButtonPressFeedback.cs` itself (defaults stay), `SafeAreaFitter.cs`, `Gps/**`, `FadeController`, `LayeredPush`, `NavSlotHighlight`.
- EditMode: `PressFeedbackCoverageTests` (every prefab under `Assets/Prefabs/UI/` minus `Gps/`: Button count == feedback count, with the exclusion list as the only allowed difference — this is the regression guard, and it runs on every future prefab), `ScrollFeelTests` (every in-scope ScrollRect at the reference values), `ToastFadeParityTests`.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] **A1 · Button table** — live JSON, per-site verdict, exclusions named; after the builder: `playerFacing && !hasFeedback` count **0** (script-generated), before count quoted.
- [ ] **A2 · Press feedback seen** — one clip of five buttons that lacked feedback before (one per pillar) pressing at 0.95 after; one still each.
- [ ] **A3 · Scroll table** — every ScrollRect in scope at the reference values, exclusions with reasons; the two over-scroll clips.
- [ ] **A4 · Safe-area table** — every surface with its verdict + capture at iPhone 15 Pro Max; every `top hit`/`bottom hit` fixed with `SafeAreaFitter` and re-captured `clear`.
- [ ] **A5 · Rest parity 0 px** at 1170×2532 on every screen vs the b baselines (the fitter and the feedback component change nothing at rest; the scroll settings change nothing at rest).
- [ ] **A6 · Toast parity** log within tolerance.
- [ ] **A7 · Lint** delta zero on every prefab touched.
- [ ] **A8 · Tests** — `PressFeedbackCoverageTests` tripwired (§20: plant a Button without feedback in a temp prefab → red → remove → green, quoted); full EditMode sweep green.
- [ ] **A9 · Scope** — no `Gps/` path, no `UiMotion.cs` change, no `ButtonPressFeedback.cs` change (`git diff --stat` quoted).
- [ ] **A10 · Report counts** generated, `check_report_citations.py` 0 unresolved.
- [ ] **A11 · Deviations** with justification.

## Out of scope (do NOT do these)

- Any new motion; the reduced-motion switch (Notion 2157); haptics (2130); Rubik Medium / JA font binding (2189/2196); the ÷1.4 / ÷1.2 sizes (2191/2192); the in-game HUD (2195); the auth screens; changing `ButtonPressFeedback`'s defaults; anything under `Gps/`.
