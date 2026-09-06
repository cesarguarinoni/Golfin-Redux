# scheme_evaluation — per-scheme decision data, tester notes, decision checklist

**Status:** SPEC_READY (2026-09-06) · **Size:** Quick (½ Code day + Architect docs) · **Roadmap:** Notion 2135
**Plan:** `Docs/CONTROL_SCHEMES_PLAN.md` §1.4, §5, §6 row 4 · **Depends on:** `control_scheme_seam`, `scheme_pendulum`, `scheme_needle`, `scheme_freeswing`, `scheme_confirm_popup`, `bot_scheme_parity` (all DONE)

## 1. Goal
Everything needed to pick the **default** control scheme from beta data instead of taste: the four schemes are live behind Settings (default Flick, plan §7.2), the timing card already splits per scheme, but three of the five decision metrics in plan §1.4 cannot be computed yet because the rows that carry them do not know which scheme they were played on. This spec closes those gaps, adds one comparison card to the dashboard, and ships the tester notes + the checklist Cesar and Ken will decide against.

## 2. What exists (verified 2026-09-06 — reuse, do not rebuild)
- `shot_taken` carries `scheme` (int, `GameSession.AppendShotTimingKeys(payload, shot, schemeId)`, written from `TelemetryHooks.OnHistoryChanged`) plus `timing01 / timing_mul / timing_band`.
- `controls_scheme_changed { from, to, where, hole }` from `TelemetryHooks.OnControlSchemeChanged` (`where` ∈ settings / ingame / settings_popup / ingame_popup).
- Dashboard: `Tools/admin-dashboard/lib/telemetryData.ts` `timingByScheme` (`SchemeTimingStat` in `lib/types.ts` l.643), scheme filter + `SCHEME_LABEL_KEYS` in `app/(panels)/telemetry/telemetry-panel.tsx` l.38–45 / l.177–283, i18n keys `tel.shots.scheme*` in `lib/i18n.ts` l.971–983, mock spread in `lib/mockTelemetry.ts` l.292–295.
- `hole_complete { hole, strokes, penalty_strokes, par, result, duration_s, fps_avg, … }` from `TelemetryHooks.OnHoleComplete` — **no `scheme`**.
- `flick_rejected { speed, hole, shot_number }`, `shot_cancelled { hole, shot_number }` via `ShotTelemetryRelay` — **no `scheme`**. Both fire for every scheme already (Pendulum `RejectExternalDrag` → `FlickRejected`; all drivers `CancelExternalDrag` → `ShotCancelled`).
- `ShotController.ResolveAndPublish` (l.755) latches `LastTimingPowerMul` / `LastCommittedTiming01`; `HoleSessionDriver` (l.118–125) reads them into `ShotRecord`. **No error-yaw or power snapshot** — miss-shot rate (plan §1.4) is not measurable.

## 3. Client (Unity) — three small additions

### 3.1 `scheme` on the three scheme-less rows
`TelemetryHooks.cs`:
- `OnFlickRejected`, `OnShotCancelled`: add `["scheme"] = (int)ControlSchemeService.Current`. Safe: `ShotSchemeHost` defers swaps to Idle, so the scheme cannot move between the gesture and the row.
- `OnHoleComplete`: add `["scheme"] = _holeStartScheme` and `["scheme_mixed"] = _holeSchemeMixed`. `_holeStartScheme` is captured from `ControlSchemeService.Current` in `OnRoundStarted` and again right after each `OnHoleComplete` payload is built (next hole starts on the scheme in force then); `OnControlSchemeChanged` sets `_holeSchemeMixed = true`, cleared where `_holeStartScheme` is captured. A hole played on two schemes is reported, not guessed: the dashboard excludes `scheme_mixed` rows from per-scheme strokes.
- Assembly note: `TelemetryHooks` is the one assembly that sees both `ControlSchemeService` and the session (existing comment at l.313–318) — keep the read there, not in `GameSession`.

### 3.2 Miss and overpower snapshots on `shot_taken`
- `ShotController.ResolveAndPublish` gains two latched properties next to `LastCommittedTiming01`: `LastCommittedErrorYawRad` and `LastCommittedPower01`. Both callers pass what they already hold: the Flick path passes its own error term and `mag`-derived power; `CommitExternal` passes `i.ErrorYawRad` / `i.PowerNormalized`. NOTE: pass the **error component only**, not the aim yaw — `aimYawRad` already folds aim + error; if the Flick path has no separate error variable at that point, latch it where `EvaluateFlickGate`/the yaw error is computed and read it in `ResolveAndPublish`.
- `ShotRecord` gains `ErrorYawDeg` (abs, degrees) and `Power01`; `HoleSessionDriver` fills them from the two snapshots (same pattern as `timing01` at l.118–121; NaN when no controller).
- `GameSession.AppendShotTimingKeys` writes `["err_yaw_deg"] = Round(|ErrorYawDeg|, 1)` and `["overpower"] = Power01 > 1.0f`. Existing keys and their rounding untouched; `ShotTimingTelemetryTests` extended for the two keys.
- `ShotController` diff is confined to the two properties + the `ResolveAndPublish` signature. Flick physics output must stay byte-identical (`ShotControllerSeamParityTests` is the guard).

### 3.3 Bots
Bot rows already carry `scheme` through the same hooks — nothing to add. `VersusBot` shots are excluded from the comparison the way they are excluded today (NOTE: confirm the existing bot filter in `telemetryData.ts`; if none exists, exclude by the existing `is_bot`/user marker and say which in the report).

## 4. Dashboard — one "Scheme comparison" section
`Tools/admin-dashboard`, telemetry panel, new `<Section id="schemes" title={t("tel.schemes.title")}>` directly under the Shots section. One table, four rows (Flick / Pendulum / Tap Timing / Free Swing — `SCHEME_LABEL_KEYS`), columns:

| Column | Source | Rule |
|---|---|---|
| Players | distinct `user_id` with ≥1 `shot_taken` on the scheme | adoption |
| Shots | `shot_taken` count | existing `timingByScheme.shots` |
| Strokes over par | mean(`strokes + penalty_strokes − par`) over `hole_complete` where `scheme` = row and `scheme_mixed` = false | em-dash when < 5 holes |
| Miss rate | share of `shot_taken` with `err_yaw_deg > 0.5` | plan §1.4 "miss-shot rate" |
| Mean \|err yaw\| | mean `err_yaw_deg` | ° with 1 decimal |
| Gold / Green / Red | existing `timingByScheme` rates | reuse |
| Cancel rate | `shot_cancelled` (scheme) ÷ (`shot_cancelled` + `shot_taken`) | mirrors `cancelRate` |
| Reject rate | `flick_rejected` (scheme) ÷ (… + `shot_taken`) | mirrors `flickRejectRate` |
| OB rate | `terminal == OB` share | mirrors `obRate` |
| Overpower | share of `shot_taken` with `overpower` true | putts never overpower, so this is full swings only |
| Switched to | `controls_scheme_changed` with `to` = row, split settings / ingame (pop-up variants folded into their surface) | discovery |

Rows lacking `scheme` bucket to Flick exactly as `timingByScheme` does (comment at `telemetryData.ts` l.394–396 — same reasoning, reuse the same helper). Types: extend `SchemeTimingStat` or add `SchemeComparisonStat` in `lib/types.ts`; aggregation next to `timingByScheme`; mock data in `mockTelemetry.ts` gets `scheme` on `hole_complete` / `shot_cancelled` / `flick_rejected` rows and `err_yaw_deg` / `overpower` on `shot_taken` (spread across schemes like l.292–295). Strings: `tel.schemes.*` in `lib/i18n.ts` `DICT`, `en` + `ja` (ADMIN_DASHBOARD_OPS §3.4). A CSV export button is NOT in scope.

## 5. Tester notes (Architect deliverable — in this folder)
`TESTER_NOTES.md` (EN + JA) — the paragraph for TestFlight "What to Test" and Ken's daily report: how to switch (Settings › Controls, or the in-game gear), play at least one full 9 on each scheme before judging, what to write back (one line per scheme: feel, confusion, hand fatigue). Code does not touch this file.

## 6. Decision checklist (Architect + Cesar + Ken)
Decide the default when **each** scheme has ≥ 3 players and ≥ 150 shots (Flick will have more; that is fine). Compare on:
1. Strokes over par (lower wins; ties within 0.2 are ties).
2. Miss rate and mean |err yaw| — the fairness signal; a scheme that is easier only because its window is wider shows up here as *both* lower miss and lower strokes, which is a tuning question, not a winner.
3. Cancel + reject rate — friction. > 15 % on a scheme means the gesture is being fought, not learned.
4. Switched-to counts and whether players who tried a scheme *stayed* (their later shots on it) — retention beats first impression.
5. Feel notes from §5, tie-breaker only.
Outcome is a one-line entry in `CONTROL_SCHEMES_PLAN.md` §7 ("Default: X, decided YYYY-MM-DD") and, if X ≠ Flick, a `ControlSchemeService` default change filed as its own Quick spec (with the pop-up shown once on first launch after the change).

## 7. Acceptance
- EditMode: `ShotTimingTelemetryTests` cover `err_yaw_deg` / `overpower`; `ShotControllerSeamParityTests` + `ShotControllerFlickGateTests` pass unchanged; a new test asserts `hole_complete` `scheme_mixed` flips when the scheme changes mid-hole and resets on the next hole.
- Lab run (LabScaffold, each scheme once, one cancel and one hole-out): the four row kinds carry `scheme`; `shot_taken` carries `err_yaw_deg` and `overpower`; quote one row of each in the report.
- Dashboard: `npm run typecheck` + `npm run lint` clean; mock mode renders four rows with no em-dashes except where the rule says so; live mode against the beta project renders without console errors.
- Zero new hardcoded strings: dashboard text through `DICT` en+ja (grep quoted in the report). No `LocalizationText.csv` change is expected — if one appears, it is wrong.
- **All new player text from the whole schemes track is imported AND published in the admin** (Cesar, 2026-09-06): every `SETTINGS_CONTROLS*`, scheme grade-pop, Fade-Draw/analyzer and `SCHEME_CONFIRM_*` key added by `control_scheme_seam`, `scheme_pendulum`, `scheme_needle`, `scheme_freeswing` and `scheme_confirm_popup`. Run `python3 Tools/content/export_content.py --check` for `texts` — clean means the published catalog matches `LocalizationText.csv`. If any key is missing or unpublished, take it through the importer (`import_content.py --catalogs texts` PLAN → `--apply` → publish `texts` in the admin) and list the keys in the report. CONFLICTS in the plan = stop and report, never `--overwrite-dirty`.

## 8. Out of scope (backlogged same session)
- CSV export of the comparison table.
- Per-player retention curve (players who switched and stayed) beyond the "switched to" counts.
- Any change to scheme tuning or the default itself — that is §6's outcome, filed separately.
