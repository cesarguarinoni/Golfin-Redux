# Quick · `demo_build_removal` — delete the GOLFIN demo build, for good

**Cesar, 2026-09-04:** "Demo build is no longer needed so you can scrub its existence forever."
Architect (Cowork) wrote this from a `git grep` of HEAD `4176e92aa` + the two docs edits below;
Code implements. No UI, no Figma, no subagent chain. The EditMode suite is the gate.

## What "the demo build" is (so nothing else gets caught)

The `GOLFIN_DEMO` compile-time slice from `demo_build_slice` (2026-07): two build profiles,
a build entry point + shell script, a scene-strip processor, a runtime gate + config, and
~25 `DemoGate.IsDemo` / `DemoConfig.Instance` branches in gameplay/UI code that are dead code
in every shipping lane today (the define is never set outside the two demo profiles).

**NOT the demo build — leave alone:** the `*DemoRecorder.cs` editor scripts
(`ClubControlArrowDemoRecorder`, `ClubRosterDemoRecorder`, `DailyMissionPillDemoRecorder`,
`ShotAimParityDemoRecorder`, `TournamentDemoRecorder`, `RankingsDemoRecorder`, …) — those
record report videos and the `enforce_implementer_done.py` hook knows the family by name;
`Assets/Art/3D/Props/URPWater/Demo/` (vendor pack content); `Assets/Packs/**/Demo*` (gitignored
vendor demos). `DemoShowcaseRecorder.cs` IS demo-build-only (requires `GOLFIN_DEMO`) — delete it.

## Delete (file + `.meta`, `git rm`)

- `Assets/Settings/Build Profiles/iOS-Demo.asset`, `Android-Demo.asset`
- `Assets/Editor/DemoBuild.cs`, `Assets/Editor/DemoSceneProcessor.cs`
- `Assets/Scripts/Demo/` (`DemoConfig.cs`, `DemoGate.cs`, folder meta)
- `Assets/Resources/Data/demo_config.csv`
- `Assets/Scripts/UI/Editor/DemoShowcaseRecorder.cs`
- `Tools/build-demo.sh`; `Builds/build-demo-ios.log` (untracked artifact — delete from disk)
- `Docs/Specs/Queued/demo/DEMO_BUILD_PLAN.md` (+ the now-empty `Queued/demo/`)

## Unwind the call sites — remove the branch, keep the non-demo path byte-for-byte

Every `DemoGate.IsDemo` is a compile-time `const false` in every surviving lane, so the
non-demo path is what ships today; deleting the `if` must not change it. Sites (from
`git grep`, re-grep before you start — this list is the minimum, not the maximum):

`CharacterManager.cs:301`, `ClubManager.cs:278–306`, `TournamentService.cs:239/259`,
`TournamentBackendPolicy.Choose(…, isDemo)` (drop the parameter; its doc comment at :43),
`StarterGate.cs:87`, `AuthGate.cs:28/68`, `BuildStamp.cs:24` (comment only),
`HoleCardController.cs:321/347–349` (+ `IsRewardTypeEnabledInDemo` becomes dead — delete),
`HoleProgressionService.cs:31`, `HomeScreenController.cs:167–169/212/326/436/628–632`,
`HoleCompleteModalController.cs:512`, `ModeCardController.cs:498`, `ModesDatabaseCSV.cs:76`,
`PersistentUIManager.cs:294–295/436–452` (the demo nav-hide method goes entirely),
`ScreenManager.cs:271–272/520` (the allowlist check goes; keep the loop), `SplashScreenController.cs:41/204`,
`CIBuild.cs:32/263` (comments), `iOSArchivePostAction.cs:41` (comment: two lanes, not three).

Tests: `TournamentAsyncBoardTests.cs:127–129/847/851` (`A_demo_build_stays_local` is deleted
with the parameter; the reflection shim `Choose(bool,bool,bool)` becomes two-arg),
`NavBackMemoryTests.cs:234` (comment). `ScreenId` enum, `AuthGate`'s other branches,
`StarterGate`'s other branches: untouched.

Also: search `ProjectSettings/` and every `.asmdef` for `GOLFIN_DEMO` — expected zero hits
(the define lived only in the two profiles); if one exists, remove it and say where it was.

## Gates

- `git grep -n "GOLFIN_DEMO\|DemoGate\|DemoConfig\|DemoBuild\|DemoSceneProcessor\|demo_config\|build-demo\|iOS-Demo\|Android-Demo"`
  over the whole repo returns ZERO hits outside `Docs/Specs/Completed/**`, `Docs/AI_CONTEXT.md`
  history and `Docs/TellCode.md` history — quote the command and its (empty) output.
- Full EditMode suite: same pass count as HEAD minus exactly the deleted demo test(s), 0 failed.
  Name the deleted tests.
- Build lanes compile: `CIBuild.BuildIOS` reaches the Xcode export (no need to archive/upload)
  AND the standalone lane still compiles (`DemoSceneProcessor` and `StandaloneSceneProcessor`
  are siblings — make sure deleting one does not take a shared helper with it).
- No scene or prefab re-serialized: the diff is `.cs`, `.csv`, `.asset` (profiles), `.sh`, `.md`
  and their `.meta` files only. If Unity re-saves a scene on open, revert it.
- `Docs/AI_CONTEXT.md`: one line under today's date — demo build removed, commit hash.
  Notion GOLFIN_Roadmap: Architect closes the demo rows after Cesar's nod.

## Out of scope

`demo_build_slice` never had a spec folder (its design was `DEMO_BUILD_PLAN.md`, deleted above);
the mentions of it in `Docs/AI_CONTEXT.md` / `Docs/TellCode.md` history and
`Docs/Specs/Completed/safe_area_top_bar/SPEC.md` stay — git history cannot be scrubbed and
those lines are the record of why the code existed.
The MCP-package-stripping trick in `build-demo.sh` is NOT ported anywhere: the real lanes
(`Tools/testflight.sh`) have never needed it, and 2658 shipped without it.
