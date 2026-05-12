# capture_core_frozen_time_fallback — Extend CaptureCore for MCP-frozen-time

> **STATUS:** Queued (drafted 2026-05-12 23:50 JST). Tier 3, small. Surfaced by `loop_v1_2d_hole_complete_and_result_screen` iter-12 postmortem; see `Docs/Architecture/REVIEW_PIPELINE_FIXES.md` § 8.

## One-line

Extend `CaptureCore.SnapPlayModeSafe` to detect MCP-frozen-time conditions and fall back to `ScreenCapture.CaptureScreenshotAsTexture()` cleanly without mutating any scene state. This makes `CaptureCore` the only sanctioned capture path in the project — no per-task workarounds tolerated — and makes the `CLAUDE.md § Screenshots` rule 6 trivially enforceable.

## Why

Iter-12 of `loop_v1_2d_hole_complete_and_result_screen` hit a condition where Unity's `Time.frameCount` did not advance under MCP control (MCP-frozen-time), so the `WaitForEndOfFrame` capture path stalled indefinitely. The implementer wrote a custom orthographic-camera-render workaround that deactivated 10 ShotUI GameObjects in `LabScaffold.unity` as a side effect and saved the scene with the broken state. Reviewers approved because the screenshot looked fine; the corruption surfaced only when Cesar launched normal play.

If `CaptureCore.SnapPlayModeSafe` had handled MCP-frozen-time natively, the implementer wouldn't have invented a new capture path with no try/finally restore. Closing this gap is the structural fix that makes rule #3 ("capture paths must not mutate scene state") from REVIEW_PIPELINE_FIXES.md trivially enforceable: there is exactly one capture path, and it handles every supported case.

## Scope

1. **Frozen-time detector** in `CaptureCore.SnapPlayModeSafe`. Heuristic: enter the capture path, record `Time.frameCount` + `Time.realtimeSinceStartup`; wait up to ~500 ms wall clock; if neither has advanced, classify as frozen-time and switch to fallback.
2. **Fallback path:** `ScreenCapture.CaptureScreenshotAsTexture()` (synchronous, RT-backed, no scene mutation). Encode and write to the same `Docs/Diagnostics/_capture/` output location with a `_fallback` suffix on the filename so postmortems can identify which path took the capture.
3. **Symmetric extension to `SnapAtEndOfFrameAndPause`** — lower priority because the editor-side at-rest capture path rarely hits MCP-frozen-time. Could be a follow-up if not in this spec.
4. **Tests:** 1 EditMode test mocks `Time.frameCount` non-advancement and asserts the fallback returns a non-null path with `_fallback` in the filename; 1 EditMode test asserts normal-time path is unchanged.
5. **Docstring** on `CaptureCore` documenting both paths + the "no per-task workarounds" rule with a pointer to `CLAUDE.md § Screenshots` rule 6.

## Out of scope

- Visual-fidelity parity between primary and fallback paths. The fallback just needs to produce *a* valid screenshot of the Game View; pixel-perfect parity with the primary path is not required.
- Replacing `ScreenCapture.CaptureScreenshot` usage elsewhere — already banned by CLAUDE.md and unrelated to this fix.
- Fixing MCP-frozen-time itself (MCP-side issue; this spec just makes the project robust to it).

## Hard rules

1. Do NOT mutate scene state (no `SetActive`, no Transform changes, no scene saves) in either path. The whole point is that the fallback is clean.
2. Do NOT call `AssetDatabase.Refresh()` from `SnapPlayModeSafe` — the existing note in `CLAUDE.md § Screenshots` Quick Reference is load-bearing (refresh causes domain reload, kills coroutines).
3. Bit-exact pre-existing test gate must hold; +2 new tests = baseline+2 target.

## Definition of done

- `CaptureCore.SnapPlayModeSafe` returns a valid path under both normal-time and MCP-frozen-time conditions, with no scene mutation either way.
- 2 new EditMode tests PASS.
- `CLAUDE.md § Screenshots` rule 6 updated to reference this spec as closed (remove the "see `.../SPEC.md`" pointer or flip it to past-tense).
- Lesson written to `Docs/Diagnostics/PIPELINE_LESSONS.md` documenting why "one capture path" is the durable rule.

## Estimate

Half-day. Low risk. Architectural shape is unambiguous.

## Notion

TBD — add P2/Medium entry under §Pipeline Hygiene when this is ready to pick up.

## References

- `Docs/Architecture/REVIEW_PIPELINE_FIXES.md` § 8 — the postmortem that surfaced this.
- `loop_v1_2d_hole_complete_and_result_screen` iter-12 IMPLEMENTER_REPORT (under `Docs/Specs/Active/...`) — concrete example of the bad workaround pattern.
- `CLAUDE.md § Screenshots` rule 6 — the rule this spec exists to make enforceable.
