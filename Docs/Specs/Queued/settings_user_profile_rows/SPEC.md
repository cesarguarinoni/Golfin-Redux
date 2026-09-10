# SPEC — `settings_user_profile_rows` (EMAIL / ACCOUNT ID / DELETE ACCOUNT)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Queued 2026-09-10. **Not yet `SPEC_READY`** — §3 carries two decisions that are Cesar's, not an implementer's (see § Decisions still needed). The rest is researched and ready.

## Where this came from

`asset_loans_offers` had to add a LOAN OFFERS row to the Settings ▸ User Profile submenu, and its SPEC anchored the row "after the DELETE ACCOUNT block". **There is no DELETE ACCOUNT block.** The Figma frame draws USERNAME → CHANGE → **EMAIL** → **ACCOUNT ID** → **DELETE ACCOUNT** → LOAN OFFERS; the shipped `UserProfileSubmenu` holds USERNAME, an input, SAVE/CHANGE, a feedback line, an inactive `AccountLinkingSection`, and now LOAN OFFERS. Three of the node's rows have never existed in the game.

That was surfaced as deviation **D-1** in `Docs/Specs/Completed/asset_loans_offers/IMPLEMENTER_REPORT.md` (path while in flight: `Docs/Specs/Active/…`), honoured as "last in the submenu", and flagged by `golfin-reviewer` as a non-blocking scope call. Cesar's answer, 2026-09-10, was that the call was mine — hence this file rather than a note that evaporates.

## Reference

`Docs/Specs/Active/asset_loans_offers/reference/Settings_LoanOffers_14261-109878.png` (copy it into this task's `reference/` at spec-ready time). Node **`14261:109878`**; the row-level node for the toggle family is `14261:109994`.

⚠️ **The node render clips.** It shows only SOUND SETTINGS / LANGUAGE / TERMS OF USE / PRIVACY POLICY / FAQ / ABOUT / CONTACT FORM — the live list also has GRAPHICS, CONTROLS and LOG OUT. Do not treat the node's list as the intended list; only the *User Profile submenu contents* are in scope here.

## What the node asks for

| Row | Node | Shipped today |
|---|---|---|
| USERNAME + value + CHANGE (silver pill) | present | **present** |
| **EMAIL** + value (`hello@gmail.com`) | present | **absent** |
| **ACCOUNT ID** + value (`1234-1234-1234-1234`) | present | **absent** |
| **DELETE ACCOUNT** (amber/orange pill, full-width of the CHANGE column) | present | **absent** |
| LOAN OFFERS + toggle | present | present (shipped by `asset_loans_offers`) |

Labels are the same family as everything else in `SettingsList`: **Rubik SemiBold, fontSize 48** for the label, **Regular 30** for the sub-line. Do not re-derive this from `sizeDelta` — that was the defect `asset_loans_offers` iter-1 shipped and iter-2 fixed (its *Defects found and fixed*, item 6).

## Research already done (2026-09-10) — do not re-litigate, verify

- **EMAIL exists server-side but is not exposed.** `playlife/backend/auth.py` `get_current_user` returns `{"id": ..., "email": user.user.email}` from the Supabase auth user. `profiles` has **no** email column, and `GET /user/detail` selects from `profiles` — so the email never reaches the client today. Cheapest route: have `/user/detail` merge `email` from the authenticated user rather than adding a column (no migration, no sync problem, and no second copy of a PII field).
- **ACCOUNT ID** is the same auth user id, already in hand at every authenticated call. The node formats it `1234-1234-1234-1234`; a Supabase user id is a UUID, so decide whether to show the UUID, a grouped slice of it, or a separate short human-quotable code. **This matters for support** — it is the string a player will read out to Cesar.
- **DELETE ACCOUNT does not exist anywhere.** No client path, no `routers/user.py` endpoint, no RPC. It is net-new on both sides.

## Why this is more than cosmetic

**App Store Review Guideline 5.1.1(v)** requires apps that let users create an account to also let them initiate account deletion *from within the app*. GOLFIN creates accounts in-app. If that guideline applies as written, **DELETE ACCOUNT is a submission requirement, not a nice-to-have** — worth confirming against the current guideline text before scheduling, but it is the reason this task should not sit in Queued indefinitely.

## Decisions still needed (Cesar / architect — this is why STATUS is not `SPEC_READY`)

1. **What "delete" means.** Hard delete of the auth user and every owned row, or a soft anonymise (tombstone the profile, scrub PII, keep the rows that other players' history references — loans, tournaments, rankings, gacha audit)? **This is the load-bearing decision.** A hard delete of a player who is mid-loan, mid-tournament, or in someone's rankings history will leave dangling references; `asset_loans_offers` alone gives a deleted player rows in `golfin_loans` that the *other* party still needs to see resolve.
2. **The confirmation gate.** Type-to-confirm, a typed username, a re-auth, or a plain two-step modal — and whether there is a grace/undo window.

## Out of scope

The `AccountLinkingSection` that already exists inactive in the scene; the missing GRAPHICS / CONTROLS / LOG OUT question (they exist in the game and merely aren't in the node render); anything about the LOAN OFFERS row, which shipped and passed.

## Reuse mandate (§1)

Author **zero** new panels or buttons. EMAIL and ACCOUNT ID clone the existing USERNAME label/value pair inside `UserProfileSubmenu`. DELETE ACCOUNT clones the CHANGE pill's geometry and swaps the sprite for the amber/destructive variant — find the real sprite in `Docs/Architecture/UI_ELEMENT_PALETTE.md` first; if no amber pill exists, **surface it, do not hand-roll a flat fill** (Rule 19, Cesar's standing rule). The confirm modal clones `ModalController` + the existing confirm-modal shell (`LoanRescindModal` is the most recent example of that shell being cloned correctly).

## Strings

Every new label bilingual EN + JA through the content pipeline — `import_content.py` plan → `--apply` → `content_publish` RPC → re-export → `--check` clean → commit `content_version.txt`. Publishing is part of the task, not a note at Done.

## Files likely touched

`Assets/Scripts/UI/UserProfileSubmenu.cs`, `Assets/Scripts/UI/SettingsController.cs`, `Assets/Scripts/Social/UserDetailDto.cs`, `Assets/Scenes/ShellScene.unity`, `Assets/Localization/LocalizationText.csv`, and server-side `playlife/backend/routers/user.py` (+ a migration if deletion needs one).
