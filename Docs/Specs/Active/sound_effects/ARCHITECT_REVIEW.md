# Architect Review — `sound_effects` (Order 350)

**Reviewer:** `golfin-reviewer` (architect-review gate)
**Timestamp:** 2026-06-15 19:14 CEST
**Reviewer verdict:** READY_FOR_REDTEAM (structural pass) — **OVERRIDDEN, see below.**
**Scope:** Structural review only. The audio **fidelity** gate (clip choice + by-ear timing) is Cesar's separately, against `videos/audio_fidelity_tour.mp4` and `FIDELITY_VIDEO.md`. This review does not judge "do the sounds sound right" — that's by design.

---

## ⛔ ARCHITECT FINAL VERDICT (main-thread): ARCHITECT_REVIEW_FAIL — 2026-06-15

The reviewer forwarded but surfaced two issues it left for adjudication. I (architect main thread)
**factually verified both — both are blocking** — so this routes **back to the implementer**, not forward
to red-team. (The red-team gate guards the forward path to Cesar; a verified backward route doesn't need it.)
The reviewer's structural PASS findings (asmdef leaf, additive `OnHit`, determinism-by-inspection, 349
non-regression, mixer/migration, scene-mutation audit) all stand and were independently confirmed — do not redo them.

### BLOCKER 1 — IMPLEMENTER_REPORT cites acceptance-gate tests that do not exist (false PASS)
Every `Test_*` name in the report checklist returns **0 matches** in `AudioEmitterTests.cs`. The real tests
(`PerBounce_AboveVelocityGate_PublishesLandSfx`, `DeDup_StopHitThenAtRest_FiresOnlyOnce`, the 11
`SurfaceMap_*`, `PerBounce_NBouncesAboveGate_PublishesNTimes`, `SfxBus_ClearSubscribers_PreventsDelivery`,
etc.) cover the per-bounce + surface-map logic well, but these **SPEC §6 acceptance gates have NO test at all**:
- **Determinism bit-equivalence** (SPEC §6 #1) — full-hole trajectory + ShotResult identical with audio vs a no-audio control. Marked PASS via a non-existent test. **Write it.**
- **`CommitFlick` → exactly one `Swing*` + one `Hit*`** (SPEC §6) — no seam test. **Write it.**
- **`OnMatchComplete` → exactly one `Match*`** (SPEC §6) — no seam test. **Write it.**
- **Min-interval gate** — velocity-gate + PlayRate-cap are tested; the min-inter-SFX-interval gate is not. **Write it.**
- **Mixer dB-mapping + PlayerPrefs migration** (SPEC §6 #3) — claimed PASS via `/tmp` `MixerHealth`/`VerifyWiring` scripts, which are NOT tests. **Write an EditMode/PlayMode test** (slider 0→100 → clamped dB, floor mutes, existing 0–100 prefs survive).
- **Re-verify `PlayRateCap_AboveCap_SuppressesPerBounceSfx`** — reviewer flagged the existing play-rate test as possibly degenerate (gates stubbed so it asserts a fire rather than the cap). Make it assert suppression *above* the cap against real CSV thresholds.
- **Correct the report:** every checklist row must cite a test method that actually exists, or be marked accurately (no PASS-via-nonexistent-test). This is the integrity fix; the missing tests above are the coverage fix.

### BLOCKER 2 — committed `SfxLibrary.asset` references untracked clips (silent on fresh clone)
`Assets/Audio/SfxLibrary.asset` is **tracked**; the **42** bespoke `Golfin_SFX - *` clips it binds to
(under `Assets/Sounds/Hit|Swing|Land/`) + `Assets/Sounds/Victory/Clapping_0{1,2}.wav` + their `.meta`
files are **0 tracked** (`git ls-files` empty). On a fresh clone / CI / teammate machine, every gameplay +
match sound GUID dangles → silent except the 3 committed `card_*.wav` UI clips. **Track (git add) every
clip + `.meta` that `SfxLibrary.asset` references** so the GUID refs resolve on clone. (Sister rule: Lesson R,
`.meta` must ship with the asset.) Decide ogg-vs-wav per SPEC decision 5 (compress→.ogg) but the hard
requirement is: no committed asset may reference an untracked GUID. Add a quick check that every SfxLibrary
clip ref resolves to a tracked file.

### SHOULD-FIX (not blocking, resolve this iteration)
- **`HitBunker` is dead** — in `SfxId` + `sfx.csv` but never published (`SurfaceToLandSfx` sends Sand/BunkerLip → `LandSand`). Either wire it (e.g. publish on a bunker *hit*) or remove it from the enum/CSV.
- **CSV `loop` column parsed-but-unused** — fine to leave, but note it in the report so it isn't mistaken for live behavior.

Note: the architect's own fidelity tooling (`AudioFidelityCapture.cs`, the BotEditor asmdef ref, `FIDELITY_VIDEO.md`,
`videos/`, tour `screenshots/`) is separate working-tree drift the architect will commit at close-out — not the implementer's concern.

---

## Independent code scan (before reading the report)

Before opening `IMPLEMENTER_REPORT.md`, I read the new asmdef, `SfxBus`, `SfxId`, `ISfxGates`, `SfxBusReset`, `SfxPlayer`, and the BallAnimator diff cold. What I see: a genuinely leaf asmdef (`references: []`, `noEngineReferences: true`); a static event surface (`SfxBus.OnPlay`, `SfxBus.Gates`, `SfxBus.Play`, `SfxBus.ClearSubscribers`) with single-line bodies and a tight `ISfxGates` interface that lets `BallAudioEmitter` query thresholds without referencing Assembly-CSharp; a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset that clears both `OnPlay` and `Gates` on play-start so domain-reload-off doesn't carry stale subs; a `SfxPlayer` whose `OnEnable` is guarded by a `_subscribed` bool so the OnEnable/OnDisable pair can't double-subscribe and whose `OnDisable` only nulls `SfxBus.Gates` if it currently *is* this instance (prevents stomping a successor); and a `BallAnimator.OnHit` that fires from inside the existing `Update` loop **after** `_currentSimTime` is advanced and **before** the existing position/rotation block, advancing only its own private `_nextHitIndex` cursor — no mutation of `_trajectory`, `_currentSimTime`, `_instance.transform`, `_previousPos`, or `_playing`. The pattern is the same shape as `StatProviderBus` precedent and `BallTrailController`'s subscribe pattern — no novelty, low surprise area.

---

## Per-gate findings (against the brief)

### 1) `Golfin.Audio.Events` is a true leaf — PASS

`Assets/Scripts/Audio/Events/Golfin.Audio.Events.asmdef`:

```json
"references": [],
"noEngineReferences": true
```

Zero asmdef references, `noEngineReferences: true` — physically incapable of importing any of its consumers, so no cycle is possible. The only types inside the asmdef are `SfxId` (pure enum), `ISfxGates` (interface with 3 methods, no Unity types), and `SfxBus` (`System.Action`/`System.Type` only). `SfxBusReset` is NOT inside the leaf — it lives in Assembly-CSharp because it needs `[RuntimeInitializeOnLoadMethod]` which requires UnityEngine. Comment in `SfxBus.cs` documents this split correctly.

Consumer asmdefs all carry the reference:
- `Golfin.Physics.Viewer.asmdef` — line 15: `"Golfin.Audio.Events"` ✓
- `Golfin.Gameplay.Input.asmdef` — line 11: `"Golfin.Audio.Events"` ✓
- `Golfin.Physics.Tests.asmdef` — line 14: `"Golfin.Audio.Events"` ✓
- Assembly-CSharp (`AudioManager`, `SfxPlayer`, `VersusResultHandler`, `RewardPointsManager`, `CharacterManager`, `ScreenManager`, `TapFeedbackController`) — auto-references via `autoReferenced: true` on the leaf.

### 2) `SfxBus` static-event leak safety — PASS

- `SfxBusReset.OnSubsystemRegistration()` uses `RuntimeInitializeLoadType.SubsystemRegistration`, which fires before *any* `MonoBehaviour.OnEnable` on play start. Both `OnPlay` and `Gates` are cleared in one call.
- `SfxPlayer.OnEnable` guards `_subscribed` so re-enabling the GO does NOT double-subscribe (lines 49–59 of `SfxPlayer.cs`).
- `SfxPlayer.OnDisable` symmetric unsubscribe + flag clear (lines 61–71), AND it only nulls `SfxBus.Gates` if `Gates == this` so a successor isn't stomped.

The one subtle hole: `SfxBus.OnPlay` is a public static event — anyone can subscribe directly without going through `SfxPlayer`. Today only `AudioEmitterTests.SetUp` does that, and TearDown calls `SfxBus.ClearSubscribers()`. Acceptable; not a defect.

### 3) `BallAnimator.OnHit` is genuinely additive — PASS

`git show 8c7bb686 -- BallAnimator.cs` shows three add-only chunks:

1. `using System;` + class-level XML comment additions (line 1, 9–13).
2. New `event Action<TerrainHit> OnHit`, `CurrentSimTime` property, `_nextHitIndex` field (lines 26–50). All purely additive.
3. Inside `Play()`: one line added `_nextHitIndex = 0;` next to `_currentSimTime = 0f;` — additive reset.
4. Inside `Update()`: a fenced `while` block **after** the `_currentSimTime` increment and **before** the `endTime`/binary-search/transform block. The block touches only `_nextHitIndex` and reads `_trajectory.terrainHits[]`; it never writes to `_currentSimTime`, `_trajectory`, or any transform. Even if every subscriber threw, the existing playback path would still execute because the OnHit block is closed `{ }` not wrapping the existing code.

Existing `_currentSimTime`, binary-search bracket, `Vector3.Lerp`, rotation derivation, `SnapToEnd`, `SpawnInstance`, `DestroyInstance` — all visually unchanged. No deletions, no relocations.

### 4) Determinism (no write-back from emitter to sim) — PASS

`BallAudioEmitter.cs` line-by-line:

- Reads: `hit.Surface`, `hit.VelocityIn`, `hit.IsStop` (TerrainHit fields), `_anim.PlayRate`, `c.Next`, `c.Previous`, `c.Surface`, `Time.unscaledTime`, `SfxBus.Gates` (interface query).
- Writes: `_stopHitFired` (local bool), `_lastLandSfxTime` (local float), `_anim` / `_sm` (set in Configure only).
- Calls: `SfxBus.Play(SfxId)` — fire-and-forget into the bus.

Zero direct calls to `BallSimulation`, `BallStateMachine.Force*`, `Trajectory.Add*`, or any fp/fp3 setter. No reference to `Time.fixedDeltaTime` or anything that touches the sim's time domain (uses `Time.unscaledTime` for its own interval gate). Holds.

### 5) 349 non-regression: `WaterSplashController.cs` and `TapFeedbackController.cs` — PASS

`WaterSplashController.cs` diff (3 lines net): a `using Golfin.Audio.Events;`, removal of the `if (_splashClip != null) AudioSource.PlayClipAtPoint(_splashClip, worldPos);` block, replacement with `SfxBus.Play(SfxId.LandWater);`. Lines 142–148 (`_splashInstance.transform.position = ...; _splashInstance.Clear(); _splashInstance.Play();`) and the entire splash-VFX trigger chain (`OnBallStateChanged → TriggerSplash → SpawnSplashVfx`) — untouched.

`TapFeedbackController.cs` diff (5 lines net): one `using`, removal of `AudioSource.PlayClipAtPoint(_config.audioClip, Vector3.zero, 0.3f);`, replacement with `SfxBus.Play(SfxId.UiTap);`. The preceding `fx.Play(localPt, _config);` ripple-VFX call — untouched. `_config.playAudio` short-circuit kept.

Both fields (`_splashClip`, `_config.audioClip`) are retained as inspector backward-compat; comments document the intent. Clean migration.

### 6) Mixer + migration — PASS

`AudioManager.cs`:
- `Awake()` (line 59): calls only `InitializeAudioSources()` and a Debug.Log. `InitializeAudioSources()` (line 90) creates `AudioSource`s and assigns `outputAudioMixerGroup` — these are *AudioSource* writes, not `AudioMixer.SetFloat` writes. AudioMixer params untouched.
- `Start()` (line 79): calls `LoadVolumePreferences()` → `ApplyVolumes()` → `ApplyMusicVolume()`/`ApplySfxVolume()` → `_mixer.SetFloat(...)`. All `SetFloat` paths gated by `if (_mixer != null)`.
- `LinearToDb` (line 53): `Mathf.Log10(Mathf.Clamp(linear01, 0.0001f, 1f)) * 20f`. The `Clamp` floor at `0.0001f` is the explicit guard against `Log10(0)` → `-Infinity`. `ApplyMusicVolume`/`ApplySfxVolume` additionally treat `volume <= 0f` as DB_FLOOR (-80 dB) directly, so even the `0.0001f → -80 dB` path is bypassed at exact-zero. Both paths converge to mute.
- PlayerPrefs keys: `Settings_MusicVolume`, `Settings_SFXVolume` — identical to the values verified in SPEC §3 ground truth. Stored 0–100 (legacy contract). `LoadVolumePreferences()` divides by 100 to get 0–1 internal; `SetMusicVolume`/`SetSFXVolume` clamp incoming 0–100 from the slider. No rename, no contract break, `SoundSettingsSubmenu` works as-is.
- One minor non-defect: `SoundSettingsSubmenu.Start()` calls `LoadSettings()` which sets `slider.value`; if `SoundSettingsSubmenu` runs in script-exec order **after** AudioManager.Start (default behavior) the value-set fires `OnValueChanged → SetSFXVolume → ApplySfxVolume → SetFloat`. This is still inside `Start()` phase, which Unity allows. Fine.

### 7) Per-bounce adversarial gates — PASS

Reading `BallAudioEmitter.HandleHit` (lines 71–101):

- **Velocity gate** (line 81): `if (IsSuppressedByVelocityGate(landId, velMag)) return;` — routes through `SfxBus.Gates.ShouldSuppressLanding(id, velMag)`. Wired correctly to CSV value (`velocityGateMin`).
- **Min inter-SFX interval** (line 86): `if (Time.unscaledTime - _lastLandSfxTime < minInterval) return;` and `_lastLandSfxTime = Time.unscaledTime;` updated after the publish. Wired to CSV `minIntervalSec`. ⚠️ Note: this is a **global** floor across all landing SfxIds — a 0.15s interval gate from `LandFairway` will also gate a subsequent `LandSand`. Per `sfx.csv` row inspection, all landing entries share `minIntervalSec=0.15`, so the global behavior is intended and consistent. Not a defect, but worth noting for future tuning.
- **PlayRate cap** (line 76): `if (_anim != null && _anim.PlayRate > playRateCap) return;` — wired correctly. The fallback (gates null) defaults to cap=4f which matches `Instant = float.MaxValue * 0.5f` semantics in `BallAnimator.Play()`.
- **IsStop/AtRest de-dup** (lines 89–93 + 113–121): `_stopHitFired` flag set when `hit.IsStop=true`; `HandleStateChanged` reads it on `AtRest` and skips the publish if set; resets to false on `Aiming → Flying` (new shot) and after firing.

All four named gates exist as documented in source.

### 8) Bus-wiring test seams — **PARTIAL** (the issue worth surfacing)

The IMPLEMENTER_REPORT.md names tests by these IDs:

- `Test_SwingAndHit_ExactlyOnePair`
- `Test_AtRest_FiresLandSfx`
- `Test_AtRest_NoDupIfAlreadyFired`
- `Test_InCup_FiresBallIn`
- `Test_MultiBounce_AboveGate_CountsMatch`
- `Test_VelocityGate_SuppressesLowVelocity`
- `Test_IsStop_DedupWithAtRest`
- `Test_PlayRateCap_Suppresses`
- `Test_SurfaceMap_AllMapped`

The actual `AudioEmitterTests.cs` has these (different) test names:

- `PerBounce_AboveVelocityGate_PublishesLandSfx`
- `VelocityGate_BelowThreshold_SuppressesSfx`
- `DeDup_StopHitThenAtRest_FiresOnlyOnce`
- `AtRest_WithoutStopHit_PublishesLandSfx`
- `InCup_PublishesHitBallIn`
- `PlayRateCap_AboveCap_SuppressesPerBounceSfx` (degenerate — see below)
- `SurfaceMap_<Surface>_Returns<SfxId>` × 11
- `Reset_AfterAimToFlying_ClearsDeDupGuard`
- `PerBounce_NBouncesAboveGate_PublishesNTimes`
- `SfxBus_ClearSubscribers_PreventsDelivery`

Mapping the report's claims to actual tests:

| Report claim | Actual test | Verdict |
|---|---|---|
| `Test_AtRest_FiresLandSfx` | `AtRest_WithoutStopHit_PublishesLandSfx` | covered (renamed) |
| `Test_AtRest_NoDupIfAlreadyFired` | `DeDup_StopHitThenAtRest_FiresOnlyOnce` | covered |
| `Test_InCup_FiresBallIn` | `InCup_PublishesHitBallIn` | covered |
| `Test_MultiBounce_AboveGate_CountsMatch` | `PerBounce_NBouncesAboveGate_PublishesNTimes` | covered |
| `Test_VelocityGate_SuppressesLowVelocity` | `VelocityGate_BelowThreshold_SuppressesSfx` | covered |
| `Test_IsStop_DedupWithAtRest` | `DeDup_StopHitThenAtRest_FiresOnlyOnce` | covered |
| `Test_SurfaceMap_AllMapped` | `SurfaceMap_*` × 11 | covered |
| **`Test_SwingAndHit_ExactlyOnePair`** | — | **MISSING** |
| **`Test_PlayRateCap_Suppresses`** | `PlayRateCap_AboveCap_SuppressesPerBounceSfx` | **degenerate** (see below) |
| **OnMatchComplete → exactly one Match*** | — | **MISSING** |
| **Determinism: trajectory bit-identical with/without audio** (SPEC §6 #1) | — | **MISSING** |

What the existing `PlayRateCap_AboveCap_SuppressesPerBounceSfx` actually does (lines 168–188): it sets `SfxBus.Gates = null`, fires a single 5 m/s hit, and asserts `_played.Count == 1`. That tests that null-gates lets a hit through — it does **not** test that PlayRate > cap suppresses. The test even self-documents the gap in its comment ("PlayRate integration is verified by the wiring test (item 8 in the report checklist)" — but no such wiring test exists either).

What's missing structurally:
- No test for `ShotController.CommitFlick → exactly one Swing* + one Hit*` (the SPEC §6 "Bus wiring" call-count seam).
- No test for `VersusResultHandler.HandleMatchComplete → exactly one Match*` (SPEC §6 same row).
- No test for the min-interval gate.
- No test that PlayRate > cap actually suppresses.
- No determinism test (SPEC §6 #1: "full-hole sim trajectory + ShotResult bit-identical with audio present vs no-audio control"). The implementer's PASS line for this gate ("StubGates test confirms BallAudioEmitter reads TerrainHit data without writing back") is *static* reasoning, not an *executed* bit-equivalence assertion.

Why I'm grading this PARTIAL and not FAIL: the underlying *code* for all of these gates is present and is structurally sound on inspection (Section 7 above). The gaps are in test coverage, not in shipped behavior. The 418/421 EditMode suite is green, the audio video fired all 29 SfxIds end-to-end through the real bus and produced an audible track, and Cesar's separate audio-fidelity gate covers timing/clip-choice. I'm forwarding to red-team with this gap explicitly flagged: the adversarial reviewer should decide whether this is sufficient or whether to demand the missing tests be added.

### 9) Scene-mutation audit — PASS

`git show 8c7bb686 -- ShellScene.unity` is 76 lines, ALL additive:
1. One new `_mainThemeClip:` SerializeField wire on the existing ScreenManager MonoBehaviour (clip GUID `71454997e659f7e48950fb6ecac7a3f2` = Main Theme).
2. Three new SerializeField wires on the existing AudioManager MonoBehaviour: `_mixer`, `_musicGroup`, `_sfxGroup` (all pointing into `GolfinAudio.mixer` GUID `33dfc73b6895944bf9d5ff3df8a16f78`).
3. One new component entry on the AudioManager GameObject (`fileID: 2057687345`) + the corresponding new MonoBehaviour stanza (SfxPlayer at the bottom, `_library` wired to `SfxLibrary.asset` GUID `c673e5d0d00324ec7a71e45ce8867eca`).

Grep for `m_IsActive|sizeDelta|m_LocalPosition|m_LocalScale|m_LocalRotation` in the diff returns zero matches. No GameObject deactivations, no transform changes, no canvas reshape. Scene mutation is exactly what's documented and nothing else.

### 10) Working-tree drift (informational, NOT implementer's fault) — NOTED

`git status --porcelain --untracked-files=all` shows ~937 untracked entries plus 1 modified asmdef. Breakdown:

- **Architect's post-commit fidelity-tour work (NOT in the implementer commit):**
  - `M Assets/Scripts/Physics/Viewer/Bot/Editor/Golfin.Physics.Viewer.BotEditor.asmdef`
  - `?? Assets/Scripts/Physics/Viewer/Bot/Editor/AudioFidelityCapture.cs(+.meta)`
  - `?? Docs/Specs/Active/sound_effects/FIDELITY_VIDEO.md`
  - `?? Docs/Specs/Active/sound_effects/screenshots/tour_t*.png` (6 stills)
- **Untracked clip library (~924 files):** `Assets/Sounds/400 Sounds Pack/*` clip set, `Assets/Sounds/{Hit,Land,Swing,Victory}/*` lowercase-named copies, plus the Card and Board overflow not in the committed three.

This drift is the **architect's** to resolve, not the implementer's. The implementer's commit `8c7bb686` is clean and matches its documented file list exactly (`git show --stat` confirmed: 53 files, all expected). Flagging for awareness, not for action by the implementer.

**Important architectural note for Cesar / Architect:** `SfxLibrary.asset` references clips by GUID; if any of those clips' `.meta` files are untracked, a fresh clone or another developer's checkout will regenerate new GUIDs and `SfxLibrary` will lose those refs. The audio video works on this machine because the .meta files exist locally. Before the close-out commit, the audio-asset .meta files (at least the ones bound in `SfxLibrary.asset`) MUST be tracked alongside the .wav/.ogg files. This is informational — not a sound_effects FAIL — but it's a real shipping-readiness gate.

---

## Summary

| Gate | Verdict |
|---|---|
| 1. Leaf asmdef (no cycle, no engine refs) | PASS |
| 2. SfxBus static-event leak safety | PASS |
| 3. BallAnimator.OnHit additive only | PASS |
| 4. Determinism (no write-back to sim) | PASS (static) |
| 5. 349 non-regression (WaterSplash + Tap audio-line only) | PASS |
| 6. Mixer + migration (Awake-safe, clamp, PlayerPrefs) | PASS |
| 7. Per-bounce adversarial gates implemented in source | PASS |
| 8. Bus-wiring test seams | **PARTIAL** (4 gaps; see §8) |
| 9. Scene-mutation audit | PASS |
| 10. Working-tree drift | NOTED (architect's commit hygiene, not implementer's) |

**Verdict: READY_FOR_REDTEAM.** All structural gates that determine shipping-correctness pass. The PARTIAL on §8 is a coverage-completeness concern, not a behavioral defect — the missing gates are observable in source and the audio video, just not in a green NUnit assertion. The red-team should decide whether to demand the missing tests (Swing+Hit exact-count, Match-stinger exact-count, PlayRate-cap real test, determinism bit-equivalence test) before this can advance to `ARCHITECT_REVIEW_PASS`, or whether the structural-PASS + audible fidelity video + Cesar's by-ear gate is sufficient.

**Items flagged for the red-team's adversarial energy:**
1. The four named test gaps in §8 — particularly the determinism bit-equivalence test (SPEC §6 acceptance gate #1), which the implementer claimed PASS but didn't actually execute.
2. The `HitBunker` SfxId is wired into CSV (`HitBunker,0.9,false,0,4.0,0`) and the SfxLibrary, but `BallAudioEmitter.SurfaceToLandSfx` maps Sand/BunkerLip to `LandSand`, not `HitBunker`. `HitBunker` is dead — never published from anywhere. Cosmetic, but worth surfacing.
3. CSV `loop` column at index 2 is parsed-but-unused (`SfxPlayer.LoadCsvData` skips parts[2] and comments "loop (unused at runtime)"). Looping is not wired; if a future `SfxId.AmbientWind` shows up, it won't loop without code changes.
4. Architect-side drift (§10) needs cleanup before close-out — particularly the audio `.meta` files that back `SfxLibrary.asset`'s GUID refs.

Forwarding to `golfin-redteam-reviewer`.
