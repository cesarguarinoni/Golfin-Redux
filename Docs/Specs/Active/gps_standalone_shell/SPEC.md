# SPEC — `gps_standalone_shell`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`.

## Goal

**PLAYLIFE as a Unity thin-shell** — the standalone GPS app, built from THIS project, one codebase (Cesar's decision 2026-09-02: "We go Unity so we don't have to maintain 2 codebases"). Scope = PLAYLIFE features only (decision 2026-09-01): auth, Golf Profile + Welcome, GPS hub, Rounds/check-in, Score Upload, Gifts, Votes, Profile/Avatar/Badges, Settings. **No golf gameplay, no shop, no gacha, no tournaments, no missions.** A third TestFlight variant beside "Punch it" / "Punch it GPS": **"punch it standalone"** — its own bundle id, name, icon, scene list and boot path, produced by the same Fastfile shape.

Nothing about the GPS screens changes. This task is a build profile, a boot path, a gate, and the chrome that has to disappear.

## Reference

- **Build variants today:** `fastlane/Fastfile` (`testflight_build` → `iOS-Full`, `testflight_build_gps` → `iOS-Full-GPS`, both through `testflight_build_shared(gps:)`; `ensure_git_status_clean`, `assert-unity-closed.sh`, content-freshness gate, build number = commit count), `Tools/testflight.sh`, `Assets/Settings/Build Profiles/iOS-Full-GPS.asset` (defines `GOLFIN_GPS`, `m_OverrideGlobalSceneList: 0`), `GpsGate` (`#if GOLFIN_GPS || UNITY_EDITOR` → `Enabled`), `Docs/Specs/Completed/punch_it_gps_variants/` (how the two-variant lane was built and proven).
- **Boot path today:** `SplashScreenController` (Login → `CreateUsername` if no display name → `StarterGate.Resolve` → `StartingCharacterSelection` or `Home`), `AuthGate` (allowlist of pre-auth screens), `GpsAuthExtrasFlow.InterceptHubEntry` (the first-GPS-entry intercept, written with "the standalone shell's boot" in mind), `ScreenManager.Navigate` / `GoBack(fallback)` / `PillarOf`, `PersistentUIManager` (top bar: RP pill, ticket cluster, Settings gear, username; bottom nav pillars Home/Gacha/MainPlay/Inventory/Characters/Settings), `GpsNavBarBinder` (the GPS screens carry their own nav bar), `GpsHubScreenController` (`BackPill` → `GoBack(Home)`), `GpsWelcomeScreenController` (Skip → Home), `InGameSettingsModalController`, `BannerPolicy.TryGetInternalRoute("golfin://gps")`.
- **Identity:** game bundle id `com.nextinnovation.golfingame`, `productName: Golfin`, `bundleVersion 1.5.7`; the Flutter PLAYLIFE app is `com.wonderwall.playlife` (`playlife/ios/Runner.xcodeproj`).
- **Scenes in the build today:** ShellScene + LabScaffold + 18 `Hole_NN_Geo` scenes (the 3.3 GB `Builds/iOS-Full` footprint is mostly those).

## Decisions (D1 needs Cesar's word before the archive; the rest are baked in)

- **D1 · Bundle id and App Store record — DECIDED and READ FROM ASC (Architect, 2026-09-03).** The standalone uploads to the existing App Store Connect app **"GOLFIN GPS"**: Bundle ID **`com.nextinnovation.golfingps`**, SKU `com.nextinnovation.golfingps.sku`, Apple ID **`6737145432`**, same team as the game (`TCUV4A9VTJ`, NEXT INNOVATION — the `Appfile` team and ASC API key already cover it, so no new signing identity). Its last TestFlight build is **version 0.7.6, build 12** (expired; internal + external tester groups exist). Therefore the standalone lane sets `app_identifier "com.nextinnovation.golfingps"`, `bundleVersion` **1.0.0** for this variant (must exceed 0.7.6; the game keeps 1.5.7), and the commit-count build number (~2 600) already exceeds 12. The Flutter app's `com.wonderwall.playlife` is NOT this record — do not use it. Display name in the shell: "GOLFIN GPS" (the ASC app name; "PLAYLIFE" would need an app-name change on a new version). URL scheme stays `playlife://`? — no: use **`golfingps://`** to match the record; `BannerPolicy` accepts `golfin://`, `golfingps://`.
- **D2 · One project, one scene — AND no HoleData.** `iOS-Standalone` build profile: clone of `iOS-Full-GPS`, defines `GOLFIN_GPS;GOLFIN_STANDALONE`, `m_OverrideGlobalSceneList: 1` with `m_Scenes = [ShellScene]` only. No hole scenes, no LabScaffold. **Everything under `Assets/Resources/` ships regardless of scene usage** — the 2026-09-03 standalone build log shows all 18 `HoleData/*/heightmap.bytes` (16.8 MB each, 2049² float32) + `zones.json` (2–10 MB each) inside the standalone .ipa (427 MB, `resources.assets` 390 MB). The `StandaloneBuildPreprocessor` therefore MOVES `Assets/Resources/HoleData` (and any other golf-only Resources subfolder it enumerates: `Clubs`, `Balls`, `Bags`, `Characters` art that the GPS surface never loads — verify each with a Resources.Load grep) out of the Resources tree for the duration of the build and RESTORES it after (try/finally + a sentinel file so an aborted build can be repaired). Target: standalone .ipa ≤ 150 MB; report the number and the per-category Build Report. Code is not refactored out — IL2CPP stripping + the missing scenes/resources do the size work.
- **D3 · Boot lands on the hub.** Standalone: Splash → Login/SignUp/CreateUsername as today → **skip `StarterGate` and `StartingCharacterSelection` entirely** → `InterceptHubEntry(GpsHub)` (Golf Profile → Welcome → hub on first run, hub after) . The starter character still exists server-side for accounts that also play the game; the shell never asks.
- **D4 · Home does not exist in the shell.** A `StandaloneGate` (mirror of `GpsGate`/`DemoGate`: allowlist = `AuthGate`'s pre-auth screens + every `GpsGate.GpsScreens` entry) refuses everything else; `ShowScreen(Home)` and `GoBack(Home)` are REWRITTEN to `GpsHub` inside `Navigate` (not refused — Welcome Skip, the hub BackPill, nav-back fallbacks all have a sane landing). The hub's `BackPill` is hidden in the shell. `PillarOf(GpsHub)` etc. stay null.
- **D5 · Chrome.** Game bottom nav bar hidden (GPS screens bring their own); top bar keeps the RP pill, username and Settings gear, hides the ticket cluster + ShopPlusButton; `NavTitleKeyFor` unchanged. Settings modal in the shell shows Account (display name, logout), Language, and the legal links only — Graphics tier and gameplay sections hidden (`InGameSettingsModalController` gets a `standalone` layout flag, no second prefab).
- **D6 · Identity strings.** `productName` "GOLFIN GPS", display name "GOLFIN GPS" (the ASC record's name), URL scheme `golfingps://` (registered beside `golfin://` in `iOSURLSchemes`; `BannerPolicy.TryGetInternalRoute` accepts both), **app icon = `Assets/Art/Standalone/AppIcon_GolfinGps_1024_opaque.png`** (Cesar-supplied 2026-09-03: green gradient, white pin with a golf ball + tee; the `_opaque` file has the transparent corners filled because App Store Connect rejects alpha in the 1024 marketing icon — iOS masks the corners itself; generate the full iOS icon set from it in the build preprocessor or the Player Settings override); launch image = the GPS Backgrounds "Splash" variant (placeholder until Ken supplies one — backlog row), `client_platform` reports `ios-playlife` (`IClientPlatformProbe` + telemetry `app_variant` property) so the backend/dashboard can split the two apps.
- **D7 · Content.** `ContentService` runs as in the game (texts catalog needed; other catalogs harmless). `TICKET_SHOP_BUILD`/gacha/tournament services must not throw when their screens are absent — they already lazy-init; prove it in the boot log (no `NullReference`, no `MissingComponent`).
- **D8 · Fastlane + the phrase.** Cesar's operating agreement gains a third row in `Docs/PUNCH_IT_ROUTINE.md`: **"punch it standalone"** → `./Tools/testflight.sh testflight_build_standalone` → profile `iOS-Standalone` → uploads to the GOLFIN GPS record (Apple ID 6737145432). Same preflight, same guard file logic (its own `last_uploaded_build` entry per app record, so a standalone build never blocks a game build of the same commit — the ASC uniqueness rule is per app), same "confirm at Apple" step. The routine table's "tell on device" column: the app icon/name (GOLFIN GPS vs Golfin). `lane :testflight_build_standalone` → `testflight_build_shared(variant: :standalone)`; the shared lane takes a `variant` symbol (`:standard | :gps | :standalone`) instead of the boolean, selects profile + `app_identifier` + scheme product name; `Tools/testflight.sh testflight_build_standalone` = "punch it standalone". Build number stays the commit count (different app record → no ASC collision). Content-freshness gate unchanged.

## Implementation

1. `Assets/Settings/Build Profiles/iOS-Standalone.asset` (D2) + `ProjectSettings` per-profile overrides for bundle id / product name / icons where the profile supports them (otherwise a `StandaloneBuildPreprocessor` `IPreprocessBuildWithReport` sets `PlayerSettings.applicationIdentifier`, `productName`, icons, URL schemes when `GOLFIN_STANDALONE` is defined — and RESTORES them after, like `BuildStampGenerator` cleans up).
2. `StandaloneGate` (`Assets/Scripts/UI/StandaloneGate.cs`): `#if GOLFIN_STANDALONE` → `Enabled = true`; `IsScreenAllowed(id)`, `Rewrite(id)` (Home→GpsHub); two-arg testable overloads; wired as the fourth gate in `ScreenManager.Navigate` after `GpsGate`, before `AuthGate`; `GoBack` fallback rewrite.
3. `SplashScreenController` (D3): `if (StandaloneGate.Enabled) { Show(GpsAuthExtrasFlow.InterceptHubEntry(ScreenId.GpsHub)); return; }` in place of the StarterGate branch.
4. `PersistentUIManager` (D5): `ApplyStandaloneChrome()` on Start — nav bar root inactive, ticket cluster inactive, plus button inactive; `GpsHubScreenController` hides `BackPill`; `InGameSettingsModalController` standalone layout.
5. Identity (D6): URL scheme, `IClientPlatformProbe`, telemetry `app_variant`, icon/launch bakes + import settings.
6. Fastlane + `Tools/testflight.sh` (D8); `Docs/TESTFLIGHT_RUNBOOK.md` gains the third variant; `Docs/PUNCH_IT_ROUTINE.md` updated.
7. EditMode: `StandaloneGateTests` (allowlist table, Home rewrite, gate-off no-op), `GpsAuthExtrasFlowTests` gain the shell boot case, `BannerPolicyTests` for `golfingps://gps`.
8. Editor proof: activate the `iOS-Standalone` profile in the Editor (its defines apply) → play ShellScene → boot log shows Splash → Login → hub with no Home, no nav bar, no ticket pill; every GPS screen reachable; Settings modal in shell layout; `ShowScreen(Home)` log shows the rewrite. Then deactivate the profile and prove the GAME boot is untouched (Home, nav bar, tickets back).
9. Archive: `./Tools/testflight.sh testflight_build_standalone` → TestFlight (Cesar's D1 answer + the ASC record/provisioning are pre-reqs; if D1 is unresolved, stop at a signed local .ipa and quote its size).

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Profile diff vs `iOS-Full-GPS` quoted (defines, scene list); `EditorBuildSettings` untouched.
- [ ] Editor boot with the profile active: log quoted for Splash → Login → (CreateUsername) → hub; `StarterGate` never called (grep the log); Golf Profile intercept fires once on a cleared `gps_profile_prompted`.
- [ ] `StandaloneGate` table pinned (EditMode): every `ScreenId` → allowed / rewritten / refused; gate-off is a no-op; Home→GpsHub rewrite proven by a real `ShowScreen(Home)` in play mode.
- [ ] Chrome: screenshots of hub / Rounds / Profile / Settings modal in the shell — no game nav bar, no ticket cluster, RP + gear + username present; hub BackPill absent. Rest-state pixel parity of each GPS screen's `ContentContainer` vs the game build (only chrome differs — quote the diff mask).
- [ ] Game boot unchanged with the profile inactive (screenshots + the same log lines).
- [ ] `golfingps://gps` routes; `client_platform == "ios-playlife"` in a real `POST /activity/checkin` (quote the request); telemetry `app_variant` present.
- [ ] Boot log clean of exceptions with the golf services idle (grep `Exception|Missing`).
- [ ] Fastlane: lane compiles (`fastlane lanes`), `Tools/testflight.sh testflight_build_standalone` reaches the archive; .ipa size vs the GPS variant quoted; TestFlight upload id quoted OR the D1 blocker named.
- [ ] Both other lanes still build (the shared-lane refactor is the risk) — `testflight_build_gps` archive proven once more.
- [ ] Importer: no new player strings expected (PLAN `add 0`); if the Settings modal needs one, Build rule 7.
- [ ] Full EditMode sweep green, new suites executed by name.
- [ ] Deviations flagged; device-pass rows for the shell added to `Docs/GPS/GPS_DEVICE_PASS.md` §7 (install beside the game, boot to hub, full loop, deep link `golfingps://gps` from Safari).

## Files / hierarchy this task touches

`Assets/Settings/Build Profiles/iOS-Standalone.asset`, `Assets/Editor/Build/StandaloneBuildPreprocessor.cs`, `Assets/Scripts/UI/StandaloneGate.cs` (+ tests), `ScreenManager.cs`, `SplashScreenController.cs`, `PersistentUIManager.cs`, `GpsHubScreenController.cs`, `InGameSettingsModalController.cs`, `BannerPolicy.cs`, `GpsTrustSignals.cs` (platform probe), telemetry client, `ProjectSettings.asset` (URL scheme), `Assets/Art/Standalone/*` + `Docs/Scripts/make_standalone_icon.py`, `fastlane/Fastfile`, `Tools/testflight.sh`, `Docs/TESTFLIGHT_RUNBOOK.md`, `Docs/PUNCH_IT_ROUTINE.md`.

## Out of scope (do NOT do these)

- Removing golf code from the project or splitting assemblies (stripping does the size work).
- Android PlayLife profile (backlog with the other Android rows).
- Real PLAYLIFE branding (icon/launch/wordmark from Ken — placeholder bakes only, backlog row).
- App Store metadata, screenshots, review submission.
- Any change to GPS screen prefabs or behaviour.
- Push notifications, background location, Sign in with Apple beyond what the auth screens already do.
