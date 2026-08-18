# SPEC — `upload_guard_automation`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

See `STATUS.md`. Currently `SPEC_READY`.

## Goal

Remove the manual `GOLFIN → Build → Mark Current Commit As Uploaded` step. Today
`BuildStampGenerator`'s regression guard reads `Docs/Versioning/last_uploaded_build.txt`,
and that file is only written by a menu item a human has to remember. It was **not** run
after the 2026-08-17 upload of `1.5.7 (2192)` — the file still reads `0`, so the guard has
been inert since it was written. A safety net that depends on memory is not a safety net.

Replace it with an **Xcode Archive post-action**, injected into the generated scheme from
Unity so it survives project regeneration.

## Reference

N/A — build tooling, no UI, no Figma.

## Architecture context

- **Existing code:** `Assets/Editor/BuildStampGenerator.cs` — `IPreprocessBuildWithReport` +
  `IPostprocessBuildWithReport`. Sets build number from `git rev-list --count HEAD`, restores
  the pre-build values in `OnPostprocessBuild` to keep `ProjectSettings.asset` clean.
  Guard constant: `GuardFileRel = "Docs/Versioning/last_uploaded_build.txt"`.
  Menu item ~line 190: `[MenuItem("GOLFIN/Build/Mark Current Commit As Uploaded")]`.
- **Existing code:** `Assets/Editor/iOSPostProcess.cs` — `[PostProcessBuild(1000)]`, writes
  `ITSAppUsesNonExemptEncryption` into the generated `Info.plist`. Same shape as what this
  task needs; follow its conventions.
- **Build output:** `Builds/iOS-Full/` (gitignored via `.gitignore:27` `[Bb]uilds/`).
- **Asmdef:** none — both files live in the default Editor assembly under `Assets/Editor/`.

## Why this can't just be added in Xcode by hand

Unity regenerates `Unity-iPhone.xcodeproj` — schemes included — on every **Replace** build.
A post-action added through Xcode's Edit Scheme UI is destroyed the next time Cesar runs a
Replace. It has to be written by Unity on every iOS build, exactly like the Info.plist key.

## Implementation

### 1. `Tools/mark-uploaded.sh` (new, tracked, executable)

Plain shell. No Unity dependency — must be runnable by hand for debugging.

- Resolve the repo root from `$1`, defaulting to the script's own parent directory.
- `NEW=$(/usr/bin/git -C "$REPO" rev-list --count HEAD)` — **absolute path to git**; Xcode
  post-action environments do not reliably inherit a useful `PATH`.
- Read the current value from `Docs/Versioning/last_uploaded_build.txt` (missing or
  non-numeric ⇒ treat as `0`).
- **Write only if `NEW > CURRENT`.** Never regress. **Exit 0 either way** — a failing
  post-action is invisible in Xcode and would be worse than useless.
- Append to `Docs/Versioning/.mark-uploaded.log` on every run: timestamp, old value, new
  value, whether it wrote, short SHA. **This log is the only diagnostic.** Gitignore it.
- Do NOT `git commit` the guard file. Cesar commits it with his next change; auto-committing
  from a build post-action is a surprise nobody wants.

### 2. Scheme injection — extend the existing iOS post-process

Add to `Assets/Editor/iOSPostProcess.cs` (or a sibling file in the same folder — implementer's
call), leaving the existing `ITSAppUsesNonExemptEncryption` behaviour untouched.

In a `[PostProcessBuild]` callback, when `target == BuildTarget.iOS`:

- Locate the generated scheme. Expected at
  `<pathToBuiltProject>/Unity-iPhone.xcodeproj/xcshareddata/xcschemes/Unity-iPhone.xcscheme`.
  **NOTE:** verify this against a real Unity 6000.3.9f1 iOS build before relying on it —
  Unity has moved schemes between `xcshareddata` and `xcuserdata` across versions. If the
  file is absent, log a warning and return; do **not** throw and fail the build.
- Parse the `.xcscheme` as XML (`System.Xml.Linq` — it is plain XML, not a plist).
- Find `<ArchiveAction>`. Insert a `<PostActions>` child containing one `<ExecutionAction>`
  of type `Xcode.IDEStandardExecutionActionsCore.ExecutionActionType.ShellScriptAction`,
  whose `<ActionContent>` has `title="Mark commit as uploaded"` and a `scriptText` invoking
  `Tools/mark-uploaded.sh`.
- Set the `<EnvironmentBuildable>` buildable reference so Xcode's "Provide build settings
  from" resolves to the `Unity-iPhone` target — **without this `$PROJECT_DIR` is empty** and
  the script cannot find the repo.
- **Idempotent:** if a `<PostActions>` with this title already exists, replace it rather than
  appending a second copy.
- Write the XML back preserving the declaration.

Script text, roughly:

```
"$PROJECT_DIR/../../Tools/mark-uploaded.sh" "$PROJECT_DIR/../.."
```

`$PROJECT_DIR` is `Builds/iOS-Full`, so `../..` is the repo root. **NOTE:** confirm that
relative depth against the real build output rather than assuming it.

### 3. Leave the menu item in place

`GOLFIN/Build/Mark Current Commit As Uploaded` stays as a manual escape hatch, useful when an
upload happens from a machine or path the post-action didn't cover. Do not delete it.

## Known trade-off — document it in the header comment

The post-action fires on **archive**, not on **upload**. Archiving then discarding still
advances the guard. This is deliberate: over-strict is safe here, because the build number is
`git rev-list --count HEAD` and Cesar will have committed again before the next store build
anyway. Do not try to detect real upload success — Xcode does not expose it.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item marked `PASS`/`FAIL` with a one-sentence justification citing what was measured.

- [ ] `Tools/mark-uploaded.sh` exists, is executable (`chmod +x`), and is tracked in git
- [ ] Running it by hand from the repo root writes the current commit count to the guard file
- [ ] Running it again at the same commit does NOT rewrite and does NOT error
- [ ] Setting the guard file ABOVE the commit count, then running the script, leaves it
      unchanged (no regression)
- [ ] `Docs/Versioning/.mark-uploaded.log` is written on every run and is gitignored
- [ ] A fresh iOS build produces a `.xcscheme` containing `<PostActions>` under
      `<ArchiveAction>` — paste the actual XML fragment into the report
- [ ] Building twice does not produce two copies of the post-action
- [ ] The existing `ITSAppUsesNonExemptEncryption` key is still written (regression check)
- [ ] If the scheme file is missing, the build logs a warning and still succeeds
- [ ] Unity Console has no errors related to this task
- [ ] Spec deviations flagged at the bottom of the report with justification

## Files this task touches

- `Tools/mark-uploaded.sh` — NEW
- `Assets/Editor/iOSPostProcess.cs` — extended (or a new sibling Editor file)
- `.gitignore` — add `Docs/Versioning/.mark-uploaded.log`
- `Docs/TESTFLIGHT_RUNBOOK.md` — drop the manual mark step from the repeat-run loop

## Smoke evidence

Run the script by hand for the four guard-file cases above. Then do one real iOS build and
paste the generated `<PostActions>` XML into the report.

**Requires human-in-the-loop confirmation from Cesar** that an actual Product → Archive
advances the guard file — the implementer cannot verify Xcode post-action execution from
Unity alone. Flag this explicitly in the report rather than marking it PASS.

## Out of scope (do NOT do these)

- App Store Connect API integration — considered and rejected 2026-08-17 (key management for
  a problem whose worst case is one wasted archive)
- Removing the guard, the menu item, or the git-count scheme
- Android / `bundleVersionCode`
- Auto-committing or auto-pushing the guard file
- Touching `BuildStampGenerator.cs`'s numbering logic
