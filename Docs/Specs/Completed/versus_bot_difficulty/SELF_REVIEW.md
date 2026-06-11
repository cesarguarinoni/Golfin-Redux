# SELF_REVIEW — `versus_bot_difficulty` (1v1 Phase 2b)

**Iteration:** 1
**Reviewer:** golfin-self-reviewer
**Timestamp:** 2026-06-11 14:55 JST
**Verdict:** **FORWARD_TO_ARCHITECT** (PASS)
**STATUS set to:** `READY_FOR_ARCHITECT_REVIEW`

---

## Visual diff notes (Step 1 — pixel-only description)

This is a gameplay-behavior task, not a UI/Figma task. There is no Figma reference. The "canonical screenshots" are frame extracts at t=10s from the two bot-recorded clips, used here as evidence that `DebugLevelOverride` reaches the runtime and that the P2 card displays the overridden level.

**`screenshots/lv1_sloppy_frame10s.png` (1170×2532):**
- Top banner: yellow text "CAM: Chase  BALL: Flying" with a circular settings gear icon (top-right corner).
- Top-left HUD card: portrait of "CAMILA" in a red cap, "Lv 13", "TURN 1"; small "0.0 mph" badge directly below.
- Top-right HUD card: portrait of "TARO" in a brown/red cap, "Lv 1", "TURN 0"; "30 yds" badge below.
- Center field: vertical bright-blue aim line down the middle of a green fairway with tree shadows on the left edge.
- Mid-right: circular power dial reading "58%" / "144 / yd" with an orange-and-blue progress ring.
- Mid-right strip: small green grid + flag icon (mini-map / green preview).
- Bottom row: SPIN button (left), STRAIGHT direction indicator (right of center), GOLFIN ball selector (bottom-left), IRON 180 yds club selector (bottom-right).

**`screenshots/lv200_hardened_frame10s.png` (1170×2532):**
- Same banner / layout / HUD shell.
- Top-right HUD card: "TARO Lv 200, TURN 0", "15 yds" badge.
- Power dial: "52%" / "174.4 yd".
- Club selector: WOOD 230 yds.
- Same vertical blue aim line down the fairway, slightly different camera angle.

Both stills carry the production HUD un-mutated, which means the difficulty changes are confined to bot decision-making and have not bled into UI surfaces — consistent with §6 ("no changes to HUD").

---

## Bbox verification

N/A — no containment/UI-layout claim in this task. The visual gate evidence is "the level the bot is playing at is being applied correctly," which I verified via (a) the P2 card numerics (`Lv 1` vs `Lv 200`), (b) the dispersion proof tables, and (c) direct code reading.

---

## Figma fidelity

N/A — SPEC.md has no `figma.com` URL or `n:n` node reference. The §9 visual gate calls for two bot videos demonstrating behavior delta, not a pixel-perfect Figma A/B. Rule 18 does not apply.

---

## Acceptance checklist walk (§8)

| Item | Implementer | My verdict | Reasoning |
|---|---|---|---|
| `bot_difficulty.csv` (+meta) ships with §3 values; parsed via bot_clubs pattern; missing CSV → zero-error fallback with warning, no throw | PASS | **CONFIRM-PASS** | `Assets/Resources/Data/bot_difficulty.csv` byte-matches §3 (6 brackets, exact tuning values). `.meta` is a valid `TextScriptImporter` with GUID `13233c9558f34d8785e01f0d82a94aeb` (Lesson R satisfied). `EnsureDifficultyLoaded()` at `VersusBot.cs:124-162` mirrors the `EnsureTableLoaded()` pattern: catches `csv == null`, logs `Debug.LogWarning("[VersusBot] bot_difficulty.csv not found — zero-error fallback (hardened baseline).")`, returns without throwing, leaves `_difficultyTable` as an empty list. `ResolveBracket()` at `:184-189` then takes a second-level fallback (`{aim=0, pow=0, noise=0}` bracket) if the table is empty — so a missing CSV cleanly degrades to the hardened-baseline behaviour. Minor note: the implementer's report wording ("returns without setting `_difficultyLoaded=true`") doesn't match the code (`_difficultyLoaded=true` IS set before the null-check), but the net behaviour is correct because the second-level fallback in `ResolveBracket` handles the empty table. Not a defect. |
| Bracket resolved once per match from opponent's real `MatchContext` level; resolved bracket logged once | PASS | **CONFIRM-PASS** | `ResolveBracket()` at `:169-206`: reads `DebugLevelOverride >= 0 ? DebugLevelOverride : MatchContext.Players[1].Level`, caches via `_resolvedLevel == level` short-circuit (NOT a bool guard — explicitly uses the int sentinel `_resolvedLevel = -1` per the spec's domain-reload trap warning). One log line on first resolve: `[VersusBot] Difficulty: level=L bracket(minLevel=M) aim=±A° pow=±P clubNoise=C`. Within a single match (level invariant) the cache hits and re-logs never fire. Slightly more flexible than spec ("once at match start") — re-resolves if `DebugLevelOverride` is toggled in the Inspector mid-match — but that's a robustness extension, not a violation. |
| Error injected post-H2/H3, pre-commit; per-shot error log line present; **no safety re-check** on the perturbed shot | PASS | **CONFIRM-PASS — code-verified by direct read** | This is the locked-design check. Injection block at `VersusBot.cs:629-691`, immediately after the H3 putt-slope block closes at `:627`. After injection ends at `:691`, the next executable statements are: `:693` summary log, `:696` `_controller.SetClub(club)`, `:697` `_shotController.ClearStatBundleOverride()`, `:700` `_controller.SetCameraYawRadians(aimYaw)`, then the Idle gate (:702-714), the Aiming gate (:717-729), and the drag ramp (:732-746). **No conditional branching, no `WaterAvoid` / `SafeYaw` / `EvaluateLandingProbe` / retarget call** lives between `:691` and the commit. The perturbed `(club, power01, aimYaw)` flow straight to the production commit path. Per-shot log at `:689` confirmed: `[VersusBot] 2b error: Δaim={...}° Δpow={...} clubNoise={...}`. |
| Club noise: ±1 band shift, power re-inverted via `InterpolateClubPower` to same safe target, clamped to `GetMaxCarry`; suppressed when club is putter (in or out) | PASS | **CONFIRM-PASS** | Block at `:654-676`. (a) Suppressed on putts: `if (!isPutt && bkt.clubNoiseChance > 0f && Random.value < bkt.clubNoiseChance)` — `isPutt` is `club == PhysicsLabController.PutterIndex` from `:472`. (b) ±1 band shift: `int dir = (Random.value > 0.5f) ? 1 : -1; int noisyClubIndex = Mathf.Clamp(club + dir, 0, 2);` — `ClubNames = { "driver", "iron7", "wedge", "putter" }` at `:60`, so clamping to `[0..2]` excludes index 3 (putter) — putter can never be noised-INTO. Driver+(-1) clamps to 0 (no-op, which the `noisyClubIndex != club` guard then skips, falling through to D2 with no club change — correct end-clamp behaviour). (c) Power re-inverted to same safe target: `safeTargetDist = InvertClubPower(origClubName, power01)` at `:652` recovers the target that produced the current power, then `InterpolateClubPower(noisyClubName, Mathf.Min(safeTargetDist, maxCarry))` at `:668` re-derives power for the noisy club at that SAME target, clamped to `GetMaxCarry(noisyClubName)`. (d) Final `Mathf.Clamp01(noisyPower)` at `:669`. Result: cannot overshoot the safe target — by construction it can only undershoot (when `maxCarry < safeTargetDist`) or hit the same target with a different trajectory. The implementer chose to recover `safeTargetDist` via inverse interpolation rather than caching it from H1/H2 — slightly different from the spec's intent but mathematically equivalent because `power01` was set by `InterpolateClubPower(club, safeTargetDist)` in H1/H2 and `InvertClubPower` round-trips that. The only edge case where the inverse would drift is putts (where H3 nudges `power01` after club selection), but D3 is suppressed on putts — so the inversion is always clean in the cases it fires. |
| Putts: D2 error applies after H3 slope correction | PASS | **CONFIRM-PASS** | H3 putt block at `:583-627` (only entered when `isPutt && _greenReader != null`); modifies `aimYaw` (`:606`) and `power01` (`:617`). 2b block begins at `:629`, runs D2 unconditionally for all shots (no `!isPutt` guard on D2 at `:681-687`). Execution order: H3 → D2. On putts, the bot reads the break correctly via H3 then executes imperfectly via D2 — exactly per D4. |
| `DebugLevelOverride` (-1 default) overrides MatchContext level for capture | PASS | **CONFIRM-PASS** | `[SerializeField] public int DebugLevelOverride = -1;` at `:35` — no `#if UNITY_EDITOR` (production-safe; -1 means "use MatchContext"). `ResolveBracket()` reads it at `:173-175`. Verified at runtime via the two canonical frames: `screenshots/lv1_sloppy_frame10s.png` shows "TARO Lv 1" + IRON club choice on a 144yd shot at 58% power; `screenshots/lv200_hardened_frame10s.png` shows "TARO Lv 200" + WOOD club choice at 52%/174yd on the same hole. Both stills are unambiguous evidence the override propagates to the bot and to the HUD. `VersusHudCaptureMenu.cs` sets `bot.DebugLevelOverride = debugLevel` at scenario kickoff (`:868-873` of the modified file). |
| Dispersion sanity proof: ≥20 rolls at minLevel=1 and ≥20 at minLevel=180; bracket-1 spread visibly wide, bracket-180 near-zero | PASS | **CONFIRM-PASS** | Two roll tables in IMPLEMENTER_REPORT § Dispersion proof, 25 rolls each. Lv1: aimRange [-5.53..+5.50]° (within ±6.00° band), powRange [-0.1191..+0.1137] (within ±0.120 band), clubNoiseCount 5/25 (expected ≈6.3 at p=0.25). Lv180: aimRange [-0.37..+0.38]° (within ±0.40° band), powRange [-0.0098..+0.0091] (within ±0.010 band), clubNoiseCount 0/25 (expected 0 at p=0). Spread delta: lv1 aim is ≈15× wider than lv180 aim (5.5° / 0.38°); lv1 pow is ≈12× wider than lv180 pow (0.12 / 0.01). Numbers are not fabricated-looking — they have realistic floating-point distribution and per-roll independence (no streaks, no obvious patterns), consistent with `Random.Range` output. D5 bracket-resolution spot-checks (level=9→1, level=10→10, level=200→180) are also reported and correctly map to the `highest minLevel <= level` rule. |
| `VersusBot` shippable; diff confined per §6; `VersusMatchController` untouched (`git diff` proof) | PASS | **CONFIRM-PASS** | Ran `git diff --stat HEAD -- Assets/Scripts/Physics/Viewer/VersusMatchController.cs` → empty (untouched). Ran `git diff --stat HEAD` and `git ls-files --modified --others --exclude-standard` (filtering pre-existing `_Recovery/` and `_capture/` drift listed in HEARTBEAT.log baseline). In-scope diff: `Assets/Scripts/Physics/Viewer/VersusBot.cs` (+223/-4), `Assets/Scripts/Physics/Viewer/Bot/Editor/VersusHudCaptureMenu.cs` (+105/-0), `Assets/Resources/Data/bot_difficulty.csv` (new), `Assets/Resources/Data/bot_difficulty.csv.meta` (new, GUID-locked), `Docs/Specs/Active/versus_bot_difficulty/{STATUS,HEARTBEAT,IMPLEMENTER_REPORT}.md`, `Docs/Specs/Active/versus_bot_difficulty/screenshots/*.png`. No `#if UNITY_EDITOR` added to runtime code (the new field is plain `[SerializeField]`). Production `ShotController` external-drag path unchanged (only `aimYaw`/`power01` values flowing into it are now perturbed). All within the §6 allowlist (`VersusBot.cs`, new CSV+meta, optionally `VersusHudCaptureMenu.cs`). |

**Override count:** 0 PASSes flipped to FAIL.
**Total verdict:** all 8 checklist items CONFIRM-PASS.

---

## Capture-helper compliance (Step 5)

1. **Screenshot provenance.** Stills are 1170×2532 frame extracts from `BotVideoRecorder` output (Unity Recorder pipeline), not `ScreenCapture.CaptureScreenshot` or manual OS screenshots. Console log block in IMPLEMENTER_REPORT confirms `BotVideoRecorder` was used: `[BotVideoRecorder] Recording started → tasks/loop_v2_smoke_bot/versus_bot_difficulty_lv1/video/raw.mp4 (1170x2532 @ 30fps)`. This is the sanctioned full-size capture path per `feedback_record_bot_video_full_size.md` and `reference_unity_capture_video_pipeline.md`. PASS.
2. **Maintenance protocol for new contexts.** This task added **no** new `*Context.cs` file under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` — the diff is `VersusBot.cs` + `VersusHudCaptureMenu.cs` + CSV. `MatchContext.cs` is only READ (`MatchContext.Players[1].Level`), not modified. CaptureHelper `FakeMidAim` / `FakeReset` therefore need no extension. N/A — PASS by exclusion.

---

## Scene-mutation audit (Step 7)

`git diff --stat HEAD` shows no `.unity` scene files in the modified set. The recorder writes to `tasks/loop_v2_smoke_bot/versus_bot_difficulty_*/video/raw.mp4` (gitignored output path), and `VersusHudCaptureMenu.ExitingPlayMode` cleans up the `OnMatchReadyToBegin` subscription and `MaxRecordSecondsOverride`. No scene file (LabScaffold or Hole_04_Geo) is in the diff. No scene mutations from the capture path. PASS.

---

## Production-flow capture check (Step 8)

This is a behavior-randomization task, not a layout/timing task. The captures ARE production-flow: `Launch("versus_bot_difficulty_lv1")` enters Play mode on the real `LabScaffold.unity` with `GameSession.IsVersus=true` and lets `VersusMatchController.MatchFlow` drive the bot through the real `ShotController.BeginExternalDrag` → ramp → `EndExternalDrag` path. No smoke-runner state injection bypassing the production lifecycle is used; the only "injection" is `bot.DebugLevelOverride = debugLevel` which is a single Inspector-field set, not a layout/timing override. Both videos are 60s ~52-56MB MP4s at 1170×2532 (verified via `ffprobe`). PASS.

---

## Risk notes for the architect

1. **Implementer report has one wording slip** — claims `EnsureDifficultyLoaded` "returns without setting `_difficultyLoaded=true`" but the code sets it `true` before the null check. The actual zero-error fallback is correctly handled by the second-level fallback inside `ResolveBracket()` (empty `_difficultyTable` → zero-error bracket + warning). Net behavior is correct; just a slightly inaccurate self-description. Worth noting but not a FAIL.
2. **`InvertClubPower` is a new helper** (`VersusBot.cs:759-794`). Linear search over the carry table; falls back to 50m if table empty or club unknown. The implementer chose this over caching the explicit `safeTargetDist` from H1/H2 because layup paths shadow `dist` and threading it cleanly would have touched more lines. Mathematically equivalent (round-trip of `InterpolateClubPower`) in all cases where D3 fires (non-putt, so H3 hasn't nudged `power01`). Architect may want to confirm this design choice in red-team review, but I don't see a behavioral defect.
3. **`DebugLevelOverride` is a runtime-public `[SerializeField] public int`** — production-safe (no `#if UNITY_EDITOR` and -1 sentinel makes it a no-op by default), and the implementer set it directly at scenario start in `VersusHudCaptureMenu.cs`. A purist might prefer a private field with a public setter, but the spec explicitly accepts the plain `[SerializeField]` pattern and the matchmaking pipeline will never construct a bot with override>=0 from production.
4. **Bracket cache re-resolves if `DebugLevelOverride` is toggled mid-match in the Inspector.** Per spec ("level doesn't change mid-match") this should never happen in production. The cache key is `_resolvedLevel == level` (not "ever resolved"), so toggling override at runtime produces a new log line. Not a defect — it's more robust than the spec requires.

---

## Final verdict

**FORWARD_TO_ARCHITECT (PASS).** All 8 acceptance items CONFIRM-PASS, no overrides, diff confined to §6 scope, dispersion tables look real and bracket-spread delta is clearly visible (15× aim, 12× pow, 5/25 vs 0/25 club noise). The locked D1 design (no safety re-check) is verified by direct code read of the post-injection flow into `SetCameraYawRadians` / drag ramp. Capture pipeline used the sanctioned `BotVideoRecorder` path at full 1170×2532, both videos are real ~52-56MB MP4s on the same hole (Hole_04), and the P2 card numerics in the canonical stills unambiguously prove the override propagates.

STATUS set to `READY_FOR_ARCHITECT_REVIEW`.
