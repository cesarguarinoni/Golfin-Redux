# IMPLEMENTER REPORT — `fastlane_testflight_pipeline`

**Implemented:** 2026-08-18 (Claude Code, main thread — build tooling, no UI/Figma/scene work,
so the subagent chain does not apply; same treatment as `upload_guard_automation`)
**HEAD at kickoff:** `0ec922ac8` · `git rev-list --count HEAD` = **2195**
**Working tree at kickoff (pre-existing, NOT introduced here):**

```
 M .gitignore                                   (upload_guard_automation — .mark-uploaded.log rule)
 M Docs/AI_CONTEXT.md
 M Docs/Architecture/ARCHITECTURE_AUDIT.md
 M Docs/TESTFLIGHT_RUNBOOK.md                   (upload_guard_automation — Phase 3 post-action)
 M Docs/TellCode.md
 M Docs/Versioning/last_uploaded_build.txt      (0 → 2192, upload_guard_automation)
?? Assets/Editor/iOSArchivePostAction.cs(.meta) (upload_guard_automation)
?? Docs/Specs/Active/fastlane_testflight_pipeline/…
?? Docs/Specs/Active/upload_guard_automation/…
?? Tools/mark-uploaded.sh                       (upload_guard_automation — already landed)
```

`upload_guard_automation` **had** landed (uncommitted, `READY_FOR_ARCHITECT_REVIEW`), so
`Tools/mark-uploaded.sh` was **not** re-implemented here — it is called from the Fastfile as
the spec's interaction section requires, and the scheme post-action was left in place.

---

## Acceptance checklist

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | Exact fastlane install commands recorded, reproducible on a second machine | **PASS** | Cesar ran them 2026-08-18 (Homebrew was absent at 06:30; `/opt/homebrew` existed by 10:00). Verified: `fastlane 2.238.0` at `/opt/homebrew/bin/fastlane`, on Homebrew's **vendored `ruby 4.0.6`** — system Ruby 2.6.10 untouched, no gems installed against it. Commands are recorded in Findings §2 and in `Docs/TESTFLIGHT_RUNBOOK.md` § One command. ⚠️ `brew shellenv` is **not** in `~/.zprofile`/`~/.zshrc` (grep → 0 hits), so `fastlane` resolves only in shells where `/opt/homebrew/bin` is already on PATH — see Findings §5. |
| 2 | `Tools/assert-unity-closed.sh` exits non-zero with a readable message while the Editor is open | **PASS** | Run at 06:33 with the Editor live (PID 47142): printed `ERROR: the Unity Editor has this project open.` + `A Unity Editor process is running on this project. Quit it (Cmd-Q) and re-run.`, `exit=4`. After the Editor was quit: `[assert-unity-closed] OK — no Unity lock at …/Temp/UnityLockfile`, `exit=0`. |
| 3 | `Tools/unity-build-ios.sh` derives the Unity version from `ProjectVersion.txt`, not a hardcoded path | **PASS** | Script `awk`s `m_EditorVersion:` out of `ProjectSettings/ProjectVersion.txt`; every run printed `Unity : /Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/… (6000.3.9f1, from ProjectVersion.txt)`. A version with no matching Editor exits 3 with the Hub/`UNITY_PATH` remedy rather than building with the wrong Editor. |
| 4 | A deliberately broken Unity build makes the script exit **non-zero** | **PASS** | A temporary `IPreprocessBuildWithReport` (`_CIBuildFailureProbe`, `callbackOrder 9999`) threw `BuildFailedException` inside `BuildPipeline.BuildPlayer`. Log: `Build Finished, Result: Failure` → `[CIBuild] result=Failed errors=2` → `[CIBuild] FAILED: build Failed — 2 error(s)`; wrapper printed `Unity exited 1` + the last 120 log lines; **`unity-build-ios.sh exit=1`**. Run twice (before and after the fix in the Findings section), non-zero both times. Probe deleted; `git status` confirms no `_CIBuildFailureProbe` path remains. |
| 5 | `CIBuild.BuildIOS` activates `iOS-Full` via the `BuildProfile` API, not `-activeBuildProfile` | **PASS** | `AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/Build Profiles/iOS-Full.asset")` + `BuildProfile.SetActiveBuildProfile(profile)` (`CIBuild.cs`), then `BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions{ buildProfile = profile … })`. `-activeBuildProfile` appears nowhere in `Tools/unity-build-ios.sh` (the only flags are `-batchmode -quit -nographics -projectPath -buildTarget iOS -executeMethod -logFile`). Batchmode log: `[CIBuild] active build profile → iOS-Full`. |
| 6 | Batchmode build produces `Builds/iOS-Full/Unity-iPhone.xcodeproj` with correct `CFBundleShortVersionString` and `CFBundleVersion == git rev-list --count HEAD` | **PASS** | Real batchmode build, `exit=0`, `Build Finished, Result: Success`, 61 s (incremental IL2CPP; the platform was warm). `PlistBuddy` on the generated `Builds/iOS-Full/Info.plist` — read independently of `CIBuild`'s own logging: `CFBundleShortVersionString = 1.5.7`, `CFBundleVersion = 2195`; `git rev-list --count HEAD = 2195` ✅. `[BuildStamp] preprocess: build #2195 baked. stamp = "v1.5.7 (2195) 0ec922a+4959 · 08-18 08:15"`. Freshness proven, not assumed: `project.pbxproj` mtime moved from `2026-08-17 19:25` to today's run, and `CFBundleVersion` moved `2194 → 2195`. Re-run after the Findings §1 fix: `exit=0` in 50 s, same plist values, tree clean. (That second run left `project.pbxproj` at its 08:16 mtime — Unity's incremental build rewrites it only when its contents change; `Info.plist` and the scheme were regenerated.) |
| 7 | `ITSAppUsesNonExemptEncryption` present in the batchmode-generated `Info.plist` | **PASS** | `PlistBuddy -c 'Print :ITSAppUsesNonExemptEncryption'` → `false` on the freshly generated plist. `iOSPostProcess.cs` therefore runs in batchmode as it does in the GUI; that file was not modified. |
| 8 | `ensure_git_status_clean` aborts the lane on a dirty tree | **PASS** | Ran `fastlane ios testflight_build` for real against the current 17-path dirty tree: the Fastfile loaded, `LANE_NAME = ios testflight_build`, step 1 `ensure_git_status_clean` 💥 with `Git repository is dirty!` quoting `Fastfile:27`, and **`LANE EXIT=1`**. Nothing downstream ran — `Builds/unity-build-ios.log` mtime stayed at the 08:21 run, so no Unity build, no archive, no upload was attempted. Also proven: a batchmode build — success **or** failure — leaves `ProjectSettings.asset` clean, so the lane's own build no longer trips its own precondition (Findings §1). |
| 9 | `.p8`, `.env`, `Builds/ipa/`, `fastlane/report.xml` gitignored; tree clean after a run except the guard file | **PASS** | `git check-ignore -q` → IGNORED for `fastlane/.env`, `fastlane/report.xml`, `fastlane/README.md`, `Builds/ipa/Golfin.ipa`, `AuthKey_ABC123.p8`, `Builds/iOS-Full/…`; NOT ignored (correctly tracked) for `fastlane/Fastfile`, `fastlane/Appfile`, `fastlane/.env.example`. After the final successful build, `git status --porcelain ProjectSettings/ProjectSettings.asset` is empty and no `Builds/` path appears in `git status`. |
| 10 | `Tools/mark-uploaded.sh` runs after upload and advances the guard file | **PASS** | Real lane run: the guard advanced **2192 → 2201** and step 8 `../Tools/mark-uploaded.sh ..` appears in fastlane's own summary table. Precisely what happened, from `.mark-uploaded.log`: the **Xcode Archive post-action** fired first at `11:02:07` (`old=2192 new=2201 wrote=yes`), so the Fastfile's post-upload call at `11:05:09` correctly logged `old=2201 new=2201 wrote=no — no advance`. Both mechanisms ran, neither double-wrote, and the guard is now truthful. See Findings §8 — the post-action firing under `xcodebuild` contradicts the spec's premise. |
| 11 | Unity Console / batchmode log has no errors related to this task | **PASS** | Successful run: `[CIBuild] result=Succeeded errors=0 warnings=139` (the 139 are pre-existing shader/obsolete-API warnings, none naming a file from this task). Editor-side compile after adding `CIBuild.cs`: reflection probe returned `type=Golfin.EditorTools.CIBuild asm=Assembly-CSharp-Editor BuildIOS=present profileLoads=iOS-Full SetActiveBuildProfile=present`; console `Error` query returned only pre-existing `CS0618`/`CS8632` warnings in unrelated files. |
| 12 | Spec deviations flagged at the bottom with justification | **PASS** | Findings + Deviations below. |

### End to end — run for real, 2026-08-18 11:05 JST

Cesar authorized a full run. `LANE EXIT=0`, **11 min 27 s** wall clock, all 8 steps green:

| Step | Action | Time |
|---|---|---|
| 1 | `default_platform` | 0 s |
| 2 | `ensure_git_status_clean` | 0 s |
| 3 | `../Tools/assert-unity-closed.sh` | 0 s |
| 4 | `../Tools/unity-build-ios.sh` | 63 s |
| 5 | `app_store_connect_api_key` | 0 s |
| 6 | `build_app` (archive + export + sign) | 521 s |
| 7 | `upload_to_testflight` | 99 s |
| 8 | `../Tools/mark-uploaded.sh ..` | 0 s |

Outputs: `Builds/ipa/Golfin.ipa` (522 MB) + `Golfin.app.dSYM.zip` (291 MB), both gitignored.

**Verified at Apple, not merely reported by fastlane.** A read-only Spaceship query
(`get_builds`, `sort: "-uploadedDate"`) against the live record, polled until it appeared:

```
2201   state=VALID   uploaded=2026-08-17T19:06:19-07:00   ← this run
2194   state=VALID   uploaded=2026-08-17T07:00:30-07:00
2192   state=VALID   uploaded=2026-08-17T04:16:14-07:00
```

It took ~4 minutes after the lane returned for the build to become visible over the API — worth
knowing, since `skip_waiting_for_build_processing: true` means the lane returns before Apple has
surfaced anything. `state=VALID` (not `PROCESSING`) by the time it appeared.

The `get_builds` call needed `includes: nil` to work around the spaceship drift in Findings §6.

---

### Second run — 2026-08-18 15:42 JST, `1.5.7 (2211)`

The repeat that matters: a lane is only unattended if it works the *second* time, on a machine
whose state the first run changed. `./Tools/testflight.sh` from a clean tree at `bdec09259`,
Editor closed, **exit 0 in 8 min 12 s** (15:42:32 → 15:50:44) — 3¼ minutes faster than the
11:05 run, entirely in `build_app` (322 s vs 521 s: incremental IL2CPP off the 11:05 archive).

| # | Step | Time |
|---|---|---|
| 1 | `default_platform` | 0s |
| 2 | `ensure_git_status_clean` | 0s |
| 3 | `assert-unity-closed.sh` | 0s |
| 4 | `unity-build-ios.sh` | **88s** (warm iOS platform, incremental IL2CPP) |
| 5 | `app_store_connect_api_key` | 0s |
| 6 | `build_app` (archive + export + sign) | **322s** |
| 7 | `upload_to_testflight` | **79s** |
| 8 | `mark-uploaded.sh ..` | 0s |

- `Successfully exported and signed the ipa file: Builds/ipa/Golfin.ipa`, dSYM compressed and
  uploaded alongside it.
- `Successfully uploaded the new binary to App Store Connect`.
- **Confirmed at Apple, not just in the log:** polled the App Store Connect API until the record
  appeared — `FOUND 1.5.7 (2211) state=VALID`, ~5 minutes after upload (the 11:05 run took ~4).
  It is the newest build on the `Golfin Game` record (id 6741622475), ahead of `2201`, `2194`, `2192`.
- Guard advanced `2201 → 2211` — this time the Fastfile's call did the writing, since the
  archive post-action had already fired at the same commit; `Docs/Versioning/last_uploaded_build.txt` is the only dirty file
  afterwards, exactly as designed.
- Signing needed no interaction: automatic signing from Player Settings +
  `-allowProvisioningUpdates`, no `match`, no keychain prompt.
- Contents differ from 2201: this build is the first carrying the in-flight `beta_telemetry`
  code (`bdec09259`), committed on Cesar's explicit call to clear the tree for the lane.

**Every acceptance item PASSES. Nothing in this spec is AWAITING.**

---

## Findings

### 1. A failed batchmode build used to poison the next run — fixed in `CIBuild`

The first broken-build test exited 1 correctly but left `ProjectSettings.asset` dirty
(`iPhone: 2113 → 2195`, `AndroidBundleVersionCode: 2113 → 2195`).

Cause: `BuildStampGenerator.OnPreprocessBuild` writes the git-derived build number into
`PlayerSettings` and restores it in `OnPostprocessBuild`, **which fires on success only**. Its
failure safety net is `EditorApplication.delayCall += RestoreFieldsSafetyNet` — and a
`delayCall` never gets a frame in batchmode, because `EditorApplication.Exit(1)` ends the
process first. So every failed batchmode build would have left the tree dirty, and the *next*
`fastlane ios testflight_build` would abort at `ensure_git_status_clean` blaming a file the
pipeline itself dirtied — with a build number that looks alarming in the diff.

Fix, contained entirely in `CIBuild.cs` (`BuildStampGenerator` untouched, per Out of scope):
`BuildIOS` snapshots the two fields before building, `BuildIOSCore` **returns** a failure
string instead of exiting, and the restore runs before `Fail()`. It cannot be a `finally` —
`Exit()` ends the process, so a `finally` around the call would never execute. Idempotent with
`BuildStampGenerator`'s own restore (identical captured values).

Proven both ways after the fix:
- failed build → `exit=1` **and** `git status --porcelain ProjectSettings/ProjectSettings.asset` empty, log shows `[CIBuild] restored PlayerSettings buildNumber → iOS=2113 Android=2113`
- successful build → `exit=0`, tree clean, `CFBundleVersion = 2195`

The 2195 left by the *first* (pre-fix) test was reverted surgically by editing the two lines
back to `2113` — not `git checkout`.

### 2. fastlane install — done by Cesar, Homebrew route

At 06:30 there was **no Homebrew** on this Mac (`brew` off PATH, `/opt/homebrew` and
`/usr/local/bin/brew` both absent) and `ruby -v` was `2.6.10p210` (Apple's system Ruby, EOL).
Per the spec system Ruby was left untouched — no `gem install`, no `sudo gem`, nothing. Because
the Homebrew installer needs an admin password, Cesar ran these himself (2026-08-18), rather
than have a non-standard user-prefix Homebrew or an rbenv/source-built Ruby introduced:

```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

```bash
eval "$(/opt/homebrew/bin/brew shellenv)" && brew install fastlane
```

Result, verified: `fastlane 2.238.0` at `/opt/homebrew/bin/fastlane`, running on Homebrew's
vendored `ruby 4.0.6` — exactly what the spec's "do not touch system Ruby" line was protecting.

Before fastlane existed, the same files were checked the only ways available: the Fastfile and
Appfile parse (`ruby -c` → `Syntax OK` — parse-only, installs nothing), all three shell scripts
pass `bash -n`, and the two `sh(...)` call sites were exercised directly under fastlane's cwd
semantics. Real fastlane has since loaded the Fastfile and executed step 1 (item 8).

### 5. `brew shellenv` is not in the shell profile

`grep -c "brew shellenv" ~/.zprofile ~/.zshrc` → **0** in both. `/opt/homebrew/bin` is therefore
not on PATH in a fresh non-interactive shell, and `fastlane` resolves only where it already
happens to be (an interactive Terminal that inherited it some other way). Every fastlane
invocation in this report was run with an explicit `PATH="/opt/homebrew/bin:$PATH"`.

Fix — append the line the installer printed, then open a new shell:

```bash
echo 'eval "$(/opt/homebrew/bin/brew shellenv)"' >> ~/.zprofile
```

Left for Cesar rather than done here: `~/.zprofile` is personal shell config outside the repo.

### 6. API key authenticates — proven read-only, nothing uploaded

`Spaceship::ConnectAPI::Token.create` with the three `.env` values, then `App.find`:

```
AUTH OK  app=Golfin Game  id=6741622475  bundle=com.nextinnovation.golfingame
```

That is the real record (`appstoreconnect.apple.com/apps/6741622475/testflight/ios`, the same id
the runbook cites), so key + issuer + `.p8` are all correct together — not merely well-formed.
Read-only: one `GET`, no build listed as uploaded, no state changed.

One drift worth knowing about: a follow-up `Spaceship::ConnectAPI.get_builds` failed with
`'betaBuildMetrics' is not a valid relationship name` — spaceship 2.238.0 asks for a relationship
Apple's API no longer serves. It is **not** a key problem (auth had already succeeded). It does
not affect this lane, which passes `skip_waiting_for_build_processing: true` and
`skip_submission: true` and so never lists builds — but `latest_testflight_build_number` and
anything that waits on processing would hit it. Flagged rather than worked around; if a future
lane needs those, `brew upgrade fastlane` first.

### 3. `CFBundleIdentifier` in the generated plist is `${PRODUCT_BUNDLE_IDENTIFIER}`

Expected, not a defect: Unity writes the build-setting reference and `xcodebuild` resolves it
from `project.pbxproj` at archive time. Same as the manually-archived 2026-08-17 project.

### 4. Interaction with `upload_guard_automation` — no regression

The Archive post-action survived this task's rebuild: `grep -c 'Mark commit as uploaded'` on the
regenerated `Unity-iPhone.xcscheme` returns exactly **1** (injected, not duplicated). Both
paths now feed the guard — the scheme post-action for manual GUI archives, the Fastfile `sh`
call for lane runs — and `mark-uploaded.sh` never regresses, so both firing is harmless.

### 7. The first real end-to-end run failed in `build_app` — on the locale, not the build

Run 1 (10:48, clean tree at build 2201): steps 1–3 passed, Unity produced the Xcode project in
94 s with `CFBundleVersion=2201`, `app_store_connect_api_key` succeeded — then `build_app` died
**3 seconds** into `xcodebuild archive`:

```
[!] invalid byte sequence in US-ASCII (ArgumentError)
    gym/lib/gym/error_handler.rb:15:in 'Regexp#==='
```

`~/Library/Logs/gym/Golfin-Unity-iPhone.log` contains no `error:` line at all — the build had
barely started. gym streams every xcodebuild output line through error-matching regexes;
xcodebuild prints `➜` (U+279C) in its dependency graph (log line 18, confirmed with
`grep -P '[^\x00-\x7F]'`); matching that under `Encoding.default_external = US-ASCII` raises, and
the raise killed fastlane and `xcodebuild` with it. **A build failure that was not a build
failure.**

Measured, not inferred:

| Check | Result |
|---|---|
| `ruby -e 'puts Encoding.default_external'` in the shell that ran the lane | `US-ASCII` (`LANG=nil LC_ALL=nil`) |
| same, with `LC_ALL=en_US.UTF-8` exported first | `UTF-8` |
| `ruby -e 'ENV["LC_ALL"]="en_US.UTF-8"; puts Encoding.default_external'` | **`US-ASCII`** — setting it in-process is too late |

That third row is why `fastlane/.env` cannot fix it: Ruby fixes its external encoding at process
start and dotenv loads `.env` after. The `.env` lines were kept (children inherit them, and they
do silence the cosmetic warning) with the comment corrected to say what they are not.

Fix: `Tools/testflight.sh`, a thin wrapper that exports `LC_ALL`/`LANG` and prepends
`/opt/homebrew/bin` **before** `exec fastlane ios testflight_build`. Both problems it solves are
environment preconditions that bite exactly when nobody is watching — a non-interactive shell,
cron, CI — so leaving them to "remember to run it from Terminal" was not good enough.

### 8. The scheme post-action DOES fire under `xcodebuild` — the spec's premise was wrong

`SPEC.md` § Interaction opens with: *"Scheme post-actions do not reliably fire under
`xcodebuild`, which is what fastlane's `build_app` invokes — so under this pipeline the guard
would silently stop being marked."* The real run says otherwise. `.mark-uploaded.log`:

```
11:02:07  old=2192  new=2201  wrote=yes  sha=6d243bd  advanced …last_uploaded_build.txt
11:05:09  old=2201  new=2201  wrote=no   sha=6d243bd  no advance (new <= current)
```

`11:02:07` is mid-`build_app` — that is `iOSArchivePostAction`'s injected Archive post-action
running under `xcodebuild archive`. `11:05:09` is the Fastfile's own call after
`upload_to_testflight`, correctly declining to write again.

This changes nothing about the design and is the best possible outcome for it: the spec's
resolution — keep the script, call it from the Fastfile, leave the post-action in place, rely on
idempotency — is what made a double-fire a no-op instead of a bug. Two independent paths now
feed the guard and neither can corrupt it. Recorded because the *reasoning* in the spec is
inaccurate and someone will otherwise remove the post-action believing it dead weight under CI.

**Side effect for `upload_guard_automation`:** its one outstanding human-verification item was
"confirm a real archive advances the guard file". That is now observed — under `xcodebuild
archive` rather than a GUI **Product → Archive**, but both run the same injected scheme action.

---

## Deviations from the spec

| Deviation | Why |
|---|---|
| `CIBuild` also restores `PlayerSettings` build numbers on every exit path | Findings §1. Without it, acceptance item 9 ("tree clean after a run") is false after any failed build and the lane blocks itself on the next run. `BuildStampGenerator` was **not** touched. |
| `unity-build-ios.sh` calls `assert-unity-closed.sh` itself, in addition to the Fastfile calling it | Makes a direct `./Tools/unity-build-ios.sh` just as safe as a lane run. The lane still calls it first, as specced, so the failure message stays at the top of the fastlane output. |
| `unity-build-ios.sh` exits 5 if Unity exits 0 but `Unity-iPhone.xcodeproj` is missing; `CIBuild` returns the same failure | Cheap defence against the exact scenario the spec calls out — handing a stale project to `xcodebuild`. `Builds/iOS-Full` already contained a project from 2026-08-17, so "the folder exists" proves nothing on its own. |
| `CIBuild` logs the generated `Info.plist` version keys | Spec asked for the resolved build number in the log. It cannot be read back from `PlayerSettings` (restored by then), so it is parsed from the generated plist — diagnostic only, never fails the build. |
| `.gitignore` also ignores `fastlane/test_output/` | fastlane writes it on any `scan`/report run; same class as `report.xml`. |
| `-buildTarget iOS` passed on the command line | Matches `Tools/build-demo.sh`; avoids a platform switch inside the build when the Editor was last on another target. The profile API still owns activation. |

---

## Files modified or created

| File | Change |
|---|---|
| `Assets/Editor/CIBuild.cs` | **NEW** — `Golfin.EditorTools.CIBuild.BuildIOS`, the `-executeMethod` entry point. Activates `iOS-Full` via `BuildProfile.SetActiveBuildProfile`, builds to `Builds/iOS-Full` with `BuildOptions.None`, exits 1 on every failure path (including exceptions), restores `PlayerSettings` build numbers on all exits, logs the generated plist's version keys. |
| `Assets/Editor/CIBuild.cs.meta` | **NEW** — Unity-generated. |
| `Tools/unity-build-ios.sh` | **NEW** — batchmode wrapper. Derives the Editor version from `ProjectVersion.txt`, propagates Unity's exit code, tails the log on failure, asserts the Xcode project exists. |
| `Tools/assert-unity-closed.sh` | **NEW** — refuses to run while `Temp/UnityLockfile` exists; distinguishes a live Editor from a stale post-crash lock. |
| `fastlane/Fastfile` | **NEW** — the single `ios testflight_build` lane: clean-tree → lock check → Unity build → `build_app` → `upload_to_testflight` → `mark-uploaded.sh`. |
| `fastlane/Appfile` | **NEW** — `com.nextinnovation.golfingame` / `TCUV4A9VTJ`. |
| `fastlane/.env.example` | **NEW** — documents `ASC_KEY_ID` / `ASC_ISSUER_ID` / `ASC_KEY_PATH` and where the `.p8` comes from. |
| `.gitignore` | Added `fastlane/.env`, `fastlane/report.xml`, `fastlane/README.md`, `fastlane/test_output/`, `Builds/ipa/`, `*.p8`. |
| `Docs/TESTFLIGHT_RUNBOOK.md` | Added § "One command — `fastlane ios testflight_build`": what the lane does step by step, one-time setup (Homebrew + fastlane, API key, `.env`), and failure triage. Manual Phases 2–4 kept as the fallback. |
| `ProjectSettings/ProjectSettings.asset` | **Reverted to HEAD**, not changed — the two `2113 → 2195` lines left by the pre-fix broken-build test were edited back. Net diff vs HEAD: none. |

Untracked build output (`Builds/iOS-Full/**`, `Builds/unity-build-ios.log`) is covered by
`.gitignore`'s `[Bb]uilds/` and does not appear in `git status`.

---

## Needs Cesar

1. ~~Install fastlane~~ — **DONE 2026-08-18** (2.238.0 on vendored ruby 4.0.6).
2. ~~Mint the App Store Connect API key~~ — **DONE 2026-08-18**, proven to authenticate.
3. ~~Run the lane end to end~~ — **DONE**, twice: `1.5.7 (2201)` at 11:05 JST and
   `1.5.7 (2211)` at 15:42 JST, both `VALID` on App Store Connect. All 12 acceptance items PASS.
4. **Commit `Docs/Versioning/last_uploaded_build.txt`** (now `2211`) — the one file the lane
   leaves dirty, by design. NOT committed here: no close-out commit was requested, and CLAUDE.md
   rule 12 halts one while unrelated drift exists outside the task folder.
5. **Optional, one line** — put the locale and `brew shellenv` in `~/.zprofile` (Findings §5,
   §7) so `fastlane ios testflight_build` works directly from any shell. `Tools/testflight.sh`
   already covers it for the common path.
6. **Check TestFlight** — `2211` should have reached `In-House Testers` automatically, and it is
   the first tester build carrying the in-flight `beta_telemetry` code, so it is worth a device
   smoke rather than a glance. Worth
   confirming once that the internal group really does auto-distribute a fastlane-uploaded
   build, since the lane deliberately passes no `groups:`.

---

## API key — done 2026-08-18

Cesar created the key; `fastlane/.env` was filled in from it. Values read off App Store Connect
→ Users and Access → Integrations → App Store Connect API, **Team Keys** tab:

| Field | Value | Source |
|---|---|---|
| Key name | `golfingame` | key table row (GENERATED BY 賢 小松) |
| `ASC_KEY_ID` | `D63D7CJR92` | key table `KEY ID`, matches the `.p8` filename on disk |
| `ASC_ISSUER_ID` | `c6ec9386-…7801f` | the per-team Issuer ID above the table |
| `ASC_KEY_PATH` | `/Users/cesar/.appstoreconnect/AuthKey_D63D7CJR92.p8` | file on disk, mode `-rw-------` |

Checked rather than assumed:
- It is a **Team Key**, not an Individual Key — Individual Keys carry no Issuer ID and the lane's
  `ENV.fetch("ASC_ISSUER_ID")` would fail on one.
- The `.p8` is a real key, not a truncated download: `openssl pkey -noout -text` parses it as a
  256-bit EC private key (257 bytes, `-----BEGIN PRIVATE KEY-----`).
- `git check-ignore -v fastlane/.env` → `.gitignore:293`, and `git status` shows no
  `fastlane/.env` entry. The `.p8` lives outside the repo and `*.p8` is ignored as a backstop.
- Access role is **Admin**, not the App Manager that was recommended. It works (Admin is a
  superset) — worth downgrading only if you want the key on your disk to hold less authority.

The key **was** exercised, read-only — see Findings §6: it authenticates and resolves the live
`Golfin Game` record (id `6741622475`).

⚠️ **Correction.** An earlier revision of this report presented the `LC_ALL`/`LANG` lines in
`fastlane/.env` as the fix for fastlane's UTF-8 locale warning. They silence the *warning* but
do **not** fix the *encoding*, and the first real lane run proved it by dying in the archive.
See Findings §7.
