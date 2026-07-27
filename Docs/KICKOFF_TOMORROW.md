# Kickoff for next session

> **Last session:** 2026-07-23. Localization sweep closed, login/signup Phase 1 shipped, Tournaments v1 epic closed, roadmap housekeeping done. This file is what to read when opening a fresh chat.

## State at end-of-session 2026-07-23

**Pipeline is IDLE.** `Docs/Specs/Active/` contains only `_TEMPLATE` — nothing in flight, nothing waiting on a review.

**Closed this session:**
- **Localization sweep (`10c`) — COMPLETE.** Ran audit-first (`localization_audit_tooling`) then 8 batch specs + `gameplay_localization_access`. Result: `LocalizedText` binders **32 → 232** (164 prefab / 68 scene); CSV **227 → 337** keys, **zero** missing JP, zero `[JP-TODO]` placeholders, no duplicate keys; hardcoded `.text` literals **79 → 59**. Also deleted 29 dead superseded prefabs under `Prefabs/Original/` (commit `1a398637a`). Evidence: 85 EN/JP screenshot pairs across the 8 specs + 2 demo videos in `Docs/Reports/Media/`.
- **`login_signup_screens` Phase 1 — DONE.** Four account screens (Login, Create Username, Sign Up, Email Confirmation) built as standalone editable prefabs in `Assets/Prefabs/UI/Account/`, registered in `ScreenManager`. All auth actions are deliberate `// TODO(Phase 2)` stubs — no backend calls.
- **Tournaments v1 EPIC — CLOSED.** All 13 sub-specs done. The T5 open risk (relaunch could double-claim a prize → RP duplication) was verified resolved: `SaveData.cs:25` has a persisted `claimed` flag, `ClaimPrize` guards on `_store.IsClaimed()`, and `SaveBackedEntryStoreTests` covers persistence-across-reload + idempotency.
- **Roadmap housekeeping.** Fixed 4 stale/duplicate Notion cards (`tree_aware_bot`, Tournaments epic, `localization_audit`, `11a — Auth`).

## NEXT: `phone_build_smoke_test` (P1, M)

**No spec written yet — it needs two answers from Cesar before it can be specced:**
1. **Target: iOS, Android, or both?**
2. **Is a physical device on hand?** (decides real-hardware vs simulator/emulator path)

**Why this is next.** GOLFIN is a mobile game that appears to have never been built to a device — no `Builds/` directory, no `.apk`/`.ipa`/`.aab` artifacts anywhere in the tree. Everything so far is editor/play-mode verified. Three reasons it's now urgent:
- **The localization sweep just added a JP font dependency.** `Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset` is 2.1 MB; `Assets/Fonts/` totals 23 MB. JP TMP atlases are a classic device-side problem — memory, build size, and runtime hitching when a dynamic atlas grows mid-scene. That path has only ever been validated in the editor.
- **It gates Phase 8/9.** `8a — FPS capture`, `8b — Memory profile`, `9a–9d` are all queued and their numbers are meaningless off-device.
- **Risk compounds.** Touch input, aspect ratios, shader variants, and IL2CPP stripping all fail *late*, and there is now a lot of UI built on unverified assumptions.

## BLOCKED: `11a — Auth` = Phase 2 of login/signup

⚠ **Waiting on Ken for the Supabase keys.** Do not spec this separately — the `11a — Auth` card was merged to *be* Phase 2 (promoted to P1, re-estimated M since Phase 1 built the screens and seams).

**Scope when unblocked:** wire the existing Phase-1 stubs to GPS/PLAYLIFE Supabase Auth — `auth.signUp({email,password})`, sign-in, password reset, Google/Apple OAuth, confirm-email flow, and `display_name` via `/user/update`.

**Reference:** `Docs/GPS/GPS_INTEGRATION_REFERENCE.md` + `GPS_UNITY_PORT_SPEC.md` §6 (`ApiClient` → `SupabaseAuthManager`).

🚩 **The Confluence GPS pages are OUTDATED (Cognito-era). Do not use them.** `Docs/GPS/` is the source of truth. (`Docs/GPS/` was Windows-only and uncommitted until 2026-07-22 — it is now on origin/main.)

🚩 **Pre-existing code to reconcile before wiring:** `Assets/Scripts/Auth/` already holds an earlier auth iteration (`AuthService`, `ISupabaseAuthClient`, `SupabaseAuthClient`, `MockSupabaseAuthClient`, `SupabaseConfig`, `OAuthUrlBuilder`, `OAuthCallbackParser`). Also the older `AUTH_*` CSV copy reads `Welcome Back` / `Continue with Google` vs the Figma's `LOGIN WITH EMAIL` / `Login with Google`.

**Locked decisions (Cesar):** username uniqueness/availability check deferred to v2/v3 · terms/privacy lives in Settings, not these screens · password rules are advisory client-side only, server is source of truth · flow = Sign Up → Email Confirmation → Login → Create Username.

## Alternatives if `phone_build_smoke_test` isn't the pick

**Quick P1 wins — specs already sitting in `Docs/Specs/Queued/`:**

```
Use the golfin-implementer subagent on "putter_aim_blue_line"
```

- `B-followup — Housekeeping` (P1, XS <2h)
- `B-followup — Lab-only verification gap` (P1, S)
- `putter_aim_blue_line` (P1, S) — spec ready in `Queued/`
- `Multi-club architecture refactor` (P1, M) — spec ready in `Queued/`; architectural, worth doing before more club-dependent features land on the old structure

**Full P1/P2 queue (verified 2026-07-23):**

| Item | Pri | Est | Phase |
|---|---|---|---|
| `B-followup — Housekeeping` | P1 | XS | Putter P1 |
| `B-followup — Lab-only verification gap` | P1 | S | Putter P1 |
| `putter_aim_blue_line` | P1 | S | Loop v2 |
| `phone_build_smoke_test` | P1 | M | Loop v2 |
| `Multi-club architecture refactor` | P1 | M | Loop v2 |
| `11a — Auth` (Phase 2) | P1 | M | Server — BLOCKED |
| `Auto-dirty layout cleanup` | P2 | M | Foundations |
| `C.6 — fpMath.Cos/Sin range-reduction repair` | P2 | S | Putter P1 |
| `Tooling: CaptureCore frozen-time fallback` | P2 | S | Loop v1 |
| `Pre-condition: canvas audit before closing Loop v2` | P2 | S | Loop v2 |
| `5a — Bot opponent pool` | P2 | M | Matchmaking |
| `5b — Matchmaking surface` | P2 | S | Matchmaking |
| `5c — Async result UI` | P2 | S | Matchmaking |

## Loose ends (small, not worth their own task)

**Localization residue — 2 user-visible English strings still hardcoded.** Fold into whatever ships next:
- `Assets/Scripts/UI/Roster/UI/LevelUpModalController.cs:270` — `"MAX"` shows in English to a JP player at max level.
- `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs:386` — `"Bio coming soon."` last-resort fallback (the main bio path was localized in `b4e5b474e`).

The other 57 remaining hardcoded `.text` literals were spot-checked and are legitimate: runtime-overwritten placeholders, `// TODO: load real value` server stubs (e.g. `HomeScreenController`'s `"Player"`), and editor-only scripts.

**Localization guard now in place:** `UIFidelityLinter` has a `LocalizationHealth` layer emitting `unlocalized-text` at **WARN** severity (`UIFidelityLinter.cs:211`). It is deliberately WARN, not FAIL — a FAIL would red-gate Rule 21 across every task. Leave it WARN unless the whole project is bound.

**Audit tool available:** `Tools/Localization/Audit Project` (`Assets/Editor/Localization/LocalizationAudit.cs`) re-runs the classification and writes `Docs/Reports/localization_audit_<date>.{csv,md}`.

## Standing reminders for a fresh chat

- **Repo is on the Mac:** `/Users/cesar/Documents/GolfinRedux`. Cesar also works on a Windows box (`C:\Users\cesar\GolfinRedux`) — if a referenced file isn't here, check whether it was committed from the other machine before assuming it doesn't exist.
- **Verify "Done" against actual repo state** before accepting it (STATUS.md + the deliverables on disk + `git status`), rather than taking a report at face value. This has caught real gaps.
- **Notion roadmap** = `GOLFIN_Roadmap` database, data source `364b3e97-02b7-8190-b82b-000ba7847856`. Task rows use Item(title)/Status/Priority/Order/Estimate/Phase/Description/Notes, with `Notes` carrying `SPEC: <path>`. Valid Status values: Queued / In Progress / Done / Deferred (there is no "Blocked").
- **Kickoff lines always go in a fenced code block**, in chat and in SPEC.md/STATUS.md — never inline backticks.

---
