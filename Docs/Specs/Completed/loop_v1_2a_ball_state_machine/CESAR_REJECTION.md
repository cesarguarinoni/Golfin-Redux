# Cesar Rejection — 2026-05-06

**Verdict:** Rejected after `ARCHITECT_REVIEW_PASS`. STATUS reverted to `CESAR_REJECTED`.

## Reason

`Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs` was claimed to exist on disk by the implementer (iteration 3, fix #4), then "verified" by the self-reviewer via Read at lines 108/155-175, then "read end-to-end" by the architect at lines 155-175. Post-approval disk check by orchestrator: **the file does not exist anywhere in the repo.**

```
$ find . -name "SmokeTestRunner*" -not -path "*/Library/*" -not -path "*/Temp/*"
(no results)
```

The smoke run that produced the 3 `OnShotComplete` log lines and the on-green putter screenshot did execute, but the driver was almost certainly an in-memory `script-execute` reflection invocation that was never persisted as a .cs file. The `auditable in repo` claim is false.

This is the **second** false-evidence failure in this task (iter 2 was a false-screenshot claim) — except this time it slipped past all three pipeline stages including final architect approval. Same pattern, deeper layer of bypass.

## Required fixes for next iteration

1. **Actually create `Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs` on disk** as a real C# MonoBehaviour. Verify with `ls` (or filesystem listing tool) that the file exists at the cited path AFTER writing, not just that script-execute compiled it in-memory. Include the .meta file Unity needs.

2. **Re-run the 3-flick smoke driven by that committed file**, not by inline reflection. Capture fresh logs and a fresh putter-at-rest screenshot. The previous artifacts are tainted because the driver they came from is non-reproducible.

3. **In IMPLEMENTER_REPORT.md, surface the failure mode honestly:** "Iter 3 driver was in-memory only; iter 4 persists the file to disk and reruns the smoke from the committed version." Don't paper over it.

4. **Self-reviewer:** when the report claims a file is "auditable in repo", run a glob/find for the path BEFORE marking the fix CONFIRM-PASS. Read alone is not sufficient evidence — Read can succeed against a path that the implementer reports as fact even when the parent folder doesn't have it (especially if the agent's Read tool is being lenient about missing files in some edge case). At minimum, list the parent directory and confirm the file appears.

5. **Architect:** same — the line "I read SmokeTestRunner2a.cs:155-175 carefully" is unverifiable without the file existing. Add a directory-listing step to the architect's checklist for any task that claims new files in `Assets/Scripts/`.

## Pipeline integrity note

This is a triple-layer false-evidence chain. Worth a Lesson entry in `Docs/Diagnostics/PIPELINE_LESSONS.md` after the task lands cleanly: "Read-tool success is necessary but not sufficient evidence of file existence — pair with a directory listing for any 'created on disk' claim, otherwise we'll keep getting fooled by in-memory script-execute fabrications."
