READY_FOR_ARCHITECT_REVIEW

# STATUS — `fastlane_testflight_pipeline`

**Current:** `READY_FOR_ARCHITECT_REVIEW`

**Spec written:** 2026-08-17 (Architect)
**Implemented:** 2026-08-18 (Claude Code, main thread — build tooling, no UI/Figma/scene work,
so the subagent chain does not apply)
**Origin:** follow-on from Order 424. The 2026-08-17 upload proved the manual path; this
automates it.

## Built and verified

Every acceptance item is measured in `IMPLEMENTER_REPORT.md`; the highlights:

- `Assets/Editor/CIBuild.cs`, `Tools/unity-build-ios.sh`, `Tools/assert-unity-closed.sh`,
  `fastlane/Fastfile` + `Appfile` + `.env.example`, `.gitignore`, runbook § "One command".
- **A failed batchmode build exits 1** — proven twice with a deliberate `BuildFailedException`
  inside `BuildPlayer`. This was the acceptance item that, done wrong, silently uploads stale
  binaries.
- **A successful batchmode build** produces `Builds/iOS-Full/Unity-iPhone.xcodeproj` with
  `CFBundleShortVersionString 1.5.7`, `CFBundleVersion` = `git rev-list --count HEAD` and
  `ITSAppUsesNonExemptEncryption false`, read back with `PlistBuddy`.
- The lock check fails readably with the Editor open and passes with it closed.
- Both success and failure now leave `ProjectSettings.asset` clean (see report Findings §1 —
  a real defect found and fixed, without touching `BuildStampGenerator`).

## ✅ PROVEN END TO END — 2026-08-18 11:05 JST

`fastlane ios testflight_build` ran clean from a committed tree to a live TestFlight build:
**`LANE EXIT=0`, 11 min 27 s, all 8 steps green**, `1.5.7 (2201)` uploaded and independently
confirmed on App Store Connect as `state=VALID` via a read-only Spaceship query. The guard file
advanced `2192 → 2201`. **All 12 acceptance items PASS.**

Two real defects were found by running it, not by reading it:
1. A failed batchmode build left `ProjectSettings.asset` dirty (fixed in `CIBuild`).
2. The first end-to-end attempt died 3 s into `build_app` with `invalid byte sequence in
   US-ASCII` — fastlane's gym regexing xcodebuild's `➜` under a US-ASCII Ruby. Not a build
   failure. Fixed with `Tools/testflight.sh`, which exports the locale before fastlane starts.
   The `LC_ALL` in `fastlane/.env` does NOT fix this and had been wrongly recorded as doing so.

## Remaining for Cesar

1. **Commit `Docs/Versioning/last_uploaded_build.txt`** (now `2201`) — the one file the lane
   leaves dirty, by design. Not committed here: the tree carries unrelated in-flight
   `Tools/admin-dashboard/` + `home_notices` work and CLAUDE.md rule 12 halts a close-out on
   drift outside the task folder.
2. **Confirm the build reached `In-House Testers`** — the lane passes no `groups:` on purpose
   (fastlane's `groups:` is external-only and the internal group auto-distributes). Worth
   eyeballing once that this holds for a fastlane-uploaded build.
3. **Optional, one line in `~/.zprofile`** — locale + `brew shellenv`, so plain
   `fastlane ios testflight_build` works from any shell. `Tools/testflight.sh` already covers
   the common path.

**Unity Editor was quit** to run the batchmode builds (Cesar approved, 2026-08-18) and left
closed.

## Decisions already taken — do not re-litigate

| Question | Answer | Why |
|---|---|---|
| fastlane vs hand-rolled shell | **fastlane** | Cesar's call, 2026-08-17. Less script to own; handles ASC auth. |
| `match` for signing | **No** | Automatic signing works; a certs repo is overhead for one machine. |
| Ruby source | **Homebrew fastlane** | System Ruby is 2.6.10 — EOL, Apple-deprecated. Do not install gems against it. |
| Wait for processing | **No** (`skip_waiting_for_build_processing: true`) | Lane returns in minutes. Costs changelog support; internal testers don't read them. |
| Assign `groups:` | **No** | fastlane's `groups:` is external-only; `In-House Testers` auto-distributes already. |
| Homebrew install route | **Cesar runs it** | Chosen 2026-08-18 over a user-prefix Homebrew or a source-built rbenv Ruby, both non-standard and slow. |

## History

| Date | State | Note |
|---|---|---|
| 2026-08-17 | `SPEC_READY` | Spec authored. Supersedes the Xcode-post-action half of `upload_guard_automation` for automated runs — see that interaction section in SPEC.md. |
| 2026-08-18 | `READY_FOR_ARCHITECT_REVIEW` | Implemented. One defect found and fixed: a failed batchmode build used to leave `ProjectSettings.asset` dirty and block the next lane run. |
| 2026-08-18 (later) | `READY_FOR_ARCHITECT_REVIEW` | Cesar installed fastlane and created the API key mid-task; key proven to authenticate against the live `Golfin Game` record, read-only. |
| 2026-08-18 11:05 | `READY_FOR_ARCHITECT_REVIEW` | **Ran end to end.** `LANE EXIT=0` in 11 min 27 s; `1.5.7 (2201)` uploaded, confirmed `VALID` on App Store Connect; guard advanced `2192 → 2201`. **12 of 12 acceptance items PASS.** Second defect found and fixed en route: the US-ASCII locale crash in `build_app` (`Tools/testflight.sh`). Also disproved the spec's premise that scheme post-actions don't fire under `xcodebuild` — it fired, and idempotency made the double-fire a no-op (report Findings §8). |
