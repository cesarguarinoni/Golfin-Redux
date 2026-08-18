READY_FOR_ARCHITECT_REVIEW

# STATUS — `fastlane_testflight_pipeline`

**Current:** `READY_FOR_ARCHITECT_REVIEW`

**Spec written:** 2026-08-17 (Architect)
**Implemented:** 2026-08-18 (Claude Code, main thread — build tooling, no UI/Figma/scene work,
so the subagent chain does not apply)
**Origin:** follow-on from Order 424. The 2026-08-17 upload proved the manual path; this
automates it.

## Built and verified

Everything the implementer can reach without an App Store Connect key is done and measured —
see `IMPLEMENTER_REPORT.md`:

- `Assets/Editor/CIBuild.cs`, `Tools/unity-build-ios.sh`, `Tools/assert-unity-closed.sh`,
  `fastlane/Fastfile` + `Appfile` + `.env.example`, `.gitignore`, runbook § "One command".
- **A failed batchmode build exits 1** — proven twice with a deliberate `BuildFailedException`
  inside `BuildPlayer`. This was the acceptance item that, done wrong, silently uploads stale
  binaries.
- **A successful batchmode build** produces `Builds/iOS-Full/Unity-iPhone.xcodeproj` with
  `CFBundleShortVersionString 1.5.7`, `CFBundleVersion 2195` (= `git rev-list --count HEAD`) and
  `ITSAppUsesNonExemptEncryption false`, read back with `PlistBuddy`.
- The lock check fails readably with the Editor open and passes with it closed.
- Both success and failure now leave `ProjectSettings.asset` clean (see report Findings §1 —
  a real defect found and fixed, without touching `BuildStampGenerator`).

## Blocked on Cesar (not on Code)

1. ~~**fastlane**~~ — **DONE 2026-08-18**, Cesar installed Homebrew + `brew install fastlane`
   (2.238.0 on vendored ruby 4.0.6; system Ruby 2.6.10 untouched). The lane has since been run
   for real against a dirty tree and correctly aborted at `ensure_git_status_clean` with exit 1.
   Open nit: `brew shellenv` is not in `~/.zprofile`, so `fastlane` is not on PATH in a fresh
   shell — one line, `IMPLEMENTER_REPORT.md` § Findings 5.
2. ~~**App Store Connect API key**~~ — **DONE 2026-08-18.** Team key `golfingame`
   (`D63D7CJR92`, Admin) created by Cesar; `.p8` at `~/.appstoreconnect/`, mode 600, verified as
   a real EC key with `openssl pkey`. `fastlane/.env` filled in and confirmed gitignored.
   Details in `IMPLEMENTER_REPORT.md` § API key.
3. **The end-to-end lane run.** `build_app` and `upload_to_testflight` are code-complete but
   **unexecuted** — the first real run uploads a build to a live App Store Connect record, which
   is a human decision, not one to make on his behalf. Flagged AWAITING, never PASS.
   (`ensure_git_status_clean` is no longer in this list — it now PASSES, run for real.)

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
| 2026-08-18 (later) | `READY_FOR_ARCHITECT_REVIEW` | Cesar installed fastlane and created the API key mid-task, so three items moved off AWAITING: **10 of 12 acceptance items now PASS**, 1 PARTIAL (`mark-uploaded.sh` call site proven, real invocation waits on the lane run), 1 AWAITING (`build_app` + `upload_to_testflight` — the first run uploads for real). Key proven to authenticate against the live `Golfin Game` record, read-only. |
