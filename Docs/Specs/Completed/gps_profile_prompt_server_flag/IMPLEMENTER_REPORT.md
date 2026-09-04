# IMPLEMENTER_REPORT — `gps_profile_prompt_server_flag` (Quick)

Built by Claude Code, 2026-09-04. Ordered ahead of `gps_standalone_shell` round 2 because that
round's R3 says this lands first — and because the shell's first launch is the case it fixes.

## Acceptance checklist

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | Migration applied + backfill count; `PUT` stamps; `GET` echoes; Fly deploy id | **PASS** | Migration was applied by Cesar before this task; re-derived from the live DB rather than taken on trust: **19 profiles, 3 with `golf_profile_prompted_at` NOT NULL, 3 with `avatar_color`/`golf_experience` set, and 0 "saved but unstamped"** — the backfill covered exactly its target set. The file `backend/migrations/2026_09_03_golf_profile_prompted.sql` is written as the record (idempotent, COALESCE-guarded so a re-run cannot move a stamp; `pglast` parses it). **Fly deploy: image `deployment-01M1MNKVRKBW4SGFQTAPC316DD`, machines 68 → 69**, verified three ways (flyctl image id, machine version, and the LIVE `openapi.json` now listing `golf_profile_prompted` on `UpdateProfileRequest` — it did not before). End-to-end against the deployed API: column cleared → `PUT {"display_name":"Cratilo","golf_profile_prompted":true}` → 200 with `golf_profile_prompted_at = 2026-09-03T22:21:00.729192+00:00`; a FRESH `GET` echoes the same value; a follow-up `PUT` with `false` leaves it **unchanged** — the one-way latch. |
| 2 | Editor: local cleared + server NULL → offered; SAVE stamps; relaunch → not offered, local re-cached | **PASS** | Play mode, standalone define on. Column cleared server-side, `ClearPrompted()` + `UserService.ResetForTest()` (a true fresh install): `[GpsAuthExtrasFlow] account flag resolved in 0.14s — prompted_at=null` → `[ScreenManager] gps_profile_prompt_on_entry — first GPS entry, GpsHub -> GpsGolfProfile` → screen `GpsGolfProfile` (`../gps_standalone_shell/screenshots/r2_shell_firstrun_clean_account_capture.png`). Re-cache proven in the mirror case (row 3). |
| 3 | Editor: server flag set, local cleared → NOT offered, straight to the hub | **PASS** | The case Cesar reported, and the one that matters most: `[StandaloneShellBoot] … routing to GpsHub` → `[ScreenManager] GpsHub — first entry on this install, resolving the account's Golf Profile flag before deciding.` → `[GpsAuthExtrasFlow] account flag resolved in 0.20s — prompted_at=set` → `[GpsAuthExtrasFlow] server says this account already answered the Golf Profile — caching the local flag; this install will never offer it.` → `ShowScreen called: GpsHub`. Final state `promptedLocally=True promptedOnAccount=True`, screen `GpsHub` (`../gps_standalone_shell/screenshots/r2_shell_firstrun_server_stamped_to_hub.png`). §5 re-cache is that third line. |
| 4 | Skip → the column stamped by the Skip PUT; body quoted | **PASS** | The real "Skip for now" button, driven through its own `onClick`: `[GpsGolfProfile] skipped — PUT /user/update {display_name, golf_profile_prompted:true}.` then `[GpsGolfProfile] skip recorded on the account`. Body pinned by `SkipBody_CarriesTheFlagAndNothingElse` and proven on the wire in row 1. **Caveat, since it is the honest reading:** the in-editor Skip ran BEFORE the Fly deploy, so that particular call 200'd without stamping (the deployed model dropped the unknown field — confirmed against the pre-deploy `openapi.json`). The same body against the DEPLOYED endpoint stamps, quoted in row 1. |
| 5 | Detail fetch failure: not offered, no crash, next entry retries | **PASS** by construction and asserted, not observed live (no way to fail one request in play mode without a proxy). `EnsureAccountFlagThen` waits at most `AccountFlagBudgetSeconds` (2.5 s) and then continues WITHOUT offering, logging `/user/detail did not answer within 2.5s — continuing to the hub WITHOUT offering (never nag; the next entry retries).` `UserService.EnsureDetail` marks `DetailAttempted` before it yields, so the resumed navigation cannot re-enter the wait and a dead network costs one 2.5 s hold per session, not per entry. `ShouldOffer` never sees a true from an unfetched row: `PromptedOnAccount_ReadsTheColumn_NullAndEmptyBothMeanNeverAsked` pins that an absent row reads as "do not claim answered". |
| 6 | EditMode: truth table (local × server × fetched) | **PASS** | `ShouldOffer_TruthTable_LocalTimesServer` (all four local×server rows plus both dominating reasons across every column), `PromptedOnAccount_ReadsTheColumn_NullAndEmptyBothMeanNeverAsked` (unfetched / NULL / empty / stamped), `NeedsAccountCheck_IsFalse_ForEveryScreenThatIsNotTheHub` (the fetched axis), `SkipBody_CarriesTheFlagAndNothingElse`. Full sweep **2398 tests, 2395 passed, 0 failed, 3 skipped**. |
| 7 | No prefab/string change (`git status Assets/Prefabs` empty; PLAN add 0) | **PASS** | `git status --porcelain Assets/Prefabs` is empty; no `Assets/Localization/**` change; no `LocalizationManager.Get` call added. The Skip button already existed — only what it does changed. |
| 8 | `GPS_DEVICE_PASS.md` §1 cross-app rows | **PASS** | See § below. |

## Design notes worth keeping

- **OR, never AND.** Either flag alone means answered. The local flag alone is the returning player
  and the offline launch; the server flag alone is a fresh install of either app. Requiring both
  would re-ask everyone the moment the network was down.
- **The wait is bounded at 2.5 s and fails to "do not offer".** `ApiClient.TimeoutSeconds` is 30 and
  this sits in front of a navigation; an unbounded wait would freeze a player on the Splash for half
  a minute with no spinner, which is worse than the thing being prevented. A missed offer costs one
  more entry; a wrong offer is the defect.
- **Skip sends the ACCOUNT's display name, not the nickname field.** The field is editable and may
  hold something half-typed or already taken; SAVE validates it and Skip does not, so sending the
  field's text could 409 — or silently rename the player — on the path where they declined to change
  anything.
- **Skip advances regardless of the write.** A failed write must not trap a player on a screen they
  just dismissed, and must not re-ask them on this device either. "Never nag" outranks the
  cross-install guarantee in the single case where they conflict; the failure is logged loudly.
- **`false` is "no opinion", not "un-ask".** No client path un-answers the screen, and accepting one
  would let a stale request re-open a prompt already dismissed on another device.
- **`now()` is computed server-side**, so a device with a wrong clock cannot write a timestamp from
  next year.
- **The timestamp is never parsed.** `UserDetailDto.GolfProfilePromptedAt` is a `string`, read only
  for null-vs-not. `ApiEnvelope` parses with `DateParseHandling.None` precisely so it arrives as the
  characters the server sent; the alternative is the bug that made a round checked in at 12:26 JST
  render "Since 21:26".

## The defect this found in round 1's own code

`StandaloneShellBoot.TryGetPostAuthScreen` resolved `GpsAuthExtrasFlow.InterceptHubEntry` itself.
That looked like honesty — the boot saying where it was really going — and became a bug the moment
the decision needed a round trip: it jumped straight over the account-flag wait in
`ScreenManager.Navigate`, so **a fresh shell install of a server-stamped account still showed the
capture** — the exact defect being fixed. The boot now names `GpsHub` and Navigate decides. Caught by
running the proof, not by reading the diff; pinned by `ShellBoot_HasTheSingleSharedEntryPoint`.

## `GPS_DEVICE_PASS.md` §1 — cross-app rows added

Two rows, both of which need two installs and cannot be checked in the Editor: complete or skip the
Golf Profile in the GAME then install the standalone → lands on the hub with no capture; and the
reverse. A third row covers a genuinely new account seeing it exactly once, in whichever app it
opens first.

## Needs manual on-device verification

- The two cross-app rows above (two installs of two different app records on one phone).
- A second device on the same account (the "or another phone" half of Cesar's ask) — the server
  column is device-independent by construction, but only a second phone proves it.
