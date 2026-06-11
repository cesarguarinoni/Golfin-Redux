# REDTEAM_REVIEW — `versus_bot_difficulty` (1v1 Phase 2b)

**Iteration:** 1
**Reviewer:** golfin-redteam-reviewer (adversarial gate — last automated step before Cesar)
**Timestamp:** 2026-06-11 08:08 CEST
**Verdict:** **ARCHITECT_REVIEW_PASS** — genuinely tried to break it on every flagged axis, could not.
**STATUS set to:** `ARCHITECT_REVIEW_PASS`

---

## Evidence I generated myself (not re-used from prior reviewers)

- **Frames I extracted** (own angle, not the reviewer's t=10s): `bot_lv1_sloppy.mp4` and `bot_lv200_hardened.mp4` at t=5s, t=20s, t=40s → `/tmp/rt_frames/{lv1,lv200}_t{5,20,40}.jpg`. Visually inspected lv1_t5, lv200_t5, lv1_t40, lv200_t40 inline.
- **Numbers I re-ran:** independent bound + stdev + spread re-derivation from the pasted roll tables (Python); club-noise-only overshoot sweep 10–360m; D2 power-error overshoot quantification; full downstream commit-path code trace.
- **Source I re-read directly:** `VersusBot.cs` (full, 1–796), `PhysicsLabController.SetCameraYawRadians` (`:807`), `ShotController.{BeginExternalDrag,SetExternalPower,EndExternalDrag,CommitFlick}` (`:76-127`, `:237-294`), `LabClubs`/`PutterIndex` (`:578-588`), `bot_difficulty.csv` + `.meta`, `bot_clubs.csv`, `VersusHudCaptureMenu.cs` diff, `VersusMatchController` grep for `_debugBothBots`/`OnMatchReadyToBegin`.

## Prior Cesar rejections to replay

None — no `CESAR_REJECTION.md`. This is the first pass through the red-team gate. Step 1 (replay every prior rejection) is N/A.

---

## Break attempt 1 — D1: is a perturbed shot silently corrected back ANYWHERE downstream? (load-bearing)

**Attack:** the spec lives or dies on "no safety re-check after perturbation." Reviewers only checked `:691→:696`. I followed the perturbed values ALL the way into the physics solver to find a hidden clamp/retarget.

- Between injection-end `:691` and commit, the only code is `Debug.Log` at `:693`. Confirmed by direct read. ✓
- `_controller.SetCameraYawRadians(aimYaw)` → `PhysicsLabController:807-812`: pure assignment `_cameraYaw = yawRadians; _shotController.CameraHeadingRadians = _cameraYaw;`. **No clamp, no retarget, no safe-yaw recompute.** ✓
- Drag path `BeginExternalDrag` → ramp `SetExternalPower(power01, 0f)` → `EndExternalDrag` → `CommitFlick`:
  - `SetExternalPower` (`ShotController:84-92`): only `Mathf.Clamp01(powerNormalized)` — a 0..1 range clamp, NOT a clamp toward any safe target. Bot already Clamp01'd. ✓
  - `CommitFlick` (`:237-245`): `_aimYawRadians = CameraHeadingRadians + finetune*HalfConeAngleRad() + degradYaw`. Via the drag path `_coneFinetune` was set to `0f` by the bot (passes `coneFinetune=0`), and `_degradationYawRad` is reset to 0 in `Reset()` and only set non-zero in `FireDebugShot` (which the bot does NOT use). So `_aimYawRadians = CameraHeadingRadians` = the perturbed aim, **verbatim**. ✓
  - `flickMag = PowerNormalized` (perturbed, already-clamped power). No safe-target cap. ✓

**Why it failed to break:** there is no code path — not in `VersusBot`, not in `SetCameraYawRadians`, not in the external-drag commit — that re-targets or clamps the perturbed shot back toward a safe spot. The error fully reaches the deterministic physics input. D1 holds.

## Break attempt 2 — D3/D2: can a low-level layup still blow into water?

**Attack (D2 overshoot):** even if D3 club-noise can't overshoot, D2 power error applies ON TOP. Quantified with the live carry table: a wedge layup to a safe target can be pushed **+39.6m (safe=60m) to +61.1m (safe=150m)** past the target by `+0.12` power error at lv1 — easily into a hazard the bot just laid up to avoid.

**Why it's NOT a break:** this is the spec's *explicit, locked intent* (§2 D1, lines 19-21): *"A low-level bot aims at the safe spot and misses into water... The existing 2a drop/penalty flow handles the consequence of an errored shot finding water."* The difficulty model is SUPPOSED to let a sloppy bot overshoot a layup via execution error. The D3 "can never overshoot" claim is scoped to *club noise alone* (§D3 line 35), with "The D2 aim/power error then applies ON TOP" stated openly. Satisfies letter AND intent.

**Attack (D3 non-monotonic/extrapolation overshoot):** probed whether `InterpolateClubPower(noisyClub, Min(safeTargetDist, maxCarry))` could yield MORE carry than the safe target. Swept every club±1 shift across 10–360m: **worst-case club-noise-only overshoot = 0.0m**. The carry tables are strictly monotone per club (verified in `bot_clubs.csv`), so `InterpolateClubPower ∘ carry-inversion` is an identity → re-derived power reproduces the safe carry exactly (or undershoots when clamped to a shorter club's `maxCarry`). No overshoot path exists. D3 holds.

## Break attempt 3 — can both levels secretly play identically? (bracket/override bug)

**Attack:** if `DebugLevelOverride` weren't plumbed to the lookup, or the bracket always resolved to the same row, lv1 and lv200 would be the same.

- `ResolveBracket` reads `DebugLevelOverride >= 0 ? DebugLevelOverride : MatchContext.Players[1].Level` at `:173-175` **before** the lookup. Override 1 → bracket(minLevel=1, aim 6.0); override 200 → bracket(minLevel=180, aim 0.4). Lookup `:191-199` iterates ascending, picks highest `minLevel ≤ level`. Correct.
- `VersusHudCaptureMenu.cs` sets `bot.DebugLevelOverride = debugLevel` (1 or 200) directly at scenario start. Verified in diff.
- **Independent dispersion re-derivation** (own Python, from the pasted tables): all rolls within bracket bounds; **14.7× aim / 12.2× pow** spread delta (matches architect's 14.6×/12.2×); clubNoise 5/25 (p=0.25) vs 0/25 (p=0.0). The lv180 bracket genuinely carries clubNoiseChance=0.0. Per-bracket **stdevs match the theoretical uniform distribution to 3 sig figs** (lv1 aim 3.341 vs expected 3.46; lv180 aim 0.231 vs expected 0.231) — authentic `Random.Range`, not a fabricated table.
- **Videos are two genuinely different matches** on Hole_04, not one captioned twice: lv1_t5 = "TARO Lv 1" / IRON 180 / 58%·144.7yd / trees-left framing; lv200_t5 = "TARO Lv 200" / WOOD 230 / 52%·129.3yd / bunkers+green framing; they stay distinct at t=40s (lv1 3yds skewed aim line, lv200 5yds straight line). Correct level badges throughout.

**Why it failed to break:** the override reaches the lookup, the brackets resolve to a 15× difference, and the spread is both statistically authentic and visually readable. The asymmetry the spec demands (sloppy human vs hardened baseline) is delivered.

---

## Secondary checks (all clean)

- **D4 putter suppression — both directions:** `PutterIndex == LabClubs.Length-1 == 3`. Club-noise gated by `!isPutt` at `:656` (no noise when already putter) AND `Mathf.Clamp(club+dir, 0, 2)` at `:661` (putter index 3 can never be a noise-IN target). `ClubNames` indices align with `LabClubs`. Off-green putter override (`:483-495`) correctly resets `isPutt=false`/`club=2` before injection, so `safeTargetDist` re-inversion round-trips cleanly. ✓
- **`-1` int sentinel:** `_resolvedLevel = -1` (`:81`), cache key `_resolvedLevel == level` (`:178`) — int compare, not a bool guard. Domain-reload-safe. ✓
- **Missing-CSV fallback returns a usable zero-error bracket (not null-ref):** `EnsureDifficultyLoaded` early-returns on null TextAsset with a `LogWarning`, leaving an empty (non-null) list; `ResolveBracket` `:184-189` then returns `{minLevel=0, aim=0, pow=0, noise=0}`. No `throw`, no null-ref. The implementer-report wording slip ("returns without setting `_difficultyLoaded=true`") is cosmetic — net behavior is correct (both reviewers flagged it). ✓
- **CSV + meta:** `bot_difficulty.csv` byte-matches §3 (6 brackets); `.meta` GUID `13233c9558f34d8785e01f0d82a94aeb`, TextScriptImporter (Lesson R). ✓
- **Diff confinement / shippability:** `git diff --stat HEAD` = `VersusBot.cs`, `VersusHudCaptureMenu.cs`, task STATUS.md only. `git status --porcelain` out-of-scope = only the new CSV+meta (allowed by §6). `VersusMatchController` byte-untouched (`_debugBothBots`/`OnMatchReadyToBegin` pre-exist). No `#if UNITY_EDITOR`, no `ForceShotCompleteForBot` in `VersusBot.cs`. Videos gitignored. ✓
- **`InvertClubPower` 50m fallback** (`:761`): only fires if the carry table loaded but lacks the club name — impossible per the calibration harness, and `Min(safeTargetDist, maxCarry)` downstream still clamps. Defensive smell, not a defect. ✓

## Three-way verdict on the flagged risk notes

The architect routed forward 4 risk notes. My adjudication: (1) `InvertClubPower` design — mathematically identity on the monotone table, swept to 0.0m overshoot, fine; (2) report wording slip — cosmetic, net behavior correct; (3) bracket re-resolve on inspector toggle — strictly more robust than spec, production never toggles; (4) 50m fallback — impossible-in-production, harmless. None rise to a blocker.

---

## Final verdict

**ARCHITECT_REVIEW_PASS.** I attacked the load-bearing D1 claim through the entire downstream commit path (`SetCameraYawRadians` → external-drag → `CommitFlick`) and found no clamp/retarget that corrects a perturbed shot back — D1 is airtight. I constructed the D2 overshoot numerically and confirmed it is the spec's deliberate intent, not a bug, while proving club-noise-alone overshoot is exactly 0.0m. I independently re-derived the dispersion (14.7× aim / 12.2× pow, stdevs matching the uniform distribution to 3 sig figs) and confirmed the two videos are genuinely different Hole_04 matches with correct level badges and visibly different bot play. Putter suppression, the `-1` sentinel, the zero-error fallback, the CSV+meta GUID, and diff confinement all hold. Could not find a concrete blocker after genuinely trying.

STATUS set to `ARCHITECT_REVIEW_PASS`.
