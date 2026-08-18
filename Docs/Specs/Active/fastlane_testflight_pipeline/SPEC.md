# SPEC — `fastlane_testflight_pipeline`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

See `STATUS.md`. Currently `SPEC_READY`.

## Goal

One command produces a TestFlight build. Today the loop is: commit → Unity Build (GUI) →
open Xcode → set destination → Product → Archive → Distribute → four dialogs. Roughly two
hours, of which ~4 minutes is human input spread across the whole span, so it can't be left
alone.

Target: `bundle exec fastlane ios testflight_build`, walk away, get a TestFlight email.

**This does not make it faster.** Unity's build and the IL2CPP compile dominate and are
unchanged. It makes it *unattended*, and it removes the four places a human currently picks
the wrong thing.

## Reference

N/A — build tooling, no UI.

## ⚠️ Interaction with `upload_guard_automation` (read this first)

That spec injects an **Xcode Archive post-action** into the generated `.xcscheme` to run
`Tools/mark-uploaded.sh`. **Scheme post-actions do not reliably fire under `xcodebuild`**,
which is what fastlane's `build_app` invokes — so under this pipeline the guard would silently
stop being marked.

Resolution, and it is strictly better than the post-action:

- **Keep `Tools/mark-uploaded.sh`** exactly as that spec defines it. It is the unit of work.
- **Call it from the Fastfile after `upload_to_testflight` succeeds.** That fires on genuine
  *upload*, not on *archive*, which removes the known over-strictness trade-off documented in
  the other spec.
- **Leave the scheme post-action in place too.** It still covers manual GUI archives. The
  script is idempotent and never regresses, so both firing is harmless.

If `upload_guard_automation` has not landed when this task starts, implement
`Tools/mark-uploaded.sh` here to that spec's requirements and note it in the report.

## ⚠️ Ruby — the real prerequisite

`ruby -v` on this Mac reports **2.6.10p210**. That is Apple's system Ruby: EOL since 2022 and
long deprecated by Apple. Do **not** `gem install fastlane` against it — that path leads to
permission errors and native-extension failures.

**Preferred:** `brew install fastlane`, which vendors its own Ruby and sidesteps the problem
entirely. Only fall back to `rbenv` + a modern Ruby + `Gemfile`/`bundler` if the Homebrew
route proves insufficient. **Do not modify system Ruby.**

Whichever route is taken, record the exact commands in `IMPLEMENTER_REPORT.md` — Cesar has to
be able to reproduce this on a second machine.

## Architecture context

- **Build output:** `Builds/iOS-Full/Unity-iPhone.xcodeproj`, gitignored (`.gitignore:27`)
- **Bundle ID:** `com.nextinnovation.golfingame` · **Team:** `TCUV4A9VTJ` (NEXT INNOVATION PTE. LTD.)
- **Signing:** automatic, driven from Player Settings (`appleEnableAutomaticSigning: 1`)
- **`Assets/Editor/BuildStampGenerator.cs`** — `IPreprocessBuildWithReport`; build number =
  `git rev-list --count HEAD`. Runs in batchmode. Commits between builds are mandatory.
- **`Assets/Editor/iOSPostProcess.cs`** — `[PostProcessBuild(1000)]`, writes
  `ITSAppUsesNonExemptEncryption`. Runs in batchmode.
- **Internal group `In-House Testers`** already auto-distributes new builds. No fastlane
  configuration is needed to reach it — see the `groups:` note below.

## Implementation

### 1. `Assets/Editor/CIBuild.cs` (new)

A static entry point for `-executeMethod`. Namespace `Golfin.EditorTools`.

- `public static void BuildIOS()`
- **Set the active build profile via the `BuildProfile` API, NOT the `-activeBuildProfile`
  CLI flag** — that flag has a Unity 6 batchmode bug (exits batchmode when the profile is
  already active, and requires a project-relative path). Already documented in
  `DEMO_BUILD_PLAN.md` §3.1. Load `Assets/Settings/Build Profiles/iOS-Full.asset` and activate it.
- Output path `Builds/iOS-Full`, `BuildOptions.None` (NOT `Development` — the
  `BuildStampGenerator` guard deliberately skips its refusal for development builds).
- On failure, `EditorApplication.Exit(1)` so the shell sees a non-zero code. On success,
  exit 0. **A batchmode build that fails silently and returns 0 is the classic way to upload
  a stale binary** — get this right.
- Log the resolved build number and output path so the fastlane log is diagnosable.

### 2. `fastlane/Appfile` (new)

```ruby
app_identifier("com.nextinnovation.golfingame")
team_id("TCUV4A9VTJ")
```

### 3. `fastlane/Fastfile` (new)

One lane. Shape — adapt as needed, but keep the ordering and the guards:

```ruby
default_platform(:ios)

platform :ios do
  desc "Unity build -> archive -> TestFlight"
  lane :testflight_build do
    # Build number is `git rev-list --count HEAD`; a dirty tree means the number
    # does not describe what is in the binary.
    ensure_git_status_clean

    # Fails fast and loudly if the Unity Editor holds the project lock.
    # A batchmode build cannot take it and the error is otherwise cryptic.
    sh("../Tools/assert-unity-closed.sh")

    sh("../Tools/unity-build-ios.sh")

    api_key = app_store_connect_api_key(
      key_id:       ENV.fetch("ASC_KEY_ID"),
      issuer_id:    ENV.fetch("ASC_ISSUER_ID"),
      key_filepath: ENV.fetch("ASC_KEY_PATH")
    )

    build_app(
      project:         "Builds/iOS-Full/Unity-iPhone.xcodeproj",
      scheme:          "Unity-iPhone",
      configuration:   "Release",
      export_method:   "app-store",
      xcargs:          "-allowProvisioningUpdates",
      output_directory: "Builds/ipa",
      clean:           false
    )

    upload_to_testflight(
      api_key: api_key,
      skip_waiting_for_build_processing: true,
      skip_submission: true
    )

    # Fires on real upload, not on archive — see the interaction note above.
    sh("../Tools/mark-uploaded.sh", "..")
  end
end
```

**Notes on the choices above, do not silently change them:**

- **No `groups:` parameter.** Per fastlane's docs `groups:` applies to *external* testing
  groups only. `In-House Testers` is internal and already auto-distributes. Passing `groups:`
  here would be a no-op at best.
- **`skip_waiting_for_build_processing: true`** so the lane returns in minutes instead of
  blocking the terminal for 30. The trade-off: fastlane cannot set a changelog in this mode
  (supplying one forces a partial wait). Accepted — internal testers don't read changelogs.
- **No `match`.** Automatic signing already works from Player Settings; adding a certificates
  repo is real overhead for a one-machine, one-developer setup. `-allowProvisioningUpdates`
  covers it. Revisit only if a second build machine appears.

### 4. `Tools/unity-build-ios.sh` and `Tools/assert-unity-closed.sh` (new)

Thin wrappers, so the Unity path and lock check are testable without fastlane.

- Unity binary at `/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity`.
  **Derive the version from `ProjectSettings/ProjectVersion.txt`** rather than hardcoding it —
  the next Unity upgrade must not break this silently.
- `-batchmode -quit -nographics -projectPath <repo> -executeMethod Golfin.EditorTools.CIBuild.BuildIOS -logFile <path>`
- Propagate Unity's exit code. Tail the log to stdout on failure.
- Lock check: `Temp/UnityLockfile` present ⇒ exit non-zero with a plain-English message.

### 5. Secrets and gitignore

- The `.p8` key must live **outside the repo** — suggest `~/.appstoreconnect/`. Never commit it.
- `ASC_KEY_ID`, `ASC_ISSUER_ID`, `ASC_KEY_PATH` come from the environment. Ship a
  `fastlane/.env.example` documenting the three names with placeholder values.
- Add to `.gitignore`: `fastlane/report.xml`, `fastlane/README.md`, `fastlane/.env`,
  `Builds/ipa/`, `*.p8`.

**Cesar generates the key himself** at App Store Connect → Users and Access → Integrations
(he is Admin). It downloads once and cannot be re-downloaded. Do not attempt to create it.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item marked `PASS`/`FAIL` with a one-sentence justification citing what was measured.

- [ ] Exact fastlane install commands recorded in the report, reproducible on a second machine
- [ ] `Tools/assert-unity-closed.sh` exits non-zero with a readable message while the Editor is open
- [ ] `Tools/unity-build-ios.sh` derives the Unity version from `ProjectVersion.txt`, not a hardcoded path
- [ ] A deliberately broken Unity build makes the script exit **non-zero** (prove it — this is
      the one that silently uploads stale binaries if wrong)
- [ ] `CIBuild.BuildIOS` activates the `iOS-Full` profile via the `BuildProfile` API, not `-activeBuildProfile`
- [ ] Batchmode build produces `Builds/iOS-Full/Unity-iPhone.xcodeproj` with the correct
      `CFBundleShortVersionString` and a `CFBundleVersion` equal to `git rev-list --count HEAD`
- [ ] `ITSAppUsesNonExemptEncryption` is present in the batchmode-generated `Info.plist`
- [ ] `ensure_git_status_clean` aborts the lane on a dirty tree
- [ ] `.p8`, `.env`, `Builds/ipa/` and `fastlane/report.xml` are all gitignored; `git status` is
      clean after a full run except for the guard file
- [ ] `Tools/mark-uploaded.sh` runs after upload and advances the guard file
- [ ] Unity Console / batchmode log has no errors related to this task
- [ ] Spec deviations flagged at the bottom of the report with justification

## Files this task touches

- `Assets/Editor/CIBuild.cs` — NEW
- `Tools/unity-build-ios.sh` — NEW
- `Tools/assert-unity-closed.sh` — NEW
- `Tools/mark-uploaded.sh` — NEW *if `upload_guard_automation` has not landed yet*
- `fastlane/Fastfile`, `fastlane/Appfile`, `fastlane/.env.example` — NEW
- `.gitignore` — additions listed above
- `Docs/TESTFLIGHT_RUNBOOK.md` — add the one-command path; keep the manual path as fallback

## Smoke evidence

**The end-to-end run is Cesar-only** — it needs the `.p8` key, and it uploads a real build to
a real App Store Connect record. The implementer verifies everything up to `build_app` and
then stops.

Specifically, the implementer CAN and MUST verify: the batchmode Unity build end to end, the
generated `Info.plist` values, the non-zero exit on failure, the lock check, and the gitignore
hygiene. The implementer CANNOT verify `upload_to_testflight` — do not fake it, do not mark it
PASS, flag it as awaiting Cesar.

## Out of scope (do NOT do these)

- `match` / a certificates repo — automatic signing already works on this machine
- Android, or a second lane of any kind
- CI runners, GitHub Actions, Xcode Cloud
- Changelog or release-notes automation (blocked by `skip_waiting_for_build_processing`)
- External testing groups or Beta App Review submission
- Touching `BuildStampGenerator.cs` numbering logic, or the guard's semantics
- Creating the App Store Connect API key — Cesar does that by hand
