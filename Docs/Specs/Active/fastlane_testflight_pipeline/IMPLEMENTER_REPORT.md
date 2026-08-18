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
| 10 | `Tools/mark-uploaded.sh` runs after upload and advances the guard file | **PARTIAL — call site proven, real invocation AWAITING CESAR** | The Fastfile calls it after `upload_to_testflight`. fastlane's `sh` runs with cwd = `fastlane/`, so the arguments were tested under exactly those semantics in a throwaway git repo (`cd fastlane && ../Tools/mark-uploaded.sh ".."`): guard `0 → 3` written, second run at the same commit `wrote=no` and `exit=0`, guard forced to `9999` then re-run left it at `9999` (no regression), `.mark-uploaded.log` appended on all three. Deliberately **not** run against the real repo — it would advance the live guard to `2195` and refuse every store build at this commit. |
| 11 | Unity Console / batchmode log has no errors related to this task | **PASS** | Successful run: `[CIBuild] result=Succeeded errors=0 warnings=139` (the 139 are pre-existing shader/obsolete-API warnings, none naming a file from this task). Editor-side compile after adding `CIBuild.cs`: reflection probe returned `type=Golfin.EditorTools.CIBuild asm=Assembly-CSharp-Editor BuildIOS=present profileLoads=iOS-Full SetActiveBuildProfile=present`; console `Error` query returned only pre-existing `CS0618`/`CS8632` warnings in unrelated files. |
| 12 | Spec deviations flagged at the bottom with justification | **PASS** | Findings + Deviations below. |

**Not claimed, per the spec:** `upload_to_testflight` itself. It needs the App Store Connect
API key that only Cesar can mint, and it uploads to a real record. Everything up to and
including `build_app` is code-complete; `build_app` has not been executed either (it needs
`api_key` from the same `.env`, and fastlane is not installed).

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

1. ~~Install fastlane~~ — **DONE 2026-08-18** (Homebrew + `brew install fastlane`, verified
   2.238.0 on vendored ruby 4.0.6).
2. ~~Mint the App Store Connect API key~~ — **DONE 2026-08-18**, and proven to authenticate.
   See § API key below and Findings §6.
3. **Add `brew shellenv` to `~/.zprofile`** — one line, Findings §5. Without it `fastlane` is not
   on PATH in a fresh shell.
4. **Commit, then run the lane end to end** — the tree must be clean (that is now demonstrably
   enforced), the Editor closed. `build_app` and `upload_to_testflight` remain the only two
   steps never executed; the first real run **uploads a build to a live App Store Connect
   record**, so it is deliberately left as a human decision rather than run here.
5. **After the run**, commit `Docs/Versioning/last_uploaded_build.txt` — it is the one file the
   lane leaves dirty, by design.

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
`Golfin Game` record (id `6741622475`). `fastlane/.env` also carries `LC_ALL`/`LANG=en_US.UTF-8`,
which silences fastlane's UTF-8 locale warning (confirmed: the warning appears on a run without
them and is absent with them). Item 10's real invocation still waits on the first lane run.
