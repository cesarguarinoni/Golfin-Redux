# IMPLEMENTER_REPORT — `gps_standalone_shell`

**Iteration shape:** `build-variant:new-shell-gate`
**Canonical screenshot:** `screenshots/shell_hub.png` (1170×2532, the PLAYLIFE shell booted to the hub through the real Splash → StartButton path)

Built directly by Claude Code on Cesar's instruction ("read the SPEC and implement it"), not through the
subagent chain. The pipeline's gates are honoured here in report form: baseline in `HEARTBEAT.log`,
per-item PASS/FAIL with cited evidence, real-entry proof, and every uncommitted path outside this folder
accounted for below.

---

## Baseline (Rule 1)

`HEARTBEAT.log` carries the `=== iter-1 kickoff baseline ===` block: HEAD `022e62ebe` (`git rev-list --count HEAD` = 2632),
dirty tree at kickoff = `Docs/Reports/content_art.txt`, `Docs/TellCode.md`, `Docs/Versioning/last_uploaded_build.txt`
(three pre-existing doc/guard files, untouched by this task).

---

## Acceptance checklist

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | Profile diff vs `iOS-Full-GPS` quoted; `EditorBuildSettings` untouched | **PASS** | `diff` of the two `.asset` files (name normalised) is **exactly three things**: `m_OverrideGlobalSceneList: 0 → 1`, `m_Scenes: [] → [ShellScene.unity]`, and `+ GOLFIN_STANDALONE` under `m_ScriptingDefines` (plus the RID of the cloned `iOSPlatformSettings` block, whose contents are identical field-for-field). `git status --porcelain ProjectSettings/EditorBuildSettings.asset` is empty — the global list still holds **21** scenes (ShellScene + LabScaffold + 18 `Hole_NN_Geo`, ~543 MB of Generated/); the profile's list holds **1**. |
| 2 | Editor boot with the profile active; `StarterGate` never called; Golf Profile intercept fires once on a cleared flag | **PASS** | Profile `iOS-Standalone` activated AND `GOLFIN_STANDALONE` added to the global iPhone defines (build-profile defines never reach editor assemblies — `reference_build_profile_defines_not_in_editor`). Live gate read: `[GateCheck] StandaloneGate.Enabled=True GpsGate.Enabled=True AppVariantInfo.Current=ios-playlife`. Boot log, verbatim: `[ScreenManager] ShowScreen called: Splash (current: Logo, instant: True)` → `[DevAutoSignIn] Tapping Splash StartButton → Home.` → `[StandaloneShellBoot] PLAYLIFE shell — StarterGate skipped, routing to GpsHub.` → `[ScreenManager] ShowScreen called: GpsHub (current: Splash, instant: False)`. **Zero `StarterGate` lines** in that boot (`grep` over the log slice returns nothing; the game boot in row 5 *does* show `StarterGate:Resolve`, which is what makes the absence meaningful). With `gps_profile_prompted` cleared: `[StandaloneShellBoot] … routing to GpsGolfProfile` → screen `GpsGolfProfile` (`screenshots/shell_firstrun_golf_profile.png`); the flag was restored afterwards. |
| 3 | `StandaloneGate` table pinned (EditMode); gate-off is a no-op; Home→GpsHub proven by a real `ShowScreen(Home)` in play mode | **PASS** | `StandaloneGateTests` (9 tests) walks **every `ScreenId`** in both build states, not a sample: `GateOff_IsANoOp_ForEveryScreen`, `EveryScreenIsEitherShellOrRefused_NoThirdState`, `EveryGpsScreen_IsReachableInTheShell` (read from `GpsGate.IsGpsScreen`, so a GPS screen added later is covered without editing the test), `ShellPreAuthList_MatchesAuthGatesPreAuthList`, `Enabled_IsFalseInTheEditor`. Live play mode: `[StandaloneGate] rewrote Home -> GpsHub (the shell has no Home).` then `current=GpsHub`; `[StandaloneGate] blocked Roster` and `blocked GeneralShop` with `current=GpsHub` unchanged; `GoBack()` and `GoBack(Home)` on the hub root both return **False** (never a quit, never a blank). |
| 4 | Chrome: hub / Rounds / Profile / Settings screenshots; no game nav bar, no ticket cluster; RP + gear + username present; hub BackPill absent | **PASS** | Four full-res captures (all 1170×2532, real navigation via the GPS nav bar's own `onClick`, never a harness): `shell_hub.png`, `shell_rounds.png`, `shell_profile.png`, `shell_settings_modal.png`. Measured, not eyeballed — `[WhereAmI] screen=GpsHub … bottomNav=False topBarContent=True rpText=True gear=True username=True ticketText=False shopPlus=False` (identical readout on Rounds and Profile). BackPill: `[BackPill] BackPill activeInHierarchy=False activeSelf=False listeners=0`. Settings: `[OpenSettings] open=True | userProfile UserProfileRow=True | sound SoundSettingsRow=False | graphics GraphicsRow=False | language LanguageRow=True | about AboutRow=True | terms=True | logout=True`. **Note on "username":** the slot is present and active; on a GPS screen it carries the screen title ("GOLFIN GPS", "ROUNDS", "PROFILE") because `NavTitleKeyFor` is unchanged per §D5 — the same behaviour the GPS variant already ships, not a regression. |
| 5 | Game boot unchanged with the profile inactive | **PASS** | Defines and active profile restored to the exact prior values (`[DisableStandalone] iPhone defines = 'UNITY_MCP_READY;UNITY_MCP_DEPS_3' activeProfile=iOS-Full-GPS`), then a fresh boot: `[GateCheck] StandaloneGate.Enabled=False … AppVariantInfo.Current=game-gps`, `Rewrite(Home)=Home Allowed(Roster)=True Allowed(GeneralShop)=True`; boot log `ShowScreen called: Home` with `StarterGate:Resolve` running normally and **no** `[StandaloneGate]` / `[StandaloneShellBoot]` / `[SettingsController] standalone` line anywhere. `[WhereAmI] screen=Home … bottomNav=True ticketText=True shopPlus=True`. Screenshots bracket the whole proof: `game_boot_home_gate_off.png` (before) and `game_boot_home_after_restore.png` (after) — same Home, same five-slot nav, same ticket cluster. `git status ProjectSettings/` is clean: `ProjectSettings.asset` is byte-identical to HEAD. |
| 6 | `golfingps://gps` routes; `client_platform == "ios-playlife"`; telemetry `app_variant` present | **PARTIAL — closes on the round-2 build** (unchanged from round 1: the Editor reports `client_platform:"editor"` by design, so only a device can show the real label; device row 7.9 covers it, and the round-2 upload is the build it runs on) | **Routing PASS**, in play mode: `[GateProbe] deep link golfingps://gps -> matched=True screen=GpsHub`. Plus `BannerPolicyTests` pins `golfingps://gps`, `GOLFINGPS://GPS`, and that the second scheme is held to the *same* enumerated routes (`golfingps://shop`, `…/checkin`, `?tab=1`, `a@gps`, `golfingpsx://` all refused). **`client_platform` PASS at the source**: `AppVariantInfo.Current=ios-playlife` read live under the define, and `UnityClientPlatformProbe.Label()` returns `IosPlayLife` on `IPhonePlayer` under `#if GOLFIN_STANDALONE`. **NOT proven end-to-end**: a real `POST /activity/checkin` from the shell needs a device build — the Editor reports `client_platform:"editor"` by design, so the request could not be quoted here. Device-pass row **7.9** covers it. **`app_variant` PASS at the source**, not observed on the wire for the same reason. See the deviation note on where it is stamped. |
| 7 | Boot log clean of exceptions with the golf services idle | **PASS** | Over the whole standalone boot slice, `grep -E "^[A-Za-z.]*(NullReference\|MissingComponent\|MissingReference\|InvalidOperation\|Argument\|IndexOutOfRange\|KeyNotFound)Exception:"` returns **nothing**. The golf services lazy-init and log normally with their screens absent: `[CharacterManager] Loaded 12 characters from CSV`, `[ClubManager] Loaded 14 owned clubs from save (schema v12)`, `[GachaTicketManager] Last known Standard balance: 2890`, `[TournamentService] Backend = Remote … Tournaments=3`. §D7 holds: nothing throws when its screen does not exist. |
| 8 | Fastlane lane compiles; `Tools/testflight.sh testflight_build_standalone` reaches the archive; .ipa size quoted; upload id OR blocker | **CLOSED in round 2** — the lane ran end to end (Cesar authorized the upload). The Unity half was verified on its own first: `Tools/unity-build-ios.sh standalone` SUCCEEDED with **Total User Assets 98.3 MB** (Textures 79.7, Other 11.6, Sounds 4.3, Levels 1.5) against the **555 MB** the same report showed for build 2635 — an 82 % cut, comfortably inside the ≤150 MB target. Round-1 text below kept for the record. | `ruby -c fastlane/Fastfile` → Syntax OK. `fastlane lanes` lists all three, including `----- fastlane ios testflight_build_standalone / Unity build -> archive -> TestFlight (the PLAYLIFE standalone shell, its own app record)`. `Tools/unity-build-ios.sh standalone` resolves to `CIBuild.BuildIOSStandalone` and rejects an unknown variant (`exit=2`). **The archive itself was NOT run**, deliberately: the lane goes straight to a TestFlight **upload**, which is Cesar's "punch it standalone" authorization, not mine to give — and it requires a clean tree and a closed Editor, neither of which held while this work was in flight. **The .ipa size comparison is therefore the one unmet acceptance row.** See § Remaining. |
| 9 | Both other lanes still build (the shared-lane refactor is the risk) | **PARTIAL — same reason** | The refactor is `gps: bool` → `variant: symbol` + a `variant_table`; the two existing lanes now read `testflight_build_shared(variant: :standard)` / `(variant: :gps)` and the body's only variant-dependent lines are the `unity_arg` and the `mark-uploaded` record. Proven statically (`fastlane lanes`, `ruby -c`, and `Tools/unity-build-ios.sh` still maps `""`→`BuildIOS` and `gps`→`BuildIOSGps` unchanged); **not** proven by a second archive, for the reason in row 8. |
| 10 | Importer: no new player strings (`add 0`) | **PASS** | No `LocalizationManager.Get` call was added and no CSV touched — `git diff` shows zero changes under `Assets/Localization/`. The Settings shell layout HIDES existing rows; it adds no label. The only new user-visible strings are the placeholder icon's baked wordmark (art, not a localized string). |
| 11 | Full EditMode sweep green; new suites executed by name | **PASS** | Final sweep with everything restored: **2394 tests, 2391 passed, 0 failed, 3 skipped** (the three documented Stage-C1 `HoleCompleteDriverTests` skips), 1m34s. New suites proven to RUN by a deliberate tripwire (class filters are ignored by `tests-run`, and a passing test is never named in the output): three assertions were flipped, the run named exactly `GolfinRedux.Tests.EditMode.StandaloneGateTests.Home_IsRewrittenToTheHub_InTheShell`, `GolfinRedux.Tests.EditMode.GpsAuthExtrasFlowTests.ShellBoot_FirstRun_LandsOnTheCapture_ThenOnTheHubOnceAnswered` and `Golfin.Tournaments.WireupTests.BannerLinkAllowlistTests.Accepts_the_standalone_scheme_for_the_same_hub_route`, and the tripwires were reverted (`grep TRIPWIRE Assets/` → nothing). One earlier run showed `UiMotionAllocationTests.CountUp_AllocatesOnlyWhenTheDrawnNumberChanges` failing; it did not reproduce on three subsequent runs and asserts frame-count timing in `GpsPaintMotion`, which this task does not touch — flake, not a regression. |
| 12 | Deviations flagged; device-pass rows added | **PASS** | Five deviations below. `Docs/GPS/GPS_DEVICE_PASS.md` gains **§7 · PLAYLIFE standalone shell** — ten rows (7.1–7.10): install beside the game and tell the icons apart, boot to hub, fresh-account chain, chrome inventory, full surface walk, Settings layout, back gesture on the root, `golfingps://gps` from Safari, `client_platform` on a real row, and the game still unchanged on the same phone. The old §7/§8 renumbered to §8/§9. |

---

## Deviations from the SPEC (all deliberate, none silent)

1. **`ProjectSettings.asset` is NOT modified** — the SPEC's file list names it for the URL scheme. Registering `golfingps` on the GAME would be wrong: the two apps are installed side by side and iOS resolves a custom scheme to whichever app claims it, so two claimants is undefined. The scheme is added by `StandaloneBuildPreprocessor` **during the standalone build only** and removed after. `BannerPolicy` accepts both schemes in both variants (a dashboard banner row is written once and served to every app), which is the half that genuinely needs to be shared.
2. **The Settings layout flag went on `SettingsController`, not `InGameSettingsModalController`** — §D5 names the latter, but the top-bar gear opens `SettingsController` (the accordion overlay). `InGameSettingsModalController` is the in-round pause modal, which is unreachable in a shell with no gameplay. Rows hidden: Graphics and Sound. Kept: User Profile, Language, About, the four legal links, Log Out.
3. **`StandaloneShellBoot` is a new file the SPEC did not list.** §D3 says the shell skips `StarterGate` "entirely", and the Splash is only ONE of four post-auth routers — `LoginScreenController`, `CreateUsernameScreenController` and `SignUpScreenController`'s OAuth callback do the same thing. Left alone, a fresh account signing in through the Login screen would have been routed to `StartingCharacterSelection`, which `StandaloneGate` refuses — a dead end on the account screen with nothing happening. One shared short-circuit, taken BEFORE the gate resolves so the golf-inventory round trip is never issued.
4. **A warm deep-link handler was added to `ScreenManager`.** Device row 7.8 asks for `golfingps://gps` from Safari, and nothing was listening for internal routes outside the banner strip. Warm links only, no `Application.absoluteURL` cold sweep: a cold sweep would race the boot and could land a player past the title gate with no session — and the shell needs no cold handling anyway, since its boot destination already *is* the hub.
5. **`app_variant` is stamped into each event's PAYLOAD, not the batch envelope.** The envelope's fields are columns on `telemetry_events` and the FastAPI ingest model binds only the ones it declares (`TelemetryBatch` in `playlife/backend/routers/telemetry.py`), so an unknown envelope key is silently dropped and would never reach a row. `payload` is `jsonb` and the admin explorer already renders it — so this is observable today, with no migration and no server deploy. **Also:** `client_platform` reports `ios-playlife` only on real hardware; the simulator test still wins and returns `ios-simulator`, because that label is what earns the server's mock penalty (`score.py:189`) and a shell build must not be able to launder it.

## Things caught while building (worth knowing)

- **`Assets/Editor/Build/` is gitignored.** The preprocessor was written there first; `.gitignore:26`'s blanket `[Bb]uild/` rule silently swallowed it — it would have worked on this machine and been absent from the repo, which for a build hook means the next machine builds the shell with the GAME's bundle id. Moved to `Assets/Editor/`, beside `CIBuild.cs`, with a comment saying why.
- **The upload guard had to become per-record.** ASC's build-number uniqueness rule is per app, and the shell is a second app. One shared `last_uploaded_build.txt` would have made a standalone upload refuse the next GAME upload at the same commit — a collision that does not exist at Apple. `mark-uploaded.sh` now takes `game` | `standalone`, the lane passes it, and the Xcode archive post-action resolves it at build time (otherwise a PLAYLIFE archive would silently advance the game's guard). Seeded `last_uploaded_build.golfingps.txt` with **12**, the record's real last TestFlight build (0.7.6 (12)).
- **`BuildIOSStandalone` asserts BOTH defines** before building, and restores identity before `Fail()` exits the process — a batchmode `delayCall` never gets a frame, and leaving the PLAYLIFE bundle id on disk would point the next "punch it" upload at the wrong App Store record.

## Not mine — uncommitted paths outside this task (Rule 13)

`Assets/Scripts/UI/Gps/GpsRoundsScreenController.cs` and `Assets/Scripts/UI/Polish/Tests/GpsPolishMotionTests.cs`
were **already being edited by someone else** while this ran (a chip `pixelsPerUnitMultiplier` fix — `ChipPpum = 88f/30f` —
plus a new `PillCornerRadiusTests` fixture; that fixture is the +3 in the test total, 2391 → 2394). Untouched and
uncommitted, left for whoever owns them. `Docs/Reports/content_art.txt`, `Docs/TellCode.md` and
`Docs/Versioning/last_uploaded_build.txt` were dirty at kickoff (quoted in the HEARTBEAT baseline) and are not mine either.

## Remaining — needs Cesar

The archive is the one acceptance row not met, and it is not mine to run: `testflight_build_standalone` ends in a
**TestFlight upload**, which is what "punch it standalone" authorizes. It also needs a clean tree and a closed Editor.
When you want it:

```bash
./Tools/testflight.sh testflight_build_standalone
```

That is the whole third phrase. It produces the .ipa whose size answers §D2's "report the size vs the GPS variant",
and uploads to the GOLFIN GPS record (Apple ID 6737145432). The GPS lane's re-proof (`testflight_build_gps`) is the
same story — the shared-lane refactor is verified statically, not by a second 40-minute archive.

---

# ROUND 2 — `KICKOFF_ADDENDUM.md` (R1–R5)

**Iteration shape:** `build-variant:size-and-branding`
**Canonical screenshot:** `screenshots/r2_shell_hub_on_stripped_scene.png` — the hub, in play mode,
on a ShellScene that has had all 15 refused game screens actually destroyed.

Baseline in `HEARTBEAT.log` (`=== iter-2 kickoff baseline ===`, HEAD `12b5895e0`, count 2635).

## R1 · The real icon — **PASS**

`StandaloneBuildPreprocessor.IconPath` → `Assets/Art/Standalone/AppIcon_GolfinGps_1024_opaque.png`.
The generated placeholder is **deleted**, and so is the icon half of
`Docs/Scripts/make_standalone_icon.py` — a baker left in place is a generated icon that can silently
outrank real artwork later. The launch-image baker stays. Left opaque and un-rounded exactly as the
addendum says. Confirmed in the build log: `[StandaloneIdentity] icon → Assets/Art/Standalone/AppIcon_GolfinGps_1024_opaque.png`.

## R2 · The size — **PASS, target beaten**

| | Build 2635 (shipped) | Verification build 2636 | Build 2637 (shipped) |
|---|---|---|---|
| Total User Assets | **555 MB** | 98.3 MB | **98.6 MB** |
| of which `Resources/HoleData` | 385 MB | 0 | 0 |
| Textures / Other / Sounds / Levels | — | 79.7 / 11.6 / 4.3 / 1.5 MB | — |

2636 was a local verification build, never uploaded — it exists to prove the numbers before an
upload was spent on them. 2637 is the one on TestFlight; the 0.3 MB between them is the round-2
docs/lessons commits changing nothing but the build stamp.

Mechanism: `StandaloneBuildPreprocessor.MoveGolfResourcesOut()` / `RestoreGolfResources()`, called
from a `try`/`finally` in `CIBuild.BuildIOSStandalone`, with the sentinel
`Assets/Resources/.standalone_moved` recording what moved. `AssetDatabase.MoveAsset` to
`Assets/_StandaloneResourceStash` — a **rename that preserves GUIDs and import artifacts**, so this
costs seconds rather than a 545 MB re-import, and outside a `Resources/` folder an asset ships only
if something references it.

**Which folders, and why — enumerated against the call sites, not guessed.** Every
`Resources.Load`/`LoadAll` in `Assets/Scripts` + `Assets/Editor` was listed with its literal path
prefix (178 call sites; the dynamic ones resolved through their `const` prefixes). Moved (13):
`HoleData, Clubs, Balls, Portraits, Sprites, HoleImages, Items, Art, TournamentImages, Bags, FX,
Prefabs, Rarities`. Kept (7 + one asset): `Data` (texts, content_version, build_stamp, sfx),
`UI` (the app-wide `TapFeedbackConfig`/`TapFeedbackFX`), `Physics`, `Gameplay`, `MapView`,
`Environment`, `SupabaseConfig.asset` (auth) — **and `Characters`**.

**`Characters` is the find.** `GpsAvatarScreenController.BindCharacterFigure` loads
`Characters/Homescreen/{name}` for the avatar figure — a PLAYLIFE screen reaching into golf art.
Moving it would have blanked the Avatar screen, and no amount of reasoning about "golf folders"
would have caught it; the grep did. `Characters/` is *entirely* `Homescreen/`, so it stays whole,
which is also exactly what R4 asks for.

Measured before shipping: `Resources` **560.9 MB → 17.4 MB → 560.9 MB** with `git status
Assets/Resources` empty at the end.

**The restore was proven by a real failure, not a simulation.** The first build attempt died on the
tree-bake drift gate (see below) *inside* the moved window, and the `finally` put all 13 folders
back, deleted the sentinel, removed the stash and left git clean.

**The ordering bug that failure exposed.** `ValidateTreeBake()` reads
`Assets/Resources/HoleData/**/tree_obstacles.csv`, so with `HoleData` moved it reported 18/18 holes
missing and failed a build that was correct. Fixed by skipping that gate **for the standalone
only** (`BuildIOSCore(..., validateTreeBake: false)`), because for a variant whose scene list is
ShellScene alone the gate protects nothing — it exists to stop shipping holes whose invisible tree
colliders disagree with what the hole renders, and this build ships no hole. Every other lane keeps
it armed. There is also a second ordering trap I hit while writing it and fixed before running:
`BuildIOSCore` runs *inside* the moved window, so the aborted-build repair had to move to the entry
points (`BuildIOS`, `BuildIOSGps`, `BuildIOSDev`, and `BuildIOSStandalone` **before** the move) —
placed in `BuildIOSCore` it would have un-stashed the folders the build had just moved and shipped
427 MB again while the log claimed the diet had run.

**Aborted builds are self-repairing** three ways: the `finally`, `RestoreNow()` before `Fail()`
exits the process (a batchmode `delayCall` never gets a frame), and an `[InitializeOnLoadMethod]`
that repairs on the next editor load. That last one matters most: a missing `Resources` folder is
not a build error, so a stashed `HoleData` would otherwise make the next **game** build ship
without its holes, silently.

### The .ipa, measured — and the target read two ways

`Builds/ipa/GOLFINGPS.ipa` = **196.2 MB**, against **427 MB** for build 2635 (same path, same
measure): a 54 % cut. **That misses the addendum's literal "≤ 150 MB .ipa".** Unpacking it says why,
and the answer is not the assets:

| Inside the .ipa | Compressed | Uncompressed |
|---|---|---|
| `Symbols/` (dSYM) | **121.1 MB** | 503 MB |
| `Payload/GOLFINGPS.app` | **75.0 MB** | 224 MB |
| — of which `Data/` (Unity assets) | | 117 MB |
| — of which `Frameworks/UnityFramework` | | 106 MB |

Two honest readings, and I am not going to pick the flattering one silently:

- **As the .ipa FILE: missed** — 196.2 MB vs 150. Most of it (121 MB) is debug symbols, which Apple
  keeps for crash symbolication and strips from what a tester downloads; `Docs/BUILD_SIZE_AUDIT.md`
  makes the same point about the game's 711 MB.
- **As the app: met** — the Payload is **75.0 MB** compressed. The addendum's own model for the
  target was "≈ 100 MB assets + the framework", i.e. the Payload, not the symbols.

The asset half landed exactly where the addendum predicted (98.6 MB ≈ "100 MB assets"). What did not
shrink is `UnityFramework` at 106 MB uncompressed: the whole golf codebase is still compiled into the
shell, because §D2 says code is deliberately not refactored out and IL2CPP stripping does the work.
Getting the .ipa FILE under 150 would mean attacking that binary — assembly splitting or a higher
managed-stripping level — which is exactly what this task's § Out of scope excludes and what
`build_size_diet` owns. **Flagging rather than deciding: if the ≤150 MB was meant as the .ipa file,
that is a scope call for Cesar, not one for me to take silently.**

## R3 · Once per account — **PASS, and it found a round-1 defect**

`gps_profile_prompt_server_flag` landed first; its own report is in
`Docs/Specs/Active/gps_profile_prompt_server_flag/IMPLEMENTER_REPORT.md`. Backend deployed and
verified (Fly image `deployment-01M1MNKVRKBW4SGFQTAPC316DD`, machines 68 → 69, live `openapi.json`
carries `golf_profile_prompted`; PUT stamps, fresh GET echoes, `false` does not clear).

Both first-run cases re-run on the shell:

- **Server-stamped account, local flag cleared** → `[GpsAuthExtrasFlow] account flag resolved in
  0.20s — prompted_at=set` → `server says this account already answered … caching the local flag`
  → **hub**. `screenshots/r2_shell_firstrun_server_stamped_to_hub.png`.
- **Column cleared (a genuinely new account)** → `account flag resolved in 0.14s — prompted_at=null`
  → `first GPS entry, GpsHub -> GpsGolfProfile` → **capture, once**.
  `screenshots/r2_shell_firstrun_clean_account_capture.png`.

**The defect.** Round 1's `StandaloneShellBoot` resolved `InterceptHubEntry` itself. That jumped
over the account-flag wait in `Navigate`, so a fresh shell install of an already-answered account
**still showed the capture** — the exact thing the feature exists to prevent. The boot now names
`GpsHub` and `Navigate` decides. Found by running the proof; a review would not have seen it.

## R4 · The game screens still ship — **PASS**

`Assets/Editor/StandaloneSceneProcessor.cs`, an `IProcessSceneWithReport` gated on
`ForceStandaloneStrip` (set by `CIBuild` around the build — profile defines never reach editor
assemblies). For ShellScene it destroys the root of every screen `StandaloneGate.IsShellScreen`
refuses **and nulls the `ScreenManager` field**: `DestroyImmediate` alone leaves Unity's "fake null",
which compares equal to null and throws the moment anything touches a member.

From the build log:

```
[StandaloneStrip] stripped 15 refused screen container(s) from 'ShellScene': Home, Roster,
  Inventory, HoleSelection, ModeSelection, MissionSelection, Leaderboard, TournamentHoleSelection,
  TournamentLeaderboard, TournamentSelection, StaminaShopSelection, StaminaShopDetail,
  GeneralShop, GachaHistory, GachaPrizes
[StandaloneStrip] kept 18 shell screen(s): Logo, Splash, Loading, GpsHub, ScoreUpload, GpsProfile,
  GpsAvatar, GpsBadges, GpsGolfProfile, GpsWelcome, GpsGift, GpsVote, GpsRounds, Login,
  CreateUsername, SignUp, EmailConfirmation, ResetPassword
```

The list is derived from the same predicate the runtime gate uses, so a GPS screen added later is
kept, and a golf screen added later is stripped, without this file being told.

**Editor proof, on the stripped scene** (the processor run against the loaded ShellScene, never
saved, reloaded after): all ten GPS screens open — `[Walk2] asked GpsHub → current=GpsHub`,
`GpsRounds`, `ScoreUpload`, `GpsGift`, `GpsVote`, `GpsProfile`, `GpsAvatar`, `GpsBadges`,
`GpsWelcome`, `GpsGolfProfile` — and the refused ones still refuse cleanly with their objects gone
(`Home`, `Roster`, `GeneralShop` → `current=GpsHub`). **Zero** `MissingReference` /
`MissingComponent` / `NullReference` / `UnassignedReference` exceptions across the whole walk. The
hub renders identically to round 1 (`r2_shell_hub_on_stripped_scene.png` vs `shell_hub.png`).

## R5 · Uncompressed textures — **PASS (partial by design)**

93 textures import with compression `None` and no iPhone override. Ranked by what
"uncompressed" actually costs in a build (`width × height × 4`), not by PNG size on disk — the two
differ by ~100×. The four the shell ships got an iPhone override → **ASTC 6x6, max 2048**:

| Texture | Dimensions | Raw | After |
|---|---|---|---|
| `Art/UI/Account/S_SocialPillBordered.png` | 2048×459 | 3.59 MB | ~0.39 MB |
| `Art/HomeScreen/S_DailyPillGlow.png` | 1242×388 | 1.84 MB | ~0.20 MB |
| `Art/Original UI/MainScreen/S_Top_Area.png` | 1170×313 | 1.40 MB | ~0.15 MB |
| `Art/HomeScreen/S_DailyPillPanel.png` | 1098×244 | 1.02 MB | ~0.11 MB |

The other two big ones are deliberately left alone: `Resources/Art/Gacha/Banners/GachaBanner_StandardClub1.png`
(4.87 MB) is inside a folder R2 now moves out of the standalone entirely, and
`Assets/Packs/TreePackVol.1/Textures/Leave_4K_.psd` (64 MB) is game-only vendor art that belongs to
`build_size_diet`, per the addendum.

## Known remainder (not blocking, honest about it)

The Build Report still shows the **nine `Assets/Skybox/*.hdr` (8.4 MB total)** and
`Assets/Music/Main Theme.mp3` (3.5 MB) in the standalone. The music is **correct to keep** — menu
music plays unbroken from Splash through the account screens, which is shell surface. The skyboxes
are not: ShellScene's own `RenderSettings.skybox` is Unity's built-in `Default-Skybox`, so they
arrive through some other reference I did not chase. At 98.3 MB against a 150 MB target, hunting
8.4 MB was not worth the risk of clearing a reference the game needs; it is a clean backlog row.

## Files changed in round 2

See the § at the end of the round-1 list plus: `StandaloneSceneProcessor.cs` (new),
`CIBuild.cs` (strip flag, tree-gate parameter, repair at the entry points),
`StandaloneBuildPreprocessor.cs` (icon path, move/restore/sentinel/auto-repair),
`make_standalone_icon.py` (icon baker removed), four `.png.meta` texture overrides, the
`gps_profile_prompt_server_flag` set, and `GPS_DEVICE_PASS.md` §1b.
