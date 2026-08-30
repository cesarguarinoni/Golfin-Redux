# SPEC — `daily_mission_home_pill`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-08-30 (Architect via Cowork). Follow-up to `missions_v1` (DONE 2026-08-30,
> `Docs/Specs/Completed/missions_v1/`), which shipped the Missions mode and the Daily Mission
> with **no Home-screen surface** — deferred then because it had no design. The Figma landed
> 2026-08-29; this is that surface. Cesar's brief, verbatim intent: the pill enters from the
> left of the screen with an animation and has a pulsating glow; the flame is only present for
> streaks and the number inside is the current streak (auto-size); the same flame goes on the
> streak counter of the Daily Mission card in Mission Selection; when a new mission appears
> because the timer ran out, the old pill disappears and a new one enters from the left; when
> the player completes the daily the pill disappears and reappears when there is a new one.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

A `NEW DAILY MISSION!` pill on the Home screen that tells the player a daily is waiting, shows
their streak in a flame, follows the notice panel's visibility, animates in/out, and opens
Mission Selection with the daily card. Plus the flame on the Mission Selection daily card.

## Reference

- **Figma:** file `5gEAHjl6xAtW8iYY7NMvWd` — Home `2098:8490` (with maintenance notice),
  `13994:1935` (without). Renders: `reference/Home_DailyPill_WithNotice_2098-8490.png`,
  `reference/Home_DailyPill_NoNotice_13994-1935.png`.
- **Placeholder vs canonical:** ⚠️ **both mockups are the OLD Home layout** (a `NEXT MISSION`
  card at the bottom, no mode carousel). Take ONLY the pill and its position relative to the
  notice panel from them. Everything else on Home stays as built (mode carousel, promo banner,
  nav bar) — do not touch the carousel. `5` is a sample streak; `USERNAME`, the notice copy and
  the card below are placeholder.
- **Art:** `Assets/Art/HomeScreen/Flame.svg` (source) and `Assets/Art/HomeScreen/flame.png`
  (import as a UI sprite; added 2026-08-30).

## Figma Fidelity (Rule 18)

| Element | Figma node | Property → value |
|---|---|---|
| Pill root `Mission Card Container` | `13994:1963` (no notice) / `13994:1927` (with notice) | 549×122; same navy panel + gold border style as the Home card headers; **left edge x 36** in the 1170-wide frame (Content Container 96 + Empty Space 10 − 70); **top y 361** with no notice, **y 725** with the notice (361 + News Banner Container 340 + 24 gap) |
| Flame | `I13994:1963;13994:2108` | 58×90 at (24, 16) inside the pill; sprite from `flame.png`; **visible only when streak ≥ 1** |
| Streak number | `I13994:1963;13994:2115` | `{streak}` centred in the flame (one digit renders 29×60 at 38,46); TMP **auto-size** (min 24 / max 60) so two digits fit; white, bold; hidden with the flame |
| Label | `I13994:1963;13994:1745` | key `HOME_DAILY_PILL` — EN `NEW DAILY MISSION!` / JA `新しいデイリーミッション！`; 433×60 at (92, 31); gold; same face as the card headers. When the flame is hidden the label keeps its x — do not re-centre |
| Glow (NEW, motion only) | — | a second Image behind the pill root, same border sprite, additive (`Assets/Prefabs/UI/TapSparkle_Additive.mat` or a soft-blurred copy of the border), alpha 0.25 ↔ 0.65 on a 1.6 s sine loop while shown |
| Tap target | whole pill | opens `ScreenId.MissionSelection` (same route as the Missions mode card; the daily card is there, expanded by `MissionSelectionScreenController.RefreshDaily`) |
| Mission Selection daily card — streak | Completed `missions_v1` daily card | replace the text-only streak (`MissionCardController.streakText` / `streakTextExp`, key `MISSION_DAILY_STREAK`) with the same `StreakFlame` prefab (flame + auto-sized number), collapsed AND expanded copies; hidden at streak 0 exactly as today's `SetDailyStatus` rule |

## Architecture context

- `Assets/Prefabs/UI/HomeScreen.prefab` + `Assets/Scripts/UI/HomeScreenController.cs` —
  `newsPanelRoot` (`GameObject`, toggled at `HomeScreenController.cs:429` from
  `Golfin.Notices.NoticeService.OnNoticesChanged`); no vertical layout group on Home (three
  `HorizontalLayoutGroup`s only) — the pill's Y is computed, not laid out.
- `Assets/Scripts/Economy/MissionsClient.cs` — `MissionsClient.Instance.FetchDailyRoutine(cb)` →
  `ApiResult<DailyMissionResult>` with `Date`, `RecipeHash`, `Claimed`, `ClaimedRp`, `Streak`,
  `Recipe`; `ClaimDailyRoutine(...)`.
- `Assets/Scripts/UI/MissionSelection/MissionSelectionScreenController.cs` — `RefreshDaily()`
  (`:441`), `FetchDailyRoutine` (`:448`), `SetDailyStatus(untilReset, streak, claimed)` call
  (`:485`), `ClaimPendingDailyRoutine`.
- `Assets/Scripts/UI/MissionSelection/MissionCardController.cs` — `SetDailyStatus` (`:490`),
  `streakText` / `streakTextExp` (`:114–115`), `streakChip` (legacy, unused — remove it in this
  task).
- `ScreenManager` (`ScreenId.MissionSelection`), `ModeSelectScreenController.TargetMissionSelect`.
- `Assets/Scripts/UI/ModeSelect/ModeCarouselController.SnapAndExpandCoroutine` — the eased
  coroutine pattern to copy (no tween library in the project).
- `FramePacingBootstrap` — the glow loop must not allocate per frame.
- Strings: `Assets/Localization/LocalizationText.csv` → `Tools/content/import_content.py`
  (see §Strings).

## Implementation

1. **`DailyMissionPill.prefab`** (`Assets/Prefabs/UI/HomeScreen/`): root `RectTransform`
   549×122 anchored top-left of the Home Content Container; children `Glow` (Image, behind),
   `Panel` (Image, border sprite), `StreakFlame` (prefab instance: `Flame` Image + `Number` TMP
   auto-size), `Label` (TMP, `LocalizedText` binder on `HOME_DAILY_PILL`), `Button` (whole
   root). `StreakFlame.prefab` is its own prefab so the Mission Selection card can instance it.

2. **`DailyMissionPillController`** (new, `Assets/Scripts/UI/Home/`), owned by
   `HomeScreenController` via `[SerializeField]`:
   - **State** `Hidden | Entering | Shown | Leaving`. Source of truth = one
     `MissionsClient.Instance.FetchDailyRoutine` on Home `OnEnable` (cached per UTC date; a
     cleared claim elsewhere sets `Claimed` on the cache through the same
     `MissionSelectionScreenController` claim path — expose a static `DailyMissionState`
     with `Date`, `Streak`, `Claimed`, `HasRecipe` and an `OnChanged` event; both screens read
     and write it, so the pill and the card can never disagree). `Shown` when
     `HasRecipe && !Claimed`; otherwise `Hidden`.
   - **Rollover**: a 1 s tick compares `DateTime.UtcNow.Date` to `DailyMissionState.Date`;
     on change → `Leaving`, then a fresh fetch, then `Entering` with the new streak. Same tick
     the Mission Selection countdown already runs — reuse the value, don't add a second clock
     class.
   - **Claim**: when `DailyMissionState.Claimed` flips true → `Leaving` (the Hole Complete
     modal returns to Home or Mission Selection; either way the pill is gone next time Home
     is shown, and animates out if Home is visible when the claim lands).
   - **Placement**: `anchoredPosition.y` = −(notice bottom + 24) when `newsPanelRoot.activeSelf`,
     else −0, relative to the Content Container top; recomputed on `OnNoticesChanged` and on
     every state change. Assert in a test: notice hidden ⇒ y 361-equivalent; notice shown ⇒
     y 725-equivalent (in the 1170×2532 reference space).
   - **Animations** (one coroutine, eased `t`, `SnapAndExpandCoroutine` style): *Enter* — x from
     −(549+36) to 36 over 0.45 s ease-out cubic, starting 0.25 s after Home becomes interactive
     (after the boot/loading fade, not during it); *Leave* — reverse, 0.30 s ease-in; *Glow* —
     alpha 0.25 ↔ 0.65, 1.6 s sine, runs only in `Shown`, stops at the start of `Leaving`.
     Rollover = Leave → (fetch) → Enter; if the fetch fails, stay `Hidden` (never a stale pill).
   - **Tap** → `ScreenManager.ShowScreen(ScreenId.MissionSelection)`; telemetry `daily_pill_tap`.
   - Offline: the deterministic local daily recipe `missions_v1` already builds counts as
     `HasRecipe`, so the pill shows; `Claimed` is whatever the last successful fetch said.

3. **Mission Selection daily card**: instance `StreakFlame` in the header of both the collapsed
   and expanded copies; `SetDailyStatus` writes the number and toggles visibility with the
   existing `showStreak` rule; delete `streakText`, `streakTextExp`, `streakChip` and the
   `MISSION_DAILY_STREAK` usage (leave the key in the CSV — keys are never deleted, only
   deactivated in the admin).

4. **Strings** (mandatory path — PIPELINE_HARDENING §24): add `HOME_DAILY_PILL` (EN + JA) to
   `Assets/Localization/LocalizationText.csv` → `python3 Tools/content/import_content.py
   --env-file … --catalogs texts` (plan, read verdicts) → `--apply` → publish `texts` from the
   admin → `export_content.py --check` clean. CONFLICTS ⇒ stop and report. No hardcoded `.text`.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Fidelity table PASS/FAIL against both Home renders (EN + JA screenshots, notice shown and hidden).
- [ ] Y follows the notice: screenshots with a live `home_notices` row and with none; flipping the notice live moves the pill without a relaunch.
- [ ] Enter animation from off-screen left with the glow loop running in `Shown` (video in `videos/`); no per-frame allocation (Profiler screenshot or `GC.Alloc` assertion).
- [ ] Claiming the daily (real claim on prod, `daily_mission` ledger row quoted) → pill leaves; next launch with a claimed daily → no pill.
- [ ] Simulated UTC rollover (device clock or a test seam on `DailyMissionState.Date`) → old pill leaves, fetch, new pill enters with the new streak.
- [ ] Flame + number hidden at streak 0; shown at 5 and at 12 (two digits fit, auto-size).
- [ ] Same `StreakFlame` prefab on the Mission Selection daily card, collapsed + expanded; `streakText`/`streakTextExp`/`streakChip` removed.
- [ ] Tap opens Mission Selection with the daily card present; `daily_pill_tap` telemetry event lands.
- [ ] `HOME_DAILY_PILL` reached the `texts` catalog via the importer; `--check` clean; grep shows zero new hardcoded `.text` literals.
- [ ] Mode carousel, promo banner, notice panel untouched (prefab diff limited to the new child + controller field); full EditMode sweep green; no Console errors.

## Files / hierarchy this task touches

- `Assets/Prefabs/UI/HomeScreen.prefab` — new `DailyMissionPill` child + controller wiring (only).
- `Assets/Prefabs/UI/HomeScreen/DailyMissionPill.prefab`, `Assets/Prefabs/UI/Common/StreakFlame.prefab` (new).
- `Assets/Scripts/UI/Home/DailyMissionPillController.cs`, `Assets/Scripts/Gameplay/Missions/DailyMissionState.cs` (new).
- `Assets/Scripts/UI/HomeScreenController.cs` — one field + placement hook on notice change.
- `Assets/Scripts/UI/MissionSelection/{MissionSelectionScreenController,MissionCardController}.cs` — write `DailyMissionState`; `StreakFlame` replaces the streak texts.
- `Assets/Art/HomeScreen/flame.png` import settings (Sprite 2D/UI); `Assets/Localization/LocalizationText.csv`; telemetry event.

## Smoke evidence

Editor: EN/JA screenshots of Home in the four states (notice × streak), Mission Selection daily card collapsed/expanded with the flame. Video of enter → glow → leave (claim) and of the rollover swap. Human play-and-confirm note (Lesson O) on the motion feel — timing values above are the starting point, Cesar signs off on device.

## Out of scope (do NOT do these)

- Any change to the mode carousel, promo banner, notice panel logic, or the Home card layout in the old mockups.
- New daily rewards, streak rules, or server changes — `missions_v1` shipped all of that.
- Mission leaderboards; the Rankings button.
