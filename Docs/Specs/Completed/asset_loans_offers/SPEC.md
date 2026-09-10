# SPEC — `asset_loans_offers` (lend to anyone; the recipient accepts or declines)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Starts at `SPEC_READY` (2026-09-10).

## Decisions of record (Cesar, 2026-09-10)

1. **Anyone can be lent to.** The lend modal gains a display-name search (existing `GET /user/search?q=`); the followed players stay listed underneath as the default suggestions. The `not_following` refusal is retired.
2. **A loan starts as an OFFER the recipient must ACCEPT or DECLINE.** It surfaces on the recipient's Home as a pill (like the Daily Mission pill) that opens an accept/decline modal. Nothing enters the recipient's roster until they accept.
3. **The asset is LOCKED from the moment it is offered** — the lender must not be mid-match with it when the recipient accepts. A decline, a rescind or an offer expiry unlocks it.
4. **Guard against offer-bombing.** A recipient can only hold a bounded number of pending offers, and a lender can only have one pending offer per recipient.
5. **Settings toggle "Loan offers"** (User Profile submenu). Off = the player is removed from search results and the followed list in every lend modal, and a direct lend to them is refused.
6. **The lender can rescind an unanswered offer**, and then cannot re-offer the same player until a cooldown passes (anti-spam).

Architect defaults — not Cesar's words, flag in the report if any bites: offer expires unanswered after **48 h** (`LOAN_OFFER_TTL_HOURS = 48`); a recipient holds at most **3 pending offers** (`LOAN_MAX_PENDING_IN = 3`); rescind/decline cooldown before the same lender can re-offer the same recipient: **24 h** (`LOAN_REOFFER_COOLDOWN_HOURS = 24`); pending offers **count toward** the existing 3-out limit; the loan's `days` clock starts at **accept** (`starts_at` is rewritten on accept, `ends_at = starts_at + days`) — the lock time during the offer is not deducted from the loan.

Takes up Notion deferrals **2212** (offer/accept + arrival notification) and **2216** (lend to non-followed players).

## Goal

Today a loan is instant and only to a followed player. After this task a player can offer a character or club to anyone by name; the offer locks the asset on the lender's side (OFFERED ribbon, RESCIND button) and shows up on the recipient's Home as a pill; the recipient opens it, sees the asset and the terms, and accepts (the loan becomes exactly today's active loan) or declines (the asset unlocks). Players who don't want offers switch them off in Settings and vanish from every recipient list. Nobody can be spammed: bounded pending offers per recipient, one pending offer per lender→recipient pair, and a cooldown after a rescind or decline.

## Build playbook (Figma-node screens)

This task builds from Figma nodes: the implementer and EVERY reviewer work `Docs/Architecture/FIGMA_SCREEN_BUILD_PLAYBOOK.md`; its § 7 self-diff is an acceptance line.

## Reference

- **Figma file** `5gEAHjl6xAtW8iYY7NMvWd`, frames designed 2026-09-10 (clones of the shipped screens; only the loan elements differ):
  - page **Home Screen** (`2098:3766`): `Home — Loan Offer Pill` **`14261:32997`** (pill `14261:33108`), `Home — Loan Offer Modal` **`14261:107063`** (modal `14261:107143`).
  - page **Characters Screen** (`2470:14192`): `Roster — Offered (lender)` **`14261:109119`**, `Roster — Lend Modal v2 (search)` **`14261:109475`** (search field `14261:109851`).
  - page **Settings** (`4060:841`): `Settings — Loan Offers toggle` **`14261:109878`** (row `14261:109994`, toggle `14261:109998`).
  - The Clubs panel gets the same OFFERED state as the Roster one (ribbon text + RESCIND in the LEND slot) — no separate frame; `Clubs — On Loan (lender)` `14183:108287` + this spec's table is the reference.
- **Node renders in `reference/`** (1170×2532): `Home_OfferPill_14261-32997.png`, `Home_OfferModal_14261-107063.png`, `Roster_Offered_14261-109119.png`, `Roster_LendModalV2_14261-109475.png`, `Settings_LoanOffers_14261-109878.png`.
- **Placeholder content:** KENJI / KENDRA / MARTA / LUCAS, "ken|", "46h", Elizabeth Blackwood RARE Lv 80 / 3 DAYS are mock values.
- **Polish atoms this task uses** (WORKFLOW_NOTES rule): `ModalController` for both new modals; `ButtonPressFeedback` on every new Button (pill, RESCIND, DECLINE/ACCEPT, toggle, search clear); `PendingSpend.Begin(button, label, alsoDisable)` around ACCEPT / DECLINE / RESCIND / the toggle's PUT; `UiSelection.Bump` on the picked search row (already on rows); the offer pill reuses `DailyMissionPillController`'s enter/leave motion and glow (`UiMotion` slide + `Pulse`); `LoanRibbonView` entrance unchanged (Rise + Fade); search results arrive via `GpsPaintMotion.StaggerRise` like the followed rows do, guarded by `SuppressedByPush`; **no shimmer** on the search (one ~200 ms request — the `GameShimmerSites` rule) — the results region simply repaints.

## Figma Fidelity (enumerate EVERY element — Rule 18)

| Element | Figma node | Property → value |
|---|---|---|
| Loan offer pill | `14261:33108` in `14261:32997` | A second pill under the Daily Mission pill in the same `Empty Space` column: same pill chrome (`Mission Card Container` detached — same sprite, stroke, 122 tall, HUG width, padding as the daily pill), **y = daily pill y + 122 + 40** (gap 40; when the daily pill is absent the offer pill takes its slot); icon = `IconLoanIn` 56×56 in the flame's slot; label `LOAN_PILL_FMT` "LOAN OFFER FROM {0}" Rubik SemiBold **39** (daily is 45 — one size down so a long name fits) `#FCF195`-family yellow as the daily label; glow/pulse as the daily pill |
| Offer modal panel | `14261:107143` | 780 wide, HUG, centred; the loan-modal chrome (gradient #133453→#091B33, 3 px white stroke, r 20, drop shadow 0/4/4 @ 25 %), VERTICAL gap 24 pad 24; behind it `Dark` #000 @ 50 % + blur 8 |
| Title | TEXT | `LOAN_OFFER_TITLE` "LOAN OFFER" Rubik SemiBold 45 white centred |
| Subtitle | TEXT | `LOAN_OFFER_FROM_FMT` "{0} wants to lend you" Regular 33 white centred |
| Asset row | `AssetRow` | 732×140, r 12, fill #050F1F @ 60 %, HORIZONTAL pad 20/24 gap 24 items centred: 100×100 portrait (r 12; the asset's `portraitSprite` / club thumbnail), name Rubik SemiBold 33 white, second line `{RARITY}` SemiBold 30 in the rarity colour (`RarityHelper`) + `Lv {level}` Regular 30 white + `· {days} DAYS` Regular 30 #BFD1E6 |
| Terms | TEXT | `LOAN_OFFER_TERMS_FMT` "You play with it and can level it up — the levels stay on it when it goes back. {0} gets {1}% of the RP you earn with it." Regular 30 white, width 732 |
| Fine print | TEXT | `LOAN_OFFER_FINE_FMT` "Offer expires in {0}. Turn offers off in Settings › User Profile." Regular 26 #BFD1E6 centred |
| Footer | `Footer` | DECLINE `Silver, Enabled=Yes` 354×120 + ACCEPT `Gold, Enabled=Yes` 354×120, gap 24, equal widths (gold sprite stretched, 9-sliced like the lend modal) |
| OFFERED ribbon (lender) | `LoanRibbon` in `14261:109119` | the shipped ribbon; text `LOAN_STATUS_OFFERED_FMT` "OFFERED TO {0} · {1}" where `{1}` = `LOAN_TIME_TO_ANSWER_FMT` "{0}h to answer"; icon `IconLoanOutBig`; **dim ON** (locked) |
| Lender buttons while offered | `14261:109119` | LEVEL UP / BOOST (REPAIR) / COMPARE / SELECT (EQUIP) disabled exactly as the lent state; the LEND slot becomes **RESCIND** (`LOAN_BTN_RESCIND`), `Silver -Small, Enabled=Yes`, 233×54 |
| Card badge while offered | card | the `IconLoanOutSmall` badge, same as lent |
| Lend modal v2 — LEND TO section | `14261:109851` + section | after the `LEND TO` label: **search field** 732×88, r 12, fill #050F1F @ 75 %, 2 px stroke white @ 35 %, HORIZONTAL pad 20 gap 16: 36×36 magnifier glyph + TMP_InputField Regular 33 white, placeholder `LOAN_SEARCH_PLACEHOLDER` "Search by name…" @ 55 % white; then `RESULTS` label (`LOAN_SEARCH_RESULTS`, SemiBold 30 #BFD1E6) + result rows; then `PEOPLE YOU FOLLOW` label (`LOAN_FOLLOWED_HEADER`) + followed rows. Rows unchanged (732×96, selected = blue). With an empty query the RESULTS label and rows are hidden. `LOAN_NO_FOLLOWING` stays the empty state for the followed section; `LOAN_NO_RESULTS` "Nobody by that name." for an empty search |
| Settings row | `14261:109994` in `14261:109878` | appended to the User Profile submenu block (`Frame 2610873`, VERTICAL gap 24) after the DELETE ACCOUNT block: HORIZONTAL pad 24 L/R gap 24, items centred; texts column FILL: `LOAN_SETTING_TITLE` "LOAN OFFERS" Rubik SemiBold 48 white + `LOAN_SETTING_SUB` "Let other players offer to lend you characters and clubs" Regular 30 #BFD1E6; toggle 112×60 pill r 30, ON fill #2775DD with a 48 px white knob at the right (6 px inset, shadow 0/2/3 @ 30 %), OFF fill #38597F knob left. The submenu grows by the row's height — `SettingsMenuItem.submenuHeight` auto-detects (0) so LOG OUT and CLOSE move down; the render clips them, the game must not |

## Architecture context

- **Asmdef:** `Golfin.Social` (`LoanService`, `LoanDtos`, new `UserSearch` call), `Golfin.Net` (`Endpoints`), main assembly (UI, `LoanSyncBehaviour`, Home pill, Settings). No new asmdef.
- **Existing code:**
  - `Assets/Scripts/Social/LoanService.cs` — `Apply` (~220, sorts `Out`/`In`/`Ended` by liveness), `Following` (~243), `Lend` (~267), `Return` (~275), `BuildLendJson`; `Assets/Scripts/Social/LoanDtos.cs` (`LoanDto.Status`, `StartsAt`, `EndsAt`, `LoanPartyDto`, `FollowedUserDto`).
  - `Assets/Scripts/EconomyRuntime/LoanSyncBehaviour.cs` — `ILoanReconciler` (`EnsureBorrowed`, `MarkLentOut`, `RemoveBorrowed`, `ClearLentOut`, `ReconcileFinished`, `WasReconciled`), `RequestRefresh(why)`, `OnScreenChanged`.
  - `Assets/Scripts/UI/Loans/LoanModalController.cs` (`LoadRecipients` ~216 with the one-frame wait, `SpawnRow`, `OnRowClicked`, `ConfirmRoutine` with `PendingSpend`), `LoanRecipientRow.cs` (`Bind(FollowedUserDto)`, `BindPlaceholder`, `SetSelected`), `LoanRibbonView.cs` (`Show(loan, asLender)` / `Clear`), `LoanBadgeView.cs`, `LoanReturnModalController.cs` (the yes/no shell to clone for RESCIND), `Assets/Scripts/UI/Loans/Editor/LoanUiBuilder.cs` (authoring pass — extend, do not hand-edit prefabs).
  - `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` (~463 `loanRibbon.Show`/`Clear` branch), `Assets/Scripts/UI/Inventory/ClubDetailPanel.cs` (same branch).
  - `Assets/Scripts/UI/Home/DailyMissionPillController.cs` — the pill pattern: `Enter/Leave` motion, `ComputeTargetY` (seats under the notice panel), `RefreshPlacement`, `FetchThenPresent`, glow `Pulse`; `HomeScreenController` calls `RefreshPlacement` when the notice shows/hides.
  - `Assets/Scripts/UI/SettingsController.cs` (`userProfileItem`), `SettingsMenuItem.cs` (`submenuHeight` auto-detect), the User Profile submenu prefab/scene object.
  - `Assets/Scripts/Social/UserService.cs` — `Update(...)` / `BuildUpdateJson` (the PUT `/user/update` body; `golf_profile_prompted` shows how a one-field flag rides it), `LastDetail` (`UserDetailDto`).
  - `Assets/Scripts/Net/Endpoints.cs` — `UserDiscover`, `UserUpdate`, `SocialFollowing`, `Loans`, `LoansReturn`.
- **Backend (playlife):** `backend/routers/loans.py` (`_is_live` ~120, `_expire` ~219, `list_loans` ~251, `_follows` ~309, `_blob_owns` ~332, `_live_count` ~370, `_live_row` ~385, `_seed_progress` ~423, `lend` ~480, `return_loan` ~606, `resolve_shares` ~664), `backend/migrations/2026_09_09_golfin_loans.sql` (`golfin_loans.status check in ('active','returned','expired')`, partial unique indexes on `status = 'active'`, the loan-aware `golfin_level_up`), `backend/routers/user.py` (`GET /user/search?q=` ~199 — `ilike` on `display_name`, excludes self; `PUT /user/update` ~123 + `UserUpdateRequest` ~28), `routers/points.py` `earn_game_pts` (`loan_ids` → `resolve_shares`).

## 1. Server

### 1.1 Migration `backend/migrations/2026_09_10_golfin_loan_offers.sql` (idempotent; paste in chat for Cesar; apply BEFORE deploy)

```sql
-- offer lifecycle
alter table public.golfin_loans drop constraint if exists golfin_loans_status_check;
alter table public.golfin_loans add constraint golfin_loans_status_check
  check (status in ('offered','active','returned','expired','declined','rescinded','offer_expired'));
alter table public.golfin_loans add column if not exists offered_at   timestamptz;
alter table public.golfin_loans add column if not exists offer_expires_at timestamptz;
alter table public.golfin_loans add column if not exists answered_at  timestamptz;   -- accept / decline / rescind / offer expiry
alter table public.golfin_loans alter column starts_at drop not null;                  -- null while offered
alter table public.golfin_loans alter column ends_at   drop not null;

-- the asset is LOCKED while offered OR active: both partial unique indexes widen
drop index if exists golfin_loans_live_asset_idx;
create unique index if not exists golfin_loans_live_asset_idx
  on public.golfin_loans (lender_id, kind, ref_id) where status in ('offered','active');
drop index if exists golfin_loans_live_borrower_ref_idx;
create unique index if not exists golfin_loans_live_borrower_ref_idx
  on public.golfin_loans (borrower_id, kind, ref_id) where status in ('offered','active');
-- one pending offer per lender -> recipient
create unique index if not exists golfin_loans_pending_pair_idx
  on public.golfin_loans (lender_id, borrower_id) where status = 'offered';
create index if not exists golfin_loans_borrower_offered_idx
  on public.golfin_loans (borrower_id) where status = 'offered';

-- the setting
alter table public.profiles add column if not exists golfin_loan_offers boolean not null default true;
```

Verification block at the bottom (constraint present, 4 indexes, column default true) — quote the output.

### 1.2 Liveness and locking — the predicates, restated

- **Live (playable by the borrower, splits RP, redirects level-ups):** `status = 'active' and now() < ends_at` — **unchanged**. `resolve_shares`, `golfin_level_up`'s borrower lookup and the client's `IsBorrowed` keep using it; an `offered` row is NOT live for the borrower.
- **Locked (the lender may not use it):** `status in ('offered','active')` (and for `active`, `now() < ends_at`; an `offered` row past `offer_expires_at` is flipped by `_expire` before anyone reads it). `golfin_level_up`'s "lender may not level what is out" check widens from `status = 'active'` to this predicate (new migration `create or replace` of the function — one line; quote it).
- **Lazy expiry** (`_expire`): in addition to `active` past `ends_at` → `expired`, an `offered` row past `offer_expires_at` → `offer_expired`, `answered_at = now()`. No cron.

### 1.3 Router `backend/routers/loans.py`

| Endpoint | Change |
|---|---|
| `GET /loans` | `out` now includes `offered` rows (with `offer_expires_at`); a new list **`offers_in`** carries the caller's pending offers as borrower (same DTO; the borrower's client must NOT treat them as loans); `ended` now also includes `declined` / `rescinded` / `offer_expired` within the 14-day window so the lender's client can toast and unlock. DTO gains `offered_at`, `offer_expires_at`, `answered_at`. |
| `POST /loans` (`lend`) | Drop the `_follows` check and the `not_following` status. New refusals, in this order after the existing `self` / `bad_days` / `unknown_ref`: **`not_accepting`** (`profiles.golfin_loan_offers = false`), **`already_on_loan`** (now also catches `offered`), `borrower_has_it` (unchanged), **`limit_out`** (counts `offered` + `active`), **`limit_in`** (recipient's `active` count — unchanged), **`pending_limit`** (recipient's `offered` count ≥ `LOAN_MAX_PENDING_IN`), **`pending_pair`** (a pending offer from me to them already exists), **`cooldown`** (a row lender→borrower with `status in ('rescinded','declined')` and `answered_at > now() − LOAN_REOFFER_COOLDOWN_HOURS`; returns `retry_after` ISO). On ok: insert with `status = 'offered'`, `offered_at = now()`, `offer_expires_at = now() + LOAN_OFFER_TTL_HOURS`, `starts_at`/`ends_at` NULL, `level_at_start` = the seeded owner level (`_seed_progress` unchanged — it runs at offer time so the level is pinned while locked). |
| **`POST /loans/{id}/accept`** — borrower only | `not_borrower`, `not_offered` (anything but a live `offered` row — includes expired), `limit_in` (re-checked at accept: the recipient's `active` count), `borrower_has_it` (re-checked: they may have acquired it since). On ok: `status = 'active'`, `starts_at = now()`, `ends_at = starts_at + days`, `answered_at = now()`. Returns the DTO. Idempotent: an `active` row whose `answered_at` is set returns `ok` again. |
| **`POST /loans/{id}/decline`** — borrower only | `not_borrower`, `not_offered`. On ok: `status = 'declined'`, `answered_at = now()`. Idempotent. |
| **`POST /loans/{id}/rescind`** — lender only | `not_lender`, `not_offered`. On ok: `status = 'rescinded'`, `answered_at = now()`. Idempotent. |
| `POST /loans/{id}/return` | unchanged (`active` only). |

Constants at the top of the module: `LOAN_OFFER_TTL_HOURS = 48`, `LOAN_MAX_PENDING_IN = 3`, `LOAN_REOFFER_COOLDOWN_HOURS = 24`, next to `LOAN_LENDER_SHARE_BP`.

### 1.4 `routers/user.py`

- `UserUpdateRequest` gains `golfin_loan_offers: Optional[bool] = None` → written to `profiles.golfin_loan_offers` when present. `GET /user/detail` returns it (add to the select).
- `GET /user/search` and `GET /social/{id}/following` **exclude** `golfin_loan_offers = false` rows **only when the caller asks** — add `?for_loans=1` to both; the loan modal passes it, nothing else changes. (`search` also keeps excluding self.)
- Tests: `backend/tests/test_loans.py` — offer lifecycle (offer → accept/decline/rescind/expiry), every new refusal, the pair index, the cooldown window, `accept` re-checks; `test_user.py` — the setting round-trips and filters search + following.

Deploy: migration (Cesar) → `fly deploy` → smoke `403-not-404` on the three new routes; commit playlife from Code.

## 2. Client — `LoanService`

- `LoanListDto` gains `OffersIn` (`offers_in`); `LoanDto` gains `OfferedAt`, `OfferExpiresAt`, `AnsweredAt`.
- `Apply`: `Out` keeps `active` live rows AND `offered` unexpired rows (a new `IsOffered(kind, refId, out loan)` distinguishes them; `IsLentOut` returns **true for both** so every existing lock path — `SelectCharacter`, `EquipClub`, `LevelUp`, the panels — locks an offered asset with no new code); new `OffersIn` list (never merged into `In`); `Ended` gains the three new terminal statuses.
- New calls, same shape as `Return`: `Accept(loanId, …)`, `Decline(loanId, …)`, `Rescind(loanId, …)`; `SearchUsers(query, Action<ApiResult<List<FollowedUserDto>>>)` → `GET /user/search?q=&limit=20&for_loans=1` (the search DTO is the same fields — reuse `FollowedUserDto`; NOTE: `/user/search` returns `avatar_level` and `display_name` at the top level, `following` nests them under `profiles` — check `FollowedUserDto`'s JSON path and add a second mapping if needed); `Following` passes `for_loans=1`.
- `Endpoints`: `UserSearch(q, limit, forLoans)`, `LoansAccept(id)`, `LoansDecline(id)`, `LoansRescind(id)`; `SocialFollowing` gains the flag.
- **Reconcile** (`LoanSyncBehaviour`): `offered` out → `MarkLentOut` (lock; the ribbon reads the status); `declined` / `rescinded` / `offer_expired` out, first time → `ClearLentOut` + toast `LOAN_TOAST_DECLINED_FMT` / `LOAN_TOAST_RESCINDED_FMT` / `LOAN_TOAST_OFFER_EXPIRED_FMT`; an `active` row whose id was previously seen as `offered` → toast `LOAN_TOAST_ACCEPTED_FMT` ("{0} accepted {1}") — track "seen offered ids" in the same `reconciledLoanIds` mechanism. `OffersIn` never touches the managers; it feeds the Home pill. Refresh triggers gain **Home entry** (`ScreenId.Home` in `OnScreenChanged`) so the pill is fresh.

## 3. Client — UI

### 3.1 Lend modal v2 (`LoanModalController`, prefab `LoanModal.prefab` via `LoanUiBuilder`)

Search field (TMP_InputField, `onValueChanged` debounced 300 ms; empty → results hidden; ≥ 1 char → `SearchUsers`, keep the last answer if a newer one is in flight — compare a request counter), `RESULTS` section (rows spawned like followed rows, `StaggerRise`, one-frame wait after clearing — the `asset_loans_polish` lesson), `PEOPLE YOU FOLLOW` section (existing rows, existing empty state). One selection across both sections (`_selectedUserId`). Selecting a search result and pressing LEND → `Lend` as today. New refusal toasts: `LOAN_ERR_NOT_ACCEPTING`, `LOAN_ERR_PENDING_LIMIT`, `LOAN_ERR_PENDING_PAIR`, `LOAN_ERR_COOLDOWN_FMT` ("You can offer to {0} again in {1}"). Success toast `LOAN_TOAST_OFFERED_FMT` ("Offered {0} to {1}") replaces `LOAN_TOAST_LENT`'s wording (key retired via admin deactivate — `LOAN_TOAST_LENT` row stays in the CSV as inactive per the pipeline rule).

### 3.2 OFFERED state on both detail panels

`UpdatePanel` branch: `IsOffered` → `loanRibbon.Show(loan, asLender: true)` where `LoanRibbonView` picks `LOAN_STATUS_OFFERED_FMT` + `LOAN_TIME_TO_ANSWER_FMT` (hours to `offer_expires_at`, refreshed on the tick) when `loan.Status == "offered"`; dim on; buttons disabled as the lent state; **the LEND button relabels to `LOAN_BTN_RESCIND` and is enabled** (on lent: disabled LEND; on offered: enabled RESCIND — same object, `LendButton`). RESCIND → confirm popup (clone of the return popup shell: `LOAN_RESCIND_TITLE` "RESCIND OFFER?", `LOAN_RESCIND_CONFIRM_FMT` "Take back the offer to {0}? You can't offer them this again for {1}.", CANCEL + gold RESCIND) → `Rescind` under `PendingSpend` → `RequestRefresh` → reconcile unlocks → toast `LOAN_TOAST_RESCINDED_FMT`.

### 3.3 Home — offer pill + offer modal

- `LoanOfferPillController` (new, `Assets/Scripts/UI/Home/`), a sibling of `DailyMissionPillController` built the same way (clone the pill object in ShellScene via the builder; icon `IconLoanInBig` 56 px in the flame slot; label `LOAN_PILL_FMT`). Placement: `ComputeTargetY` = the daily pill's target y + 122 + 40 when the daily pill `IsShowing`, else the daily pill's own slot; `HomeScreenController` calls its `RefreshPlacement` alongside the daily pill's. Shows when `LoanService.OffersIn.Count > 0` (the newest offer's lender name; with 2+ pending, label `LOAN_PILL_MANY_FMT` "{0} LOAN OFFERS"). Enter/leave/glow = the daily pill's motion (reuse its serialized feel values). Tap → offer modal for the newest offer.
- `LoanOfferModalController : ModalController` (new prefab `Assets/Prefabs/UI/Modals/LoanOfferModal.prefab`, Figma `14261:107143`): binds the offer's asset from the catalog (`CharacterDatabaseCSV` / `ClubDatabaseCSV` by `ref_id`; portrait, localised name, rarity via `RarityHelper`, `Level`, `Days`), `{1}` in the terms from `lender_share_bp / 100`, fine print hours from `offer_expires_at`. DECLINE → `Decline`; ACCEPT → `Accept` (both under `PendingSpend`, the other button in `alsoDisable`) → `RequestRefresh` → the new `In` row reconciles through the existing `EnsureBorrowed` path → toast `LOAN_TOAST_ACCEPTED_IN_FMT` ("{0} is yours for {1} days") / `LOAN_TOAST_DECLINED_IN_FMT`. Refusals: `not_offered` → `LOAN_ERR_OFFER_GONE` ("This offer is no longer available."), `limit_in` → `LOAN_ERR_LIMIT_IN_SELF` ("You already have 3 loans."), `borrower_has_it` → `LOAN_ERR_HAVE_IT`. With more than one pending offer, DECLINE/ACCEPT closes the modal and the pill re-evaluates; the next tap shows the next offer (no carousel — deferral).

### 3.4 Settings — Loan offers toggle

User Profile submenu gets `LoanOffersRow` (Figma `14261:109994`): a `Toggle` (Unity `Toggle` on the 112×60 pill; knob slides 0.15 s via `UiMotion.Tween`, fill colour swaps #2775DD / #38597F), reads `UserDetailDto.GolfinLoanOffers` (new field), writes `UserService.Update(golfinLoanOffers: value)` under `PendingSpend.BeginOn(toggleButton)`; on failure reverts and toasts `PointsSpendGate.OfflineMessage`. `SettingsMenuItem.submenuHeight` stays 0 (auto) — verify LOG OUT and CLOSE still land below the row (report a screenshot). Default ON.

### 3.5 Strings (EN + JA → importer PLAN/APPLY → publish `texts` → `--check` clean)

`LOAN_PILL_FMT` LOAN OFFER FROM {0} / {0}からの貸出オファー; `LOAN_PILL_MANY_FMT` {0} LOAN OFFERS / 貸出オファー{0}件; `LOAN_OFFER_TITLE` LOAN OFFER / 貸出オファー; `LOAN_OFFER_FROM_FMT` {0} wants to lend you / {0}があなたに貸したいそうです; `LOAN_OFFER_TERMS_FMT` You play with it and can level it up — the levels stay on it when it goes back. {0} gets {1}% of the RP you earn with it. / 使ってレベルアップでき、レベルは返却後も残ります。あなたが獲得したRPの{1}%は{0}に入ります。; `LOAN_OFFER_FINE_FMT` Offer expires in {0}. Turn offers off in Settings › User Profile. / オファーは{0}で失効します。設定 › ユーザープロフィールでオファーをオフにできます。; `LOAN_BTN_ACCEPT` ACCEPT / 受ける; `LOAN_BTN_DECLINE` DECLINE / 断る; `LOAN_BTN_RESCIND` RESCIND / 取り消す; `LOAN_STATUS_OFFERED_FMT` OFFERED TO {0} · {1} / {0}にオファー中 · {1}; `LOAN_TIME_TO_ANSWER_FMT` {0}h to answer / 回答まで{0}時間; `LOAN_SEARCH_PLACEHOLDER` Search by name… / 名前で検索…; `LOAN_SEARCH_RESULTS` RESULTS / 検索結果; `LOAN_FOLLOWED_HEADER` PEOPLE YOU FOLLOW / フォロー中; `LOAN_NO_RESULTS` Nobody by that name. / その名前のプレイヤーはいません。; `LOAN_RESCIND_TITLE` RESCIND OFFER? / オファーを取り消しますか？; `LOAN_RESCIND_CONFIRM_FMT` Take back the offer to {0}? You can't offer them this again for {1}. / {0}へのオファーを取り消しますか？{1}の間、同じ相手に再オファーできません。; `LOAN_SETTING_TITLE` LOAN OFFERS / 貸出オファー; `LOAN_SETTING_SUB` Let other players offer to lend you characters and clubs / 他のプレイヤーからキャラクターやクラブの貸出オファーを受け取る; `LOAN_TOAST_OFFERED_FMT` Offered {0} to {1} / {0}を{1}にオファーしました; `LOAN_TOAST_ACCEPTED_FMT` {0} accepted {1} / {0}が{1}を受け取りました; `LOAN_TOAST_ACCEPTED_IN_FMT` {0} is yours for {1} days / {0}を{1}日間使えます; `LOAN_TOAST_DECLINED_FMT` {0} declined {1} / {0}が{1}を断りました; `LOAN_TOAST_DECLINED_IN_FMT` Declined {0} / {0}を断りました; `LOAN_TOAST_RESCINDED_FMT` Offer for {0} taken back / {0}のオファーを取り消しました; `LOAN_TOAST_OFFER_EXPIRED_FMT` Offer for {0} expired / {0}のオファーが失効しました; `LOAN_ERR_NOT_ACCEPTING` They're not taking loan offers. / 相手は貸出オファーを受け付けていません。; `LOAN_ERR_PENDING_LIMIT` They have too many offers waiting. / 相手のオファーがいっぱいです。; `LOAN_ERR_PENDING_PAIR` You already have an offer waiting with them. / この相手にはすでにオファー中です。; `LOAN_ERR_COOLDOWN_FMT` You can offer to {0} again in {1} / {1}後に{0}へ再オファーできます; `LOAN_ERR_OFFER_GONE` This offer is no longer available. / このオファーは無効になりました。; `LOAN_ERR_LIMIT_IN_SELF` You already have 3 loans. / すでに3つ借りています。; `LOAN_ERR_HAVE_IT` You already have this one. / すでに持っています。

34 rows. Retired: `LOAN_TOAST_LENT`, `LOAN_ERR_NOT_FOLLOWING` (Cesar deactivates in admin). Zero hardcoded literals.

### 3.6 Telemetry

`loan_offer_sent {kind, ref_id, days, via: search|followed}`, `loan_offer_answered {loan_id, answer: accept|decline}`, `loan_offer_rescinded {loan_id}`, `loan_offers_setting {on}`, `loan_pill_open`.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Migration applied (verification output quoted), `fly deploy` green, `/loans/{id}/accept|decline|rescind` 403-not-404; `golfin_level_up`'s lock line quoted.
- [ ] Router tests: offer → accept (starts_at/ends_at set at accept, level pinned from offer time) / decline / rescind / lazy `offer_expired`; each refusal incl. `cooldown` with `retry_after`; pair index; `for_loans=1` filters both lists; setting round-trips.
- [ ] Client: offering locks the asset immediately (OFFERED ribbon, dim, RESCIND enabled, every other button off — `interactable` dump); RESCIND flow unlocks and toasts; decline/expiry from the server unlocks on the next refresh with the right toast; accept flips the same row to lent with `LOAN_TOAST_ACCEPTED_FMT`.
- [ ] Recipient: pill appears on Home entry with a pending offer (under the daily pill when both show, in its slot when not — y dump both cases); modal binds the real asset art/name/rarity/level/days and the share from the row; ACCEPT → asset borrowed (existing borrowed state, reconcile path unchanged); DECLINE → pill gone, nothing in the roster; with 2 offers the pill reads `LOAN_PILL_MANY_FMT` and the second offer follows the first.
- [ ] Lend modal v2: typing filters results after ≤ 300 ms, empty query hides RESULTS, a stale response never overwrites a newer one (test), a player with offers OFF is absent from both lists (server-side filter proven by a curl with and without `for_loans=1`).
- [ ] Settings toggle persists across relaunch (`/user/detail` value quoted), reverts + toasts on a failed PUT; LOG OUT + CLOSE still visible below the new row (screenshot).
- [ ] Figma fidelity table PASS/FAIL per row against the five renders; playbook § 7 crop-diffs (pill, offer modal, offered panel, search field, settings row).
- [ ] Polish atoms line honoured — `PendingSpend` on ACCEPT/DECLINE/RESCIND/toggle, pill motion = daily pill's, `StaggerRise` on results, no shimmer.
- [ ] Strings: 34 rows EN+JA, PLAN/APPLY, published version, `--check` clean, zero literals; the two retired keys named for Cesar to deactivate.
- [ ] EditMode: `LoanServiceTests` extended (offered ≠ live; `IsLentOut` true for offered; `OffersIn` separate; accepted-after-offered toast once), `PendingPointsOp` untouched, suite count before/after, 0 fail.
- [ ] Telemetry rows seen then deleted; console clean; `[SerializeField]` wired; deviations flagged; architect defaults (48 h / 3 pending / 24 h cooldown / clock-at-accept) flagged.

## Files / hierarchy this task touches

- playlife: `backend/migrations/2026_09_10_golfin_loan_offers.sql` — NEW; `backend/routers/loans.py`, `backend/routers/user.py`, `backend/routers/followers.py` (`for_loans`), `backend/tests/test_loans.py`, `backend/tests/test_user.py`
- `Assets/Scripts/Social/LoanService.cs`, `LoanDtos.cs`, `UserDetailDto.cs`, `UserService.cs` (+ Tests); `Assets/Scripts/Net/Endpoints.cs`
- `Assets/Scripts/EconomyRuntime/LoanSyncBehaviour.cs`
- `Assets/Scripts/UI/Loans/LoanModalController.cs`, `LoanRibbonView.cs`, `LoanRecipientRow.cs`; `LoanOfferModalController.cs`, `LoanRescindModalController.cs` (or the return shell parameterised) — NEW; `Assets/Scripts/UI/Loans/Editor/LoanUiBuilder.cs`
- `Assets/Scripts/UI/Home/LoanOfferPillController.cs` — NEW; `Assets/Scripts/UI/HomeScreenController.cs` (placement hook)
- `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs`, `Assets/Scripts/UI/Inventory/ClubDetailPanel.cs`
- `Assets/Scripts/UI/SettingsController.cs` + the User Profile submenu object (+ `LoanOffersToggle.cs` — NEW)
- `Assets/Prefabs/UI/Modals/LoanModal.prefab` (search), `LoanOfferModal.prefab` — NEW, `LoanRescindModal.prefab` — NEW; `Assets/Scenes/ShellScene.unity` (pill, settings row) — only what the builder writes
- `Assets/Localization/LocalizationText.csv` — 34 rows; `Docs/Architecture/UI_ELEMENT_PALETTE.md` (search field, toggle, offer pill rows); `Docs/AI_CONTEXT.md`

## Smoke evidence

Two-account editor run with the stubbed transport (`LoanUiCaptureBot` extended): lender offers via search → OFFERED panel; recipient Home pill → modal → ACCEPT → borrowed; second run DECLINE → lender toast + unlock; RESCIND path; settings toggle off → the player disappears from a search (server curl). Frame strips for the pill entrance and the modal. **Live E2E with Cesar's two accounts is his to run** (report checklist names the SQL).

## Out of scope (do NOT do these) — Notion deferrals

- Push notification / Home notice when an offer arrives (the pill is on Home entry only).
- Offer carousel in the modal (2+ offers are answered one after another).
- Lender recall of an ACTIVE loan (2211 stands); admin Loans panel (2213); durability/SP (2214/2215); balls/items (2216 partial — the "anyone" half is taken up here, balls/items stay).
- A "my offers" list; blocking a specific player (the toggle is global).
