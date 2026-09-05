# Control Schemes Plan — Flick / Pendulum (Neko) / Needle (Golf Clash) / Free Swing (TrueSwing)

**Status:** PLAN, not a spec. Cesar approves → Figma frames → specs in order §6.
**Written:** 2026-09-03 (Architect, Cowork). Project mirror: `claude/CONTROL_SCHEMES_PLAN.md`.
**Goal:** try three alternative shot-control schemes side by side with the current flick, switchable in Settings and in the in-game gear modal, with every existing stat still doing its job, so telemetry + feel decide which one ships.

---

## 0. Where we stand (what every scheme must keep)

The shot pipeline is already split in two, which is what makes this cheap:

| Layer | Owner | Changes per scheme? |
|---|---|---|
| Gesture → intent (`ShotState` machine, pull px → power, handle → finetune, arrow timing, flick gate) | `Assets/Scripts/Gameplay/Input/ShotController.cs` `Tick()` / `TickArrow()` / `ComputePower()` / `ComputeFinetune()` / `TimingPowerMultiplier()` | **YES — this is the scheme** |
| Intent → physics (`ShotInputBuilder.Build(bundle, StatCoefficients.Default, StatCaps.Default, flickMag, aimYaw, …, spin, fadeDraw)`) then `OnShotResolved` → sim, telemetry, SFX, tournament `ShotCommand`, stamina | `CommitFlick()` tail (from `PublishShotSfx()` to `OnShotResolved?.Invoke`) | **NO — shared by all four** |

Current stat → input coupling (SHOT_CONTROLS_DESIGN §6, verified in code):

| Stat | Today (Flick) | Where |
|---|---|---|
| Club Accuracy (0..120) | cone half-angle 5°→20° = aim range AND error tolerance | `HalfConeAngleRad()` via `ConeHalfAngleAtAcc0Deg/100Deg` |
| Character Club Control (0..120) | arrow speed `BaseArrowSpeedHzAtCC0 + cc*ArrowSpeedHzPerCC` (clamped `MinArrowSpeedHz`), clean passes `MaxCleanPassesAtCC0 + cc*CleanPassesPerCC` before `DegradationYawDegPerPass` | `TickArrow()` |
| Character Strength | overpower forgiveness (`OverpowerForgivenessFraction`) + swing velocity (`CharStrengthVelocityPerPoint`) | `StatModifierResolver` |
| Club Power / Ball stats | velocity / physics only | resolver |
| Timing (slab band at aim latch) | power × 0.70 / 0.90 / 1.0 (`TimingPowerMulRed/Gold`) | `TimingPowerMultiplier()` |
| Putter Control / Accuracy / Weight | off-centre forgiveness / gravity well / aim cycles | resolver + UI |
| Recovery / Stamina | not per-shot (stamina degrades stats once via `StaminaModel.EffectiveStat`) | — |

**Invariant for the plan:** each scheme must give every one of Club Accuracy, Club Control, Strength and the timing multiplier a visible job. The stat→physics side (`StatModifierResolver`, `StatCoefficients`) is not touched by any scheme.

---

## 1. Shared architecture — the "shot intent" seam (Spec 0)

One small refactor buys all three schemes and keeps Flick byte-identical.

### 1.1 `ShotIntent` + `ShotController.CommitExternal(ShotIntent)`
- Extract the tail of `CommitFlick()` (SFX publish → `LastShotWasClean` → `ShotInputBuilder.Build` → `State = Resolving` → `OnShotResolved`) into `private void ResolveAndPublish(float power, float aimYawRad, float timingMul, float timing01, Vector2 spin, float fadeDraw01)`. `CommitFlick()` becomes a 10-line caller. Parity test: `ShotAimParityTests`, `ShotTimingPowerTests`, `ShotControllerFlickGateTests` unchanged and green.
- New `public readonly struct ShotIntent { float PowerNormalized /*0..1.2*/; float AimOffset01 /*-1..1 of half-cone*/; float ErrorYawRad /*scheme miss, added like degradYaw*/; float TimingMul; float Timing01; float FadeDraw01; }` in `Golfin.Gameplay.Input`.
- `public void CommitExternal(in ShotIntent i)` = `aimYaw = AimYawFor(i.AimOffset01) + i.ErrorYawRad` → `ResolveAndPublish`. Spin still comes from `PendingSpinInput` (spin selector modal is scheme-independent). Putt clamp (`flickMag ≤ 1`) stays inside.
- Existing `BeginExternalDrag / SetExternalPower / EndExternalDrag` stay for the Flick driver (`ClubHandleDragger`). Add an `ownsTiming` flag on `BeginExternalDrag(bool ownsTiming = false)`: when true, `Tick()` skips `TickArrow()` (no arrow, no per-pass degradation, no `MaxTotalPasses` auto-cancel) — the driver is the timing authority.

### 1.2 `ControlScheme` + `ControlSchemeService`
- `enum ControlScheme { Flick = 0, Pendulum = 1, Needle = 2, FreeSwing = 3 }` (internal names; player-facing labels are a §7 decision).
- `static class ControlSchemeService` cloned from `QualityTierService`: PlayerPrefs key `controls.scheme`, `Current`, `Set(ControlScheme)`, `event Action<ControlScheme> OnSchemeChanged`. PlayerPrefs, not SaveData — same reasoning as quality tier (property of the device/hand, not the account).

### 1.3 `ShotSchemeHost` (in `ShotUI_Canvas`, `LabScaffold.unity`)
- One driver root per scheme, each a prefab: `SchemeRoot_Flick` = the existing `ClubHandle` + `ShotConeView` + `ConeMeshGraphic` + `TimingSlabGraphic` + arrows, re-parented, zero code change. `SchemeRoot_Pendulum`, `SchemeRoot_Needle`, `SchemeRoot_FreeSwing` new.
- `interface IShotSchemeDriver { void Bind(ShotController c); void Activate(); void Deactivate(); }`.
- Host activates the selected root when `ShotState == Idle`; a change while a swing is in progress is applied at the next Idle (no mid-swing swap).
- Everything that subscribes to `ShotController.OnStateChanged` (`PowerGaugeWidget`, `CentralBallWidget`, `ShotInProgressUiGate`, `ConeAlphaController`, `TeeIdleGlowController`, `MapViewController`, `PuttPathPredictor`, `PutterAimLine`, `ActionButtonsRoot`) keeps working because every driver still publishes `ShotInputState` through the external-drag API (power, finetune, state). `ConeAlphaController` is cone-specific → lives inside `SchemeRoot_Flick`.
- Bots / capture drivers / `FireDebugShot` / `DebugShotInputSource` never touch the driver layer — they call `CommitFlick` paths with no samples, so `TimingMul = 1` (D4) holds for every scheme.

### 1.4 Telemetry (needed to pick a winner)
- `shot_taken` gains `scheme` (int) next to `timing01 / timing_mul / timing_band` (`GameSession.cs` §shot_timing_telemetry writer). Each scheme maps its own accuracy measure onto `timing01` so the existing Flick-timing dashboard card works per scheme with one filter (dashboard string in `lib/i18n.ts` DICT, en + ja).
- New event `controls_scheme_changed { from, to, where: settings|ingame }`.
- Decision metrics per scheme: strokes vs par, miss-shot rate (`ErrorYawRad > 0`), timing01 distribution, retries (cancelled swings), and Cesar/Ken feel notes.

### 1.5 Settings switch (Cesar: Settings screen + in-game gear modal)
- `ControlsSubmenu` cloned from `GraphicsSubmenu` (4 buttons, `UpdateButtonColor` highlight), new `SettingsMenuItem controlsItem` + submenu in `SettingsController` accordion (between Graphics and Language).
- `InGameSettingsModalController`: one 4-segment row under the sound sliders; same service call.
- Strings via the importer path (`LocalizationText.csv` EN+JA → `import_content.py` plan → apply → publish `texts` → `--check`): `SETTINGS_CONTROLS`, `SETTINGS_CONTROLS_FLICK/PENDULUM/NEEDLE/FREESWING`, plus per-scheme grade pop-ups (§2–4).

---

## 2. Scheme 1 — "Pendulum" (白猫GOLF / Neko Golf)

**Research (JP sources):** power = pull the blue circle toward you; then a **timing gauge swings left↔right like a pendulum** (pauses a beat at each end); you **flick up when the marker is near the red centre point**; the offset at the flick decides landing deviation and direction; shot shape (draw/fade/pitch&run/lob) is a **pre-shot menu**, not a flick angle; too slow a flick = a mishit. It is the scheme our Confluence 2024/9/17 design was already borrowing, minus the converging circles (not confirmed in any source; the pendulum is). Sources: game.watch.impress.co.jp/docs/kikaku/1403342.html, yoyaku-top10.jp/pc/blogs/MTE2Nw, appmedia.jp/shironekogolf/76151108, colopl.co.jp/shironekogolf/en/.

**Why it fits us best:** one continuous gesture like today; reuses `ComputePower`, the windowed flick gate (`EvaluateFlickGate`), the arrow-speed constants and the map-view aim. Delta is "arrow up the cone" → "marker across a bar".

### 2.1 Phases (one touch)
1. **Aim** — unchanged: camera heading + map target (`MapTargetCarryM`) + existing Straight/Fade-Draw toggle (`FadeDrawActive`) as the Neko pre-shot shape menu. No cone drawn.
2. **Pull** — touch the ball handle, drag down. Power = `ComputePower(pullPx)` (0..1.0, overpower to 1.2 past `Max100PercentPullPx`). Lateral finger movement does nothing (Neko) — `AimOffset01 = 0` from the pull. Gauge shows % + yards.
3. **Pendulum** — as soon as `PowerNormalized > 0` a marker sweeps across a horizontal bar under the ball, sinusoidal (slow at the ends, fast in the middle — matches the "pause at the edges"). `PendulumHz = BaseArrowSpeedHzAtCC0 + cc*ArrowSpeedHzPerCC` (same constants, same `MinArrowSpeedHz` clamp, `PuttArrowSpeedMultiplier` on putts). Overpower speeds it: `× (1 + (power−1) * PendulumOverpowerGain * (1 − OverpowerForgivenessFraction))` — that is Strength's job here (Confluence: "power above accuracy → gauge faster").
4. **Flick up** — release with upward velocity ≥ `FlickVelocityThresholdPxPerSec` (existing gate; slow = reset, `FlickRejected` toast as today). Marker position at release `m ∈ [−1, 1]`:
   - `|m| ≤ JustWindow01` → **JUST**: `ErrorYaw = 0`, `TimingMul = 1`.
   - `|m| ≤ GoodWindow01` → **GOOD**: `ErrorYaw = m * HalfConeAngleRad()`, `TimingMul = TimingPowerMulGold`.
   - else → **MISS**: `ErrorYaw = sign(m) * HalfConeAngleRad() * MissYawGain`, `TimingMul = TimingPowerMulRed`.
   - `JustWindow01 = lerp(JustWindowAtAcc0, JustWindowAtAcc120, accNorm)` — Club Accuracy sizes the window (its "error tolerance" half of today's cone job); the aim range half is gone in this scheme because aim is map/camera only.
   - `timing01 = 1 − |m|` for telemetry.
5. **Auto-cancel** — no pass cap needed; holding forever just costs the pendulum a beat. Keep `MaxTotalPasses` as a safety (10 sweeps → reset) so a stuck touch never blocks the hole.

### 2.2 Stat map
| Stat | Pendulum |
|---|---|
| Club Accuracy | JUST/GOOD window width; MISS yaw magnitude |
| Club Control | pendulum speed |
| Strength | overpower speed-up forgiveness (+ velocity, unchanged) |
| Timing | JUST/GOOD/MISS power multiplier (same three constants) |
| Putter Accuracy/Control | window width / gravity well as today; pendulum at putt speed, no overpower |

### 2.3 UI (Figma frames)
Pull handle (reuse `ClubHandle` sprite), vertical power gauge with 100 % and 120 % ticks and a yard readout (the "Yard Meter" from Old Control Fixes), pendulum bar with red centre pip + green JUST band + amber GOOD band (colours from `ConeBandPalette`), grade pop-up (JUST! / GOOD / MISS — localised), shape toggle (existing). Putt variant: shorter bar, no 120 % tick.

### 2.4 Config (`controls.csv`)
`PendulumOverpowerGain, JustWindowAtAcc0_01, JustWindowAtAcc120_01, GoodWindow01, MissYawGain`.

---

## 3. Scheme 2 — "Needle" (Golf Clash)

**Research:** aim by dragging the landing target on the course; pull the ball back inside the blue power circle (spin is set on the ball before the pull); **release** launches the swing; the **accuracy arc appears with a needle sweeping across it — tap when the needle is in the blue centre zone**; early tap = hook, late = slice, no tap = shank; needle turns yellow→red as it leaves the zone; overpower (pull past 100 %) makes the needle faster; Accuracy stat shrinks the landing ring, Power the pull range, Ball Guide the shown trajectory. Sources: wingnaprayer.golf/getstarted-golf-clash.pdf, west-games.com/golf-clash-accuracy/, appgamer.com/golf-clash/strategy-guide/how-to-pick-club-stats, golfclashnotebook.io/tools/overpower/.

**What is different for us:** two touches (pull-release, then tap). No flick gate. Aim is map-first — `map_view_aiming` already gives us the target and the `RingFrac` landing rings (80/100/120 %).

### 3.1 Phases (two touches)
1. **Aim** — map view target drives heading (existing `MapTargetCarryM`); on the course the targeting line + landing ring (`RingFrac`, ring radius from Club Accuracy) show where 100 % lands. Spin via the existing selector.
2. **Pull** — touch the ball, drag back inside a **power circle** drawn around the ball (rings at 80/100/120 %). Power = `ComputePower`. Lateral drag = 0 (Golf Clash aims elsewhere).
3. **Release** — commits power, starts the swing animation; the **accuracy arc** (horizontal arc at the top of the ball widget) appears, needle starts at the left end and sweeps right **once**. `NeedleSweepSeconds = lerp(NeedleSweepAtCC0, NeedleSweepAtCC120, ccNorm)`; overpower: `÷ (1 + (power−1) * NeedleOverpowerGain * (1 − OverpowerForgivenessFraction))` (Strength).
4. **Tap** — anywhere. Needle position `n ∈ [−1, 1]` (0 = arc centre):
   - `|n| ≤ PerfectZone01` (Club Accuracy) → `ErrorYaw = 0`, `TimingMul = 1`.
   - else → `ErrorYaw = n * HalfConeAngleRad() * NeedleYawGain` (early → left, late → right, as Golf Clash), `TimingMul = lerp(1, TimingPowerMulGold, |n|)`.
   - **No tap** before the needle reaches the right end → shank: `ErrorYaw = +HalfCone * MissYawGain`, `TimingMul = TimingPowerMulRed`.
   - `timing01 = 1 − |n|`.
5. **Cancel** — releasing inside `PullStartThresholdPx` = no shot (as today).

### 3.2 Stat map
| Stat | Needle |
|---|---|
| Club Accuracy | perfect-zone width + landing ring radius |
| Club Control | needle sweep duration |
| Strength | overpower needle speed-up forgiveness |
| Timing | proportional power loss outside the zone; shank on timeout |

### 3.3 UI
Power circle (3 rings, red overpower crescent past 100 %), ball-with-spin widget (existing `CentralBallWidget`), accuracy arc with blue perfect zone, needle (white→yellow→red by distance from centre), tap pip left on the arc after the tap, result chip (PERFECT / HOOK / SLICE / SHANK — localised), landing ring on course. Putt: same circle, no overpower, arc flatter, `PuttPathPredictor` line stays.

### 3.4 Config
`NeedleSweepAtCC0_s, NeedleSweepAtCC120_s, NeedleOverpowerGain, PerfectZoneAtAcc0_01, PerfectZoneAtAcc120_01, NeedleYawGain, MissYawGain (shared)`.

---

## 4. Scheme 3 — "Free Swing" (Tiger Woods TrueSwing, iPhone touch)

**Research:** the iPhone Tiger Woods games used one continuous drag on a vertical meter — **drag down to set power (the lower, the more power), then drag back up; the straightness of the upstroke decides the shot** ("arc to the left or right as you drag your finger back up, and the ball will take a similar path"; "the smoother your dragging manoeuvre, the more accurate"). Console TrueSwing scores **Speed, Impact, Club Path, Tempo** on a post-shot analyzer; stick deviation = hook/slice; backswing-vs-downswing diagonal = deliberate draw/fade; overswing past 100 %. No 3-click on mobile. Sources: macworld.com/article/196846/tigerwoodspgatour.html, pocketgamer.com/tiger-woods-pga-tour-09-iphone/review/, toucharcade.com/2011/03/29/tiger-woods-pga-tour-12-review/, TW08 manual (rc-services.org/launchpad/PDF/TigerWood08_Manual.pdf).

**What is different for us:** no timing widget at all — the player's own tempo and path are the skill. Deterministic and fast, but the most feel-tuning (thumb noise). It also finally gives the parked "scheme C" (`Docs/Specs/Queued/flick_vector_aim_DESIGN_NOTE.md`) its home: the up-path IS the aim error.

### 4.1 Phases (one touch, no release needed)
1. **Aim** — camera / map as today. Shape toggle optional: TrueSwing derives fade/draw from a *deliberate* diagonal, see 3 below — decide in §7.
2. **Backswing** — touch the ball, drag down along a vertical **swing lane**. Backswing depth → power via `ComputePower` (100 % tick, overswing to 120 %). Backswing duration `tB` recorded.
3. **Downswing** — reverse direction (finger moves up). The **shot fires the frame the finger crosses the impact line** (the touch-origin y), not on release — this is what makes it feel like a swing. Measured over the upstroke (needs the `PushTouchSample` ring widened from 6 samples to the whole gesture, ~60):
   - **Impact** `xI` = lateral px at the crossing minus origin x → `ErrorYaw = clamp(xI / ImpactWindowPx, −1, 1) * HalfConeAngleRad()`; `ImpactWindowPx = lerp(ImpactWindowAtAcc0, ImpactWindowAtAcc120, accNorm)` (Club Accuracy).
   - **Path** = signed mean lateral deviation of the upstroke vs a straight line origin→crossing. Small = noise, ignored inside `PathDeadzoneDeg` (Club Control widens the deadzone); beyond it → `FadeDraw01 = clamp(path / PathFullDeg, −1, 1)` fed to the existing `fadeDrawInputFp` (the curve, exactly what Order 356 wired). A deliberate diagonal = draw/fade, a wobbly thumb inside the deadzone = straight.
   - **Tempo** = `tD / tB` vs `IdealTempo` (TW's 3:1 feel; start 0.5): inside `TempoWindow` (Club Control) → `TimingMul = 1`, else lerp to `TimingPowerMulGold`, past `2 × window` → `TimingPowerMulRed`. `timing01 = 1 − |tempoError|/window`.
   - **Speed** — upstroke velocity below `FlickVelocityThresholdPxPerSec` = duff: `TimingMul = TimingPowerMulRed`, `ErrorYaw` doubled (TW: a slow downswing is a weak, crooked shot; we do not reset because there is no gate moment the player can see).
4. **Release** without crossing the line (finger lifts during the backswing or comes back down) = cancel, as today.

### 4.2 Stat map
| Stat | Free Swing |
|---|---|
| Club Accuracy | impact window (px of lateral tolerance at the ball) |
| Club Control | path deadzone + tempo window |
| Strength | overswing forgiveness (+ velocity) |
| Timing | tempo → power multiplier; speed floor → duff |

### 4.3 UI
Vertical swing lane (rounded rail, left of the ball or through it — Figma decides) with the impact line at the origin, depth ticks 50/100/120 % + yards, a **live finger trace** (ghost line, like the TrueSwing analyzer arc) so the player sees their own path, and a **post-shot analyzer chip** for ~1.5 s: POWER 98 % · IMPACT ◀ 3 px · PATH straight · TEMPO good — this readout is what makes the scheme learnable. Putt: same lane, half height, no overswing.

### 4.4 Config
`ImpactWindowAtAcc0Px, ImpactWindowAtAcc120Px, PathDeadzoneAtCC0Deg, PathDeadzoneAtCC120Deg, PathFullDeg, IdealTempo, TempoWindowAtCC0, TempoWindowAtCC120, SwingSampleWindow (60)`.

---

## 5. Scheme comparison (for the decision later)

| | Flick (today) | Pendulum | Needle | Free Swing |
|---|---|---|---|---|
| Touches | 1 | 1 | 2 | 1 |
| Timing widget | arrow up the cone | pendulum bar | needle on arc | none (tempo) |
| Aim input | handle in cone / map | map only | map only | map (+ path curve) |
| Uses flick gate | yes | yes | no | speed floor only |
| Reuse from today | — | ~80 % | ~55 % | ~45 % |
| Tuning risk | low | low | medium | high |
| Accessibility (one-hand, shaky) | medium | medium | best | worst |

---

## 6. Phasing → specs (each its own `Docs/Specs/Active/<slug>/`)

| # | Slug | Content | Depends on | Rough size |
|---|---|---|---|---|
| 0 | `control_scheme_seam` | §1 entirely: `ShotIntent`, `CommitExternal`, `ownsTiming`, `ControlSchemeService`, `ShotSchemeHost` + `SchemeRoot_Flick`, Controls submenu + in-game row, strings, telemetry `scheme` key + dashboard filter. Acceptance: with `Flick` selected every existing EditMode test passes unchanged; switching mid-swing defers to Idle; pref persists across relaunch | — | 1 Code day |
| — | Figma | §8 frames drafted for approval | plan approval | Architect |
| 1 | `scheme_pendulum` | §2 driver + view from Figma; Lab test driver; tests for window/yaw/timing mapping | 0, Figma | 1–2 days |
| 2 | `scheme_needle` | §3 | 0, Figma | 1–2 days |
| 3 | `scheme_freeswing` | §4 | 0, Figma; widened sample buffer | 2 days + on-device tuning |
| 4 | `scheme_evaluation` (Quick) | dashboard card per scheme, tester instructions in the beta notes, decision checklist | 1–3 | ½ day |

Order 1 → 2 → 3 is by reuse and risk; 3 can be pulled forward if Cesar wants the TW feel first. Every scheme ships behind the Settings toggle with **Flick as the default** until §5 metrics say otherwise, so the beta build is never worse than today.

---

## 7. Decisions (Cesar, 2026-09-03) — LOCKED

1. **Player-facing names:** `Flick` / `Pendulum` / `Tap Timing` / `Free Swing`. JP: フリック / 振り子 / タップタイミング / フリースイング. Internal enum stays `Flick / Pendulum / Needle / FreeSwing`.
2. **Audience:** ship in Settings to all testers, **default Flick**. Not TESTBUILD-gated.
3. **Shape control:** Straight/Fade-Draw toggle shared by Pendulum and Needle; **Free Swing hides the toggle** and derives fade/draw from the upstroke path (§4.1 step 3).
4. **Pendulum slow flick:** **reset, no shot** — same `EvaluateFlickGate` + `FlickRejected` toast as Flick.
5. **Free Swing fires when the finger crosses the impact line** (origin y), not on release. Release after the crossing is ignored.
6. **Overpower to 120 % on all three schemes; never on putts.**
7. **Putts are per scheme** (pendulum at putt speed, flat needle arc, half-height swing lane).
8. **Roadmap:** one Notion GOLFIN_Roadmap row per spec (5 rows, Queued).

## 8. Figma — page "Shot Controls — Schemes" in `5gEAHjl6xAtW8iYY7NMvWd` (DRAFTED 2026-09-03, awaiting approval)

17 frames, every one a detached clone of `In-Game - Shot Tests 9` (`4065:15675`) / `Language` (`4065:16942`) / `In-game Settings` (`4095:29120`) with a scheme layer added inside `Shoot Controls`. Geometry is 1:1 with the game: pull lane 300 px = 100 %, 360 px = 120 % (`Max100PercentPullPx` / `MaxOverpowerPullPx`), ball rest at Shoot-Controls-local (537, 961).

| Section | Frames (node ids) |
|---|---|
| 1 — Pendulum | Idle `14086:32483`, Pull `14086:32586`, Timing `14084:33319`, Result JUST `14086:32689`, Putt `14086:32793` |
| 2 — Needle | Aim `14088:32823`, Pull `14088:32898`, Timing `14087:32852`, Result PERFECT `14088:33002`, Putt `14088:33077` |
| 3 — Free Swing | Idle `14089:33163`, Backswing `14089:33261`, Downswing `14088:101476`, Result analyzer `14089:33359`, Putt `14089:33466` |
| 1b / 2b / 3b — club-handle variants (Cesar 2026-09-03: "it makes more sense to pull on a club than the ball") | Same 15 states with the `In-game Club` head (`2657:5753`, Shot Tests 3) as the pulled handle instead of the G-ball: sections `14091:33667` (Pendulum), `14091:102411` (Needle), `14091:102934` (Free Swing). Both batches kept until Cesar picks. |
| 4 — Settings switch | Settings › Controls accordion `14089:101926` (4 radio rows, `S_Common_RadioButton`), In-game modal Controls row `14090:101896` (4 gold/navy segments) |

Reused atoms: G-ball handle (`Balls` instance), `Power Indicator`, `In-Game Select Button`s, `Settings Icons`, `S_Common_RadioButton`, Rubik Medium/Bold/SemiBold, navy `#001E39`, gold gradient from the RETURN button, `ConeBandPalette` band colours (red `#FF3B3B`/`#FF5A5A`, amber `#FFEBA6`, green `#ADEBAD`), blue perfect zone `#4DA3FF`.
New drawn elements (vectors only, no art needed): pull/swing lane, pendulum track + bands + marker, accuracy arc + needle + hub, power rings + overpower crescent, landing ring, finger trace, impact line, result chips.
**Gaps to source:** a CONTROLS icon for the Settings row (no fitting variant in `Settings Icons` — placeholder uses the globe/sound glyph). Stylised JUST!/PERFECT lettering only if Cesar wants more than Rubik Bold.

## 9. Deferred (to `GPS_BACKLOG.md` when the specs are filed)

**Filed by `scheme_needle` (2026-09-05, SPEC § 7 — out of scope for that spec):**
- **Grade SFX.** PERFECT / HOOK / SLICE / SHANK have no sound. `ShotController.PublishShotSfx`
  already fires at commit for the shot itself; a per-grade cue is a second event and wants the
  Settings on/off that the haptics row above is already parked behind.
- **`needle_grade` telemetry key.** `shot_taken` currently carries `scheme=2`, `timing01` and
  `timing_mul`, which is enough to reconstruct the band (`timing01 = 1 − |n|`, and a SHANK is the
  only row with `timing01 == 0` at `timing_mul == TimingPowerMulRed`). An explicit grade column
  would remove that inference; it is a telemetry-schema change, so it moves with the next one.
- **Bot executor.** `bot_scheme_parity` Stage B adds `DriveBot` for this scheme after the
  Pendulum's; until then a bot plays every scheme through Flick.
- **Golf-Clash-style drag-the-landing-marker aiming.** Map view already covers placing a target
  (`MapTargetCarryM` + the power-gauge notch), so this is a second way to do a thing that exists.


- Haptics per grade (already parked with the Settings on/off requirement, 2026-09-02).
- TW 3-click meter as a fifth scheme (accessibility option) — cheap once Spec 0 exists.
- Per-scheme tutorial / first-shot hint.
- Bot parity: bots keep `TimingMul = 1` in every scheme; a "bot uses the same scheme error model" is a later fairness item for 1v1.
- Converging-circle timing (Confluence 2024/9/17) — unconfirmed in Neko Golf; only if the pendulum does not feel right.
