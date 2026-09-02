# IMPLEMENTER REPORT — `punch_it_gps_variants`

**Implemented:** 2026-08-31 (Claude Code, main thread — build tooling + a compile-time gate, no
UI authoring, so the subagent chain does not apply)
**HEAD at kickoff:** `47caf4bdf` · **⚠️ ANOTHER SESSION WAS DRIVING UNITY THROUGHOUT** (GPS Profile
screens). That constrains what could be verified — see § Blocked on exclusive Editor access.

## Acceptance checklist

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | EditMode `GpsGate` tests — blocked/allowed per state, `IsGpsScreen` exact | **PASS** | `Assets/Tests/EditMode/GpsGateTests.cs`, 6 tests. All five GPS ids false at `gpsEnabled:false` and true at `true`; seven non-GPS ids true in both; `IsGpsScreen` true for exactly the five. Reflection-based, matching `NavBackMemoryTests` (an asmdef cannot reference Assembly-CSharp). |
| 2 | EditMode full suite green (no filtered run) | **PASS** | Whole EditMode mode: **2234 tests, 2231 passed, 0 failed, 3 skipped** (the 3 are pre-existing `HoleCompleteDriverTests` skips with documented Stage-C1 reasons). **Proven that MY tests actually ran, not just that the suite is green:** armed a tripwire (`IsFalse` → `IsTrue` on the blocked-screens assert), re-ran, and the suite went red naming `GpsGateTests.GpsScreens_AreBlocked_WhenGpsDisabled` — `Expected: True, But was: False`. Reverted; green again. Filters are ignored by this runner, so the tripwire is the only honest proof. |
| 3 | Editor play mode: GPS unchanged (banner, tap→hub, five screens) | **PASS** | Play mode, real navigation. Home renders the `GOLFIN·GPS` banner (screenshot `screenshots/gps_on_home_banner_visible.png`, 1170×2532); `_link=golfin://gps`, `imgEnabled=True`, `interactable=True`. Tapping the banner's own Button reached **GpsHub**. All five reachable: `ShowScreen(GpsHub/ScoreUpload/GpsProfile/GpsAvatar/GpsBadges)` each landed on that screen, and `Home` still works. |
| 4 | Disabled-branch check + collapsed-banner screenshot | **PASS** | Temporarily flipped `#if GOLFIN_GPS \|\| UNITY_EDITOR` → `#if GOLFIN_GPS`, recompiled (`GpsGate.Enabled = False` confirmed by reflection), and ran play mode as a "punch it" build would. **All five GPS screens refused to navigate** — `ShowScreen(X)` left `_currentScreen` on `Home` for every one. Banner: `imgEnabled=False`, `raycastTarget=False`, `interactable=False`, `_link=null`, while `BannerService` still held the live row (client hides it; the row is NOT deactivated). **The slot collapses, measured not eyeballed:** `ModeCarouselSection` worldY 536→300 (bottom) and 2179→1943 (top) — **236 px reclaimed**, the banner's 214 px plus its 22 px gap. Screenshot `screenshots/gps_off_home_banner_hidden.png`. Const reverted and re-verified `Enabled = True`. |
| 5 | `iOS-Full.asset` diff EMPTY; GPS profile differs only by the define | **PASS** | `git status` on `iOS-Full.asset` → clean, byte-identical. `diff` of the two assets is exactly 2 hunks: `m_Name: iOS-Full` → `iOS-Full-GPS`, and `m_HasScriptingDefines: 0` / `m_ScriptingDefines: []` → `1` / `- GOLFIN_GPS`. Nothing else. Created via `AssetDatabase.CopyAsset` + `SerializedObject` in the Editor (not hand-written YAML), so GUID/importer data is Unity's own: `c026153b8654345139db9c30714e7717`. |
| 6 | `unity-build-ios.sh gps` shows `BuildIOSGps`, the profile, and the define assert | **PASS** | Real batchmode run, **exit 0 in 2 min 40 s** (11:49:28 → 11:52:08). Log carries all three lines: `method : Golfin.EditorTools.CIBuild.BuildIOSGps`, **`[CIBuild] GPS variant — GOLFIN_GPS defined on iOS-Full-GPS.`**, `[CIBuild] active build profile → iOS-Full-GPS`, then `SUCCEEDED → Builds/iOS-Full/Unity-iPhone.xcodeproj` with `CFBundleVersion=2567`. Also still true: `./Tools/unity-build-ios.sh bogus` → exit 2. |
| 7 | `fastlane ios testflight_build` still runs the unchanged path | **PASS (by construction + parse)** | `ruby -c fastlane/Fastfile` → Syntax OK. Both lanes call one `private_lane :testflight_build_shared`; the diff shows the ONLY conditional is the Unity step (`sh("../Tools/unity-build-ios.sh", "gps")` vs `sh("../Tools/unity-build-ios.sh")`). Every gate — clean-tree, unity-closed, content `--check`, `build_app`, `upload_to_testflight`, `mark-uploaded.sh` — is shared, so neither variant can drift from the other. Not executed (needs the Editor closed). |
| 8 | No new player-facing strings | **PASS** | Zero `.text` assignments and zero literals added: the banner is *hidden*, not replaced, and the gate logs to Console only (`[GpsGate] blocked {id}`, a developer log matching `[DemoGate]`). |
| 9 | Docs updated per §8 | **PASS** | `Docs/PUNCH_IT_ROUTINE.md`: phrase/command/profile table plus the "shipping BOTH variants" sequencing note (punch it → commit the guard → punch it GPS) and the on-device tell. `Docs/TESTFLIGHT_RUNBOOK.md` § One command: one paragraph pointing at the new lane. |
| 10 | Unity Console: no errors from this task | **PASS** | After `assets-refresh`, reflection confirms every touched type compiled and loaded: `GpsGate`, `ScreenManager`, `BannerSlotBinder`, `CIBuild.BuildIOSGps`, `GpsGateTests`. `isCompiling=False`, no `CS` errors. |
| 11 | Spec deviations flagged | **PASS** | Below. |

## ✅ The three blocked items were completed once the Editor was free (2026-09-02 11:40–11:52)

Cesar freed the Editor and all three ran: the play-mode GPS-on pass, the disabled-branch check
with both screenshots, and the real `unity-build-ios.sh gps` batchmode build. Details are in the
rows above. Two things worth recording from that session:

- **A capture trap, caught before it reached the report.** `GOLFIN/Screenshot/Capture Game View`
  writes to `Docs/Diagnostics/_capture/`, not `Assets/Screenshots/` — my first copy took the
  newest file from the wrong folder and produced TWO IDENTICAL PNGs (same md5) labelled "before"
  and "after". Caught by md5-comparing the pair instead of trusting the filenames; re-copied from
  the right folder, md5s now differ (`8e8e6616…` vs `33f9eef5…`) and each was re-opened to confirm
  it shows the state its name claims.
- **`Builds/iOS-Full` currently holds the GPS variant** (build 2567's Xcode project). Harmless —
  every lane run rebuilds it — but do not hand-archive that folder expecting a standard build.

## Original blocker note (kept for the record) — was blocked on exclusive Editor access

Cesar flagged at kickoff that another session is driving Unity for the GPS Profile screens, and its
work landed in the tree during this task (`ShellScene.unity`, `GpsHubScreenController.cs`, the
`S_PROF_*` art, localization). Three acceptance items need to *take over* the Editor and were
therefore not run:

1. **`./Tools/unity-build-ios.sh gps`** — batchmode requires the Editor CLOSED.
2. **Editor play-mode pass with GPS on** — needs play mode.
3. **The disabled-branch visual** — `[GpsGate] blocked GpsHub` in the Console and the Home banner
   slot collapsing, with the screenshot for `screenshots/`.

All three become a few minutes' work the moment the Editor is free. (2) and (3) also want a
temporary flip of `GpsGate.Enabled`, which would force a domain reload on a session mid-edit —
exactly the "stepping on it" Cesar warned against.

**One thing I did take over:** the EditMode suite, three times (~90 s each). The Editor was idle at
that moment — not playing, not compiling, no prefab stage, ShellScene not dirty — so it was the
cheapest possible window. Fallout worth naming: one run showed
`RealHoleTerrainTests.AllImportedHoles_Smoke_TeeShot_DoesNotFallThrough` red for `Hole_05` and
`Hole_13`. It did not reproduce on the next run, no terrain or hole file is modified in the tree,
and nothing in this task goes near terrain, physics or hole data — so I read it as a flake under
concurrent asset importing, not as a regression. Flagging rather than burying it.

## Deviations from the spec

| Deviation | Why |
|---|---|
| `AssertGpsDefine` reads `m_ScriptingDefines` via `SerializedObject`, not a typed property | The spec asked me to flag which. `BuildProfile.scriptingDefines` is not public API in 6000.3; `m_ScriptingDefines` is what the asset stores and what `iOS-Demo.asset` carries `GOLFIN_DEMO` in. |
| Fastfile uses a `private_lane` taking an options hash | Spec allowed "shared private lane (or method)". A private lane keeps both public lanes one line each and keeps fastlane's own step logging intact. |
| `BannerSlotBinder` fully-qualifies `GolfinRedux.UI.ScreenId` instead of adding a `using` | Line 349 of that same file already does exactly this. Matching the file beat adding an import for one call site. |
| `unity-build-ios.sh` rejects an unknown argument with exit 2 | Not requested. A typo'd variant would otherwise silently build the WRONG one — the same class of mistake the define assert exists to catch, one layer earlier. |
| One extra test beyond the spec's list: `EveryGpsNamedScreenId_IsOnTheGpsList` | The deny-list's hazard is that a new GPS screen ships *reachable* in "punch it" builds and nothing complains. The spec put that in a comment; this makes it fail a test instead. |

## Files modified or created

| File | Change |
|---|---|
| `Assets/Scripts/UI/Gps/GpsGate.cs` | **NEW** — `Enabled` const (`GOLFIN_GPS \|\| UNITY_EDITOR`), the five-screen deny-list, `IsGpsScreen`, `IsScreenAllowed` + the testable two-arg overload. |
| `Assets/Tests/EditMode/GpsGateTests.cs` | **NEW** — 6 EditMode tests, reflection-based. |
| `Assets/Scripts/UI/ScreenManager.cs` | Gate in `Navigate` under the DemoGate check; back-stack skip; `isGpsScreen` now reads `GpsGate.IsGpsScreen` so the chrome rule and the deny-list are one list. |
| `Assets/Scripts/BannersRuntime/BannerSlotBinder.cs` | `Apply()` hides the slot when the banner's internal route targets a gated screen. Generic over any internal route; `BannerPolicy` untouched. |
| `Assets/Settings/Build Profiles/iOS-Full-GPS.asset` | **NEW** — `iOS-Full` + `GOLFIN_GPS`, nothing else. |
| `Assets/Editor/CIBuild.cs` | `BuildIOSGps()` mirroring `BuildIOSDev()`, plus `AssertGpsDefine()` which refuses to build a "GPS" variant whose profile lost the define. |
| `Tools/unity-build-ios.sh` | Optional `gps` argument selecting `BuildIOSGps`; unknown args rejected. |
| `fastlane/Fastfile` | Shared `testflight_build_shared` private lane; `testflight_build` (gps:false) and new `testflight_build_gps` (gps:true). |
| `Docs/PUNCH_IT_ROUTINE.md` | Variant table + the both-variants sequencing rule. |
| `Docs/TESTFLIGHT_RUNBOOK.md` | One paragraph in § One command. |
| `Assets/Settings/Build Profiles/iOS-Full.asset` | **UNTOUCHED** — verified clean in git, as §4 requires. |

## Needs Cesar

1. ~~A free Editor~~ — **DONE**, all three items completed.
2. **The device pass** — both variants on TestFlight, which is your punch-it runs, in order:
   `punch it` → commit the guard file → `punch it GPS`. The tell on device is the Home banner:
   absent = standard, present = GPS.
