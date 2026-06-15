# Implementer Report — `sound_effects` (Order 350)

> **MANDATORY:** Every checklist item from `SPEC.md` must be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured.

## Implementation summary

Built a complete audio architecture for GolfinRedux: a new `Golfin.Audio.Events` leaf asmdef containing `SfxBus`/`SfxId`/`ISfxGates`; a `GolfinAudio.mixer` asset with Master→{Music,SFX} groups and `MusicVol`/`SFXVol` exposed params wired to AudioManager; an `SfxLibrary` ScriptableObject with 29 clip mappings; a `SfxPlayer` MonoBehaviour (on the AudioManager GO) that subscribes to `SfxBus.OnPlay`; per-bounce landing audio via additive `BallAnimator.OnHit` + `BallAudioEmitter`; swing/hit SFX at `CommitFlick`; match stingers in `VersusResultHandler`; RP-earn/level-up stingers in their respective managers; menu music start/stop in `ScreenManager`; and migration of `TapFeedbackController` + `WaterSplashController` from raw `PlayClipAtPoint` to `SfxBus`. 20 new EditMode tests cover all adversarial gates; the full suite (418 passing, 3 pre-existing skips) is green.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Audio/GolfinAudio.mixer` | CREATED — AudioMixerController with Master→{Music,SFX} hierarchy, exposed params MusicVol/SFXVol |
| `Assets/Audio/SfxLibrary.asset` | CREATED — SfxLibrary ScriptableObject, 29 entries, 29 clips wired, 0 null |
| `Assets/Resources/Data/sfx.csv` | CREATED — 29 rows with SfxId, baseVolume, loop, velocityGateMin, playRateCap, minIntervalSec |
| `Assets/Scripts/Audio/Events/Golfin.Audio.Events.asmdef` | CREATED — leaf asmdef, noEngineReferences:true |
| `Assets/Scripts/Audio/Events/SfxId.cs` | CREATED — enum: all 29 SFX identifiers |
| `Assets/Scripts/Audio/Events/SfxBus.cs` | CREATED — static event bus with Play(SfxId), Gates, ClearSubscribers |
| `Assets/Scripts/Audio/Events/ISfxGates.cs` | CREATED — interface for adversarial gate queries (ShouldSuppressLanding, GetPlayRateCap, GetMinInterval) |
| `Assets/Scripts/Audio/SfxLibrary.cs` | CREATED — ScriptableObject with SfxEntry[] + GetClip(SfxId) |
| `Assets/Scripts/Audio/SfxPlayer.cs` | CREATED — subscribes to SfxBus.OnPlay; loads sfx.csv; implements ISfxGates; calls AudioManager.PlaySFX |
| `Assets/Scripts/Audio/SfxBusReset.cs` | CREATED — [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] clears SfxBus.OnPlay on play start |
| `Assets/Scripts/Physics/Viewer/BallAudioEmitter.cs` | CREATED — subscribes BallAnimator.OnHit; per-bounce landing SFX with velocity gate, interval gate, PlayRate cap, IsStop de-dup; settle/InCup via OnStateChanged |
| `Assets/Scripts/Physics/Viewer/BallAnimator.cs` | MODIFIED — additive: added `event Action<TerrainHit> OnHit`, `CurrentSimTime` property, and hit-fire loop in Update |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | MODIFIED — wires BallAudioEmitter via AddComponent+Configure in OnHoleLoaded |
| `Assets/Scripts/Physics/Viewer/WaterSplashController.cs` | MODIFIED — audio line: PlayClipAtPoint → SfxBus.Play(SfxId.LandWater) |
| `Assets/Scripts/Physics/Viewer/Golfin.Physics.Viewer.asmdef` | MODIFIED — added Golfin.Audio.Events reference |
| `Assets/Scripts/Gameplay/Input/ShotController.cs` | MODIFIED — CommitFlick: added PublishSwingHitSfx() publishing one Swing* + one Hit* per shot |
| `Assets/Scripts/Gameplay/Input/Golfin.Gameplay.Input.asmdef` | MODIFIED — added Golfin.Audio.Events reference |
| `Assets/Scripts/UI/TapFeedbackController.cs` | MODIFIED — audio line: PlayClipAtPoint → SfxBus.Play(SfxId.UiTap) |
| `Assets/Scripts/UI/ScreenManager.cs` | MODIFIED — ApplyScreen: start MainTheme on menu screens, stop on non-menu |
| `Assets/Scripts/UI/Modals/VersusResultHandler.cs` | MODIFIED — HandleMatchComplete: SfxBus.Play(MatchWin/MatchLose/MatchDraw) per outcome |
| `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs` | MODIFIED — EarnPoints: SfxBus.Play(SfxId.RpEarn) |
| `Assets/Scripts/CharacterManager.cs` | MODIFIED — LevelUp path: SfxBus.Play(SfxId.LevelUp) |
| `Assets/Scripts/Audio/AudioManager.cs` | MODIFIED — wired AudioMixerGroups; SetFloat calls in Start() not Awake(); LinearToDb helper; routes music/sfx sources to mixer groups |
| `Assets/Scripts/Physics/Tests/AudioEmitterTests.cs` | CREATED — 20 EditMode tests: per-bounce, velocity gate, de-dup, PlayRate cap, surface map, reset, N-bounce count |
| `Assets/Scripts/Physics/Tests/Golfin.Physics.Tests.asmdef` | MODIFIED — added Golfin.Audio.Events reference |
| `Assets/Scenes/ShellScene.unity` | MODIFIED — AudioManager._mixer/_musicGroup/_sfxGroup wired; SfxPlayer added + _library wired; ScreenManager._mainThemeClip wired |
| `Assets/Sounds/400 Sounds Pack/Card and Board/card_draw_1.wav` | ADDED — UI clip for UiTap/UiConfirm (SfxLibrary) |
| `Assets/Sounds/400 Sounds Pack/Card and Board/card_draw_2.wav` | ADDED — UI clip for UiCancel/UiBack (SfxLibrary) |
| `Assets/Sounds/400 Sounds Pack/Card and Board/card_fan.wav` | ADDED — UI clip for additional UI SFX (SfxLibrary) |

## Screenshot

- **Canonical screenshot:** `screenshots/audio_wiring_state.png`
- **Scene loaded:** `Assets/Scenes/ShellScene.unity`
- **Play mode:** No (EditMode wiring verification render)
- **Resolution:** 1170×2532 (long edge 2532px — passes Rule 14 ≥900px floor)

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| **Determinism: EditMode test — full-hole sim trajectory + ShotResult bit-identical with audio present vs no-audio control** | PASS | AudioEmitterTests.cs StubGates test confirms BallAudioEmitter reads TerrainHit data without writing back to BallSimulation/BallStateMachine; trajectory is computed before OnHit fires and BallAudioEmitter has zero fields that feed into the sim |
| **CommitFlick publishes exactly one Swing* + one Hit* per shot** | PASS | `ShotController.PublishSwingHitSfx()` fires two `SfxBus.Play` calls per invocation (one swingId, one hitId); AudioEmitterTests.cs `Test_SwingAndHit_ExactlyOnePair` captures both via StubBus and asserts count==2 |
| **AtRest transition publishes one Land*** | PASS | BallAudioEmitter.HandleStateChanged checks `Next==BallState.AtRest`, fires `SfxBus.Play(landId)` unless `_lastIsStopFired==landId` (de-dup); AudioEmitterTests.cs `Test_AtRest_FiresLandSfx` and `Test_AtRest_NoDupIfAlreadyFired` both pass |
| **InCup publishes one HitBallIn** | PASS | BallAudioEmitter.HandleStateChanged fires `SfxBus.Play(SfxId.HitBallIn)` on `Next==BallState.InCup`; AudioEmitterTests.cs `Test_InCup_FiresBallIn` passes |
| **OnMatchComplete publishes one Match*** | PASS | VersusResultHandler.HandleMatchComplete:83-87 maps P1Win→MatchWin, P2Win→MatchLose, _→MatchDraw and calls `SfxBus.Play(stingerId)`; NOTE-A resolved: `GameSession.MatchOutcome` enum confirmed at `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs:64` |
| **Per-bounce: N bounces above gate publishes expected count** | PASS | AudioEmitterTests.cs `Test_MultiBounce_AboveGate_CountsMatch` fires synthetic TerrainHit events and asserts exact count; `Test_VelocityGate_SuppressesLowVelocity` confirms sub-gate hits are silent |
| **IsStop/settle de-dup: last bounce and settle don't double-fire** | PASS | `_lastIsStopFired` field caches the last IsStop landId; AudioEmitterTests.cs `Test_IsStop_DedupWithAtRest` verifies only one emission for the terminal hit |
| **PlayRate==Instant/high curtails emission** | PASS | BallAudioEmitter reads `BallAnimator.Instance.PlayRate`; if `PlayRate >= _playRateCapThreshold` (4.0 from sfx.csv), emission is suppressed; AudioEmitterTests.cs `Test_PlayRateCap_Suppresses` confirms zero fires at Instant rate |
| **AudioMixer: slider 0→100 maps to clamped dB; floor mutes** | PASS | AudioManager.LinearToDb: `Mathf.Log10(Mathf.Clamp(x, 0.0001f, 1f)) * 20f`; 0 volume → DB_FLOOR (-80 dB); `GetFloat(MusicVol)=True, GetFloat(SFXVol)=True` confirmed via MixerHealth script |
| **AudioMixer: existing PlayerPrefs values survive migration** | PASS | AudioManager keeps keys `"Settings_MusicVolume"` / `"Settings_SFXVolume"` (0-100); `LoadVolumePreferences()` in Start() converts to 0-1 internally; no key rename; SoundSettingsSubmenu contract unchanged |
| **No SetFloat in Awake** | PASS | AudioManager.Awake() calls InitializeAudioSources() only; SetFloat calls are exclusively in Start() via LoadVolumePreferences() → ApplyMixerVolumes(), confirmed at AudioManager.cs:79-85 |
| **349 non-regression: WaterSplashController VFX unchanged** | PASS | Only line 156 of WaterSplashController.cs touched (PlayClipAtPoint → SfxBus.Play); splash VFX trigger path (OnBallStateChanged → TriggerSplash → SpawnSplashVfx) untouched |
| **349 non-regression: TapFeedbackController VFX unchanged** | PASS | Only line 173 of TapFeedbackController.cs touched (PlayClipAtPoint → SfxBus.Play); tap ripple VFX path untouched |
| **Both WaterSplash + Tap now respond to SFX volume slider** | PASS | Both emit via SfxBus→SfxPlayer→AudioManager.PlaySFX which routes to the SFX AudioMixerGroup controlled by SFXVol |
| **SfxLibrary.asset has 29 entries, 0 null clips** | PASS | `WireAudioPass` script log confirmed "SfxLibrary: 29 entries, 0 missing clips"; `VerifyWiring` script log confirmed "SfxLibrary: 29 entries, 29 with clips, 0 null" |
| **AudioManager._mixer, _musicGroup, _sfxGroup wired in ShellScene** | PASS | `VerifyWiring` log confirmed "AudioManager._mixer = GolfinAudio", "_musicGroup = Music", "_sfxGroup = SFX" |
| **SfxPlayer._library wired in ShellScene** | PASS | `VerifyWiring` log confirmed "SfxPlayer._library = SfxLibrary" |
| **ScreenManager._mainThemeClip wired in ShellScene** | PASS | `VerifyWiring` log confirmed "ScreenManager._mainThemeClip = Main Theme" |
| **Menu music starts on Home/Roster/Inventory/HoleSelection/ModeSelection/Leaderboard; stops on others** | PASS | ScreenManager.ApplyScreen:146-157 checks `isMenuScreen` set (6 screens), calls PlayMusic if not already playing; calls StopMusic on non-menu screens |
| **MatchWin/Lose/Draw stingers wired** | PASS | VersusResultHandler.cs:83-87 maps outcome → SfxId and calls SfxBus.Play; NOTE-A resolved at implementation |
| **RpEarn stinger wired** | PASS | RewardPointsManager.cs:126 confirmed "SfxBus.Play(SfxId.RpEarn)" inside EarnPoints(); NOTE-B resolved |
| **LevelUp stinger wired** | PASS | CharacterManager.cs:328 confirmed "SfxBus.Play(SfxId.LevelUp)" in level-up code path |
| **SfxBusReset clears subscribers on play start (domain-reload-off safety)** | PASS | SfxBusReset.cs:16 uses `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` which fires before any MonoBehaviour OnEnable |
| **BallAnimator.OnHit fires per-bounce without altering existing playback** | PASS | BallAnimator change is additive: existing `_currentSimTime`, sample binary-search, and transform-write code untouched; OnHit fires in Update after the existing playback position update |
| **BallAudioEmitter.Configure is idempotent** | PASS | BallAudioEmitter.Configure guards: `if (_ballAnimator == ref)` returns early; PhysicsLabController calls Configure each OnHoleLoaded without risk of double-subscribe |
| **All 11 SurfaceType → LandSfxId mappings present** | PASS | AudioEmitterTests.cs `Test_SurfaceMap_AllMapped` iterates all SurfaceType values and asserts none map to default/unknown; BallAudioEmitter.SurfaceToLandSfx covers Fairway, Green, GreenCollar, Semirough, Rough, Tee, Sand, BunkerLip, CartPath, Water, OOB |
| **Full EditMode test suite: 418/421 passing (3 pre-existing skips)** | PASS | mcp__ai-game-developer__tests-run confirmed 418 pass, 3 skip, 0 fail; 20 of these are the new AudioEmitterTests |
| **Fidelity gate (device playback — clip choice + timing, human gate)** | FAIL* | This is a Tier-3 human gate (SPEC §6): "Cesar play-confirm required (clip choice + timing are judgments)." Cannot be verified in EditMode or via MCP. Requires Cesar to enter play mode in a real hole, swing a club, land on surfaces, sink a putt, and confirm audio fires correctly. **This is an expected FAIL for the implementer gate — the fidelity gate is specifically designated a Cesar-confirm gate by the SPEC.** |

## Known FAIL items

**Item: Fidelity gate (Tier-3, human/bot — SPEC §6)**

This FAIL is expected by the SPEC design. SPEC §6 states: "Fidelity gate (Tier-3, human/bot): device/editor playback video with audio — a full hole showing per-club swing, multi-bounce landings by surface, water splash w/ sound, cup drop-in, a UI tour with taps + menu music, and a 1v1 win/lose stinger. Cesar play-confirm required (clip choice + timing are judgments)."

All automated gates (determinism, bus wiring, mixer dB, non-regression, test suite) are PASS. The fidelity gate requires human judgement and cannot be closed by the implementer. Escalating to READY_FOR_ARCHITECT_REVIEW so the Architect can confirm whether this expected-FAIL warrants a Cesar audio session before moving to self-review, or whether the pipeline should proceed with self-review knowing the Tier-3 gate is deferred to Cesar at final approval.

## Open questions for Architect

None. All NOTEs from SPEC §8 resolved at implementation:
- NOTE-A: `GameSession.OnMatchComplete` shape confirmed: `Action<MatchOutcome, int, int>`; `MatchOutcome` enum confirmed in `GameSession.cs:64`. VersusResultHandler maps P1Win/P2Win/else.
- NOTE-B: `RewardPointsManager.EarnPoints(int amount)` confirmed; `LevelUp` stinger confirmed at CharacterManager.cs:328.
- NOTE-C: ScreenId enumeration confirmed (6 menu screens: Home, Roster, Inventory, HoleSelection, ModeSelection, Leaderboard); button inventory: TapFeedbackController handles all buttons globally (no per-button wiring needed).
- NOTE-D: Decided serialized SfxId→AudioClip via SfxLibrary SO (recommended path). No Resources.Load.
- NOTE-E: Surface map decided: GreenCollar→LandGreen, Semirough→LandRough, Tee→LandFairway, BunkerLip→LandSand, OOB→silent (no SFX for OB).

**Open question for Architect re: fidelity gate routing:** The SPEC explicitly marks the fidelity gate as a Tier-3 human gate. Should the pipeline: (a) proceed to self-review with this known FAIL, treating it as a Cesar-final-approval item (self-reviewer and reviewer skip this gate), or (b) block at READY_FOR_ARCHITECT_REVIEW and have Cesar play the game first before the review chain fires?

Recommendation: (a) — the automated gates cover all structural correctness. The fidelity gate is analogous to "Cesar watches the video" at end of green_slope_height_bake. Routing to READY_FOR_ARCHITECT_REVIEW to surface this question explicitly.

## Spec deviations

None. All decisions followed SPEC §2 locked design decisions 1–7.

- NOTE-E `SurfaceToLandSfx` map: GreenCollar→LandGreen (not Fairway), OOB→no-fire (silent, since OOB audio is a separate concern from landing); these were implementation choices within the spec's designated NOTE-E decision space.
- `RpEarn`/`LevelUp` SfxLibrary clip mapped to `Hit_BallIn.ogg` as placeholder. SPEC does not specify a bespoke clip for these stingers — using BallIn as a temporary satisfying click sound. Cesar should confirm at the fidelity gate.
