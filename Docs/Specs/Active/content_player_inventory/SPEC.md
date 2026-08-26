# SPEC — `content_player_inventory`

> **Phase 4 — the last piece of Cesar's original ask.** Collapsed into one spec: testers only, no
> real players, so the 4a→4b→4c ladder in `CONTENT_PIPELINE_PLAN.md` §6 is ceremony (Cesar,
> 2026-08-26). Build push + restore + two-way sync + grants in one pass.
>
> Plan: §6. Depends on Phases 0–2, all shipped.

## Status

`SPEC_READY`.

## Goal

Move player inventory server-side so it survives a reinstall, is visible in the admin, and can be
granted to. Keep the stored blob small — Cesar's cost constraint from day one.

## 1. Storage — one JSONB column

`profiles.golfin_inventory jsonb` + `profiles.golfin_inventory_rev int`.

⚠️ **Do NOT reuse `user_inventory`** — that table already exists and is the PARTNER APP's gift
inventory (`routers/gifts.py`). Different concern, different row.

**Shape: one blob per user, not a row per owned thing.** Row-per-owned-club is ~300 k rows at 10 k
players; a blob is ~3 KB each.

**Store only deltas from the catalog default.** A club at level 1 with full durability is just its
id. This is the cost requirement, and it means catalog rebalances propagate to untouched instances
for free.

| Moves | Stays server-owned (never duplicate) | Stays device-local |
|---|---|---|
| `ownedClubs`, `ownedCharacters` (level, SP, allocation), `itemQuantities`, `ballQuantities`, `ticketBalances`, `unlockedHoles`, `starterCharacterId`, `selectedCharacterId` | RP balance, `lifetimeRpEarned`, the daily/weekly/monthly accumulators, `tournamentEntries` | language, audio, UI state, `playedHoles` |

## 2. Endpoint

`GET` / `PUT /api/v1/user/golfin-inventory`. **Auth required**; the server stamps `user_id` from the
bearer token and never trusts one in the body — same posture as `/user/golfin-character`.

## 3. Sync

- **Write-behind** on `SaveDataHost.OnSaved`, coalesced to **at most one PUT per 30 s**, plus one on
  pause/quit. Never per mutation.
- **Read at boot**, merge, then continue.
- **Merge is ADDITIVE and never subtracts.** On a `rev` mismatch: re-fetch, union owned ids, take the
  max of levels and quantities, keep the higher durability.

  This stays even though testers' inventories are expendable — **it is what keeps loss
  diagnostic.** With additive merge a missing item is unambiguously a bug; with last-write-wins,
  loss is sometimes correct and you cannot tell which you are looking at. Worth more during testing,
  not less, and it is the hardest thing to change once real players exist. ~30 lines vs ~5.
- Subtraction happens only through an explicit server-side spend, which already exists for RP.

## 4. Grants queue

`golfin_pending_grants` — the admin's way to give a tester items without touching the blob.

- Client drains at boot, applies, acks.
- **Idempotent by grant id**; additive-only; impossible to subtract.
- Admin: a grant action in the Users drawer.

## 5. Admin

Inventory tab in the Users drawer: read the blob (owned clubs/characters/items, levels), and issue
grants. `checkAdmin()` + `writeAudit()` like every other mutation. Mock fixtures obviously fake.

## 6. Not authoritative — say so

This is sync and backup, **not anti-cheat**. A modified client can still grant itself anything;
moving inventory server-side does not change that, exactly as prices are not enforced by the shop
panel (§11.5). Put it on the admin panel so nobody assumes otherwise. Server-authoritative spends
are a separate, later decision.

## Acceptance

- [ ] Blob round-trips; a fresh install with no local save restores from it
- [ ] Blob is deltas-from-default — a default-state club is just its id; paste a real blob and its byte size
- [ ] Write-behind coalesces: 10 rapid mutations produce ONE PUT, plus one on pause
- [ ] `rev` mismatch merges additively — union ids, max levels; nothing lost on either side
- [ ] A grant applies once and is idempotent across three boots
- [ ] Admin shows a real tester's inventory and can grant; audit row written
- [ ] RP, leaderboard accumulators and tournament entries are NOT in the blob
- [ ] Offline: no sync, no exception, local save unaffected
- [ ] "Not server-enforced" notice visible on the panel
- [ ] `/health`, `/notices`, `/banners`, `/tournaments/golfin` still 200 after deploy
- [ ] Full unfiltered EditMode sweep green (baseline 1706 / 1703 / 0 / 3)

## Out of scope

- Server-authoritative purchases / anti-cheat.
- Addressables, art URLs, `LevelUpCosts`.
- Any change to the content endpoint or catalogs.
