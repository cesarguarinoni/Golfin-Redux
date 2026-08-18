READY_FOR_ARCHITECT_REVIEW

# STATUS — `upload_guard_automation`

**Current:** `READY_FOR_ARCHITECT_REVIEW`

**Spec written:** 2026-08-17 (Architect)
**Implemented:** 2026-08-18 (Claude Code, main thread — build tooling, no UI/Figma/scene work,
so the subagent chain does not apply)
**Origin:** fell out of Order 424 `testflight_distribution`. The 2026-08-17 upload of
`1.5.7 (2192)` shipped without anyone running
`GOLFIN → Build → Mark Current Commit As Uploaded`, leaving
`Docs/Versioning/last_uploaded_build.txt` at `0` and the regression guard inert.

## Blocking on Cesar

One acceptance item is **not** claimed as PASS and needs a human: a real **Product → Archive**
must be observed to advance the guard file. The post-action has already been injected into the
existing `Builds/iOS-Full` project, so this can be tested **without a Unity rebuild** — see
`IMPLEMENTER_REPORT.md` § "Needs manual on-device verification".

## History

| Date | State | Note |
|---|---|---|
| 2026-08-17 | `SPEC_READY` | Spec authored. Approach chosen by Cesar: Xcode Archive post-action, injected from Unity so it survives Replace builds. ASC API approach explicitly rejected. |
| 2026-08-18 | `READY_FOR_ARCHITECT_REVIEW` | `Tools/mark-uploaded.sh` + `Assets/Editor/iOSArchivePostAction.cs` implemented. Both spec `NOTE` markers verified empirically (scheme lives in `xcshareddata`, confirmed across 3 real builds; `$PROJECT_DIR/../..` confirmed and now *computed* rather than assumed). 11/11 checklist items PASS at the build-callback level; the real-archive execution is flagged for Cesar. Nothing committed — pre-existing uncommitted drift outside the task folder (CLAUDE.md rule 12). |
