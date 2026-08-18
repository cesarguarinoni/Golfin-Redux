# IMPLEMENTER_REPORT — `upload_guard_automation`

**Iteration:** iter-1
**Baseline:** HEAD `0ec922ac8` · date 2026-08-18
**Implemented by:** Claude Code (main thread, direct implementation — not the subagent pipeline;
this is build tooling with no UI, no Figma node, no scene mutation, so Rules 18/19/21 do not apply)

---

## Pre-flight baseline (uncommitted at kickoff)

`git status --porcelain --untracked-files=all` at HEAD `0ec922ac8`, **before** any edit:

```
 M Docs/Architecture/ARCHITECTURE_AUDIT.md
 M Docs/TESTFLIGHT_RUNBOOK.md
 M Docs/TellCode.md
?? Docs/Specs/Active/fastlane_testflight_pipeline/
?? Docs/Specs/Active/upload_guard_automation/
```

Everything above is **pre-existing** and NOT introduced by this task — `ARCHITECTURE_AUDIT.md` is
the session-startup regeneration, and `TESTFLIGHT_RUNBOOK.md` / `TellCode.md` / the two `Active/`
spec folders were already dirty when this task started. `TESTFLIGHT_RUNBOOK.md` is now **also**
touched by this task (it is in the spec's file list), so its diff is mixed — see § Files below.

**Nothing was committed.** See § Handoff.

---

## Verification of the spec's two `NOTE` markers

The spec explicitly demanded these be verified rather than assumed. Both are **CONFIRMED**, and
neither needed a `NOTE:` flag in the code for being unconfirmable.

| NOTE | Question | Method | Result |
|---|---|---|---|
| SPEC §2 | Where does Unity 6000.3.9f1 emit the scheme — `xcshareddata` or `xcuserdata`? | `find Builds/{iOS-Full,iOS-Dev,iOS-Demo} -name '*.xcscheme'` across three real builds | **`xcshareddata`**, all three: `Unity-iPhone.xcodeproj/xcshareddata/xcschemes/Unity-iPhone.xcscheme`. `xcuserdata/cesar.xcuserdatad/xcschemes/` also exists but holds no `Unity-iPhone.xcscheme`. |
| SPEC §2 | Is `$PROJECT_DIR/../..` really the repo root? | Not hardcoded — the callback **computes** the depth from `pathToBuiltProject` vs `Application.dataPath`, then logs it. Unity Console: `repo root resolves to $PROJECT_DIR/../..` | **`../..` confirmed** for `Builds/iOS-Full`, and it is now derived rather than assumed, so a different output path stays correct automatically. |

---

## Acceptance checklist

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | `Tools/mark-uploaded.sh` exists, executable, tracked | **PASS** | `ls -l` → `-rwxr-xr-x`; `git status` lists it as `?? Tools/mark-uploaded.sh` — it is a new untracked file staged for Cesar's commit, not gitignored (`git check-ignore` returns nothing for it). |
| 2 | Run by hand from repo root writes the current commit count | **PASS** | Guard was `0`; `Tools/mark-uploaded.sh` → guard `2195`, which equals `git rev-list --count HEAD` = 2195. Exit 0. |
| 3 | Second run at the same commit does not rewrite and does not error | **PASS** | mtime identical before/after (`1787001627` → `1787001627`), guard still `2195`, exit `0`. Log line: `old=2195 new=2195 wrote=no  no advance (new <= current)`. |
| 4 | Guard ABOVE the commit count is left unchanged (no regression) | **PASS** | Set guard to `99999`, ran the script → guard still `99999`, exit `0`, log `old=99999 new=2195 wrote=no`. |
| 5 | `.mark-uploaded.log` written on every run and gitignored | **PASS** | 8 script runs → 8 log lines, including the no-write cases. `git check-ignore -v` → `.gitignore:265:Docs/Versioning/.mark-uploaded.log`. |
| 6 | A `.xcscheme` contains `<PostActions>` under `<ArchiveAction>` | **PASS** (build-callback level; see § Not verified) | The real `[PostProcessBuild(1001)]` callback was invoked against the genuine `Builds/iOS-Full` project. XML pasted below; `xmllint --noout` → OK; `xcodebuild -list` still parses the project and lists the `Unity-iPhone` scheme. |
| 7 | Building twice does not produce two copies | **PASS** | Callback invoked twice in a row against the same project → `occurrences of the post-action title in the scheme after 2 passes = 1`. |
| 8 | `ITSAppUsesNonExemptEncryption` still written (regression check) | **PASS** | Stripped the key from `Builds/iOS-Full/Info.plist` (`key present after strip = False`), re-ran `iOSPostProcess.OnPostprocessBuild` → `key present = True value=False`. `iOSPostProcess.cs` was **not edited** — zero diff. |
| 9 | Missing scheme → warning, build still succeeds | **PASS** | Invoked against `Builds/DoesNotExist` → `LogWarning` "Xcode scheme not found at … — upload-guard archive post-action NOT injected", and `missing-scheme path returned normally (no throw) = TRUE`. No `BuildFailedException`, no rethrow. |
| 10 | Unity Console has no errors related to this task | **PASS** | `console-get-logs` over the whole session: zero `Error`/`Exception` entries. The warnings present (`CS8632`, `CS0618`) are pre-existing, in `Assets/Scripts/UI/Inventory/Editor/*` and `Assets/Scripts/Editor/CourseImporter/*` — unrelated files, untouched by this task. |
| 11 | Spec deviations flagged | **PASS** | § Deviations below. |

### Extra tests beyond the checklist

| Test | Verdict | Evidence |
|---|---|---|
| Non-iOS target is a no-op | **PASS** | Invoked with `BuildTarget.Android` → `Android no-op preserved file: True` (byte-identical). Confirms nothing Android is touched. |
| Script survives a stripped `PATH` and a foreign cwd (real Xcode conditions) | **PASS** | `cd / && env -i PATH=/usr/bin:/bin PROJECT_DIR=… /bin/sh -c "<scriptText from the generated scheme>"` → guard `2192` → `2195`, exit `0`. This is the actual `scriptText` parsed back out of the scheme, not a paraphrase. |
| Bogus `$1` falls back to the script's own repo | **PASS** | `Tools/mark-uploaded.sh /nonexistent/path/nope` → guard written correctly, exit `0`. |
| Non-numeric / missing guard file treated as `0` | **PASS** | Wrote `not-a-number`, then deleted the file — both runs wrote `2195` and exited `0`. |
| C# compiles into the editor assembly | **PASS** | `Golfin.EditorTools.iOSArchivePostAction` resolves in `Assembly-CSharp-Editor`; `.cs.meta` generated (Lesson R). Active build target is `iOS`, so the `#if UNITY_IOS` body is really compiled, not skipped. |

---

## Generated `<PostActions>` XML (verbatim, from `Builds/iOS-Full/…/Unity-iPhone.xcscheme`)

```xml
  <ArchiveAction buildConfiguration="Release" revealArchiveInOrganizer="YES">
    <PostActions>
      <ExecutionAction ActionType="Xcode.IDEStandardExecutionActionsCore.ExecutionActionType.ShellScriptAction">
        <ActionContent title="Mark commit as uploaded" scriptText="&quot;$PROJECT_DIR/../../Tools/mark-uploaded.sh&quot; &quot;$PROJECT_DIR/../..&quot;&#xA;">
          <EnvironmentBuildable>
            <BuildableReference BuildableIdentifier="primary" BlueprintIdentifier="1D6058900D05DD3D006BFB54" BuildableName="Golfin.app" BlueprintName="Unity-iPhone" ReferencedContainer="container:Unity-iPhone.xcodeproj"></BuildableReference>
          </EnvironmentBuildable>
        </ActionContent>
      </ExecutionAction>
    </PostActions>
  </ArchiveAction>
```

`&quot;` / `&#xA;` are XML attribute escaping for `"` and newline — Xcode writes the same escapes.
The `BlueprintIdentifier` is **cloned from the scheme's own `BuildableReference`**, not hardcoded,
so it stays correct if Unity changes the target's UUID.

---

## ⚠️ Needs manual on-device verification (Cesar-only)

**One item cannot be verified from Unity and is NOT marked PASS above:**

> **Does a real `Product → Archive` in Xcode actually execute the post-action?**

Everything up to Xcode's own execution of the scheme is proven: the XML is well-formed, Xcode
parses the scheme, the exact `scriptText` from the scheme runs correctly under archive-like
conditions (stripped `PATH`, foreign cwd, `/bin/sh`). What no Unity-side test can prove is that
Xcode fires post-actions as expected on this machine's Xcode version.

**You can test this right now without rebuilding.** The post-action was injected into your
**existing** `Builds/iOS-Full` project, so:

1. Open `Builds/iOS-Full/Unity-iPhone.xcodeproj`
2. `Product → Archive` (destination: Any iOS Device)
3. Then check: `cat Docs/Versioning/last_uploaded_build.txt` → should read `2195`, not `2192`
4. And `cat Docs/Versioning/.mark-uploaded.log` → a new `wrote=yes` line

(Optional cross-check: `Product → Scheme → Edit Scheme → Archive → Post-actions` should show one
"Mark commit as uploaded" entry with "Provide build settings from: Unity-iPhone".)

**Known degraded case:** if `<EnvironmentBuildable>` were ever lost, `$PROJECT_DIR` expands empty
and the post-action exits `127` (`/../../Tools/mark-uploaded.sh: No such file or directory`) —
measured. It does not fail the archive (the archive is already complete), but the guard silently
would not advance. This is exactly why the `EnvironmentBuildable` is set and commented as
load-bearing.

---

## Files modified or created

| File | Status | 1-line summary |
|---|---|---|
| `Tools/mark-uploaded.sh` | **NEW** (executable, tracked) | Advances `last_uploaded_build.txt` to `git rev-list --count HEAD`, writes only on a strict increase, always exits 0, appends every run to the gitignored `.mark-uploaded.log`. |
| `Assets/Editor/iOSArchivePostAction.cs` | **NEW** | `[PostProcessBuild(1001)]` injects an idempotent Archive post-action into the generated `.xcscheme` that runs the script; computes the repo-root depth rather than assuming it; warns instead of throwing on any failure. |
| `Assets/Editor/iOSArchivePostAction.cs.meta` | **NEW** | Unity-generated meta, committed alongside the `.cs` (Lesson R). |
| `.gitignore` | **MODIFIED** | Adds `Docs/Versioning/.mark-uploaded.log` (+ a comment noting the guard file itself stays tracked). |
| `Docs/TESTFLIGHT_RUNBOOK.md` | **MODIFIED** | Repeat-upload box: nothing to remember post-upload; Phase 3 step 5 documents the archive post-action, the archive-not-upload trade-off, and where to look when the guard doesn't move. **Diff is mixed with pre-existing uncommitted edits from an earlier session.** |
| `Docs/Versioning/last_uploaded_build.txt` | **MODIFIED** | `0` → `2192`. See § Deviations. |
| `Docs/Specs/Active/upload_guard_automation/{STATUS,IMPLEMENTER_REPORT}.md` | NEW/MODIFIED | Pipeline bookkeeping. |
| `Docs/AI_CONTEXT.md` | **MODIFIED** | Session status. |
| `Builds/iOS-Full/**` (scheme + `Info.plist`) | touched, **gitignored** | Test target for the callbacks; the scheme now carries the post-action so Cesar can archive-test without rebuilding. `Info.plist` was restored to `ITSAppUsesNonExemptEncryption=false` by the regression test itself. |

`Assets/Editor/iOSPostProcess.cs` — **not modified. Zero diff.**

---

## Deviations from the spec

1. **New sibling file instead of editing `iOSPostProcess.cs`.** The spec allowed either
   ("implementer's call"). A sibling guarantees the export-compliance behaviour cannot be
   disturbed, and keeps two unrelated concerns (Info.plist vs scheme) independently readable.
   `iOSPostProcess.cs` has a literal zero-byte diff.

2. **The repo-root depth is computed, not hardcoded to `../..`.** The spec's script text was
   `"$PROJECT_DIR/../../Tools/mark-uploaded.sh"`. That is what gets **emitted** for
   `Builds/iOS-Full` (verified), but `RepoRootExpression()` derives it by walking from
   `pathToBuiltProject` up to `Application.dataPath`'s parent, falling back to an absolute path
   if the build is written outside the repo. Same output for the documented case, no silent
   breakage for any other. This is a strict superset of the spec.

3. **`Docs/Versioning/last_uploaded_build.txt` left at `2192`, not the `2195` the tests wrote.**
   The acceptance tests necessarily wrote the *current* commit count (2195 = HEAD `0ec922ac8`),
   but nothing was uploaded at 2195 — that would be a false record, and being the current HEAD it
   would refuse the very next store build. `2192` is the **truthful** value: the real
   `1.5.7 (2192)` upload of 2026-08-17 that this whole task exists because nobody recorded. So
   this change also *fixes the inert guard as a side effect*: it now reads 2192 instead of 0.
   Flagging because the spec did not ask for a value change, only for automation.

4. **The script falls back to its own location when `$1` isn't a git work tree.** Not specified;
   added because a wrong `$PROJECT_DIR` would otherwise silently no-op. The script physically
   lives in the repo, so its own path is the more trustworthy anchor.

5. **The runbook had no explicit manual mark step to delete.** The spec said "drop the manual
   mark step from the repeat-run loop" — but the manual step was never actually written into the
   repeat-run loop (that omission is precisely why it was forgotten). The edit therefore *adds*
   the now-automatic behaviour to the repeat-upload box and Phase 3 instead of removing prose,
   which satisfies the intent: the loop no longer requires a human mark step.

**Out-of-scope items confirmed untouched:** `BuildStampGenerator.cs` (zero diff — numbering logic,
guard read, and the `GOLFIN/Build/Mark Current Commit As Uploaded` menu item all intact), Android /
`bundleVersionCode`, App Store Connect API, auto-commit of the guard file.

---

## Handoff

**Nothing was committed.** Per CLAUDE.md rule 12, a close-out commit halts when uncommitted paths
live outside the task folder — and `Docs/TellCode.md`, `Docs/Architecture/ARCHITECTURE_AUDIT.md`
and the `fastlane_testflight_pipeline/` spec folder were already dirty before this task started.
Committing here would sweep another session's work into this change (the `k10`/`k11` failure mode).
Cesar decides how to split it.
