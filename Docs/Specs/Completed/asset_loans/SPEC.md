# SPEC — `asset_loans` (character / club "scholarship" lending)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Starts at `SPEC_READY` (2026-09-09).

## Decisions of record (Cesar, 2026-09-09)

1. **Benefit model:** the asset comes back to the lender **levelled** (every level the borrower buys lands on the lender's `golfin_progress` row), and the lender takes a **20 % cut of the RP the borrower earns in rounds played with the asset**. The borrower gets to play with gear they don't own and keeps the other 80 %. No RP is created — the cut comes out of the borrower's earn — so a player lending to their own alt gains nothing.
2. **Recipients:** only accounts the lender **follows** (the PLAYLIFE `followers` graph). No share codes, no marketplace.
3. **Duration:** **1 / 3 / 7 days**, chosen at lend time. **Auto-return at expiry** (server clock). The lender **cannot recall early**; the borrower **can return early**.
4. **Direct assignment, no accept step.** The asset moves the moment the lender confirms; the borrower sees it on their next launch / Roster / Inventory entry. (Offer/accept is a deferral.)

Architect defaults — not Cesar's words, flag in the report if any bites: at most **3 loans out** per lender and **3 loans in** per borrower at a time; the share is a server constant `LOAN_LENDER_SHARE_BP = 2000`, admin-tunable later; the lender's cut is **capped at 40 % of an action** when several borrowed assets were used in the same round (pro-rata scale).

## Goal

A player opens a character or club they own, taps **LEND**, picks someone they follow and a duration, and the asset is theirs to use — and level — until it comes back. Both detail screens and both thumbnail cards show who has what: a lent asset is marked **ON LOAN** on the lender's side and locked; a borrowed asset is marked **BORROWED** on the borrower's side with the time left. Levels are server-authoritative already (`progress_server_side`); this task makes the level's *owner* and the level-up's *payer* two different players for the duration of a loan, and splits round RP between them on the server.

## Build playbook (Figma-node screens)

This task builds from Figma nodes: the implementer and EVERY reviewer work `Docs/Architecture/FIGMA_SCREEN_BUILD_PLAYBOOK.md`. Its § 7 self-diff (crop matched node/built regions and enumerate) is an acceptance line.

## Reference

- **Figma file:** `5gEAHjl6xAtW8iYY7NMvWd` (Golfin Game Redux). Frames designed 2026-09-09 by the Architect (Cesar reviewed: RETURN gold, LEND full-width), all 1170×2532, all COMPONENTs cloned from the page's base screen so everything not listed in the fidelity table is byte-identical to the shipped screen:
  - page **Characters Screen** (`2470:14192`): `Roster — Lend` **`14181:33450`**, `Roster — On Loan (lender)` **`14181:33672`**, `Roster — Borrowed` **`14181:33894`**, `Roster — Lend Modal` **`14183:32758`**, `Roster — Return Confirm` **`14183:107541`**.
  - page **Clubs Screen** (`2563:11339`): `Clubs — Lend` **`14183:107899`**, `Clubs — On Loan (lender)` **`14183:108287`**, `Clubs — Borrowed` **`14183:108675`**, `Clubs — Lend Modal` **`14185:34162`** (carries the equipped-club warning line).
- **Node renders in `reference/`** (1170×2532 each, pulled at spec time — the A/B ground truth): `Roster_Lend_14181-33450.png`, `Roster_OnLoan_14181-33672.png`, `Roster_Borrowed_14181-33894.png`, `Roster_LendModal_14183-32758.png`, `Roster_ReturnConfirm_14183-107541.png`, `Clubs_Lend_14183-107899.png`, `Clubs_OnLoan_14183-108287.png`, `Clubs_Borrowed_14183-108675.png`, `Clubs_LendModal_14185-34162.png`.
- **Icon PNGs in `reference/`:** `IconLoanOutSmall.png` (24×24), `IconLoanOutBig.png` (40×40), `IconLoanInSmall.png` (24×24), `IconLoanInBig.png` (40×40) — the tray+arrow glyph the Figma frames use (`IconLoanOut` / `IconLoanIn` frames inside the ribbon and the card badge), white on transparent. Copy to `Assets/Art/RosterScreen/`, sprite import settings from `IconSelectedSmall.png.meta`.
- **Placeholder vs canonical content:** "KENJI", "MARTA / LUCAS / AIKO", "2d 4h", "Lv 82", "20%" are mock values; the 20 comes from `lender_share_bp`, names from the follow list, time from `ends_at`. The loan status lives ONLY in the ribbon over the portrait and the card badge — no icon is added beside the name (Cesar, 2026-09-09).

## Figma Fidelity (enumerate EVERY element — Rule 18)

Measured from the Figma nodes; the game canvas is 1170×2532 so Figma px = canvas px. Implementer + both reviewers reproduce this table PASS/FAIL against the renders in `reference/`.

| Element | Figma node | Property → value |
|---|---|---|
| Compare + Lend row (Roster) | `14181:107801` in `14181:33450` | 489×54, HORIZONTAL, gap 24, x 24 inside `Right` (same column as the LEVEL UP / BOOST row `Frame 10`), y = the old Compare row's y; **no fill** |
| COMPARE button | `14181:33658` | `Main Buttons / Silver -Small, Enabled=Yes`, 232×54, label COMPARE (Rubik SemiBold 39 #1E293B) |
| LEND / RETURN button | `14181:107802` (Roster) / `14183:108282` (Clubs) | same variant, 233×54, label `LOAN_BTN_LEND` / `LOAN_BTN_RETURN` |
| Compare + Lend row (Clubs) | `14183:108281` in `14183:107899` | identical row replacing the club panel's 489×54 Compare instance (`4300:31768` in the base) |
| Loan ribbon | `14182:32760` (Roster lender), `14182:107177` (Roster borrower), `14183:109280` / `14183:109309` (Clubs) | 537×72 across the TOP of the Left (portrait) panel, absolute (0,0); fill #050F1F @ 72 %; top corners radius 20 (matches the panel); HORIZONTAL, padding 24 L/R, gap 16, items centred |
| Ribbon icon | `IconLoanOut` / `IconLoanIn` frame inside the ribbon | 40×40 tray+arrow glyph, white, 3.5 px round strokes (arrow up = out/lent, arrow down = in/borrowed) |
| Ribbon text | TEXT inside ribbon | Rubik SemiBold **28** white, single line: `LOAN_STATUS_OUT_FMT` "ON LOAN TO {0} · {1}" / `LOAN_STATUS_IN_FMT` "BORROWED FROM {0} · {1}" |
| Lent dim | `14182:32765` (Roster), `14183:109279` (Clubs) | rectangle over the whole Left panel (537×1483 / 537×1347), #000 @ 55 %, BELOW the ribbon — lender view only |
| Disabled buttons (lender view) | swapped instances in `14181:33672` / `14183:108287` | LEVEL UP, BOOST/REPAIR, COMPARE, LEND → `Silver -Small, Enabled=No` (`2541:12099`); SELECT/EQUIP → `Gold, Enabled=No` (`2541:12071`); sizes unchanged |
| Borrowed view buttons | `14181:33894` / `14183:108675` | BOOST **hidden** (Roster, `14181:34094` visible=false — LEVEL UP keeps its 230 width and slot); REPAIR → `Silver -Small, Enabled=No` (Clubs); COMPARE, RETURN, SELECT/EQUIP enabled |
| Card loan badge | `14182:32786` (Roster lender), `14182:107182` (Roster borrower), `14183:109305` / `14183:109319` (Clubs) | 44×44 circle at (8,8) of the thumbnail card (top-left — the Lv pill stays top-right), fill #050F1F @ 85 %, 2 px white stroke, 26×26 glyph centred |
| Lend modal panel | `14183:32983` (Roster), `14185:34374` (Clubs) | 780 wide, HUG height (≈1080 / ≈1140 with the warning), centred on the screen; VERTICAL gap 24, padding 24; fill = the Level Up modal's linear gradient #133453→#091B33 (top→bottom); 3 px white stroke; radius 20; drop shadow 0/4/4 @ 25 %; behind it `Dark` 1170×2532 #000 @ 50 % + background blur 8 |
| Modal title | first TEXT in the panel | `LOAN_MODAL_TITLE` "LEND {0}", Rubik SemiBold 45 white, left, wraps at 732 |
| DURATION label + segments | `14183:32988` / `14183:32990` | label Rubik SemiBold 39 white; three `Main Buttons` small instances 230×54 gap 21: unselected `Silver -Small`, selected `Gold - Small` (`2541:11883`); default 3 DAYS selected |
| Terms line | TEXT | `LOAN_TERMS_FMT`, Rubik Regular 30 white, width 732, wraps |
| Equipped warning (Clubs modal only) | TEXT in `14185:34374` | `LOAN_WARN_EQUIPPED`, Rubik SemiBold 30 #FFB847; shown only when the club is in a bag |
| LEND TO list | `14183:107512` | label Rubik SemiBold 39; rows 732×96, radius 12, HORIZONTAL padding 16/24 gap 20: 64 px avatar circle (#38597F placeholder, `avatar_url` when present), name Rubik SemiBold 33 white FILL, "Lv {avatar_level}" Rubik Regular 30 #BFD1E6; unselected fill #050F1F @ 60 %; selected fill #2775DD @ 35 % + 3 px #2775DD stroke; gap 16 between rows; scrolls when > 4 |
| Modal footer | `14183:107530` | CANCEL `Silver, Enabled=Yes` (`2182:5458`) 354×120 + LEND `Gold, Enabled=Yes` (`2180:1006`) 354×120, gap 24 — **both the same size, the gold sprite stretched to 354** (Cesar) |
| Return confirm popup | `14183:107775` | 810 wide, padding 40/48/32/48, gap 32, same gradient/stroke/radius as the modal; title `LOAN_RETURN_TITLE_*` Rubik SemiBold 45 **#ED6B21** centred; body `LOAN_RETURN_CONFIRM` Rubik Regular 33 white centred, width 714; CANCEL `Silver` 345×120 + RETURN **`Gold, Enabled=Yes`** 345×120 (Cesar: gold, not copper), gap 24 |

## Architecture context

- **Asmdef boundaries affected:** `Golfin.Net` (Endpoints only), `Golfin.Social` (new `LoanService` + DTOs — same assembly as `GiftService`/`UserService`, it is the same "other players" concern), `Golfin.Economy` (`PendingPointsOp` gains `loans`), `Golfin.InventorySync` (codec must skip borrowed rows), main assembly (managers + UI). No new asmdef.
- **Existing code referenced:**
  - `Assets/Scripts/CharacterManager.cs` — `GetCharacterData`, `GetAllCatalogCharacters` (the Roster already lists EVERY catalog character, owned or locked — a borrowed one flips from locked to borrowed, no carousel change), `IsOwned`, `SelectCharacter` (line ~521), `GetSelectedCharacterId`, `LevelUp`, `OnRosterChanged`, `OnCharacterSelected`.
  - `Assets/Scripts/ClubManager.cs` — `GetClubData`, `GetAllOwnedClubs`, `GetOwnedClubsOfType`, `EquipClub`, `SetLevel(clubId, newLevel)` (line ~559), `RehydrateFromSave`, `OnInventoryChanged`, `OnClubEquipped`; `BagManager.RemoveClubFromBag`.
  - `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` — `UpdatePanel` (259), `ApplyLockedState` (421), `UpdateSelectButton` (758), `OnLevelUpClicked` / `OnBoostClicked` / `OnCompareClicked` / `OnSelectClicked`; serialized `levelUpButton, boostButton, compareButton, selectButton, selectedIcon (IconSelectedBig), levelUpReadyIcon (IconLevelUpBig)`.
  - `Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs` — `RefreshIcons` (141), `SetLocked` (289); serialized `selectedIcon, levelUpReadyIcon, staminaIcon, _lockedOverlay, _lockedLabel`.
  - `Assets/Scripts/UI/Inventory/ClubDetailPanel.cs` — `UpdatePanel` (169), `OnEquipClicked` (359), `OnRepairClicked`, `OnLevelUpClicked`; serialized `levelUpButton, repairButton, compareButton, equipButton, equippedIcon`.
  - `Assets/Scripts/UI/Inventory/ClubThumbnailCard.cs` — `RefreshIcons` (136); serialized `equippedIcon, durabilityLowIcon`.
  - `Assets/Scripts/UI/Inventory/ClubCarouselController.cs` (86–110) — builds the list from `GetAllOwnedClubs` / `GetOwnedClubsOfType`; borrowed clubs must be appended here (§4.4).
  - `Assets/Scripts/UI/Roster/UI/LevelUpModalController.cs` — `OnServerAnswered` (553), the `ProgressLevelUpVerdict` switch; SP allocation UI.
  - `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs` — `EnqueueServerEarn` (197) → `PointsService.EnqueueEarn(action, amount)`.
  - `Assets/Scripts/Economy/PendingPointsOp.cs` — `NewEarn`, `ToEarnGameJson` (58); `PointsService.cs`, `PendingOpsQueue.cs`.
  - `Assets/Scripts/InventorySync/InventoryCodec.cs` — `EncodeToObject` (73), `KChars` rows (~161).
  - `Assets/Scripts/Social/UserService.cs`, `GiftService.cs`, `GiftDtos.cs` — the DTO (`[JsonProperty]`) and `IEnumerator X(Action<ApiResult<T>>)` pattern to copy.
  - `Assets/Scripts/Net/Endpoints.cs` — append URLs.
  - `Assets/Scripts/UI/Modals/ModalController.cs` — base for the lend modal; `Assets/Scripts/UI/Inventory/BagSelectionModalController.cs` — nearest existing "pick one from a list" modal to clone the list row from.
  - `Golfin.Telemetry.TelemetryService.Instance.RecordSafe(name, () => payload)`.
- **Scene facts (ShellScene, read from the YAML 2026-09-09 — do not re-derive):** Roster `RightPanel` 541×1492; `CompareButton` 496×56 at anchoredPos (268.2, 228), anchors bottom-left. Club `ClubDetailPanel/RightPanel` 539 wide; its `CompareButton` 496×56 at (269.5, −707.47), anchors top-left. `ButtonsPanel` (LevelUp + Boost / LevelUp + Repair, 235×56 each, gap 16, HorizontalLayoutGroup) is the row the new Compare+Lend row mirrors — do not add a third button to it. Roster Left panel 537×1483, Club Left panel 537×1347.
- **Backend (playlife, separate repo):** `backend/routers/followers.py` (`/api/v1/social/{user_id}/following` — `followers` table `follower_id, following_id`), `routers/points.py` `earn_game_pts` (`EarnGameRequest`, `earn_pts_v2` rpc), `routers/progress.py` `level_up` → rpc `golfin_level_up(p_user_id, p_kind, p_ref_id, p_from, p_to, p_expected_cost, p_key, p_build)` in `migrations/2026_08_28_golfin_progress.sql` (`golfin_progress (user_id, kind, ref_id, level, grandfathered_from)`, `golfin_progress_events`), `migrations/2026_08_26_golfin_inventory.sql` (`profiles.golfin_inventory` blob — client-asserted, NOT anti-cheat), `migrations/2026_08_18_golfin_leaderboards.sql` (`golfin_leaderboard` ranks only ledger rows whose `type` is in `game_point_actions`, line ~247).

## 1. Server — data

New migration `backend/migrations/2026_09_09_golfin_loans.sql` (idempotent, RLS on / no policies, service-role only, same posture as `golfin_progress`). Verification query at the bottom; **paste the full SQL in chat for Cesar** (WORKFLOW_NOTES rule).

```sql
create table if not exists public.golfin_loans (
  id               uuid primary key default gen_random_uuid(),
  lender_id        uuid not null,
  borrower_id      uuid not null,
  kind             text not null check (kind in ('character','club')),
  ref_id           text not null,
  days             int  not null check (days in (1,3,7)),
  starts_at        timestamptz not null default now(),
  ends_at          timestamptz not null,
  ended_at         timestamptz,                -- set on return / expiry
  status           text not null default 'active'
                   check (status in ('active','returned','expired')),
  lender_share_bp  int  not null default 2000,
  level_at_start   int  not null,
  level_at_end     int,
  rp_to_lender     int  not null default 0,   -- running totals, for the detail panel + ops
  rp_to_borrower   int  not null default 0,
  idempotency_key  uuid not null,
  created_at       timestamptz not null default now(),
  unique (lender_id, idempotency_key),
  check (lender_id <> borrower_id)
);
-- one live loan per (lender, asset): partial unique index
create unique index if not exists golfin_loans_live_asset_idx
  on public.golfin_loans (lender_id, kind, ref_id) where status = 'active';
-- one live copy of a ref per borrower (they cannot hold two of the same club)
create unique index if not exists golfin_loans_live_borrower_ref_idx
  on public.golfin_loans (borrower_id, kind, ref_id) where status = 'active';
create index if not exists golfin_loans_lender_idx   on public.golfin_loans (lender_id, status);
create index if not exists golfin_loans_borrower_idx on public.golfin_loans (borrower_id, status);

alter table public.golfin_progress_events add column if not exists on_behalf_of uuid; -- lender id when a borrower paid
```

**Liveness is one predicate, used everywhere:** `status = 'active' and now() < ends_at`. Expiry is **lazy** — `GET /loans` (and the two functions below) treat a past-`ends_at` row as ended, and `GET /loans` flips it to `expired` + fills `ended_at` / `level_at_end` on read. No cron.

### 1.1 `golfin_level_up` — loan-aware (edit the function in a new migration, `create or replace`)

Before the level guard:

```
select lender_id into v_owner from golfin_loans
 where borrower_id = p_user_id and kind = p_kind and ref_id = p_ref_id
   and status = 'active' and now() < ends_at;
if not found then v_owner := p_user_id; end if;
-- a LENDER may not level an asset that is out on loan
if v_owner = p_user_id and exists (select 1 from golfin_loans where lender_id = p_user_id and kind = p_kind and ref_id = p_ref_id and status='active' and now() < ends_at)
  then return json_build_object('status','not_available','reason','on_loan'); end if;
```

Then: the progress row read / upsert uses `v_owner`; the debit (`spend_pts`) uses `p_user_id`; the event row gets `on_behalf_of = case when v_owner <> p_user_id then v_owner end`. The `p_from` guard compares against the **owner's** row. Grandfathering: the seed row for a lent asset is written at **lend time** (§1.3), so a borrower never seeds. Everything else in the function is untouched; the router `routers/progress.py` is untouched (`not_available/on_loan` is a new `reason` value only).

### 1.2 RP split — `POST /points/earn-game` gains `loan_ids`

`EarnGameRequest.loan_ids: Optional[List[str]] = None`. After `pts` is resolved (fixed or validated amount) and the daily-cap check:

```
live = rows of golfin_loans where id = any(loan_ids) and borrower_id = user_id and status='active' and now() < ends_at
if live:
    share_each = floor(pts * lender_share_bp / 10000)      # 20 % each
    total = sum(share_each); cap = floor(pts * 0.40)
    if total > cap: scale each share by cap/total (floor), recompute total
    borrower_pts = pts - total
    -> rpc golfin_loan_split(p_user_id, p_action, p_pts=pts, p_borrower_pts, p_description, p_key, p_shares json[{loan_id, lender_id, pts}])
else: existing earn_pts_v2 call, unchanged
```

`golfin_loan_split` (plpgsql, one transaction): calls `earn_pts_v2(user, action, borrower_pts, description, key)`; for each share calls `earn_pts_v2(lender, 'loan_lender_share', share_pts, '<description> · loan share from <borrower display_name>', uuid5(key, loan_id))`; bumps `rp_to_lender` / `rp_to_borrower` on each loan row. Idempotent by the same `p_key` (the first `earn_pts_v2` replay returns the original — mirror that: if the borrower earn replays, skip the shares). Response shape unchanged (`{"data": <earn_pts_v2 result of the borrower's earn>}` plus `"loan_split": {"to_lenders": total}` so the client can show the toast).

**`loan_lender_share` is deliberately NOT a `game_point_actions` row** — `golfin_leaderboard` whitelists ranked actions from that catalog (line ~247), so the lender's cut never ranks. NOTE for the implementer: confirm `earn_pts_v2` does not itself require the action to exist in the catalog (read `2026_08_12_points_spend_idempotency.sql`); if it does, add the row with `pts = null`, `max_per_event = null` and add an explicit `and t.type <> 'loan_lender_share'` to `golfin_leaderboard` in the same migration.

A round that finishes after the loan expired earns with no split (the earn call finds no live loan) — accepted.

### 1.3 New router `backend/routers/loans.py`, mounted at `/api/v1/loans` (`main.py`)

All AUTH (`get_current_user`). Refusals that the player can act on are **200 `{"data": {"status": "<code>"}}`**, like `/progress/level-up`; malformed input is 400.

| Endpoint | Body / result |
|---|---|
| `GET /loans` | `{"data": {"out": [LoanDto…], "in": [LoanDto…]}}` — every `active` loan (after lazy expiry) plus every loan **ended within the last 14 days**, so both clients can reconcile after being offline. |
| `POST /loans` | `{kind, ref_id, borrower_id, days, level, idempotency_key}` → `{"data": {"status": "ok", "loan": LoanDto}}` or `status` ∈ `not_following` (no `followers` row with `follower_id = me, following_id = borrower`), `self`, `already_on_loan` (live row for me+asset), `borrower_has_it` (live loan of that ref to that borrower, OR the borrower's `profiles.golfin_inventory` blob lists the ref as owned — best-effort: `InventoryCodec` writes a character as a bare id or an object with `own:false`, a club as a bare id or object; treat "present and not `own:false`" as owned), `limit_out` (3 live out), `limit_in` (borrower has 3 live in), `bad_days`, `unknown_ref` (not an active `characters` / `clubs` content row). On `ok`: insert row with `ends_at = now() + days`, `level_at_start = level`; **seed `golfin_progress` for the lender if absent** (`level`, `grandfathered_from = level` — the same trust-once rule `golfin_level_up` uses); if a row exists and disagrees with `level`, the server's row wins and `LoanDto.level` says so. Replay by `(lender_id, idempotency_key)` returns the original `ok`. |
| `POST /loans/{id}/return` | borrower only → `{"data": {"status": "ok", "loan": LoanDto}}`; `not_borrower`, `not_active`. Sets `status='returned'`, `ended_at=now()`, `level_at_end` = owner's progress level. Idempotent (a returned loan returns `ok` again). |

`LoanDto`: `id, kind, ref_id, lender {id, display_name, avatar_url}, borrower {id, display_name, avatar_url}, days, starts_at, ends_at, ended_at, status, level (owner's current golfin_progress level), level_at_start, level_at_end, lender_share_bp, rp_to_lender, rp_to_borrower`. Timestamps ISO-8601 UTC (`ApiEnvelope` uses `DateParseHandling.None` — parse with `DateTime.Parse(…, DateTimeStyles.RoundtripKind)` like the GPS DTOs).

Deploy: migration first (Cesar, SQL editor), then `fly deploy`; smoke = `/api/v1/loans` answers **403-not-404** unauthenticated (mounted, auth-gated). Backend files are committed by Code (WORKFLOW_NOTES).

## 2. Client — `LoanService` (`Assets/Scripts/Social/LoanService.cs`, `LoanDtos.cs`)

Same shape as `GiftService`: `static Instance`, `ConfigureForTest`, `IEnumerator Refresh(Action<ApiResult<LoanListDto>>)`, `IEnumerator Lend(kind, refId, borrowerId, days, level, key, Action<ApiResult<LoanMutationDto>>)`, `IEnumerator Return(loanId, Action<…>)`, `IEnumerator Following(Action<ApiResult<List<FollowedUserDto>>>)` (hits `/social/{myId}/following?limit=50`; `myId` from `UserService.Instance.LastDetail.Id` (`UserDetailDto.Id`, verified)).

State: `IReadOnlyList<LoanDto> Out`, `In` (live only), `Ended` (ended, not yet reconciled), `event Action OnLoansChanged`. Helpers: `bool IsLentOut(kind, refId, out LoanDto)`, `bool IsBorrowed(kind, refId, out LoanDto)`, `List<string> UsedLoanIdsForRound()` = ids of `In` loans whose asset is the selected character or a club in the active bag (`BagManager` bag 1 — NOTE: confirm the active-bag accessor).

Refresh triggers: after auth at boot (where `ContentService` / `PointsService.RefreshBalanceAsync` are kicked — same spot), on `ScreenId.Roster` and `ScreenId.Inventory` entry (`ScreenManager.ShowScreen` switch, lines ~490/510), and after every `Lend` / `Return`. Offline: keep the last list (persist nothing new — the loan list is the server's, it is re-fetched).

Endpoints appended to `Endpoints.cs`: `Loans`, `LoansReturn(id)`, `SocialFollowing(userId, limit)`.

### 2.1 Reconciliation (`LoanService.Reconcile()` after every successful Refresh)

For each loan in the response:
- **Borrower, live `in`:** `CharacterManager.EnsureBorrowed(refId, level)` / `ClubManager.EnsureBorrowed(refId, level)` (§3) — creates or updates a runtime-only borrowed instance at `level`.
- **Borrower, ended `in`:** `RemoveBorrowed(refId)`. If it was the selected character, `SelectCharacter(first owned character)` (starter is always owned — `NeedsStarter` false after FTUE); if it was in the bag, `BagManager.RemoveClubFromBag`. Toast `LOAN_TOAST_RETURNED_IN` with the asset name.
- **Lender, live `out`:** mark the local instance `isLentOut = true` (runtime flag, §3). If it is the selected character (it should not be — §4.1 refuses — but a second device can lend it), `SelectCharacter(first owned, not lent)`. If a lent club is in a bag, `RemoveClubFromBag`.
- **Lender, ended `out`:** clear `isLentOut`; if `level_at_end > local currentLevel`: characters — set `currentLevel = level_at_end`, `totalSPEarned = Σ CharacterLevelUpDatabase.Instance.GetSPReward(l) for l in (old+1 … level_at_end)` (SP arrives **unallocated** — the lender allocates it in the Level Up modal as usual), `RefreshStatValues`, `SyncCharacterToSaveData`, `OnRosterChanged`; clubs — `ClubManager.SetLevel(refId, level_at_end)` (NOTE: check whether `SetLevel` also credits SP; if not, mirror the character rule). `InventorySyncService.Instance?.MarkDirty()`. Toast `LOAN_TOAST_RETURNED_OUT` (name + `+N RP` from `rp_to_lender`).
- Reconciled ended-loan ids are stored in `SaveData.reconciledLoanIds` (List<string>, capped at 50 newest) so the 14-day window never re-toasts.

## 3. Managers — borrowed instances and the lent flag

- `PlayerCharacterData`: add `[NonSerialized] public bool isBorrowed; [NonSerialized] public bool isLentOut;` — runtime only, **never persisted**. `CharacterManager.EnsureBorrowed(id, level)`: if `IsOwned(id)` → no-op (server should have refused; log warning). Else set the catalog row `isOwned = true, isBorrowed = true, currentLevel = level, totalSPEarned = 0, spent* = 0` (**stats at base for the level's caps; SP allocation is disabled on a borrowed asset — §4.3**), `RefreshStatValues`, `OnRosterChanged`. `RemoveBorrowed(id)`: back to `isOwned = false`, `isBorrowed = false`. `SyncCharacterToSaveData` must **skip** rows with `isBorrowed` (the save/blob never learns about them).
- `PlayerClubData`: same two flags. `ClubManager.EnsureBorrowed(id, level)`: add a runtime `PlayerClubData` from `BuildSpec(template)` at `currentLevel = level`, `currentDurability = maxDurability`, `equippedBagSlot = 0`, `isBorrowed = true`; **`PersistOwnedClubs` skips `isBorrowed`** rows. `RemoveBorrowed` removes it from `ownedClubs` (after `RemoveClubFromBag`). `GetAllOwnedClubs` / `GetOwnedClubsOfType` include borrowed rows (so bags, the carousel and the in-game club selector see them without changes).
- `InventoryCodec.EncodeToObject`: assert-skip any character/club with `isBorrowed` (belt and braces with the two skips above — add an EditMode test that a borrowed row never reaches the blob).
- **Durability of a borrowed club is frozen:** no wear in play (NOTE: a grep on 2026-09-09 found NO durability-wear call site in `Assets/Scripts` — if that still holds, there is nothing to freeze; note it in the report), Repair disabled. **Stamina/condition of a borrowed character** is tracked in memory like any other (it resets on relaunch — accepted); Boost is hidden (§4.3).
- Lent-out assets are **locked for the lender**: `CharacterManager.SelectCharacter` refuses `isLentOut` (warning), `LevelUp` refuses; `BagManager` equip refuses `isLentOut`. (The server refuses too — §1.1 — this is UX.)

## 4. UI

### 4.1 Detail panels — the LEND / RETURN row and the loan ribbon

**The row.** Both panels replace their single `CompareButton` with a two-button row that mirrors the LEVEL UP / BOOST (Roster) and LEVEL UP / REPAIR (Clubs) `ButtonsPanel` directly above it: **COMPARE 235×56 + 16 gap + LEND 235×56**, the row's left edge = the `ButtonsPanel` LevelUp button's left edge, the row's y = the current `CompareButton` y. `LendButton` is a clone of `CompareButton` (same sprite/colours/font, a TMP child for the label). Dump both rows' rects in the report (§7 self-diff: Compare left edge = LevelUp left edge; Lend right edge = Boost/Repair right edge, to the pixel). Compare-mode mirrors (`CompareInfoPanel/…`) and the Item/Ball panels are **untouched**.

**The ribbon.** A new `LoanRibbon` object at the top of the Left (portrait) panel of both detail panels, hidden by default: 537×72 stretch-anchored to the panel's top edge, Image #050F1F α 0.72 with the panel's top corner radius, a 40×40 icon Image (`IconLoanOutBig` / `IconLoanInBig`) and a TMP label (Rubik SemiBold 28 — the font asset `rarityLabel` uses — white, one line, `ConstrainName`-style shrink) in a HorizontalLayoutGroup padding 24 / spacing 16. Plus `LentDim`, a full-panel Image #000 α 0.55 under the ribbon, lender view only. Both prefab-authored and wired as `[SerializeField]`; the Roster `_lockedDetailOverlay` is NOT reused (it carries the ACQUIRE text). No icon beside the name — the ribbon and the card badge are the only status carriers.

State by asset state (evaluate in `UpdatePanel`, both panels):

| State | Row | Other buttons | Ribbon / dim |
|---|---|---|---|
| owned, not lent, not selected / not equipped | COMPARE + **LEND** enabled | unchanged | hidden |
| owned, **selected character** | LEND disabled (label stays LEND) — the player selects another character first | unchanged | hidden |
| owned, **equipped club** | LEND **enabled** — confirming the lend removes it from the bag (the modal warns) | unchanged | hidden |
| **lent out** (lender) | COMPARE + LEND both **disabled** | Level Up, Boost / Repair disabled; Select / Equip disabled (`interactable = false`, stays visible — Figma `Gold, Enabled=No`) | ribbon `IconLoanOutBig` + `LOAN_STATUS_OUT_FMT` ("ON LOAN TO {0} · {1}"); `LentDim` on |
| **borrowed** | COMPARE enabled + **RETURN** (`LOAN_BTN_RETURN`) enabled | Level Up enabled (§4.3); Boost **hidden** (Roster); Repair disabled (Clubs); Select / Equip enabled | ribbon `IconLoanInBig` + `LOAN_STATUS_IN_FMT` ("BORROWED FROM {0} · {1}"); no dim |
| locked (not owned, not borrowed) | row hidden (existing locked behaviour) | unchanged | hidden |

`{0}` = the other party's `display_name`; `{1}` = time left from `ends_at`: `LOAN_TIME_D_H_FMT` "{0}d {1}h" when ≥ 1 day, `LOAN_TIME_H_M_FMT` "{0}h {1}m" below, refreshed on the panel's existing tick.

### 4.2 Lend modal — `LoanModalController : ModalController` (new prefab `Assets/Prefabs/UI/Modals/LoanModal.prefab`)

Built to Figma `14183:32758` (Roster) / `14185:34162` (Clubs — same prefab, the warning line toggled). Chrome = `ModalController` (panel, backdrop = the `Dark` scrim, close). Content per the fidelity table, top to bottom:
1. Title `LOAN_MODAL_TITLE` ("LEND {0}", localised asset name, upper).
2. `DURATION` label + three small buttons (`LOAN_DAYS_1/3/7`), the selected one on the **gold small** sprite the Level Up modal's LEVEL UP button already uses; default **3 DAYS**. Reuse the button atom from `Docs/Architecture/UI_ELEMENT_PALETTE.md` (Rule 22 reuse map in the report).
3. Terms `LOAN_TERMS_FMT` — `{0}` formatted from `lender_share_bp / 100`, never hardcoded.
4. `LOAN_WARN_EQUIPPED` (amber) — active only when the club is in a bag.
5. `LEND TO` label + recipient rows from `LoanService.Following` (avatar via the GPS profile avatar loader when `avatar_url` is set, else the placeholder circle; `display_name`; `Lv {avatar_level}`); one selectable (selected = blue fill + stroke); a ScrollRect when > 4. Empty state `LOAN_NO_FOLLOWING`; loading = `—` rows like the GPS screens.
6. Footer CANCEL (silver big) + LEND (gold big, `LOAN_BTN_CONFIRM`), **equal widths**; LEND enabled only with a recipient; pending state on LEND like `transaction_feedback` (disabled + spinner, no optimistic change).

On `ok`: `LoanService.Refresh` → reconcile (flips the flag, unequips, switches the panel to the lent state); modal closes; toast `LOAN_TOAST_LENT` ("{0} lent to {1} for {2}"). On a refusal `status`: toast `LOAN_ERR_<STATUS>` (`not_following`, `already_on_loan`, `borrower_has_it`, `limit_out`, `limit_in`, generic). Offline: `PointsSpendGate.OfflineMessage`.

**RETURN** (borrowed asset): the confirm popup at Figma `14183:107541` — `LOAN_RETURN_TITLE_CHAR` / `LOAN_RETURN_TITLE_CLUB` + `LOAN_RETURN_CONFIRM` ("Return {0} to {1} now? It goes back at Lv {2} — the levels you bought stay with it."), CANCEL (silver) + RETURN (**gold**). Reuse `SchemeConfirmModalController`'s yes/no shell if its layout fits, else a sibling prefab `LoanReturnModal.prefab`. Confirm → `LoanService.Return` → refresh → toast `LOAN_TOAST_RETURNED_IN`.

### 4.3 Level Up modal on a borrowed asset

`LevelUpModalController.Open` for a borrowed character/club: level-up stepper works and pays from the borrower's RP through the existing `/progress/level-up` call (the server redirects the level to the lender — nothing changes client-side except the `not_available/on_loan` reason mapping to `PointsSpendGate.OfflineMessage`-class toast `LOAN_ERR_ON_LOAN`); the **SP allocation controls are hidden** and a hint `LOAN_SP_HINT` ("SP goes to the owner when the loan ends.") sits in their place. `CommitLevelUps` on a borrowed row: bump `currentLevel` only (no SP). On the **lender's** side the modal cannot open (button disabled, §4.1).

### 4.4 Cards and carousels

- `CharacterThumbnailCard.RefreshIcons`: a new prefab-authored `LoanBadge` at the card's **top-left** (8,8) — 44×44 circle Image #050F1F α 0.85 with a 2 px white outline and a 26×26 child Image swapped between `IconLoanOutSmall` / `IconLoanInSmall`; active from the flags (the Lv pill stays top-right; the existing `IconSelectedSmall` stack is untouched). `levelUpReadyIcon` forced off when `isLentOut`. `SetLocked(false)` for borrowed (it is playable).
- `ClubThumbnailCard.RefreshIcons`: the same `LoanBadge` at (8,8).
- `ClubCarouselController` needs no list change (§3 makes `GetAllOwnedClubs` include borrowed) — verify the sort/filter code does not read a persisted list.
- In-game club selector / character pick: no change (they read the managers).

### 4.5 Round RP

`RewardPointsManager.EnqueueServerEarn` → `PointsService.EnqueueEarn(action, amount, LoanService.Instance?.UsedLoanIdsForRound())`; `PendingPointsOp` gains `[JsonProperty("loans")] public List<string> LoanIds` (null when none; `ToEarnGameJson` emits `loan_ids` only when non-empty — older queued ops deserialise with null, no queue migration). The `UsedLoanIdsForRound` snapshot is taken **at round start** (hole load) and cached on `LoanService` so a loan that ends mid-round still splits that round's earns (the server drops it if it is no longer live — accepted asymmetry). Home / HoleComplete toast: when the earn result carries `loan_split.to_lenders > 0`, append `LOAN_TOAST_SPLIT_FMT` ("{0} RP to the owner") — NOTE: hook where the earn result is already surfaced (`PointsService.ReplayPendingRoutine` result callback), not a new UI.

## 5. Strings (EN + JA → `LocalizationText.csv` → importer PLAN/APPLY → publish `texts` → `--check` clean)

`LOAN_BTN_LEND` LEND / 貸す; `LOAN_BTN_RETURN` RETURN / 返す; `LOAN_BTN_CONFIRM` LEND / 貸す; `LOAN_MODAL_TITLE` LEND {0} / {0}を貸す; `LOAN_DAYS_1` 1 DAY / 1日; `LOAN_DAYS_3` 3 DAYS / 3日; `LOAN_DAYS_7` 7 DAYS / 7日; `LOAN_TERMS_FMT` They level it up, it comes back levelled. You get {0}% of the RP they earn with it. / 相手がレベルアップさせ、レベルはそのまま戻ってきます。相手が獲得したRPの{0}%があなたに入ります。; `LOAN_NO_FOLLOWING` Follow players in PLAYLIFE to lend to them. / 貸すにはPLAYLIFEでプレイヤーをフォローしてください。; `LOAN_WARN_EQUIPPED` This club will be removed from your bag. / このクラブはバッグから外れます。; `LOAN_STATUS_OUT_FMT` ON LOAN TO {0} · {1} / {0}に貸出中 · {1}; `LOAN_STATUS_IN_FMT` BORROWED FROM {0} · {1} / {0}から借用中 · {1}; `LOAN_TIME_D_H_FMT` {0}d {1}h / {0}日{1}時間; `LOAN_TIME_H_M_FMT` {0}h {1}m / {0}時間{1}分; `LOAN_SP_HINT` SP goes to the owner when the loan ends. / SPは返却時にオーナーに渡ります。; `LOAN_RETURN_TITLE_CHAR` RETURN CHARACTER? / キャラクターを返却しますか？; `LOAN_RETURN_TITLE_CLUB` RETURN CLUB? / クラブを返却しますか？; `LOAN_RETURN_CONFIRM` Return {0} to {1} now? It goes back at Lv {2} — the levels you bought stay with it. / {0}を今すぐ{1}に返しますか？Lv {2}で戻り、上げたレベルはそのまま残ります。; `LOAN_TOAST_LENT` {0} lent to {1} for {2} / {0}を{1}に{2}貸しました; `LOAN_TOAST_RETURNED_IN` {0} returned to {1} / {0}を{1}に返しました; `LOAN_TOAST_RETURNED_OUT` {0} is back · +{1} RP / {0}が戻りました · +{1} RP; `LOAN_TOAST_SPLIT_FMT` {0} RP to the owner / オーナーに{0} RP; `LOAN_ERR_NOT_FOLLOWING` You need to follow them first. / 先にフォローしてください。; `LOAN_ERR_ALREADY_ON_LOAN` Already on loan. / すでに貸出中です。; `LOAN_ERR_BORROWER_HAS_IT` They already have this one. / 相手はすでに持っています。; `LOAN_ERR_LIMIT_OUT` You can lend up to 3 at a time. / 同時に貸せるのは3つまでです。; `LOAN_ERR_LIMIT_IN` They are already borrowing 3. / 相手はすでに3つ借りています。; `LOAN_ERR_ON_LOAN` This one is on loan. / これは貸出中です。; `LOAN_ERR_GENERIC` Couldn't complete the loan. Try again. / 貸出を完了できませんでした。もう一度お試しください。

Zero hardcoded `.text` literals (grep quoted in the report). Dashboard strings: none (no admin panel in this task).

## 6. Telemetry (`RecordSafe`)

`loan_modal_open {kind, ref_id}`, `loan_lend {kind, ref_id, days}` (on `ok`), `loan_return {loan_id, early: true}`, `loan_ended_seen {loan_id, side: out|in, level_delta, rp}` (at reconcile).

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item PASS/FAIL with the measurement.

- [ ] Migration applied by Cesar (SQL pasted in chat; verification query output quoted); `fly deploy` green; `/api/v1/loans` 403-not-404 unauthenticated.
- [ ] **Live E2E with two real accounts** (Cesar's two): A lends a character to B for 1 day → B's Roster shows it BORROWED with `IconLoanInSmall` + time left; A's shows ON LOAN, locked, `IconLoanOutSmall`. B levels it once → `golfin_progress` row is **A's**, `golfin_progress_events.on_behalf_of = A`, B's RP debited (SQL quoted). B plays a hole with it → ledger shows B's `hole_complete` at 80 % and A's `loan_lender_share` at 20 % with the same base key (SQL quoted); A's row not in `golfin_leaderboard` for that RP. B taps RETURN → A's client shows the new level with unallocated SP and the `+N RP` toast.
- [ ] Same for a **club**: lending an equipped club removes it from the bag; borrowed club shows in B's Inventory and bag; durability does not move over a round; Repair disabled.
- [ ] Refusals: selected character → LEND disabled; lend the same asset twice → `already_on_loan` toast; lend to a non-followed id (curl) → `not_following`; 4th loan out → `limit_out`.
- [ ] Expiry: set a loan's `ends_at` to the past by SQL → next `GET /loans` flips it to `expired` and both clients reconcile without a relaunch (screen re-entry is enough).
- [ ] Borrowed rows never reach the blob: EditMode test on `InventoryCodec` + a live `profiles.golfin_inventory` read after B's session shows no borrowed ref.
- [ ] Rect self-diff (§4.1): on both panels the Compare+Lend row's left edge = the LevelUp button's left edge and its right edge = the Boost/Repair button's right edge, to the pixel; ribbon 537×72 at the Left panel's top; badge 44×44 at (8,8). Compare-mode mirrors byte-identical in the scene diff.
- [ ] Figma fidelity: the table above reproduced PASS/FAIL per row against the nine renders in `reference/`; playbook §7 crop-diff of the Right panel (all three states), the Left-panel ribbon, one card with a badge, the lend modal and the return popup.
- [ ] Strings: all rows EN+JA, PLAN/APPLY, published version, `--check` clean, zero new hardcoded literals.
- [ ] EditMode suite count before/after; new tests: `LoanService` DTO parse + reconcile (lent-out lock, borrowed ensure/remove, level catch-up SP sum), `PendingPointsOp` JSON with/without `loans`, codec skip.
- [ ] Telemetry rows seen then deleted. Console clean. `[SerializeField]` wired. Deviations flagged.

## Files / hierarchy this task touches

- playlife: `backend/migrations/2026_09_09_golfin_loans.sql` (table + `golfin_loan_split` + loan-aware `golfin_level_up` replace) — NEW; `backend/routers/loans.py` — NEW; `backend/routers/points.py` (`loan_ids`), `backend/main.py` (mount)
- `Assets/Scripts/Social/LoanService.cs`, `LoanDtos.cs` (+ Tests) — NEW; `Assets/Scripts/Net/Endpoints.cs`
- `Assets/Scripts/CharacterManager.cs`, `ClubManager.cs`, `BagManager.cs`, `UI/Roster/Data/PlayerCharacterData.cs`, `UI/Inventory/ClubData.cs` (`PlayerClubData`), `Save/SaveData.cs` (`reconciledLoanIds`), `InventorySync/InventoryCodec.cs` (+ test)
- `Assets/Scripts/Economy/PendingPointsOp.cs`, `PointsService.cs`, `PendingOpsQueue.cs`, `UI/Roster/Managers/RewardPointsManager.cs`
- `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs`, `CharacterThumbnailCard.cs`, `LevelUpModalController.cs`; `UI/Inventory/ClubDetailPanel.cs`, `ClubThumbnailCard.cs`, `ClubLevelUpModalController.cs`
- `Assets/Scripts/UI/Modals/LoanModalController.cs` — NEW; `Assets/Prefabs/UI/Modals/LoanModal.prefab` — NEW (+ `LoanReturnModal.prefab` if `SchemeConfirmModalController` cannot host it)
- `Assets/Scenes/ShellScene.unity` — Compare+Lend rows on both RightPanels; `LoanRibbon` + `LentDim` on both Left panels; card prefabs — `LoanBadge` each
- `Assets/Art/RosterScreen/IconLoanOutSmall.png`, `IconLoanOutBig.png`, `IconLoanInSmall.png`, `IconLoanInBig.png` — copied from `reference/`
- `Assets/Localization/LocalizationText.csv` — 29 rows (+ importer + publish)
- `Docs/Architecture/UI_ELEMENT_PALETTE.md` — LendButton / loan status label / loan modal rows; `Docs/AI_CONTEXT.md` — at close-out

## Smoke evidence

Editor run with two signed-in accounts (two Editor instances or Editor + device): screenshots of both detail panels in each of the five states of §4.1, the lend modal (populated + empty), the return confirm; SQL for progress / events / ledger after the E2E; scene diff of the two RightPanels; publish log; telemetry SQL.

## Out of scope (do NOT do these) — filed as Notion deferrals

- Lender early recall; offer/accept flow; push notification when a loan arrives.
- Admin dashboard Loans panel (ops visibility, share tuning — `LOAN_LENDER_SHARE_BP` stays a server constant).
- Durability wear / repair on a borrowed club (frozen in v1); SP allocation by the borrower.
- Lending to non-followed players (codes / marketplace); lending balls or items.
- Illustrated loan icon art (the tray+arrow glyph ships as designed).
- A Loans tab in the GPS profile / a "my loans" list screen — the detail panels are the only surface.
