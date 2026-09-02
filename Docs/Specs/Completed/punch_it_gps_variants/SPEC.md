# SPEC — `punch_it_gps_variants`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. (Standard pipeline states — SPEC_READY → IMPLEMENTER_WORKING → … → DONE.)

## Goal

Two TestFlight variants from one codebase, on two trigger phrases:

- **"Punch it"** → the existing lane, unchanged path (`./Tools/testflight.sh`) → a build **without** the GPS/PLAYLIFE surface: the five GPS screens are unreachable and the Home promo banner (whose live row links `golfin://gps`) is **hidden entirely** — slot collapsed, not shown-but-dead. The server banner row stays LIVE and untouched.
- **"Punch it GPS"** → `./Tools/testflight.sh testflight_build_gps` → the same build **with** GPS: banner shows, tap routes to the hub, all GPS screens reachable.

Mechanism mirrors the demo builds exactly: a scripting define (**`GOLFIN_GPS`**) carried by a build profile, read by a compile-time gate class (**`GpsGate`**, modeled on `DemoGate`). No content stripping, no asmdef `defineConstraints` — GPS code compiles in both variants and in the Editor always (Cesar is actively developing GPS; constraint-stripping would grey it out of the Editor whenever the active profile lacks the define). The variant only decides *reachability*.

## Reference

- No Figma work — this is build-pipeline + gating. Figma Fidelity table: **N/A**.
- Precedents to copy, not reinvent:
  - `Assets/Scripts/Demo/DemoGate.cs` — define-driven const + `IsScreenAllowed(ScreenId)`.
  - `Assets/Settings/Build Profiles/iOS-Demo.asset` — `m_ScriptingDefines: [GOLFIN_DEMO, GOLFIN_TESTBUILD]` shows profiles carry defines in this project.
  - `Assets/Editor/CIBuild.cs` — `BuildIOS()` / `BuildIOSDev()` pair shows how a second entry point wraps `BuildIOSCore(profilePath, outputPath, options)`.
  - `fastlane/Fastfile` lane `testflight_build`; `Tools/testflight.sh` already forwards `$1` as the lane name (`"${1:-testflight_build}"`) — **zero change to testflight.sh**.

## Architecture context

- **Asmdef boundaries affected:** none. `Golfin.Gps` / `Golfin.Social` asmdefs are NOT touched (no `defineConstraints` — see Goal). All edits are Assembly-CSharp (`ScreenManager`, `BannerSlotBinder`), one new Assembly-CSharp file (`GpsGate.cs`), one Editor file (`CIBuild.cs`), one profile asset, the Fastfile, and two docs.
- **Existing code referenced:**
  - `Assets/Scripts/UI/ScreenManager.cs` — DemoGate check in `ShowScreen` at :207; back-stack skip at :354; the `isGpsScreen` five-way OR at :591-592.
  - `Assets/Scripts/BannersRuntime/BannerSlotBinder.cs` — `Apply()` (~:127) resolves the placement; `Hide()` (~:170) collapses the slot ("no live banner = no slot"); `OpenLink()` (~:327).
  - `Assets/Scripts/BannersRuntime/BannerPolicy.cs` — `TryGetInternalRoute` (:118) maps `golfin://gps` → `ScreenId.GpsHub`. **Do not gate inside BannerPolicy**: a refused link leaves the strip visible-but-dead (the documented old-build behavior), which is exactly what Cesar does NOT want here.
  - `Assets/Editor/CIBuild.cs`, `Tools/unity-build-ios.sh`, `fastlane/Fastfile`, `Docs/PUNCH_IT_ROUTINE.md`, `Docs/TESTFLIGHT_RUNBOOK.md`.
- **Manager APIs used:** `ScreenManager.Instance.ShowScreen(ScreenId)`; `BuildProfile.SetActiveBuildProfile` / `BuildPipeline.BuildPlayer(BuildPlayerWithProfileOptions)` (already in CIBuild).

## Implementation

### 1. `Assets/Scripts/UI/Gps/GpsGate.cs` — new (Assembly-CSharp, namespace `Golfin.Gps.UI`)

Modeled on `DemoGate`, inverted (GPS is IN by default in the Editor, OUT of a player build unless defined):

```csharp
public static class GpsGate
{
#if GOLFIN_GPS || UNITY_EDITOR
    public const bool Enabled = true;   // Editor: always on — daily GPS dev must not depend on the active profile.
#else
    public const bool Enabled = false;  // Player build without the define: GPS surface unreachable.
#endif

    // THE single list of GPS-surface screens. Deny-list (unlike DemoGate's allowlist): a GPS
    // screen added later MUST be added here or it ships reachable in "Punch it" builds — call
    // that out in the comment. ScreenManager reuses this via IsGpsScreen (see §2c) so the two
    // lists cannot drift.
    static readonly HashSet<ScreenId> GpsScreens = new()
        { ScreenId.GpsHub, ScreenId.ScoreUpload, ScreenId.GpsProfile, ScreenId.GpsAvatar, ScreenId.GpsBadges };

    public static bool IsGpsScreen(ScreenId id) => GpsScreens.Contains(id);
    public static bool IsScreenAllowed(ScreenId id) => IsScreenAllowed(id, Enabled);
    internal static bool IsScreenAllowed(ScreenId id, bool gpsEnabled) => gpsEnabled || !GpsScreens.Contains(id);
}
```

(`ScreenId` lives in `GolfinRedux.UI` — add the using. The two-arg overload exists so EditMode tests can exercise the disabled branch, which the Editor const otherwise makes unreachable.)

### 2. Gate the three reachability points

a. **`ScreenManager.ShowScreen`** — directly under the DemoGate check at :207, same shape:
```csharp
if (!Golfin.Gps.UI.GpsGate.IsScreenAllowed(screenId))
{
    Debug.Log($"[GpsGate] blocked {screenId}");
    return;
}
```
b. **Back-stack skip** at :354 — add alongside the DemoGate skip: `if (!Golfin.Gps.UI.GpsGate.IsScreenAllowed(candidate)) continue;`
c. **`isGpsScreen` at :591-592** — replace the five-way OR with `Golfin.Gps.UI.GpsGate.IsGpsScreen(screenId)` so the deny-list and the top-bar rule share one list.

### 3. Hide the Home banner in non-GPS builds — `BannerSlotBinder.Apply()`

After the `service.TryGet(_placement, out banner)` succeeds and before the art request:

```csharp
// punch_it_gps_variants — a banner that routes INTO the GPS surface has no business on
// screen in a build where that surface is gated off. Hide (slot collapses) rather than
// show-dead; the server row stays LIVE for GPS builds.
if (BannerPolicy.TryGetInternalRoute(banner.LinkUrl, out var routeScreen)
    && !Golfin.Gps.UI.GpsGate.IsScreenAllowed(routeScreen))
{
    Hide();
    return;
}
```

Generic over any future internal route, not hardcoded to `golfin://gps`. No change to `BannerPolicy` and no change to `OpenLink()` (its ShowScreen call now hits the §2a gate anyway — harmless double cover).

### 4. `Assets/Settings/Build Profiles/iOS-Full-GPS.asset` — new profile

Duplicate of `iOS-Full.asset` with exactly one difference: `m_ScriptingDefines: [GOLFIN_GPS]`. Create it in the Editor (duplicate the asset, add the define in the profile's Script Compilation overrides) so the GUID/importer data is sane — don't hand-copy YAML blind. `iOS-Full` itself is untouched; "Punch it" builds are byte-for-byte the lane that exists today.

### 5. `Assets/Editor/CIBuild.cs` — `BuildIOSGps()`

Mirror the `BuildIOS()` / `BuildIOSDev()` pattern exactly (same PlayerSettings snapshot/restore, same try/catch → `RestoreBuildNumbers` → `Fail`):

```csharp
const string GpsProfilePath = "Assets/Settings/Build Profiles/iOS-Full-GPS.asset";
public static void BuildIOSGps()  // → BuildIOSCore(GpsProfilePath, OutputPath, BuildOptions.None)
```

- **Same `OutputPath` (`Builds/iOS-Full`)** — the Fastfile's `build_app` path stays single; whichever variant Unity just built is what gets archived, exactly as today.
- `BuildOptions.None`, NOT Development — the upload-regression guard must stay armed for GPS uploads too.
- **Assert the define landed**: after loading the profile, verify its scripting defines contain `GOLFIN_GPS` and log `[CIBuild] GPS variant — GOLFIN_GPS defined`; fail the build if absent (a silently-undefined GPS build is the stale-binary class of bug). NOTE: use `BuildProfile.scriptingDefines` if the Unity 6 API exposes it; if not public in this Editor version, read the asset via `SerializedObject`/`FindProperty("m_ScriptingDefines")` — flag whichever you used in the report.

### 6. `Tools/unity-build-ios.sh` — optional `gps` argument

`./Tools/unity-build-ios.sh gps` → `METHOD="Golfin.EditorTools.CIBuild.BuildIOSGps"`; no arg → unchanged. Echo the chosen method (already does).

### 7. `fastlane/Fastfile` — `testflight_build_gps` lane

Extract the existing lane body into a shared private lane (or method) taking a `gps:` flag; `testflight_build` calls it with `gps:false` (behavior identical to today), new `testflight_build_gps` with `gps:true`, which changes ONLY the Unity step: `sh("../Tools/unity-build-ios.sh gps")`. Everything else — clean-tree gate, unity-closed assert, content `--check`, build_app, upload, `mark-uploaded.sh` — identical and shared. Invocation: `./Tools/testflight.sh testflight_build_gps` (testflight.sh already forwards `$1`).

### 8. Docs — `Docs/PUNCH_IT_ROUTINE.md` + `Docs/TESTFLIGHT_RUNBOOK.md`

Add to PUNCH_IT_ROUTINE.md a short "Punch it GPS" section:

- Phrase table: **"punch it"** → `./Tools/testflight.sh` → `iOS-Full`, no `GOLFIN_GPS`: GPS screens blocked by `GpsGate`, Home GPS banner hidden. **"punch it GPS"** → `./Tools/testflight.sh testflight_build_gps` → `iOS-Full-GPS`, `GOLFIN_GPS` defined: full GPS surface.
- **Uploading BOTH variants of the same code** (the point of this task): build number = commit count, and App Store Connect requires it unique — so the two runs are sequential, with the guard-file commit between them: punch it → `mark-uploaded.sh` dirties `Docs/Versioning/last_uploaded_build.txt` → commit it (ordinary sweep rules) → punch it GPS lands as the next build number. The upload guard enforces this order anyway; the doc just names it. Record which number is which variant in the report; on-device the tell is the Home banner.
- One line in TESTFLIGHT_RUNBOOK.md § One command pointing at the new lane.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] EditMode: new `GpsGate` tests — `IsScreenAllowed(gpsScreen, false)` = false for all five, `IsScreenAllowed(gpsScreen, true)` = true, non-GPS screens always true; `IsGpsScreen` matches exactly the five ids.
- [ ] EditMode: full suite green (no filtered runs — sweep per assembly).
- [ ] Editor play mode: GPS unchanged — Home banner shows, tap → GpsHub, all five screens reachable (Editor const keeps `Enabled` true regardless of active profile).
- [ ] Disabled-branch check in Editor (temporary hack acceptable, e.g. forcing the two-arg overload via a test or a temporary const flip — revert it): `ShowScreen(GpsHub)` logs `[GpsGate] blocked GpsHub` and does not navigate; Home banner slot is hidden AND collapsed (content below moves up) while `BannerService` still holds the live `golfin://gps` row.
- [ ] `iOS-Full.asset` diff is EMPTY (git). `iOS-Full-GPS.asset` differs from it only by the define (+ GUID/name).
- [ ] `./Tools/unity-build-ios.sh gps` log shows `BuildIOSGps`, profile `iOS-Full-GPS`, and the `[CIBuild] GPS variant — GOLFIN_GPS defined` assert line.
- [ ] `fastlane ios testflight_build` still runs the unchanged path (lane diff reviewed: shared body, `gps:false`).
- [ ] No new player-facing strings (none expected — the banner is hidden, not replaced; nothing else surfaces text). Zero new hardcoded `.text` literals.
- [ ] `Docs/PUNCH_IT_ROUTINE.md` + `Docs/TESTFLIGHT_RUNBOOK.md` updated as §8.
- [ ] Unity Console: no errors from this task.
- [ ] Spec deviations flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

- `Assets/Scripts/UI/Gps/GpsGate.cs` — NEW gate class + its EditMode test file (place beside existing Assembly-CSharp UI tests).
- `Assets/Scripts/UI/ScreenManager.cs` — gate in ShowScreen (:207 area), back-stack skip (:354 area), `IsGpsScreen` reuse (:591).
- `Assets/Scripts/BannersRuntime/BannerSlotBinder.cs` — internal-route gate in `Apply()`.
- `Assets/Settings/Build Profiles/iOS-Full-GPS.asset` — NEW.
- `Assets/Editor/CIBuild.cs` — `BuildIOSGps()` + define assert.
- `Tools/unity-build-ios.sh` — optional `gps` arg.
- `fastlane/Fastfile` — shared body + `testflight_build_gps` lane.
- `Docs/PUNCH_IT_ROUTINE.md`, `Docs/TESTFLIGHT_RUNBOOK.md` — §8.

## Smoke evidence

EditMode tests for the gate logic; Editor play-mode pass for the enabled path; the temporary disabled-branch check for the blocked path + banner collapse (screenshot of Home with the slot collapsed into `screenshots/`); the `unity-build-ios.sh gps` batchmode log excerpt quoted in the report. A full device/TestFlight pass of both variants is **Cesar's punch-it runs**, not the implementer's.

## Out of scope (do NOT do these)

- No asmdef `defineConstraints`, no scene stripping, no `DemoSceneProcessor`-style processor — GPS code ships in both variants, only reachability changes.
- No Android profiles/lanes (iOS first; the gate itself is platform-agnostic, so Android later = one cloned profile).
- No server/banner-row changes — the `home_promo` row stays LIVE exactly as activated.
- No BannerPolicy changes, no new ScreenIds, no changes to what "punch it" commits or asks (PUNCH_IT_ROUTINE operating agreement unchanged beyond the added section).
- No in-app variant badge/watermark (the Home banner presence IS the tell; revisit only if Cesar asks).
