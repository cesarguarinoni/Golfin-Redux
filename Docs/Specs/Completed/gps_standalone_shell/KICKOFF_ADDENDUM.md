# KICKOFF ADDENDUM — `gps_standalone_shell` round 2 (Architect, 2026-09-03 evening)

Round 1 is reviewed **PASS** against HEAD (`547567bc5` on main; profile diff exactly three
things; StandaloneGate walks every ScreenId; chrome measured not eyeballed; game boot proven
untouched; per-record upload guard; warm deep-link handler). Cesar ran "punch it standalone":
build **2635** uploaded to GOLFIN GPS (6737145432) as 1.0.0 — a **427 MB** .ipa. He installed it,
saw the placeholder icon and the size, and gave three asks. Those three are this round.

## R1 · The real icon

`Assets/Art/Standalone/AppIcon_GolfinGps_1024_opaque.png` is Cesar's icon (green gradient, white pin
with a golf ball on a tee). `StandaloneBuildPreprocessor.IconPath` → that file (delete the baked
`S_StandaloneAppIcon.png` + its baker entry). It is already opaque — ASC rejects alpha in the 1024
marketing icon and iOS masks the corners itself — keep it that way; do NOT re-round it. Launch image
stays `S_StandaloneLaunch.png` (backlog: Ken's brand launch screen).

## R2 · The size — Resources ships everything

The 427 MB is not the shell's code: the Build Report for 2635 lists all 18
`Assets/Resources/HoleData/*/heightmap.bytes` (16.8 MB each) and `zones.json` (2–10 MB each) inside
the standalone, because `Resources/` is included whole regardless of scene usage. Spec §D2 is
amended: the preprocessor **moves golf-only `Resources` subfolders out of the tree for the build and
restores them** — `try/finally` around the build in `CIBuild.BuildIOSStandalone` plus a sentinel
file (`Assets/Resources/.standalone_moved`) so an aborted build is repairable by `RestoreNow`.
Which subfolders: `HoleData` for certain; then enumerate `Assets/Resources/*` against every
`Resources.Load` / `LoadAsync` / `LoadAll` reachable from the GPS surface + auth + Top UI (grep the
call sites, list them in the report) — anything only golf screens load (`Clubs`, `Balls`, `Bags`,
`Characters` art, gacha banners…) goes too; anything shared (texts/content, GPS art, UI atoms) stays.
Prove the shell still boots and every GPS screen still paints with the folders moved (the Editor
proof from round 1, re-run). **Target: standalone .ipa ≤ 150 MB**; quote the new Build Report
category lines and the .ipa size beside 427 MB. Do NOT touch how the game loads HoleData — that is
`build_size_diet` (Notion 2121, the game session).

## R3 · Once per account

`gps_profile_prompt_server_flag` (its own quick spec + kickoff in TellCode; the column is already
live on prod) lands first; the shell's first-run proof in round 1 (`shell_firstrun_golf_profile.png`)
is re-run with a server-stamped account → hub directly, and with a clean account → capture once.


## R4 · The game screens still ship — strip them from the scene at build time

ShellScene carries every game screen as an inactive prefab instance, so the standalone build
(which refuses them via `StandaloneGate`) still ships their art: `Art/Shop`, `RosterScreen`,
`ClubsInventory`, `LoadingScreen`, `HoleSelectScreen`, `RankingsScreen`, `BagsScreen`,
`HomeScreen`, `Resources/Portraits`, `Resources/Sprites`, the gacha banner — ~35 MB — plus the nine
skybox HDRs and `Main Theme.mp3` (~12 MB) through render/audio references. Add an
`IProcessSceneWithReport` (standalone define only) that, for ShellScene, destroys the root
GameObjects of every screen `StandaloneGate.IsScreenAllowed` refuses (walk `ScreenManager`'s
serialized screen fields; null the fields), clears the skybox material and the music clip
references the GPS surface does not use, and logs the list. Editor proof: play mode with the
processed scene (a `BuildPlayer` to a temp folder, or the processor exposed as a menu item on a
scene copy) — every GPS screen still opens, no `MissingReference`. `Resources/Characters`: keep the
`Homescreen` set the Profile hero loads, move the rest with R2.

## R5 · Uncompressed textures the shell still uses

93 textures import with compression None and no iPhone override; the ones the GPS surface ships
(`Art/UI/Account/S_SocialPillBordered.png` 2680×600 = 6 MB raw on the auth screens, the daily pill
glow/panel, `S_Top_Area`) get an iPhone override → ASTC 6x6, max 2048. List every texture in the
standalone Build Report that lands > 500 KB with its import setting; fix the None ones; leave the
game-only ones to `build_size_diet`.

Target after R2 + R4 + R5: **≤ 150 MB .ipa** (≈ 100 MB assets + the framework). Quote the Build
Report category lines before/after.

## Then

Round-1 acceptance rows 6 (device `client_platform`) and 8 (.ipa size) close with the re-upload:
"punch it standalone" is Cesar's phrase — stop before the upload as before, report ready. Device
pass §7 rows (7.1–7.10) run on the new build.
