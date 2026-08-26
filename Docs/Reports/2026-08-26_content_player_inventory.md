# `content_player_inventory` — architect report

**Date:** 2026-08-26 · **Spec:** `Docs/Specs/Active/content_player_inventory/`
**Status:** shipped to prod, verified, awaiting Cesar's approval (spec not yet moved to `Completed/`).
**Implemented by:** Claude Code (main thread, direct — not the subagent pipeline; backend + client,
no Figma node, no screenshot deliverable).

**Phase 4 of `CONTENT_PIPELINE_PLAN.md` §6 — the last piece of the original ask, and the expensive
one.** Collapsed into ONE spec rather than the 4a→4b→4c ladder: testers only, so the phasing that
existed to bound blast radius on real inventories was ceremony.

---

## 1. What shipped

Player inventory is server-side. One JSONB blob per player on `profiles.golfin_inventory`, written
behind a 30-second coalescing window, read back and merged additively at boot. An admin can see a
tester's inventory in the Users drawer and issue additive-only, idempotent grants through a queue the
client drains at launch.

| Layer | Size | What |
|---|---|---|
| Backend (`playlife`) | 923 lines | Migration + `routers/golfin_inventory.py` + 15 pytest cases |
| Unity | 2 129 lines | `Golfin.InventorySync` (10 files) + the Assembly-CSharp catalog adapter |
| Unity tests | 1 167 lines | 55 EditMode cases |
| Admin | 771 lines | Inventory tab, grant modal, data + mutation layers, fixtures |

Prod: `playlife-api` **v51 → v52** (image `deployment-01M0XZD461YMEZZ2X53PFCYWGJ`), backend commit
`4bd745b`. Migration applied by Cesar, all 7 verification rows as expected.

---

## 2. The three decisions worth reviewing

Everything else is application of these.

### 2.1 Deltas from the catalog default — and it buys more than bytes

A club sitting at its catalog default is stored as a **bare id string**; only fields that DIFFER are
written at all. That is Cesar's cost constraint from day one, and it is met with room:
**a 40-club tester blob measures 765 bytes**, against the ~3 KB the plan budgeted.

The part worth the architect's attention is the second-order effect. A bare id is not a frozen copy
of today's default — it is a **reference resolved at decode time**. So a published rebalance (new
starting level, new max durability) reaches every untouched instance for free, with no migration and
no server write, while a club the player actually levelled keeps the level they earned. That is the
same I1/I5 relationship the content overlay already has with the bundled CSVs, and it means Phase 4
and Phase 2 compose rather than fight.

The cost is a seam: expanding a bare id needs the catalog, which lives in Assembly-CSharp. Hence
`IInventoryCatalog` + `InventoryCatalogAdapter`, the same split `ClubCatalogSpec` already uses. When
the catalog is not up yet, `EmptyInventoryCatalog` answers "unknown" and everything encodes in full —
bigger, never wrong. **That is the only acceptable direction to fail in**: guessing a default would
encode a delta against a number the catalog never said, and the player's real level would be the
thing that disappeared.

### 2.2 The additive merge, and what it actually costs

Kept, as specified. Union ids, max levels/quantities, keep the higher durability, OR ownership,
`-1` (unlimited) beats every finite count.

The justification in the spec is right and I will not restate it. What the spec does **not** say out
loud, and the architect should decide about before real players, is the price:

> **An additive merge on a rev mismatch can refund a spend.**

Concretely: a player consumes a repair kit (5 → 4) on device A. Device B, holding a stale rev with 5,
pushes. The merge takes `max(4, 5) = 5`. The item is back. RP was debited server-side and stays
debited — so it is not RP duplication — but it is a free consumable, repeatable by anyone who
notices.

This is the direct, intended consequence of "never subtract", and for testers it is the correct
trade: a resurrected repair kit is a shrug, an inventory that silently loses a Legendary is a
support incident you cannot diagnose. But it is **exactly what §6 step 4d (server-authoritative
spends) exists to close**, and it means 4d is now the load-bearing next decision rather than a
nice-to-have. Naming it here so it is a choice rather than a discovery.

Note the asymmetry that makes this tolerable today: the blob's normal path is **not** a merge. A PUT
at a matching rev stores the client's projection verbatim, spends included. The merge fires only on
a genuine rev mismatch — two devices, or one device racing itself across a lost response. So the
refund is a concurrency artefact, not the steady state.

### 2.3 A stale rev is a 200, not a 409

The rev is optimistic concurrency, and the mismatch answer is
`{stored:false, status:"stale", rev, inventory}` at **HTTP 200** — the same shape as the existing
"taken" username and "insufficient" tournament-entry replies. A 409 would make `ApiClient` classify
a completely normal two-device outcome as a failure and log it as one.

Two supporting choices:

- **The rev check and the write are ONE statement** (`.update().eq(id).eq(rev, expected)`).
  Read-then-write leaves a window where two devices both read rev 4, both write rev 5, and the
  loser's data is gone with nothing recording that it ever existed.
- **The server deliberately does not merge.** The merge needs catalog defaults to expand a bare-id
  club, and those live in the client's bundled CSVs. A server-side merge would be a second
  implementation of the same rules against data the server does not have — two merge implementations
  is how you get two behaviours.

The client retries **exactly once** after merging. A second stale answer means a third writer in the
same window; it defers to the next 30 s window rather than looping, because looping converges no
faster and costs a request storm.

---

## 3. Deviations from the spec

Three, all deliberate, none narrowing the ask.

1. **Stamina condition is not in the blob.** SPEC §1 moves "`ownedCharacters` (level, SP,
   allocation)" — condition is none of the three, so excluding it *follows* the spec. Flagged
   because `PersistedCharacter` carries the field and its absence looks like an oversight. It is a
   time-regenerating pool: an additive merge on it (take the max) hands a player a free refill every
   time they touch a second device. That is a live economy exploit dressed as a sync rule.
   `InventoryProjector` zeroes it out and never writes it back; a restored character arrives at the
   "never written" sentinel, which the stamina layer hydrates to full — same as a fresh grant.

2. **`equippedBagSlot` is not maxed on merge; the local device wins.** SPEC §3 says "max of levels
   and quantities" — a bag slot is neither. Maxing it would silently equip a club the player
   deliberately left out of the bag *on this device*, and there is no "more equipped". A club already
   present keeps this device's slot; a club arriving from the blob keeps the slot it arrived with.
   **This is the deviation most likely to be wrong** — it is a judgment call about whether bag layout
   is property (sync it) or preference (don't). I chose preference. Easy to flip: one line in
   `InventoryMerge` and one in `InventoryProjector.RaiseClub`.

3. **A stale PUT is a 200** — see §2.3.

---

## 4. Evidence

| Gate | Result |
|---|---|
| Full unfiltered EditMode sweep | **1761 / 1758 / 0 / 3** (baseline 1706/1703/0/3; **+55 = exactly this task's tests**, zero failures, same 3 pre-existing skips) |
| Backend pytest | 15 new cases; whole suite **25 green**. Drives the real coroutines against an in-memory Supabase fake — the shipped code path, not a reimplementation |
| Admin | `tsc --noEmit` clean, `next build` green, verified live in `MOCK_MODE=1` (EN + JA, grant queued, audit row written) |
| Prod regression | `/health` · `/notices` · `/banners` · `/tournaments/golfin` all **200** after deploy |
| Prod routing | Four new routes **403** unauthenticated, **401** on a garbage bearer — mounted and auth-gated, not a 404 route miss |
| PostgREST schema cache | New columns and the grants table both visible to `service_role` |

**The schema-cache probe is the one I would keep.** PostgREST caches its schema, so a column can
exist in Postgres while the API cannot see it. That failure is silent *by design* here — the router's
`_missing_relation` handler degrades a missing relation to "never synced" so deploy order does not
matter — which means it would have looked precisely like a healthy empty inventory. Checking it
directly is the only way to tell a working deploy from a broken one that is failing politely.

**Not proven, and it is the device pass's job:** a full authenticated round-trip. That needs a real
tester JWT. Two checks close it — (a) play, background, wipe, reinstall, sign in, confirm the bag
comes back; (b) issue a grant from the admin drawer, relaunch, confirm it applied exactly once and
did not return on the launch after that.

---

## 5. Things the architect should know

### 5.1 Schema v11 is a one-way door — and Phase 4 partly rescues it

`SaveSchemaMigrator` fails hard when the file's version exceeds the code's (Q2). So a save written by
a v11 build **cannot be read by a v10 build** — it logs an error and falls back to defaults. Testers
roll TestFlight builds back; this is the first schema bump in a while, and that combination has teeth.

The pleasant accident: with Phase 4 live, a save bricked that way now **restores from the server** on
the next launch, through the same additive apply. The rollback risk is real but is no longer total
loss. Worth knowing before the device pass, because a tester who rolls back and reports "my stuff is
gone" may just need one more launch.

### 5.2 A failed grant fetch means no grants that session

`DrainGrants` is called only from `Boot()`, and `_grantsDrained` is set only on a successful fetch. A
boot that fails to reach the grants endpoint simply does not drain — the grants are still there next
launch. Deliberate (I5: grants land at next launch, never mid-session), but it means "I issued a
grant and the tester says they don't have it" has a boring explanation before it has an interesting
one: ask whether they have relaunched *twice*.

### 5.3 `appliedGrantIds` grows without bound

Append-only, never pruned. Grants are an admin action measured in dozens per tester, so there is
nothing worth pruning — and pruning would re-open the double-apply window for whatever it pruned.
Flagging it rather than defending it: if grants ever become a per-session reward mechanism, this list
becomes the wrong shape.

### 5.4 Two public accessors were opened on the managers

`ClubManager.BuildCatalogSpecs()` and `CharacterManager.BuildCharacterClampDefinitions()` are now
public, so the adapter reuses the rarity → starting-level tables instead of re-deriving them. Both
are pure reads. The alternative — a third copy of the rarity table — is exactly the duplication
`ClubCatalogSpec` was created to prevent, and a divergence would encode deltas against a level the
catalog never said.

### 5.5 The "not server-enforced" notice is load-bearing UI

Red banner at the top of the Inventory tab, above any data, EN + JA. It is the counterpart of the
Shop panel's price notice (§11.5) and exists for the same reason: moving something server-side makes
it very easy to assume it is now enforced. **It is not.** Everything in the blob is client-asserted;
a modified client can grant itself anything and this will faithfully back it up. A panel that lets an
operator believe otherwise is worse than no panel. Both files carry a comment saying it must not be
quietly dropped in a later redesign.

---

## 6. Residual risk and ops notes

- **The refund-a-spend window** (§2.2) is the headline risk. Bounded to rev mismatches; closed by 4d.
- **`MAX_BLOB_BYTES` is 256 KB**, a hostile-client bound, not a tuning knob. A realistic worst case —
  all 799 clubs at non-default state — is roughly 64 KB, so the ceiling is ~4× headroom. Nothing
  measures actual blob sizes in prod yet; the admin tab shows per-player bytes, which is the cheapest
  place to notice drift.
- **Server-owned fields are stripped, not rejected.** A client sending `rewardPoints` loses the field,
  not its sync. A rejection would mean an older projector bricks a tester's backup over a field the
  server was going to ignore.
- **Reads fail soft, writes fail loud.** A missing relation degrades GET to "never synced" and the
  grants queue to empty (so deploy order does not matter); PUT 500s. Silently accepting a backup that
  went nowhere is the worst outcome available.
- **RLS is on with zero policies** on `golfin_pending_grants` — deny-all for `anon`/`authenticated`,
  `service_role` bypasses. ⚠️ The Supabase SQL editor warns *"creates a table without enabling RLS"*
  on the `create table` statement: **false positive**, it lints that statement in isolation and the
  enable is three statements later. `grants_rls = 1` in the verification output is the proof.
- **Out of scope and untouched, per spec:** server-authoritative purchases, Addressables, art URLs,
  `LevelUpCosts`, and every content endpoint and catalog.

---

## 7. State

- **Shipped to prod and verified.** All 11 acceptance items PASS. Awaiting Cesar's approval; spec
  folder still in `Docs/Specs/Active/`.
- **playlife** `ee42f42` → `4bd745b`, pushed. `playlife-api` v52.
- **GolfinRedux** — this commit. Staged by explicit path: the pre-existing dirt in the working tree
  (`ShellScene.unity`, `LocalizationManager.cs`, four test files, `AppVersion.cs`, `TellCode.md`,
  `last_uploaded_build.txt`, `perf_baseline_2026-08-26.md`, `tasks/quit_transition_demo/`) was
  already there at session start, is not mine, and was left alone. Provenance recorded in
  `HEARTBEAT.log`'s kickoff baseline block.
- **The device pass is unblocked** and now carries two Phase-4 checks (§4).

### Open for the architect

1. **§2.2 — is a refundable spend acceptable through the beta?** If yes, nothing to do. If no, 4d
   moves up the queue and I would want to know whether it covers all four spend paths (clubs,
   characters, items, gacha) or starts with one.
2. **§3.2 — is the bag layout property or preference?** I chose preference (local wins). Two lines to
   flip if that is wrong.
3. **Should the grants queue get its own admin panel?** Today it is read-only inside the Users
   drawer, and there is no way to see grants across players or revoke a mis-issued one before it
   drains. Revocation is a genuine gap: an unapplied grant is deletable in SQL and nowhere else.
