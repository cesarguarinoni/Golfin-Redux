# SPEC — `sound_effects` (Order 350)

**Tier:** FULL PIPELINE (Tier 3). New architecture (AudioMixer asset + a new `Golfin.Audio.Events` asmdef + static SFX bus + CSV data layer), one additive event on a **core playback class** (`BallAnimator`), edits to two shipped controllers, settings migration, and an **audio/device-playback fidelity gate** (clip choice + timing are judgments, not assertions). Per-task folder + subagent chain + hear-it gate required.
**Priority:** P2 · Phase: Gameplay Polish · Notion: Order 350
**Author:** Architect, 2026-06-15. Design locked with Cesar this session (decisions 1–7 below). Researched online + adversarially stress-tested before locking the architecture (Cesar directive).
**Handoff path:** `Docs/Specs/Active/sound_effects/SPEC.md` (this file) + `STATUS.md`. Kickoff: `Use the implementer subagent on "sound_effects"`.

---

## 1) Problem

The game is effectively **silent**. `AudioManager` (`Golfin.Audio`, `Assembly-CSharp`) is fully built — pooled SFX, music source, volume API — but the **only live consumer is the volume-settings UI** (`SoundSettingsSubmenu.cs`). Two clip hooks exist (`WaterSplashController` from Order 349, `TapFeedbackController`) and **both bypass AudioManager** via raw `AudioSource.PlayClipAtPoint`, so the SFX volume slider currently controls nothing for them (latent bug). There is **no clip registry, no AudioMixer asset**. Order 350 delivers a complete, properly-architected audio pass: gameplay SFX, UI SFX, match stingers, and menu music — wired once against a real mixer + decoupled bus.

## 2) Locked design decisions (Cesar, 2026-06-15)

1. **Asmdef-crossing trigger** → static **`SfxBus`** enum-event in a new leaf asmdef (researched; see §4B). *Not* moving AudioManager up the dependency graph.
2. **Clip lookup** → **CSV-driven** data table (`sfx.csv`). (Asset *object* refs bound on one central component — see §4C; CSV holds the tunable data.)
3. **Volume/routing** → **AudioMixer done properly up front**, not deferred, not phased. One pass, all categories.
4. **2D** for everything (chase cam follows the ball; positional adds nothing and `PlayClipAtPoint` has uncontrollable rolloff + per-call GC).
5. **Assets** → the bespoke `Golfin_SFX - *` set is canonical; **compress to `.ogg`/Vorbis** and **rename to a clean convention** as needed. Lowercase dupes ignored.
6. **Hit selection by power band**; **landing fires per bounce** (velocity-gated — see §4D adversarial).
7. **Music in scope** → `Assets/Music/Main Theme` for menus.

## 3) Verified ground truth (Architect recon, 2026-06-15 — all live in repo)

**AudioManager** (`Assets/Scripts/Audio/AudioManager.cs`, `Golfin.Audio`, in `Assembly-CSharp` — no asmdef):
- `PlaySFX(AudioClip, float volMult=1)` — 2D, pooled (`sfxSources`, 5, round-robin via `GetAvailableSFXSource`, interrupts `sfxSources[0]` when all busy, `PlayOneShot`).
- `PlaySFXAtPosition(AudioClip, Vector3, float volMult=1)` — `AudioSource.PlayClipAtPoint` (allocates a GO per call; **not used in this pass** per decision 4).
- `PlayMusic(AudioClip, bool loop=true)`, `StopMusic/PauseMusic/ResumeMusic`, `IsMusicPlaying`.
- `SetMusicVolume/SetSFXVolume(float 0–100)`, `GetMusicVolume/GetSFXVolume`, `MuteAll`. Internal store 0–1. PlayerPrefs keys `"Settings_MusicVolume"` / `"Settings_SFXVolume"` (0–100). **Volume loaded in `Awake` → must move to `Start` once mixer is introduced (Unity forbids `AudioMixer.SetFloat` in Awake/OnEnable).**

**Trigger sites:**
- Swing/Hit → `ShotController.CommitFlick()` (`Golfin.Gameplay.Input`); event `OnShotResolved : Action<ShotInput, BallPhysicsModifiers>`.
- Landing/settle + cup + OB → `BallStateMachine.OnStateChanged : Action<BallStateChange>` (`Golfin.Gameplay.Loop`). `BallStateChange` carries `(Previous, Next/terminalState, Position, Surface/terminalSurface, OBReason, Time)`.
  - Settle = `Next == BallState.AtRest` with `terminalSurface`.
  - Cup-in = `Next == BallState.InCup`.
  - OB/water = `Next == BallState.OB` (water splash already hooked in 349).
- `BallState` = { Aiming, Flying, Rolling, AtRest, InCup, OB }. **Intermediate bounces stay `Flying`** → per-bounce landing is NOT a state event; it requires the playback hook (§4D).
- `SurfaceType` (`Golfin.Physics`, byte): Fairway=0, Green, GreenCollar, Semirough, Rough, Tee, Sand, BunkerLip, CartPath, Water, OOB.
- `ClubType` (`Golfin.Inventory`, `UI/Inventory/ClubData.cs`): Driver, Wood, Iron, A_Wedge, P_Wedge, S_Wedge, Putter.
- Match result → `GameSession.OnMatchComplete` → `VersusResultHandler.HandleMatchComplete` (`Assets/Scripts/UI/Modals/VersusResultHandler.cs`); P1 WIN grants RP. *NOTE-A: confirm the result enum/shape (WIN/LOSE/DRAW) and GameSession namespace at impl.*
- RP earn → `RewardPointsManager.EarnPoints` (`UI/Roster/Managers/`). *NOTE-B: confirm signature + whether a level-up event exists for the level-up stinger.*
- UI taps → existing `TapFeedbackController` + button `onClick` across screens; `ScreenManager` (`UI/ScreenManager.cs`) drives transitions + menu-music start/stop. *NOTE-C: enumerate `ScreenId` and the button inventory for the UI sound map at impl — this is a sub-task, see §4F.*

**Playback model** (`Assets/Scripts/Physics/Viewer/BallAnimator.cs`, `Golfin.Physics.Viewer`):
- Singleton `Instance`; `Play(Trajectory)`, `CurrentBall : Transform`, `IsPlaying`, `PlayRate` (0.25/1/4/`float.MaxValue`=Instant). Plays back the **batch-computed** `Trajectory`; binary-searches `samples` by private `_currentSimTime`. **No per-hit/per-sample event today** → add one (§4D).
- `Trajectory` (`Physics/Core/Trajectory.cs`) carries `hits` (`List<TerrainHit>`); `TerrainHit { fp Time; fp3 Position; fp3 VelocityIn; fp3 VelocityOut; SurfaceType Surface; bool IsStop }`. `IsStop == true` = final resting hit; `false` = a bounce.
- `BallTrailController.cs` is the follower template — subscribes `OnStateChanged` in OnEnable/OnDisable, reads `BallAnimator.Instance.CurrentBall`. **Mirror this for `BallAudioEmitter`.**

**Static-bus idiom (precedent):** `StatProviderBus.cs` in `Assets/Scripts/Gameplay/Defaults/` (asmdef `Golfin.Gameplay.Defaults`), already referenced by `Golfin.Physics.Viewer`. Lesson W: static-bus state is the canonical asmdef-build-order workaround.

**Assets** (`Assets/Sounds/`): bespoke `Golfin_SFX - Swing_{Default,Driver,Iron,Wedge,Wood}`, `Hit_{Default,Default_02,Strong,Weak,Putt,Bunker,BallIn}`, `Landing_{Bushes,Fairway,Grass,Grass_02,Green,Road,Rough,Sand,Water}`, `Victory/Clapping_{01,02}` (each `.ogg`+`.wav`); generic `400 Sounds Pack/UI/*` etc.; `Assets/Music/Main Theme.{mp3,wav}`. No `.mixer`, no `Golfin.Audio` asmdef exist (clean slate).

## 4) Architecture

### A. Routing/volume → AudioMixer (`GolfinAudio.mixer`)
- Groups: `Master → { Music, SFX }`. Expose attenuation as params **`MusicVol`**, **`SFXVol`** (optional `MasterVol`).
- Route AudioSources via `outputAudioMixerGroup`: the 5 pooled SFX sources + any new ones → SFX group; `musicSource` → Music group. `AudioMixerGroup` is a `UnityEngine.Audio` type assignable in the inspector — **no asmdef needs to reference AudioManager for volume to work.** This dissolves the routing half of the asmdef wall.
- `AudioManager` keeps its public API (`SetSFXVolume(0–100)` etc.) but internally drives `mixer.SetFloat("SFXVol", LinearToDb(v01))` where `LinearToDb(x) = Mathf.Log10(Mathf.Clamp(x, 0.0001f, 1f)) * 20f`; slider floor → `-80 dB` / mute. **Call only in `Start` or later, never `Awake`/`OnEnable`.**
- **Settings migration:** preserve existing PlayerPrefs keys/values (0–100); convert to dB on load. `SoundSettingsSubmenu` is unchanged in its public contract (still calls `AudioManager.Set*Volume`).

### B. Triggering → static `SfxBus` (new asmdef `Golfin.Audio.Events`)
- New leaf asmdef `Assets/Scripts/Audio/Events/Golfin.Audio.Events.asmdef` (`noEngineReferences: true` — 2D, no `Vector3` needed). Contains:
  - `enum SfxId` — semantic ids (`SwingDriver`, `SwingIron`, …, `HitStrong/Weak/Default/Putt/Bunker/BallIn`, `LandFairway/Green/Rough/Sand/Water/Road/…`, `UiTap/UiConfirm/UiCancel/UiBack`, `RpEarn`, `LevelUp`, `MatchWin/Lose/Draw`).
  - `static class SfxBus { static event Action<SfxId> OnPlay; static void Play(SfxId id) => OnPlay?.Invoke(id); }`.
- Emitter asmdefs add a reference to `Golfin.Audio.Events`: `Golfin.Physics.Viewer`, `Golfin.Gameplay.Input`, `Golfin.Gameplay.UI`, plus the UI/Modals layer (Assembly-CSharp auto-references it). A leaf that references nothing of theirs **cannot create a cycle.**
- **Listener:** one persistent `SfxPlayer` MonoBehaviour (Assembly-CSharp, on the AudioManager GO, DDOL): subscribes to `SfxBus.OnPlay`, resolves `SfxId → clip + baseVolume` (§C), calls `AudioManager.PlaySFX(clip, baseVolume)`. **Domain-reload-off safety:** `[RuntimeInitializeOnLoadMethod]` clears `SfxBus.OnPlay` on play start; sub in OnEnable / unsub in OnDisable; guard against double-subscribe.

### C. Data → `sfx.csv` + central clip binder
- `Assets/Resources/Data/sfx.csv` (mirrors `fake_players.csv` loader pattern): columns `SfxId, baseVolume, loop(optional)`. CSV = the **tunable data** (per-event volume without code).
- **Clip object refs**: a serialized `SfxId → AudioClip` map on the `SfxPlayer` (or a small `SfxLibrary` ScriptableObject it references). **Do NOT `Resources.Load` the clips** — Resources bloats the build and can't unload; bind asset refs in the inspector. (CSV-first still holds for the *data*; Unity asset binding stays an inspector ref, exactly as character CSVs hold values, not asset handles.)
- *NOTE-D: if Cesar prefers everything in CSV incl. clip path, fall back to `Assets/Resources/Audio/SFX/...` + `Resources.Load`; flagged, not assumed.*

### D. Per-bounce playback timing → additive `BallAnimator.OnHit` + `BallAudioEmitter`
- **Core-file touch:** add to `BallAnimator` a minimal, additive `event Action<TerrainHit> OnHit` (and/or a public read-only `CurrentSimTime`), fired as playback advances past each `Trajectory.hits[i].Time`. Additive only — must not alter existing playback/positioning (regression-gated).
- New `BallAudioEmitter.cs` (`Golfin.Physics.Viewer`, mirrors `BallTrailController`): on each `OnHit`, if `!IsStop` publish `SfxBus.Play(Land<Surface>)`; the terminal `IsStop`/`AtRest` settle sound comes from `OnStateChanged` (de-dup so the last bounce and the settle don't double-fire). Swing/Hit published at `CommitFlick`; cup at `InCup`.
- **Adversarial gates (must be in impl):**
  - **Per-bounce machine-gunning** (≤12 bounces, pool of 5): velocity-gate (skip bounces below a `VelocityIn` magnitude threshold), enforce a min inter-SFX interval, and scale volume by impact speed. Thresholds live in `sfx.csv` / a small config — tunable, not hard-coded.
  - **`PlayRate == Instant`/high** collapses bounces into one frame → suppress or hard-curtail emission above a play-rate cap.
  - **Determinism:** `BallAudioEmitter` only *reads* the already-computed `Trajectory`/playback; **zero feedback into the fixed-point sim.** Acceptance-gated, same guarantee as 349.

### E. Music (menus)
- `AudioManager.PlayMusic(MainTheme, loop:true)` routed to the Music group; start on menu `ScreenId`s, stop/duck when entering a hole. *NOTE-C covers the screen list.*

### F. UI/match sound map (sub-task)
- Repoint `TapFeedbackController` and `WaterSplashController` from raw `PlayClipAtPoint` to `SfxBus.Play(...)` — **touch only the audio line; splash VFX and tap VFX untouched** (349 regression guard). Bonus: this fixes the volume-slider-does-nothing bug both have today.
- Button-level UI sounds + match stingers (`MatchWin/Lose/Draw` off `OnMatchComplete`, `RpEarn`/`LevelUp`) wired against the same bus. Button inventory enumerated at impl (NOTE-C).

## 5) File-level change summary (additive-first)
- **New:** `Audio/Events/Golfin.Audio.Events.asmdef`, `SfxId.cs`, `SfxBus.cs`; `SfxPlayer.cs` (+ optional `SfxLibrary` SO) in Assembly-CSharp; `BallAudioEmitter.cs` (`Physics.Viewer`); `GolfinAudio.mixer`; `Assets/Resources/Data/sfx.csv`; `.ogg` re-imports of the canonical `Golfin_SFX` set (renamed to convention).
- **Edited (minimal):** `BallAnimator.cs` (+`OnHit`/`CurrentSimTime`, additive); `AudioManager.cs` (mixer routing, volume in Start, settings migration); `WaterSplashController.cs` + `TapFeedbackController.cs` (audio line → bus); emitter `.asmdef`s (+`Golfin.Audio.Events` ref).

## 6) Acceptance gates
- **Determinism:** EditMode test — full-hole sim trajectory + ShotResult **bit-identical** with audio present vs. a no-audio control (audio never touches `BallSimulation`/`BallStateMachine`).
- **Bus wiring (call-count seams, à la `_waterOBFireCount`):** EditMode — `CommitFlick` publishes exactly one `Swing*`+`Hit*`; an `AtRest` transition publishes one `Land*`; `InCup` publishes one `HitBallIn`; `OnMatchComplete` publishes one `Match*`. Per-bounce: a synthetic `Trajectory` with N bounces above/below the velocity gate publishes exactly the expected count; `IsStop`/settle de-dup proven; `PlayRate=Instant` curtails.
- **Mixer:** EditMode/PlayMode — slider 0→100 maps to clamped dB; floor mutes; existing PlayerPrefs values survive migration. No `SetFloat` in Awake.
- **349 non-regression:** splash VFX + tap VFX visually unchanged; both now respond to the SFX slider.
- **Fidelity gate (Tier-3, human/bot):** device/editor playback video with audio — a full hole showing per-club swing, multi-bounce landings by surface, water splash w/ sound, cup drop-in, a UI tour with taps + menu music, and a 1v1 win/lose stinger. Cesar play-confirm required (clip choice + timing are judgments).

## 7) Out of scope
- AudioMixer ducking/snapshots/compressor effects (Music group ducking under stingers) — note as a fast follow-up if wanted.
- Positional/3D SFX (decision 4 = 2D).
- Ambient loops (wind/birds), commentary/voice, haptics.
- In-hole background music (this pass: menu music only; in-hole BGM = separate decision).
- New/commissioned assets — using the existing library.

## 8) Open NOTEs to resolve at implementation (flagged, not assumed)
- **NOTE-A** `GameSession.OnMatchComplete` result shape + namespace.
- **NOTE-B** `RewardPointsManager.EarnPoints` signature; level-up event existence for `LevelUp` stinger.
- **NOTE-C** `ScreenId` enumeration + button inventory for the UI sound map and menu-music screen set.
- **NOTE-D** clip-binding model: serialized `SfxId→AudioClip` (recommended) vs. CSV-path + Resources fallback.
- **NOTE-E** `SurfaceType → Land*` map fill: GreenCollar→Green? Semirough→Rough/Grass? Tee→Grass/Fairway? BunkerLip→Sand? OOB→silent? (decide in `sfx.csv`).
- **NOTE-F** power-band thresholds for `Hit_Strong/Weak/Default` (putter→`Hit_Putt`; `{A,P,S}_Wedge`→`Swing_Wedge`).
