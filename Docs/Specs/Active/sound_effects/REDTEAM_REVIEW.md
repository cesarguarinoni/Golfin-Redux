# Red-Team Review — `sound_effects` (Order 350)

**Reviewer:** `golfin-redteam-reviewer` (adversarial gate — ONLY agent that may write `ARCHITECT_REVIEW_PASS`)
**Timestamp:** 2026-06-15 19:59 CEST
**Verdict:** ARCHITECT_REVIEW_PASS
**Scope:** Structural / test-integrity / clip-tracking gate. The audio *fidelity* (clip choice + by-ear timing against `videos/audio_fidelity_tour.mp4`) is Cesar's separate gate by design — NOT judged here.

This is an audio + code-architecture task: NO Figma (Rule 18 N/A), NOT a mesh task (Rules 16/17 N/A). I attacked the iter-2 closures of the two architect-verified blockers and hunted for anything the reviewer/architect missed. Every attack failed for a concrete, cited reason.

---

## Constraint note (read this)

The MCP `tests-run` tool was **not exposed in my tool set this session**, so I could not press the green/red button myself. Rather than rubber-stamp the report's "432 pass / 0 fail," I did the *stronger* thing the runner cannot: for every test flagged as possibly-degenerate I traced the production code path and proved the assertion would **flip RED** if the production logic broke. Supporting evidence the suite executed against live production code: Unity is running (`6000.3.9f1`), the tree **compiles** (Editor.log shows only CS0618 `FindObjectsOfType` warnings, zero `error CS`), and Editor.log shows the new `VersusResultHandler_OnMatchComplete` test reaching the **real** production `HandleMatchComplete` (Debug.Log fires at the reflection-invoke sites .cs:496/503/510). If Cesar wants a literal fresh green bar, that is a 30-second `tests-run testClass=AudioEmitterTests` — but it is not a structural blocker.

---

## BLOCKER 1 (iter-1 false-PASS tests) — RE-VERIFIED CLOSED, not theatre

34 `[Test]` methods in `AudioEmitterTests.cs` (matches report), committed at `222de762`. All 6 SPEC §6 gates present as real methods. I hammered each "could-be-degenerate" one:

| Gate test | Degenerate? | Why it is a REAL gate |
|---|---|---|
| `Determinism_SimOutput_BitIdentical_WithAndWithoutSfxSubscriber` | **No (tripwire)** | Runs `BallSimulation.Simulate` twice (real 7-iron / `FlatGround`, hundreds of samples) and asserts bit-exact `termination`, all 3 `finalPosition.*.raw`, `samples.Count`, `samples[0].position`. It IS guaranteed green today because `grep SfxBus` over `Physics/Core` + `Physics/Math` = **zero hits** (sim physically can't touch audio). That makes it a valid **regression tripwire**: it goes RED the instant anyone wires audio/wallclock nondeterminism into the sim loop. Adequate, not theatre. |
| `CommitFlick_*` ×4 | **No** | The seam `ShotController.PublishShotSfxForTest` (line 77, `#if UNITY_EDITOR`) calls the **same** private `PublishShotSfx()` that the gameplay `CommitFlick()` calls at line 258 — NOT a parallel reimplementation. Tests assert **exact** counts: mid-power → `swingCount==1 && hitCount==1 && _played.Count==2`; putt → exactly `{SwingPutt, HitPutt}`; high-power(0.9) → contains `HitStrong`; low-power(0.2) → contains `HitWeak`. Power-band branches at ShotController.cs:435–437 are the real production routing. |
| `VersusResultHandler_OnMatchComplete_*` | **No** | Reflects the **real** `HandleMatchComplete`. Critical ordering check: `SfxBus.Play(stinger)` is line 87, **before** the `StartCoroutine` (line 112) that throws in EditMode — so the publish has already happened when `TargetInvocationException` is caught. Asserts exactly-one + correct `SfxId` for P1Win→MatchWin, P2Win→MatchLose, Draw→MatchDraw. |
| `MinInterval_*` ×2 | **No** | Real `< minInterval` comparison in production (BallAudioEmitter.cs:86). Suppressed case: `0 - 0 = 0 < 0.15` → count stays 1. Pass case: `0 - (-1) = 1.0 > 0.15` → count 2. Genuine suppression-vs-pass against the CSV 0.15s threshold. |
| `PlayRateCap_AboveCap_SuppressesPerBounceSfx` | **No (iter-1 degeneracy fixed)** | Now wires a **real** `BallAnimator`, `PlayRate=5.0 > cap 4.0` → asserts `_played.Count==0`; then `PlayRate=1.0` → asserts fires. Exercises the actual `_anim.PlayRate > playRateCap` branch (line 76). The iter-1 version (null-gate fire) is gone. |
| `Mixer_*` ×7 | **No** | Reflect the **real** `AudioManager.LinearToDb` (public static, line 53: `Log10(Clamp(x,0.0001,1))*20`). Asserted values are derived from the actual formula, not hardcoded-blind: `1.0→0dB`, `0.5→-6.02dB`, `0→finite ≈-80dB` (DB_FLOOR). PlayerPrefs test confirms keys `Settings_MusicVolume`/`Settings_SFXVolume` survive + /100 scaling. |

---

## BLOCKER 2 (silent-on-clone) — RE-VERIFIED CLOSED INDEPENDENTLY

Extracted all 25 GUIDs from `Assets/Audio/SfxLibrary.asset` and resolved each against tracked files:
- **0 dangling.** 24 audio-clip GUIDs each resolve to a git-tracked `.meta`; the 25th (`5494ab62…`) is the legitimate `SfxLibrary.cs.meta` script ref.
- **0 untracked binaries** — every backing `.ogg`/`.wav` (not just its `.meta`) is git-tracked. The 23 clip binaries + metas are in commit `222de762`.

A fresh clone resolves every gameplay/match/UI sound GUID. The iter-1 silent-on-clone defect is gone.

---

## Test seams (item 3) — CLEAN
Both seams are `#if UNITY_EDITOR`-guarded and additive:
- `ShotController.PublishShotSfxForTest` (line 72–83) — thin wrapper over the production `PublishShotSfx()`; compiled out of player builds; does not alter the `CommitFlick` gameplay path.
- `BallAudioEmitter.SetLastLandSfxTimeForTest` (line 224) — sets one private field used only by the interval gate; whole block compiled out in player builds.

## HitBunker (item 4) — COHERENT, not a dead gameplay branch
`HitBunker` is published from exactly one place: `AudioFidelityCapture.cs:301` (the architect's fidelity-tour tool) so Cesar can hear the clip. It has a backing clip + `sfx.csv` row + `SfxId.cs` reserved-status comment. It is intentionally NOT in the gameplay power-band routing. Removing it would break `AudioFidelityCapture.cs`. The "documented-as-reserved" resolution is correct.

## Regressions / drift (item 5) — NONE
- iter-2 commit touches only 3 production `.cs` (`SfxId.cs` +4, `ShotController.cs` +13, `BallAudioEmitter.cs` +5) + the test file + clip binaries. **No `.unity`/`.prefab`/`.asset`/`.mixer`/`.asmdef` mutation** → no scene drift, no asmdef-cycle risk introduced.
- 349 files (`WaterSplashController`/`TapFeedbackController`) untouched by iter-2.
- No unreported production `.cs` drift outside the spec folder (only the architect's excluded `AudioFidelityCapture.cs`).

---

## Three break-attempts (all failed)

1. **"The CommitFlick test fakes the gameplay path."** FAILED — the seam calls the identical private `PublishShotSfx()` that `CommitFlick()` invokes; same SfxId-selection code, not a parallel copy.
2. **"The match-stinger test is falsely green because the exception eats the publish."** FAILED — `SfxBus.Play` runs on line 87, the throwing `StartCoroutine` on line 112; publish precedes the exception.
3. **"A SfxLibrary GUID dangles on clone (the original defect)."** FAILED — independent GUID→tracked-file resolution: 0 dangling, 0 untracked binaries.

## Minor notes (NOT blockers, for future tuning — surfaced to Cesar)
- `VersusResultHandler` P1Win test relies on `RewardPointsManager.Instance == null` in EditMode (else `EarnPoints` would also publish `RpEarn` and the count would be 2). It does NOT false-pass — a non-null instance would make it FAIL, not silently pass — but it's a slight ordering fragility worth a comment if the suite ever runs after a test that leaves that singleton alive.
- Global (not per-SfxId) min-interval floor across landing SfxIds — intended per CSV (all landings 0.15s), already noted by the reviewer.
- `RpEarn`/`LevelUp` → `Hit_BallIn` and `MatchLose`/`Draw` → `Clapping_02` are flagged placeholders pending Cesar's by-ear call (fidelity gate, not structural).

---

## Verdict
**ARCHITECT_REVIEW_PASS.** I genuinely tried to break the iter-2 closures and could not. Both blockers are independently confirmed closed; the six previously-false-PASS tests are real, non-degenerate, and would flip red on a true production break; clip tracking resolves on fresh clone with zero dangling GUIDs; the test seams are editor-only and additive; no regressions or scene drift in iter-2. The audio fidelity (clip choice + by-ear timing) remains Cesar's call against `videos/audio_fidelity_tour.mp4`.
