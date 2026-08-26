# IMPLEMENTER_REPORT — `quality_tiers` (roadmap 9a, Order 900)

**Iteration shape:** `quality_tiers:initial-implementation`
**Canonical screenshot:** `screenshots/fairness_full_high_mid_low.png` (1580×1125 — High / Mid / Low at one pose)
**Implemented by:** Claude Code (main thread), 2026-08-27. Not the subagent pipeline — Cesar asked for a direct implementation.

---

## 0. The boundary, stated up front

Everything that is a question about **code, assets, state or pixels** is done and verified.
Everything that is a question about **milliseconds on a cooled phone** is not, and cannot be
from a Mac: no device, no `GOLFIN_TESTBUILD` install, no thermal sensor. `PerfBaselineBot`
jobs 14–25 are written and in the build; running them is Cesar's step. § 9 lists exactly what
is outstanding and how to get it.

---

## 1. Files modified or created

Every uncommitted path outside this task folder, attributed.

### Mine — code

| File | Change |
|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/Quality/QualityTier.cs` | **NEW.** `enum QualityTier { Low=0, Mid=1, High=2 }`. Values ARE the quality-level indices. |
| `Assets/Scripts/Gameplay/UI/ShotUI/Quality/QualityTierResolver.cs` | **NEW.** The device→tier table, pure/static so the whole thing is unit-testable without hardware. |
| `Assets/Scripts/Gameplay/UI/ShotUI/Quality/QualityTierService.cs` | **NEW.** Boots at `AfterSceneLoad`, resolves + applies the tier, persists the override in PlayerPrefs, raises `OnTierChanged`, owns shell-camera post-processing. |
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/TreeWindDriver.cs` | `SetEnabled(bool)` + authored-keyword/Spruce caches; `Apply()` honours `WindEnabled`; `RestoreAuthored()` restores keywords and Spruce too. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | `ApplyTierHoleEffects(tier)` called from `OnHoleLoaded`; subscribe in `Awake`, unsubscribe in `OnDestroy`. +34 lines, no deletions. |
| `Assets/Scripts/UI/GraphicsSubmenu.cs` | **NEW.** Auto/Low/Medium/High submenu, a 1:1 shape copy of `LanguageSubmenu`. |
| `Assets/Scripts/UI/SettingsController.cs` | `graphicsItem` + `graphicsSubmenu` slots, registered in the accordion between Sound and Language. 3 lines. |
| `Assets/Scripts/TelemetryRuntime/TelemetryHooks.cs` | `session_start` gains `tier` + `tier_source`. |
| `Assets/Scripts/Dev/PerfBaselineBot.cs` | `Job.tier` / `Job.endurance`; jobs 14–25; `tier=` in `job.txt`; `ApplyTier()`; `RunEndurance()`. |
| `Assets/Scripts/Dev/Golfin.DevHarness.asmdef` | +`Golfin.Gameplay.UI` reference (the bot must reach `QualityTierService`; no cycle). |
| `Assets/Scripts/Gameplay/Tests/QualityTierResolverTests.cs` | **NEW.** 33 tests — the whole device table incl. fallbacks. |
| `Assets/Scripts/Gameplay/Tests/QualityTierServiceTests.cs` | **NEW.** 8 tests — override round-trip, level order, frame-rate, `OnTierChanged`, fairness invariants. |
| `Assets/Scripts/UI/Editor/QualityTierVerificationRecorder.cs` | **NEW, editor-only.** The evidence harness (§ 7). Deviation — see § 8. |

### Mine — assets

| File | Change |
|---|---|
| `Assets/Settings/Mobile_RPAsset.asset` → `Mobile_High_RPAsset.asset` | **Renamed, GUID preserved** (`5e6cbd92db86f4b18aec3ed561671858`, asserted in the log). 4 cascades/100 m → **2/60**. |
| `Assets/Settings/Mobile_Mid_RPAsset.asset` | **NEW** (copy). 0.7 / 1 cascade / 40 m / 1024 / HDR off. |
| `Assets/Settings/Mobile_Low_RPAsset.asset` | **NEW** (copy). 0.6 / 1 cascade / 15 m / 512 / HDR off. |
| `ProjectSettings/QualitySettings.asset` | `Mobile` → **Low(0) / Mid(1) / High(2)**, PC now index 3. |
| `Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader` | `shader_feature _WIND` → `multi_compile _ _WIND`. **7 lines, nothing else** (`git diff --stat` = 7 ins / 7 del). |
| `Assets/Localization/LocalizationText.csv` | +5 keys, EN + JP. |
| `Assets/Localization/LocalizationTextTable.asset` | Regenerated from the CSV. |
| `Assets/Scenes/ShellScene.unity` | The Graphics accordion row. **Purely additive** — see § 6. |

### NOT mine — pre-existing drift in the working tree at kickoff

`git status` at kickoff (HEAD `311067a59`) already carried these; I did not touch them and they
are not part of this task:

```
 M Assets/Localization/LocalizationManager.cs
 M Assets/Scripts/Gameplay/Tests/StaminaLiveWiringTests.cs
 M Assets/Scripts/Save/Tests/ClubOwnershipTests.cs
 M Assets/Scripts/Save/Tests/GachaTicketTests.cs
 M Assets/Scripts/Save/Tests/SaveLayerTests.cs
 M Assets/Scripts/Tournaments/Tests/Golfin.Tournaments.Tests.asmdef
 M Assets/Scripts/UI/BuildInfo/AppVersion.cs
 M Docs/PERF_OPTIMIZATION_PLAN.md
 M Docs/TellCode.md
 M Docs/Versioning/last_uploaded_build.txt
 D _to_delete/… (13 paths)
?? Assets/Scripts/Tournaments/Tests/TournamentSnapshotImmunityTests.cs(+.meta)
?? Docs/DEVICE_PASS_CONTENT_PIPELINE.md
?? Docs/Specs/Queued/hole_heightmap_density/
?? tasks/quit_transition_demo/quit_invariants.json
```

**`ShellScene.unity` was ALREADY modified at kickoff** (one added `ContentService` component on
`TournamentService`, 13 lines). My scene work sits on top of it; § 6's structural diff separates
the two.

---

## 2. Assets — the tier table, read back off disk

| | Low | Mid | High |
|---|---|---|---|
| `m_RenderScale` | 0.6 | 0.7 | 0.8 |
| `m_ShadowCascadeCount` | 1 | 1 | 2 |
| `m_ShadowDistance` | 15 | 40 | 60 |
| `m_MainLightShadowmapResolution` | 512 | 1024 | 1024 |
| `m_SupportsHDR` | 0 | 0 | 1 |
| `m_SoftShadowsSupported` | 0 | 0 | 0 |
| renderer | `Mobile_Renderer.asset` | same | same |

Quality levels, read back after the edit:

```
[0] Low  rp=Mobile_Low_RPAsset  maxLOD=1 aniso=0 lodBias=1 terrainOverrides=0 excluded=[Standalone]
[1] Mid  rp=Mobile_Mid_RPAsset  maxLOD=0 aniso=1 lodBias=1 terrainOverrides=0 excluded=[Standalone]
[2] High rp=Mobile_High_RPAsset maxLOD=0 aniso=1 lodBias=1 terrainOverrides=0 excluded=[Standalone]
[3] PC   rp=PC_RPAsset                                                        excluded=[Android iPhone]
```

`m_PerPlatformDefaultQuality`: **iPhone 0→1, Android 0→1** (Mid, as specified). Every OTHER
platform was remapped too — old index 1 meant "PC", which after the insert is index 3. Left
unremapped, `Standalone: 1` would silently have become Mid, a level excluded on Standalone.
`m_CurrentQuality` 1→3 for the same reason.

`lodBias` is 1 on all three and `terrainQualityOverrides` is 0 on all three — the fairness rule,
enforced in the asset, not just in prose.

---

## 3. Resolver + override — EditMode tests

`1809 tests, 1806 passed, 0 failed, 3 skipped` (the 3 skips are pre-existing
`HoleCompleteDriverTests` Stage-C1 skips). **41** of those are new — 33 in
`QualityTierResolverTests` + 8 in `QualityTierServiceTests`. (An earlier revision of this
report said 42; the self-reviewer counted the `[Test]` attributes and was right.)

**Proof the new suites actually ran** (`tests-run` ignores class filters and hides passes, so a
count alone proves nothing): a deliberate `Assert.Fail` tripwire was added to
`Golfin.Gameplay.Tests`, the suite re-run → `1810 total, 1 failed —
Golfin.Gameplay.Tests._TierTripwire.DeliberateFailure`, then the tripwire was deleted and the
suite returned to 1809/0. The +1/−1 is the assembly executing.

Table rows covered: `iPhone10,3`→Low, `iPhone11,8`→Low, `iPhone12,8`→Mid, `iPhone13,2`→Mid,
`iPhone14,6`→High, `iPhone16,2`→High, `iPad13,1`→High, `iPad8,1`→Mid, `iPad7,1`→Low,
`iPod9,1`→Low, garbage/empty/null/`iPhone16`→Mid; `Adreno 740`→High, `Adreno 830`→High,
`Mali-G710`→High, `Immortalis`→High, `Xclipse`→High, `Adreno 650`→Mid, `Mali-G78`→Mid,
`Mali-G68`→Mid, `Adreno 630`→Low, `Adreno 530`→Low, `Mali-G52`→Low, `Mali-T880`→Low,
`PowerVR`→Low, unknown→Mid, `Adreno 650`+GLES3→Mid, `Adreno 740`+GLES3→Mid,
`Adreno 740`+3 GB→Low, `Adreno 740`+4 GB→Mid, `Mali-G52`+4 GB→**Low** (caps never promote),
Editor/Standalone→High.

**Override round-trip, through the REAL row** (real-entry rule — no synthetic toggle):

```
after real LowButton click : pref=0  current=Low  isOverride=True  playerPrefs=0
                             qualityLevel=0  targetFrameRate=30  maxLOD=1
after real AutoButton click: pref=-1 current=High isOverride=False
```

Independent corroboration: the on-screen dev HUD in `screenshots/tier_settings_graphics_low_selected.png`
reads **30.0 fps / 33.3 ms** the moment Low is tapped.

---

## 4. THE FAIRNESS RULE — measured, not asserted

High, Mid and Low captured **in one session at one pose without reloading**, so sky, yaw and
tree-LOD selection cannot drift between frames (two launches could not prove this — Phase 0b
saw 5,483 vs 4,043 batches on the same hole from pose drift alone).

Invariants, logged per tier, **byte-identical across all three**:

```
terrain=TerrainRoot  treeInstances=1968  treeDistance=150  treeBillboardDistance=80
treeCrossFadeLength=20  heightmapRes=2049  pixelError=5  basemapDistance=1000  lodBias=1
```

Tier deltas, same log: `renderScale 0.80/0.70/0.60 · cascades 2/1/1 · shadowDist 60/40/15 ·
hdr True/False/False · maxLOD 0/0/1`.

**Tree-silhouette displacement.** Per-column treeline height (first non-sky pixel), 930 columns
clear of HUD overlays:

| | mean | median | p95 | max | ≤1 px |
|---|---|---|---|---|---|
| High vs Low | **0.02 px** | 0 | 0 | 2 | 98.9 % |
| High vs Mid | **0.01 px** | 0 | 0 | 1 | 100 % |

Raw whole-frame mean abs diff High vs Low: 4.99/255, and it *falls* monotonically under
downsampling — the signature of a sharpness difference, not displacement.

Evidence: `screenshots/fairness_treeline_high_mid_low.png` (full-res treeline crop, stacked
High/Mid/Low) and `screenshots/fairness_full_high_mid_low.png`.

**Cesar judges this one.**

---

## 5. Tree wind — the numbers, and a real bug the read-back caught

Per-material state on the Hole 08 tee, switching tier mid-hole **without a reload**:

| | Low | Mid |
|---|---|---|
| Custom/Vegetation `_WIND` | **False on all 11** | True on the 4 LEAF materials, False on the 7 bark/imposter materials |
| `WindSpeedFloat1` | 0 | 0.1818 |
| Spruce `Vector1_b0dd…` | 0 | restored per material (0.4 / 0.4) |
| `TreeWindDriver.WindEnabled` | False | True |

Low→Mid mid-hole restored the sway with no reload — the acceptance item, satisfied.

> **Bug found and fixed during verification.** The first implementation of `SetEnabled(true)`
> *blanket-enabled* `_WIND` on every Custom/Vegetation material. But only the **leaf** materials
> author the keyword on — bark and imposters ship with it OFF (7 of 14 on Hole 08). So a
> Low→Mid switch was turning wind on for trunks that were never meant to sway, and paying for
> the vertex work. In the Editor `TreeWindDriverEditorGuard` restored the assets on play-mode
> exit and hid it completely; **a player build has no guard**, so this would have shipped.
> `SetEnabled(true)` now restores each material's *cached authored* state. Re-verified above.

Post-play editor state is exactly the authored state (7 on / 7 off, Spruce 0.4/0.4) and **no
`.mat` file appears in `git status`** — zero asset drift.

`Vegetation.shader` diff is exactly 7 insertions / 7 deletions.

> **Deviation:** the spec says "the 5 `#pragma shader_feature _WIND`". There are **7** — the spec
> missed `DepthNormals` (line 2621) and `GBuffer` (3080) alongside Forward/ShadowCaster/DepthOnly/
> Meta/Universal2D. All 7 were converted. Converting only 5 would leave two passes on
> `shader_feature`, whose off-variant is stripped at build time when every material authors the
> keyword on — so on device those passes would have had no `_` variant to fall back to while the
> others did. Partial conversion is not a smaller change, it is a broken one.

---

## 6. Settings UI

Built by **cloning `LanguageRow` wholesale** (clone provenance: `SettingsScreen/SettingsPanel/
SettingsList/LanguageRow` and its `LanguageSubmenu` child), so every sprite, font, colour and
layout value is inherited rather than re-authored. Zero new panels or buttons were drawn.

Read-back off the live objects:

- Order: `SoundSettingsRow · Divider (1) · **GraphicsRow · Divider (Graphics)** · LanguageRow · …`
- `SettingsMenuItem`: button=GraphicsRow, submenuContainer=GraphicsSubmenu, arrowIcon=RightArrow
- `GraphicsSubmenu`: auto/low/mid/high buttons + autoLabel all bound; `selectedColor` /
  `unselectedColor` copied verbatim from `LanguageSubmenu` (`0.200,0.600,1.000` / `0.149,0.259,0.373`)
- `SettingsController.graphicsItem` / `.graphicsSubmenu` assigned
- Buttons: `S_Common_BGCorner8`, Sliced — the real sprite, not a flat fill
- Submenu height 180 → **324** (20 top + 4×64 + 3×8 + 24 bottom, the Language submenu's own pitch)
- Localization resolves both ways: `SETTINGS_GRAPHICS` EN `Graphics` / JP `グラフィック`;
  `..._AUTO` `Auto`/`自動`; `..._LOW` `Low`/`低`; `..._MID` `Medium`/`中`; `..._HIGH` `High`/`高`

**Two defects the read-back caught and fixed** (neither was visible in the state dump alone):

1. `LowButton` / `MidButton` / `HighButton` inherited **`NotoSansJP`** because they were cloned
   from `JapaneseButton`, whose font is Japanese *because its content is the word 日本語*. All
   four now use `Rubik-SemiBold SDF`, matching `AutoButton` and the row header. JP still renders
   — the Language row header proves the fallback works, showing 言語 in Rubik-SemiBold.
2. The row's `LeftIcon` was still the **Language globe**, which on a Graphics row is actively
   misleading. See § 8 — flagged for Cesar.

**Scene diff is purely additive.** `git diff --stat` shows 2023 insertions / 226 deletions, but
those deletions are fileID re-serialisation, not loss. Structural diff vs HEAD:

```
GameObjects HEAD=1279  WORK=1294  (+15)
REMOVED GameObjects   : 0
m_IsActive FLIPS      : 0
RENAMES               : 0
ADDED (15): GraphicsRow, Divider (Graphics), HeaderHitArea, LeftIcon, Label, RightArrow,
            GraphicsSubmenu, AutoButton+Label, LowButton+Label, MidButton+Label, HighButton+Label
```

Screenshots: `tier_settings_graphics_en.png`, `tier_settings_graphics_jp.png`,
`tier_settings_graphics_low_selected.png` — all 1170×2532, driven through the real
`SettingsButton → GraphicsRow → LowButton` chain.

---

## 7. Home bloom — an honest negative result

State flips correctly:

```
home HIGH: postProcessing=Main Camera:True  hdr=True  renderScale=0.80
home LOW : postProcessing=Main Camera:False hdr=False renderScale=0.60  targetFrameRate=30
```

Bloom **is** authored (`SampleSceneProfile.asset`, `active: 1`, intensity 0.5).

But the two Home frames are **pixel-identical apart from the dev FPS counter**: mean abs diff
0.09/255, and all 2 302 differing pixels fall inside `x=[23,229] y=[772,834]`, which is exactly
the FPS overlay box.

So: *no visual breakage on Low/Mid* — the acceptance item is met — but the post-processing lever
**buys nothing on Home**, because Home is a Screen-Space-Overlay UI canvas that covers the 3D
view. Worth knowing before anyone counts it as a saving. Measured, not eyeballed.

---

## 8. Deviations from the spec

| # | Deviation | Why |
|---|---|---|
| 1 | 7 `_WIND` pragmas converted, not 5 | The spec undercounted; `DepthNormals` and `GBuffer` also declare it. § 5. |
| 2 | `Mobile_RPAsset.asset` **renamed** to `Mobile_High_RPAsset.asset` | Unity forces `m_Name` to match the filename on import, so changing `m_Name` alone reverts. `RenameAsset` preserves the GUID (asserted: `5e6cbd92…` before and after), which is the property the spec's stated reason depends on. No path references exist outside the asset itself (grepped). |
| 3 | Shell camera found via `FindObjectsByType(..., FindObjectsInactive.Include)`, not `Camera.allCameras` | `PhysicsLabController` disables the shell camera during a hole and `allCameras` returns only enabled ones, so a mid-hole tier switch would miss it and leave Home wrong after quitting out. |
| 4 | `Golfin.DevHarness.asmdef` gains `Golfin.Gameplay.UI` | `autoReferenced` only helps predefined assemblies; an asmdef needs the explicit reference to reach `QualityTierService`. No cycle. |
| 5 | **NEW** `QualityTierVerificationRecorder.cs` (editor-only) | The spec names no evidence harness, but the repo has ~25 sibling `*DemoRecorder`s and this is how UI evidence is produced here. It is what produced every number in § 4–7 and makes them re-runnable. |
| 6 | `PerfBaselineBot` jobs **23–25** (`T_h01_tee_{low,mid,high}`) added | Spec § 7 stops at H08/H06, but the acceptance checklist asks for an H01 row per tier. Indices 0–19 are exactly as pinned. |
| 7 | Auto row's `LocalizedText` retained; `GraphicsSubmenu` re-asserts `"Auto (High)"` in `LateUpdate` | Both LocalizedText and the submenu write that TMP on a language change and subscriber order is undefined. Re-asserting is order-independent; the object is inactive unless the accordion is open, and the string is only rebuilt when the language or resolved tier moves. |
| 8 | ~~Graphics row icon is a placeholder~~ — **RESOLVED** | Originally the Home-screen grey gear, surfaced to Cesar rather than hand-rolled because `Assets/Art/Settings/` had no display icon. Cesar supplied `Assets/Art/Settings/Quality Icon.png` (display-with-gear, 72×72, matching the row-icon family) and it is wired as of `7a8e99927`. It arrived as a **default texture** (`textureType 0` / `spriteMode None` / `alphaIsTransparency false`), so `LoadAssetAtPath<Sprite>` returned null and the row would have rendered an empty Image; its importer is now mirrored from `Language Icon.png`. Scene diff for that commit is exactly one line — the sprite GUID. |

---

## 9. NOT DONE — needs Cesar (device)

Nothing below can be produced from a Mac. All of it is blocked on the same thing: a cooled
iPhone 15 Pro Max running a `GOLFIN_TESTBUILD` install.

Build **2325** (`Dev-iOS`, `GOLFIN_TESTBUILD`) is built, signed and **installed** on the
iPhone 15 Pro Max (`iPhone16,2`). A warm triage has run; the cooled protocol has not.

| Acceptance item | Status | Note |
|---|---|---|
| Boot log reads `resolved=… source=…` on device | **DONE** | Every triage launch logged `TIER applied=Low/Mid/High pref=0/1/2 qualityLevel=0/1/2 targetFrameRate=30/60/60 maxLOD=1/0/0 aniso=Disable/Enable/Enable`. |
| **H06 tee per tier — WARM TRIAGE** | **DONE (not the protocol)** | § 9.1 below and report §12.6. Low 30.0 flat / Mid 60.0 flat, both Nominal; High 59.8 → **39.5** at Serious. |
| H08 / H06 / H01 tee per tier, **cooled, 3 runs** | **NOT RUN** | Jobs **14–19** and **23–25** via `Tools/perfbot-runjob.sh`. Hours of wall-clock — the triage is what justifies spending them. |
| H06 endurance 5 min per tier + thermal | **NOT RUN** | Jobs **20** (high), **21** (mid), **22** (low), `TIMEOUT=600`. |
| Build size / variant delta vs Phase 1 | **MEASURED** | `Data/` 1,233,700 → 1,233,728 KB = **+28 KB (+0.002 %)**; `globalgamemanagers.assets` +1,408 B; `resources.assets` unchanged. 71 of 73 `Data/` files rewritten, so it is a real rebuild. Baseline: `phase1_build_baseline.txt`; report §12.8. |
| Telemetry `tier` / `tier_source` on the wire | **CODE DONE, NOT OBSERVED** | Fields are in the `session_start` payload; confirm from a device log or the REST explorer. |
| High at 2 cascades / 60 m — look | **CESAR JUDGES** | Fallback if rejected: 4 / 100. |
| Fairness A/B Low vs High | **ACCEPTED by Cesar 2026-08-27** | § 4. Independently re-derived by the self-reviewer (4.986/255 vs the 4.99 cited here). |
| Aim-arrow feel at 30 fps on Low | **CESAR PLAYS** | Build 2325 is on the phone with the Graphics submenu live. If it reads wrong: file `arrow_speed_retune` v2, do **not** retune here. |

### 9.1 Warm triage — H06, one run per tier (2026-08-27)

Back-to-back, no cooldown, `FORCE=1`. **Directional, not publishable.**

| | Low | Mid | High |
|---|---|---|---|
| fps @ sample → +45 s | 30.0 → 30.0 | 60.0 → 60.0 | 59.8 → **39.5** |
| renderMs @ sample → +45 s | 3.22 → 3.44 | 12.26 → 2.22 | 12.29 → **18.33** |
| batches / SetPass | 2,689 / 43 | 2,783 / 43 | 3,062 / 50 |
| triangles | 1,686,415 | 2,384,868 | 2,823,808 |
| shadow casters | 204 | 300 | 579 |
| thermal tee → late | Nominal → Nominal | Nominal → Nominal | **Fair → Serious** |

**Mid holds 60.0 flat at Nominal on the hole Phase 1 could not hold**; High reproduces the Phase 1
failure (brief predicted 40.7, measured 39.5). Mid is strictly below High on batches AND shadow
casters — the spec's Mid criterion, met. Low's triangles are 29 % below Mid — `maximumLODLevel=1`
working. On this evidence static tiers close the gap and no thermal governor is needed.

**Confound:** the three ran Low → Mid → High back-to-back, so High started warmest. All three
booted at `thermalAtBoot=Nominal` and High went Nominal → Fair during its own ~30 s of
navigation, which argues the heat is its own — but that is an argument, not a controlled
measurement. Only the cooled 3-run protocol settles it.

A note on `job.txt`: it now accepts `tier=low|mid|high|auto` as an extra token anywhere in the
file, e.g. `18 0 tier=mid`. `auto` explicitly clears a pinned override, so a Low run cannot leak
into the next launch through PlayerPrefs.

---

## 10. Editor left clean

`isPlaying=False` · `ShellScene dirty=False` · `PlayerPrefs golfin.qualityTier` absent ·
vegetation materials back at authored (7 on / 7 off) · `Spruce_1/2 = 0.4` · no `.mat` in
`git status` · no auto-run scripts armed.

`PlayerSettings.runInBackground` is **True** — set by the capture harness (without it every
play-mode capture returns the splash frame) and left as-is because it is the standing setting
the other `*DemoRecorder`s rely on. It is not committed by this task.
