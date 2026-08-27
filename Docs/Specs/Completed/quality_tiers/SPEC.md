# SPEC — `quality_tiers` (roadmap `9a`, Order 900 — Phase 2 of `Docs/PERF_OPTIMIZATION_PLAN.md`)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.
> Inputs: `Docs/PERF_OPTIMIZATION_PLAN.md` §2/§3/§6, `Docs/Specs/Queued/9a_quality_tiers/ARCHITECT_BRIEF.md` (Code's Phase 1 hand-off — move it into this folder), `Docs/Reports/perf_baseline_2026-08-26.md` §10–§11.

## Status

See `STATUS.md`. Starts at `SPEC_READY`.

## Goal

Three quality tiers — **Low / Mid / High** — resolved automatically from the device at boot, overridable in Settings (Auto / Low / Mid / High), persisted like language and volume. Tiers change **presentation only**: render scale, target frame rate, shadow cascades/distance/resolution, LOD0 skipping, tree wind, post-processing/HDR on the shell camera. They never change what is on the course: same terrain mesh, same trees in the same places, same cull distance, nothing the sim reads. (Fairness rule, plan §2 — locked by Cesar.)

Why now: Phase 1 put every measured pose at 60 fps *cold* on the A17 Pro, but H08 falls to 47.5 fps and H06 to 40.7 after 45 s at thermal Serious (brief §1). Per Cesar (2026-08-27): **static tiers only — no thermal input in this task**; the beta telemetry (`fps_avg`/`fps_low` per hole, now with `tier`) decides whether a thermal governor is needed later.

## Decisions of record (Cesar)

- Low runs at **30 fps** (2026-08-26). Mid/High 60.
- Terrain pixel error, heightmap, tree placement, tree draw/cull distance: **identical on every tier.** `lodBias` is **never** used (it scales the cull threshold).
- `Vegetation.shader` may be edited in place: `shader_feature _WIND` → `multi_compile _ _WIND` (2026-08-26).
- Home-screen bloom (post-processing on the shell camera) **High only**; HDR High only.
- Plan Option C (basemap distance / `drawInstanced`) is **dead** — Phase 1 measured no gain and `drawInstanced` flattens distant terrain (brief §3). Not a tier lever.
- No Adaptive Performance package, no thermal governor (2026-08-27). H06 heightmap density is a **separate task** (`hole_heightmap_density`, Queued) running alongside.

## Tier table

| Setting | **Low** | **Mid** | **High** (= today, minus shadow trim) |
|---|---|---|---|
| `Application.targetFrameRate` | **30** | 60 | 60 |
| URP `m_RenderScale` | 0.6 | 0.7 | 0.8 |
| Main-light shadows | on — **1 cascade, 512, 15 m** (ball/near only; trees effectively unshadowed without touching 23k renderers) | 1 cascade, 1024, 40 m | **2 cascades, 1024, 60 m** (was 4 / 100 — Phase 0b (b) measured −3.7 ms at 1/40; 2/60 is the headroom High needs against thermal. Cesar judges the look — fallback 4/100) |
| Soft shadows | off | off | off |
| `QualitySettings.maximumLODLevel` | **1** (skip LOD0 — Spruce LOD0 is 15–17k tris; cull threshold unchanged) | 0 | 0 |
| Tree wind | **off** (§4) | on | on |
| Shell camera post-processing + HDR | off | off | on |
| `QualitySettings.anisotropicFiltering` | Disable | ForceEnable→ *Enable* (per-texture) | Enable |
| Everything else | identical | identical | identical |

Not in the table on purpose: terrain settings (Phase 1 §3 stays as shipped: tree distances 150/80/20 at hole load, basemap authored), water, MSAA (off everywhere), additional-light mode, reflection probes.

## Device → tier table (in code, not CSV — brief Q1)

`QualityTierResolver.Resolve()` → `(QualityTier tier, string reason)`. Unknown hardware → **Mid**.

**iOS** — parse `SystemInfo.deviceModel` (`"iPhone16,2"`, `"iPad14,3"`): major number =
- iPhone ≤ 11 (A11/A12: 8, X, XR, XS) → **Low**
- iPhone 12 (11, SE2 = `iPhone12,8`, A13), iPhone 13 (12-series, A14) → **Mid**
- iPhone ≥ 14 (13-series, SE3 = `iPhone14,6`, 14, 15, 16… A15+) → **High**
- iPad ≥ 13 → High; iPad 8–12 → Mid; older → Low. `iPod` → Low.
- Unparseable → Mid.

**Android** — start at Mid, then:
- GPU (`SystemInfo.graphicsDeviceName`): `Adreno (TM) 7xx`/`8xx`, `Mali-G710/G715/G720`, `Immortalis`, `Xclipse` → High; `Adreno 6[4-9]x`, `Mali-G7[6-8]`, `Mali-G68` → Mid; `Adreno 5xx/60x–63x`, `Mali-G5x`, `Mali-G3x`, `Mali-T`, `PowerVR` → Low.
- Caps (apply after): `SystemInfo.graphicsDeviceType == OpenGLES3` → at most Mid; `systemMemorySize < 3500` → Low; `< 5500` → at most Mid.
- Editor / Standalone → High.

Log one line at boot: `[QualityTier] resolved=<tier> source=<auto|override> device=<model> gpu=<name> mem=<mb> reason=<rule>`.

## Architecture context

- **Assemblies (verified):** the service lives in **`Golfin.Gameplay.UI`** — `Assets/Scripts/Gameplay/UI/ShotUI/Quality/` next to `HUD/TreeWindDriver.cs`. Reason: `Golfin.Physics.Viewer` references `Golfin.Gameplay.UI` (asmdef `references`), `Golfin.Gameplay.UI` already references `Unity.RenderPipelines.Universal.Runtime` (needed for `UniversalAdditionalCameraData`), and it is `autoReferenced: true` so Assembly-CSharp (`SettingsController`, `TelemetryHooks`) sees it. Assembly-CSharp cannot be referenced by asmdefs, so `Assets/Scripts/Core/` is NOT an option. Tests go in `Golfin.Gameplay.Tests`. No new asmdef.
- **Existing code:**
  - `Assets/Scripts/Core/FramePacingBootstrap.cs` — `Boot()` at `BeforeSceneLoad` pins 60. **Keep it**; the service overrides after (`AfterSceneLoad`), and its own comment says it is the Order 900 hook.
  - `Assets/Scripts/Gameplay/UI/ShotUI/HUD/TreeWindDriver.cs` — `Apply()` walks `terrain.terrainData.treePrototypes` materials, sets `WindSpeedFloat1`, caches authored values, `RestoreAuthored()`. Extend, do not duplicate.
  - `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — `OnHoleLoaded` (`:2032`), `ApplyTerrainRenderDefaults()` (Phase 1), `DisableShellCamera()`/`RestoreShellCamera()`, `OnDestroy()`.
  - `Assets/Scripts/UI/SettingsController.cs` — accordion of `SettingsMenuItem`s (`userProfileItem`, `soundSettingsItem`, `languageItem`, `aboutItem`) + submenus; `Assets/Scripts/UI/LanguageSubmenu.cs` — two buttons, `selectedColor`/`unselectedColor`, persists via `LocalizationManager`. **The Graphics submenu copies this shape exactly.**
  - `Assets/Scripts/Audio/AudioManager.cs:139` — `PlayerPrefs` persistence precedent. Tier override persists in **PlayerPrefs** (`golfin.qualityTier`, `-1` = Auto), not SaveData — it is a device setting like volume/language, not account state.
  - `Assets/Scripts/TelemetryRuntime/TelemetryHooks.cs:94` — `session_start` payload (`device_model`, `os`, `memory_mb`, `screen`). Add `tier`, `tier_source`.
  - `Assets/Scripts/Dev/PerfBaselineBot.cs` — jobs 9–13 are Phase 1; add tier jobs from index 14. `job.txt` override gains a `tier=` field.
  - `Assets/Localization/LocalizationText.csv` — `SETTINGS_*` keys (`SETTINGS_LANG,Language,言語` at line 61 is the pattern).
- **Assets:** `Assets/Settings/Mobile_RPAsset.asset` (becomes High), `Mobile_Renderer.asset` (shared by all three), `ProjectSettings/QualitySettings.asset` (levels `Mobile`, `PC`).
- **Shaders:** `Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader` (force-added, GUID `e80a1e91…`) — 5× `#pragma shader_feature _WIND` (lines 315, 1016, 1426, 1815, 2231). `Assets/Realistic Tree/Shader/URP/Leaves_URP.shadergraph` — Wind Speed reference `Vector1_b0ddedae341d4c7ba1d429299f3078ea` on materials `Assets/Realistic Tree/Source/Materials/URP/Leaves/Spruce/Spruce_1.mat`, `Spruce_2.mat` (shader guid `002c9967…`).

## Implementation

### 1. Assets: three URP pipeline assets + three Quality levels

- Duplicate `Mobile_RPAsset.asset` → `Mobile_Low_RPAsset.asset`, `Mobile_Mid_RPAsset.asset`; rename the original's `m_Name` to `Mobile_High_RPAsset` **without changing its file/GUID** (QualitySettings and GraphicsSettings reference it). All three point at the same `Mobile_Renderer.asset` (`m_RendererDataList`).
- Set per the tier table: `m_RenderScale`, `m_ShadowCascadeCount`, `m_ShadowDistance`, `m_MainLightShadowmapResolution`, `m_SupportsHDR` (Low/Mid 0, High 1). Soft shadows stay 0. Leave `m_VolumeProfile` on all three (post-processing is a **camera** flag, §3).
- `ProjectSettings/QualitySettings.asset`: replace level `Mobile` with three levels in this order — **index 0 `Low`, 1 `Mid`, 2 `High`**, then `PC` (index 3). Each `customRenderPipeline` → its asset. `maximumLODLevel`: 1 / 0 / 0. `anisotropicTextures`: 0 / 1 / 1. `excludedTargetPlatforms: [Standalone]` on all three mobile levels, `PC` keeps `[Android, iPhone]`. **`m_PerPlatformDefaultQuality` iPhone/Android → 1 (Mid)** so the first frame before the service runs is Mid. Do this in the Quality window, not by hand-editing YAML.
- ⚠️ Expect `m_Prefilter*` churn on all three RP assets after the first build (Phase 1 §10.4b). Diff all of `Assets/Settings/`; commit the churn; nothing else may change.
- ⚠️ Shader variants: three assets = more prefiltered keyword combos (cascade count, HDR). Report the build-time and `Data/` size delta vs Phase 1 (`Builds/*/build-report*.txt`).

### 2. `QualityTierService` (runtime)

`Assets/Scripts/Gameplay/UI/ShotUI/Quality/QualityTier.cs` (`enum QualityTier { Low = 0, Mid = 1, High = 2 }`, namespace `Golfin.Gameplay.UI.Quality`), `QualityTierResolver.cs` (the device table above, pure static, unit-testable), `QualityTierService.cs`:

```csharp
public static class QualityTierService
{
    public const string PrefKey = "golfin.qualityTier";        // -1 = Auto
    public static QualityTier Current { get; private set; }
    public static bool IsOverride { get; private set; }
    public static event Action<QualityTier> OnTierChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]  // after FramePacingBootstrap
    static void Boot() => Apply(ResolveEffective(), fromBoot: true);

    public static void SetOverride(int prefValue /* -1..2 */) { PlayerPrefs.SetInt(...); PlayerPrefs.Save(); Apply(ResolveEffective(), false); }

    static void Apply(QualityTier tier, bool fromBoot)
    {
        QualitySettings.SetQualityLevel((int)tier, applyExpensiveChanges: true);   // swaps the URP asset live
        Application.targetFrameRate = tier == QualityTier.Low ? 30 : 60;
        Current = tier; ... log line ...
        if (!fromBoot || ...) OnTierChanged?.Invoke(tier);
    }
}
```

- `ResolveEffective()` = PlayerPrefs override if 0..2, else `QualityTierResolver.Resolve()`.
- `SetQualityLevel` with `applyExpensiveChanges: true` is safe on Home and mid-hole (URP re-reads the asset next frame). Hole-scoped effects (§3, §4) re-apply through `OnTierChanged`.
- Tests (EditMode, `Golfin.Gameplay.Tests`): `QualityTierResolver` table — `iPhone10,3`→Low, `iPhone12,8`→Mid, `iPhone14,6`→High, `iPhone16,2`→High, `iPad13,1`→High, garbage→Mid; Android: `Adreno (TM) 740`→High, `Adreno (TM) 650`+GLES3→Mid, `Mali-G52`→Low, 3 GB→Low.

### 3. Hole-scoped effects (`PhysicsLabController`)

In `OnHoleLoaded`, next to `ApplyTerrainRenderDefaults()`, call `ApplyTierHoleEffects(QualityTierService.Current)`; subscribe to `OnTierChanged` in `Awake`/unsubscribe in `OnDestroy` and call it again on change:

- **Shell camera post-processing/HDR:** the shell camera is already disabled during a hole (Phase 1). For the Home screen: in `QualityTierService.Apply`, find the ShellScene camera's `UniversalAdditionalCameraData` and set `renderPostProcessing = (tier == High)`; HDR follows the asset. (`Camera.allCameras`, scene name `"ShellScene"`, same lookup as `DisableShellCamera`.)
- **Tree wind:** `TreeWindDriver.SetEnabled(tier != QualityTier.Low)` (§4).
- Nothing else — terrain, LOD cull, tree distances are tier-independent by rule.

### 4. Tree wind off on Low — the real saving

- `Vegetation.shader`: the 5 `#pragma shader_feature _WIND` → `#pragma multi_compile _ _WIND`. Both variants now ship. Note the material toggle `[Toggle(_WIND)]` stays; materials keep `_WIND` enabled at authoring.
- **A global `Shader.DisableKeyword("_WIND")` does NOT override a keyword enabled on the material** (material-local + global are OR'd). So the toggle is per material: extend `TreeWindDriver` with `SetEnabled(bool)`: when false, for every `Custom/Vegetation` material it already walks → cache `_WIND` state in `_authored` (same editor-safety pattern), `m.DisableKeyword("_WIND")` and `SetFloat(WindSpeedId, 0)`; when true → `EnableKeyword` + re-`Apply()`. `RestoreAuthored()` restores keywords too (editor guard already calls it).
- Spruce (`Leaves_URP` Shader Graph, no keyword): set `Vector1_b0ddedae341d4c7ba1d429299f3078ea` (Wind Speed) to 0 on `Spruce_1.mat`/`Spruce_2.mat` (find them once via `Resources.FindObjectsOfTypeAll<Material>()` filtered by shader name `Shader Graphs/Leaves_URP`; cache authored value; restore like the rest). This freezes Spruce visually; the vertex math still runs — accepted, Spruce rendering is Phase 3's problem.
- **Measure and report the build-size delta** of the multi_compile change (K5 asked for the number).
- Acceptance: on Low, trees are static on Hole 01 (BSP) and Hole 08 (Spruce); switching to Mid in Settings mid-hole brings the sway back without a reload.

### 5. Settings UI — "Graphics" accordion item

- `SettingsController`: add `graphicsItem` (`SettingsMenuItem`) + `graphicsSubmenu` (`GraphicsSubmenu : MonoBehaviour`), inserted between Sound and Language in the accordion. Build the prefab by **duplicating the Language submenu hierarchy** (four buttons instead of two: Auto / Low / Mid / High), same `selectedColor`/`unselectedColor`, same `SettingsMenuItem` wiring. Don't rebuild the accordion.
- `GraphicsSubmenu` mirrors `LanguageSubmenu`: on click → `QualityTierService.SetOverride(-1|0|1|2)`, highlight current; when Auto is selected show the resolved tier in the Auto label (`Auto (High)`).
- Localization keys (EN, JP) appended to `LocalizationText.csv` and bound with `LocalizedText`: `SETTINGS_GRAPHICS,Graphics,グラフィック` · `SETTINGS_QUALITY_AUTO,Auto,自動` · `SETTINGS_QUALITY_LOW,Low,低` · `SETTINGS_QUALITY_MID,Medium,中` · `SETTINGS_QUALITY_HIGH,High,高`. `UIFidelityLinter` `unlocalized-text` must stay clean.
- The in-game gear (`InGameSettingsModalController`) does **not** get the control in this task; changing tier mid-hole happens via the Home settings only. (Mid-hole application still works through `OnTierChanged` if a future task adds it.)
- Figma: none exists for this submenu. Match the Language submenu 1:1; screenshot EN + JP for the report.

### 6. Telemetry

`TelemetryHooks.cs:94` `session_start`: add `["tier"] = QualityTierService.Current.ToString()`, `["tier_source"] = QualityTierService.IsOverride ? "override" : "auto"`. Backend/dashboard unchanged (the panel's raw explorer shows new fields as-is). This is what decides the thermal question later.

### 7. Harness

`PerfBaselineBot`: `job.txt` gains `tier=low|mid|high` → applied via `QualityTierService.SetOverride` before the hole loads; jobs 14–19 = `T_h08_tee_{low,mid,high}`, `T_h06_tee_{low,mid,high}`; job 20 = `T_h06_endurance_high` / 21 `_mid` / 22 `_low`: hold the H06 tee pose **5 minutes**, log fps + thermal every 30 s. Indices 0–13 frozen.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Device protocol = report §10/§11 (cooled to Nominal, pinned sky + yaw, 3 runs, median + raws, frame PNG per number; `fps`/`frameMs` are the verdict, `renderMs` only when all three runs agree — brief §6).

- [ ] **Resolver:** EditMode table tests pass; boot log on the iPhone 15 Pro Max reads `resolved=High source=auto`.
- [ ] **Override:** Settings → Graphics → Low; relaunch; boot log reads `source=override` tier Low; Auto restores. EN + JP screenshots of the submenu.
- [ ] **Fairness A/B (the rule):** Hole 08 tee, pinned sky/yaw, Low vs High frames — **every tree silhouette in the same place**, same terrain, same far-tree cut. Differences allowed: shadows, sharpness (render scale), LOD detail, wind, sky bloom. Cesar signs this one.
- [ ] **H08 tee / H06 tee / H01 tee per tier (cooled):** table with fps, frameMs, batches, tris, shadow casters. High ≥ 58 fps. Mid: batches and shadow casters strictly below High. Low: 30.0 fps flat, tris below Mid (no LOD0).
- [ ] **Endurance, H06 tee, 5 min:** High, Mid, Low — fps at 0/1/2/3/4/5 min + thermal state. Target: Mid holds ≥ 55 through minute 5; Low holds 30 through minute 5. Report High's curve as-is (brief §1 says it will not hold — that is the point of the table).
- [ ] **Shadows:** High at 2 cascades/60 m vs the 4/100 frame — Cesar judges; if rejected, High reverts to 4/100 and the report says so.
- [ ] **Tree wind:** Low = static trees on Hole 01 (BSP, keyword off — confirm via Frame Debugger keyword list) and Hole 08 (Spruce); Mid/High sway. Switching Low→Mid on Home then loading a hole → sway.
- [ ] **Home bloom:** High only; Low/Mid Home screenshot shows no bloom, no visual breakage.
- [ ] **Aim-arrow feel at 30 fps (Low):** Cesar plays one hole on Low, driver + putter. Verdict recorded. If the arrow reads wrong, file `arrow_speed_retune` v2 — do not retune here.
- [ ] **Build size / variant delta** vs Phase 1 build reported (multi_compile `_WIND` + three RP assets).
- [ ] **Telemetry:** `session_start` carries `tier` + `tier_source` (device log or REST probe).
- [ ] `Assets/Settings/` diff = the three RP assets + expected prefilter churn; `ProjectSettings/QualitySettings.asset`; nothing else. No `.unity` edit except ShellScene (Settings prefab wiring — if the submenu is a prefab instance, keep the scene diff to the new item only).
- [ ] `Vegetation.shader` diff = the 5 pragma lines only.
- [ ] EditMode: 1765+ pass, 0 failed (report actual). Console clean.
- [ ] Spec deviations flagged with justification.

## Files / hierarchy this task touches

- `Assets/Settings/Mobile_RPAsset.asset` (→ High), **new** `Mobile_Low_RPAsset.asset`, `Mobile_Mid_RPAsset.asset`; `ProjectSettings/QualitySettings.asset`.
- **new** `Assets/Scripts/Gameplay/UI/ShotUI/Quality/{QualityTier,QualityTierResolver,QualityTierService}.cs` + EditMode tests in `Golfin.Gameplay.Tests`.
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/TreeWindDriver.cs` (+ `TreeWindDriverEditorGuard` restore path).
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` (§3 hook, subscription).
- `Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader` (5 pragmas).
- `Assets/Scripts/UI/SettingsController.cs`, **new** `Assets/Scripts/UI/GraphicsSubmenu.cs`, Settings prefab/ShellScene (Graphics item + submenu), `Assets/Localization/LocalizationText.csv`.
- `Assets/Scripts/TelemetryRuntime/TelemetryHooks.cs`.
- `Assets/Scripts/Dev/PerfBaselineBot.cs`.
- `Docs/Reports/perf_baseline_2026-08-26.md` → append §12 "Tiers".

## Out of scope (do NOT do these)

- Thermal governor / Adaptive Performance (Cesar, 2026-08-27: static tiers only).
- Terrain basemap / `drawInstanced` / pixel error / heightmap (Option C is dead; H06 density is `hole_heightmap_density`).
- Spruce conversion, GPU Resident Drawer, impostors (Phase 3).
- Tree shadow casting per renderer, tree draw distance, `lodBias`.
- Audio/texture/HoleData/Optimize Mesh Data (Phase 4).
- In-game gear modal control; 120 Hz; Android Swappy (Option G remainder — separate).
- Hole 02 invisible trees.
