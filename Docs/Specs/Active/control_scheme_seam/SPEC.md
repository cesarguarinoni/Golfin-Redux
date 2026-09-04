# SPEC — `control_scheme_seam`

**Status:** SPEC_READY (2026-09-04). Plan of record: `Docs/CONTROL_SCHEMES_PLAN.md` §1, §1.5, §7 (decisions locked by Cesar 2026-09-03).
**Figma:** file `5gEAHjl6xAtW8iYY7NMvWd`, page "Shot Controls — Schemes", section **4 — Settings switch**: Settings › Controls accordion `14089:101926`, in-game modal Controls row `14090:101896`. The new `Settings Icons` variant `Property 1=Controls` lives in the master set on the Components page (`4060:7534`). Renders in `reference/`.
**Scope in one line:** make the shot pipeline scheme-agnostic, add a persisted 4-way Control Scheme setting (Settings screen + in-game gear modal), and stamp every shot with its scheme — **with the existing Flick scheme byte-identical.** No new scheme ships in this spec; the three schemes are `scheme_pendulum` / `scheme_needle` / `scheme_freeswing` (Notion 2132–2134).

---

## 1. Why

Three alternative control schemes are going to be A/B'd on the beta build. Each one differs only in how a gesture becomes a `(power, aim, timing, error)` intent; everything after `ShotInputBuilder.Build` — physics, telemetry, SFX, tournament `ShotCommand`, stamina — is shared. Today that boundary lives inside `ShotController.CommitFlick()`. This spec extracts it, adds the switch, and leaves three empty driver slots for the scheme specs to fill.

## 2. Non-goals

- No Pendulum / Needle / Free Swing behaviour, UI or prefabs (their specs).
- No change to `StatModifierResolver`, `StatCoefficients`, `ShotInputBuilder`, `controls.csv` values, cone geometry, arrow speed, timing bands, flick gate.
- No SaveData / server persistence of the setting (PlayerPrefs, same as `QualityTierService`).
- No dashboard chart redesign — one filter on the existing Flick-timing card.

## 3. Design

### 3.1 `ShotIntent` + `ShotController.CommitExternal` (Golfin.Gameplay.Input)

```csharp
public readonly struct ShotIntent
{
    public readonly float PowerNormalized;  // 0..1.2 — putts clamped to 1.0 inside
    public readonly float AimOffset01;      // -1..+1 of HalfConeAngleRad(); passed to AimYawFor()
    public readonly float ErrorYawRad;      // scheme-computed miss; added like _degradationYawRad today
    public readonly float TimingMul;        // power multiplier the scheme judged (1.0 = clean)
    public readonly float Timing01;         // 0..1 for telemetry; NaN = "no timing to judge"
    public readonly float FadeDraw01;       // -1..+1; 0 unless the scheme derives curve itself
}
```

- Extract the tail of `CommitFlick()` — from `PublishShotSfx()` through `State = ShotState.Resolving; OnShotResolved?.Invoke(input, ballMods);` — into
  `private void ResolveAndPublish(float flickMag, float aimYawRad, float timingMul, float timing01, Vector2 spinInput, float fadeDrawInput)`.
  `CommitFlick()` keeps its own maths (`MapTargetCarryM = -1`, `LastShotWasClean`, `DebugFlags` reads, `TimingPowerMultiplier()`, the putt/overpower clamp, `FadeDrawActive` handling) and ends by calling `ResolveAndPublish`. **No reordering of side effects** — `PublishShotSfx` stays first, `MapTargetCarryM` reset stays before it, `LastTimingPowerMul` / `LastCommittedTiming01` are set exactly where they are today.
- `public void CommitExternal(in ShotIntent i)`:
  - Legal only when `State == Timing` (or `Pulling` — a driver may commit without an arrow phase) **and** `_externalDragActive`. Otherwise no-op + `Debug.LogWarning`.
  - `MapTargetCarryM = -1f; PublishShotSfx();`
  - `float aimYaw = AimYawFor(DebugFlags.DisableConeFineTune ? 0f : i.AimOffset01) + (DebugFlags.ForcePerfectAim ? 0f : i.ErrorYawRad);`
  - `float mag = i.PowerNormalized; if (IsPutt || DebugFlags.DisableOverpower) mag = Mathf.Min(mag, 1f); mag *= (DebugFlags.ForcePerfectTiming ? 1f : i.TimingMul);`
  - `LastTimingPowerMul = timingMul; LastCommittedTiming01 = i.Timing01; LastShotWasClean = !IsPutt && Mathf.Approximately(i.ErrorYawRad, 0f);`
  - Spin: `IsPutt ? Vector2.zero : PendingSpinInput` (unchanged rule). Fade/draw: `IsPutt ? 0 : i.FadeDraw01` with `FadeDrawMaxTiltRad` (the scheme decides whether it read the toggle or its own path — the seam does not care).
  - `ResolveAndPublish(mag, aimYaw, timingMul, i.Timing01, spin, fadeDraw)`.
- **`BeginExternalDrag(bool ownsTiming = false)`**: when `ownsTiming`, `Tick()` skips `TickArrow(dt)` in `Timing` state (no per-pass degradation, no `MaxTotalPasses` auto-cancel, `_arrowProgress` stays 0). `EndExternalDrag` is unchanged for the Flick driver; a driver that owns timing calls `CommitExternal` or `CancelExternalDrag` itself and never `EndExternalDrag`. Reset `_ownsTiming` in `TransitionToIdle`.
- `ShotInputState` gains nothing. Drivers publish state through the existing `SetExternalPower(power, finetune)` so every `OnStateChanged` subscriber (`PowerGaugeWidget`, `CentralBallWidget`, `ShotInProgressUiGate`, `ConeAlphaController`, `TeeIdleGlowController`, `MapViewController`, `PuttPathPredictor`, `PutterAimLine`, `ActionButtonsRoot`) keeps working unmodified.

### 3.2 `ControlScheme` + `ControlSchemeService` (new, `Assets/Scripts/Gameplay/Config/`)

```csharp
public enum ControlScheme { Flick = 0, Pendulum = 1, Needle = 2, FreeSwing = 3 }

public static class ControlSchemeService
{
    public const string PrefKey = "golfin.controlScheme";          // QualityTierService precedent
    public static ControlScheme Current { get; }                    // read once at first access, default Flick
    public static event Action<ControlScheme> OnSchemeChanged;      // raised only when the value moves
    public static void Set(ControlScheme s, string source);         // persists + raises; source = "settings" | "ingame"
    public static string Label(ControlScheme s);                    // localisation KEY, see §4
}
```
Unknown / out-of-range pref values read as `Flick`. Lives in `Golfin.Gameplay.Config` so both the Settings UI (Assembly-CSharp) and `ShotSchemeHost` can see it — check the asmdef reference direction before placing it; if Config cannot be referenced from the UI assembly, put it in `Golfin.Gameplay.Input` next to `ShotController` and NOTE why in the report.

### 3.3 `IShotSchemeDriver` + `ShotSchemeHost` (`Assets/Scripts/Gameplay/UI/ShotUI/`)

```csharp
public interface IShotSchemeDriver { ControlScheme Scheme { get; } void Bind(ShotController c); void Activate(); void Deactivate(); }
```
- `ShotSchemeHost : MonoBehaviour` sits on `ShotUI_Canvas` in `LabScaffold.unity`, holds `[SerializeField] GameObject[] schemeRoots` (index = `ControlScheme`), and a `[SerializeField] ShotController _shotController`.
- On enable: activate `schemeRoots[(int)ControlSchemeService.Current]`, deactivate the rest, `Bind` + `Activate` the driver found on the active root. Subscribe `OnSchemeChanged`.
- On change: if `_shotController.State == ShotState.Idle` swap now; else latch `_pending` and swap on the next `OnStateChanged` whose state is `Idle`. Never swap mid-swing.
- **`SchemeRoot_Flick`** = a new empty `GameObject` under `ShotUI_Canvas` that becomes the parent of the existing `ClubHandle` (`ClubHandleDragger`), `ShotConeView` root (`ConeMeshGraphic`, `TimingSlabGraphic`, arrows, `_targetingLine`, `_putterTrack`) and `ConeAlphaController` — **re-parent only, no property, anchor or sibling-order change that alters rendering** (verify with a before/after screenshot of the Lab at Idle and at Timing). A tiny `FlickSchemeDriver : MonoBehaviour, IShotSchemeDriver` on that root returns `Scheme = Flick` and does nothing else (the existing `ClubHandleDragger` already drives the controller).
- `SchemeRoot_Pendulum` / `_Needle` / `_FreeSwing` = empty inactive GameObjects with a `PlaceholderSchemeDriver` whose `Activate()` logs `"[ShotSchemeHost] scheme X not implemented — Flick input still active"`, and **for this spec the host keeps `SchemeRoot_Flick` active for any unimplemented scheme** (so a tester who picks Pendulum on this build still has a working game; the pref persists and the driver lands later). Remove that fallback rule in the first scheme spec.
- `CentralBallWidget`, `PowerGaugeWidget`, `ActionButtonsRoot`, `MapViewController`, `SettingsButton` etc. stay where they are (shared HUD, not scheme UI).

### 3.4 Settings switch

**Settings screen** (`ShellScene.unity`, `SettingsController`): new accordion item `controlsItem` (a `SettingsMenuItem`) + `ControlsSubmenu` between `graphicsItem` and `languageItem`, exactly the `LanguageSubmenu` radio pattern from the Figma frame `14089:101926`: 4 rows FLICK / PENDULUM / TAP TIMING / FREE SWING with the `S_Common_RadioButton` atom (Element Reuse Map: every row = a clone of the Language row; icon = `Settings Icons/Controls` exported from `4060:7534`). `ControlsSubmenu` mirrors `GraphicsSubmenu`: `OnEnable` subscribes `ControlSchemeService.OnSchemeChanged`, `UpdateUI()` paints the selected radio, a tap calls `ControlSchemeService.Set(s, "settings")`. Register the new item in `SettingsController`'s accordion sweep (it already sweeps `GetComponentsInChildren<SettingsMenuItem>(true)` — confirm nothing else needs a hand-wired reference).

**In-game gear modal** (`InGameSettingsModalController`, prefab under `ShotUI_Canvas/SettingsButton`'s modal): a CONTROLS header row (same row style as SOUND SETTINGS, icon `Controls`) + a **2×2 grid** of segment buttons per Figma `14090:101896` — selected = the gold gradient of the RETURN button with navy text, unselected = 10 % white fill, 55 % white 3 px stroke, white text, Rubik SemiBold 40, radius 20, 110 tall. Tapping calls `ControlSchemeService.Set(s, "ingame")`; the modal repaints from `OnSchemeChanged`. The panel grows; the modal already scrolls/hugs — verify at 1170×2532 and 16:9 that the CLOSE/RETURN buttons stay reachable.

### 3.5 Telemetry

- `shot_taken` gains `"scheme": (int)ControlSchemeService.Current` next to the three timing keys — add it inside `GameSession.AppendShotTimingKeys` so `ShotTimingTelemetryTests` and `ShotTimingTelemetryVerify` cover it; add `SchemeId` to `ShotRecord` (populated by `TelemetryHooks` from `ControlSchemeService.Current` at shot-complete time; the scheme cannot change mid-shot because the host defers swaps to Idle).
- New event `TelemetryEventNames.ControlsSchemeChanged = "controls_scheme_changed"` with `{ from, to, where }`, recorded from `ControlSchemeService.Set` via the existing `TelemetryService.Instance.RecordSafe` path (guard: no-op when telemetry is not initialised — Editor tests).
- Dashboard `Tools/admin-dashboard`: the Flick-timing card in the telemetry panel gets a **Scheme** filter (All / Flick / Pendulum / Tap Timing / Free Swing) applied in `lib/telemetryData.ts` where `timing_band` is aggregated; rows without `scheme` count as Flick. Labels in `lib/i18n.ts` `DICT` with `en` + `ja`. `mockTelemetry.ts` gains a `scheme` field so the mock still renders.

## 4. Strings — via the two-way importer, EN + JA in the same commit

`Assets/Localization/LocalizationText.csv`:

| key | EN | JA |
|---|---|---|
| `SETTINGS_CONTROLS` | Controls | 操作方法 |
| `SETTINGS_CONTROLS_FLICK` | Flick | フリック |
| `SETTINGS_CONTROLS_PENDULUM` | Pendulum | 振り子 |
| `SETTINGS_CONTROLS_TAPTIMING` | Tap Timing | タップタイミング |
| `SETTINGS_CONTROLS_FREESWING` | Free Swing | フリースイング |

`python3 Tools/content/import_content.py --env-file … --catalogs texts` (PLAN, read the verdicts; stop and report on CONFLICTS) → `--apply` → publish `texts` from the admin → `export_content.py --check` clean. Dashboard labels go in `lib/i18n.ts` `DICT`, never in the CSV. Zero new hardcoded `.text` literals.

## 5. Files (expected)

- `Assets/Scripts/Gameplay/Input/ShotController.cs` — `ResolveAndPublish`, `CommitExternal`, `ownsTiming`
- `Assets/Scripts/Gameplay/Input/ShotIntent.cs` (new)
- `Assets/Scripts/Gameplay/Config/ControlScheme.cs`, `ControlSchemeService.cs` (new)
- `Assets/Scripts/Gameplay/UI/ShotUI/IShotSchemeDriver.cs`, `ShotSchemeHost.cs`, `FlickSchemeDriver.cs`, `PlaceholderSchemeDriver.cs` (new)
- `Assets/Scenes/Physics/LabScaffold.unity` — `ShotSchemeHost` + the four roots (re-parent only)
- `Assets/Scripts/UI/ControlsSubmenu.cs` (new), `SettingsController.cs` (+ item/submenu fields), `Assets/Scenes/ShellScene.unity`
- `Assets/Scripts/UI/Modals/InGameSettingsModalController.cs` + its prefab
- `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` (`ShotRecord.SchemeId`, `AppendShotTimingKeys`), `Assets/Scripts/TelemetryRuntime/TelemetryHooks.cs`, `Assets/Scripts/Telemetry/TelemetryConfig.cs`
- `Assets/Localization/LocalizationText.csv`; `Tools/admin-dashboard/lib/{telemetryData,mockTelemetry,types,i18n}.ts` + the telemetry panel
- Art: `Assets/Art/UI/Settings/S_Settings_Icon_Controls.png` exported from Figma `4060:7534` variant `Controls` (same size/format as the existing settings icons)

## 6. Tests (EditMode, `Golfin.Gameplay.Tests` unless noted)

1. **Parity — the load-bearing one.** `ShotAimParityTests`, `ShotTimingPowerTests`, `ShotControllerFlickGateTests`, `ShotControllerPuttModeTests`, `ShotControllerTests`, `FadeDrawWiringTests`, `ShotTimingTelemetryTests` pass **unchanged** (no edits to their assertions). Add one `ShotControllerSeamParityTests`: drive the same synthetic flick through `CommitFlick` (via the external-drag path) and through `CommitExternal` with an intent built from the controller's own `PowerNormalized` / `_aimFinetune` / `TimingPowerMultiplier()` / degradation — the resulting `ShotInput.velocity` and `Spin` are bit-identical (`fp` equality).
2. `CommitExternal` outside `Timing/Pulling` or without an external drag → no `OnShotResolved`, state unchanged.
3. `ownsTiming: true` → 5 s of `Tick` in `Timing` leaves `_passIndex == 0`, no `TransitionToIdle`, no degradation; `ownsTiming: false` unchanged.
4. `ControlSchemeService`: default Flick with no pref; `Set` persists and raises once; setting the same value raises nothing; garbage pref reads as Flick.
5. `ShotSchemeHost` (can run on a scene-less GameObject): change during `Timing` does not swap; swap fires on the next Idle; unimplemented scheme keeps `SchemeRoot_Flick` active.
6. `AppendShotTimingKeys` writes `scheme`; `ShotTimingTelemetryVerify` (Editor tool) shows the key on a real hole.
7. Dashboard: `telemetryData` unit test (if the suite exists) — scheme filter buckets, missing `scheme` → Flick.

## 7. Acceptance

- Lab scene with Flick selected: before/after screenshots at Idle and Timing are pixel-identical (cone, handle, slab, arrows, targeting line, alpha fade).
- Full EditMode sweep per assembly (filtered runs mask failures): zero new failures vs. `main`.
- Settings › Controls and the in-game gear modal both show the 4 options, share one value, persist across relaunch, and a change mid-swing applies at the next Idle (device: pull, switch in the modal, release → this shot is still Flick; the next is the new pref).
- Picking any non-Flick scheme on this build still plays as Flick (placeholder rule) and logs the not-implemented line once.
- `shot_taken` carries `scheme`; `controls_scheme_changed` fires from both surfaces with the right `where`.
- `--check` clean for `texts`; grep quoted in the report showing zero new hardcoded `.text` literals.
- Manual on-device: the two Settings frames match Figma `14089:101926` / `14090:101896` at 1170×2532 (Element Reuse Map + `figma_diff.py` per Rule 21).

## 8. Out of scope → `Docs/GPS/GPS_BACKLOG.md` rows added this session

Haptics per grade; TW 3-click as a fifth scheme; per-scheme first-shot hint; bot error-model parity (bots stay `TimingMul = 1` in all schemes); converging-circle timing. See plan §9.
