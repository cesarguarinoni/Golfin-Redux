# SPEC — general_shop_ui (Order 610, Shop pillar)

**Status:** SPEC_READY (design pass complete, decisions locked with Cesar 2026-07-04).
**Tier:** 3 — FULL PIPELINE (new screen(s) + catalog data layer + nav entry + a save-schema change).
**Kickoff (fenced, copy-ready):**

```
Use the implementer subagent on "general_shop_ui"
```

---

## 0. What this is (and what the Figma actually revealed)

The general commerce shop, distinct from the stamina restaurant shop (`stamina_boost_shop`,
Order 517, shipped). The design pass pulled the Figma (`get_metadata` + `get_design_context`,
Lesson AK / Rule 9) and it is **not** the RP-only standalone shop the brief assumed. It is a
**REWARDS CENTER** with a three-tab shell — **GACHA | STORE | GIFTS** — where STORE is the active
tab. Prices in the mock are USD (`$1.99`, `$3.99`, `$4.99→$3.99`) — a real-money IAP storefront.
Cesar re-resolved the decisions against that reality (see §1).

**Figma source (re-pull at implementer step 0 — do NOT trust the token tables here as truth):**
- Store Screen (main commerce screen): node **`4079:28230`** — render at `reference/store_screen_4079-28230.png`.
- Store History Screen (purchase receipts, DEFERRED v1): node **`13509:2978`** — render at `reference/store_history_13509-2978.png`.
- File key: `5gEAHjl6xAtW8iYY7NMvWd`.
- `Docs/Scripts/figma_node_to_spec.py` auto-generates the Rule-21 linter `spec.json` from these nodes
  at implementer step 6e — do NOT hand-author it.

---

## 1. Locked decisions (D1–D6)

- **D1 — Sells Clubs + Balls in v1.** Not tickets/characters/items (those are gacha-adjacent or
  undefined). **610 absorbs the club-ownership economy** (Cesar's ruling — see §3) because clubs have
  no persisted grant target today; balls do.
- **D2 — Currency = RP.** Re-token the Figma's `$` prices to RP costs. The RP wallet already shown in
  the header (`RPContainer` pill) becomes the actual spend currency. Real-money/IAP is a LATER swap
  behind the same purchase seam — explicitly out of scope (§9).
- **D3 — Entry = the bottom nav bar store icon.** Tapping it opens the Rewards Center (STORE tab).
- **D4 — Card LIST, not grid.** Clone the stamina-shop Selection screen (itself a Tournament Selection
  clone). Inside the 3-tab shell.
- **D5 — Grant to inventory, NO auto-equip.** Purchased club/ball lands in inventory; equipping stays a
  separate roster/bag action.
- **D6 — Build the 3-tab shell; STORE live; GACHA + GIFTS present but GRAYED/disabled.** Same gray
  treatment for the not-ready STORE category chips and curation chips (§2).

---

## 2. Scope

### In (v1)
- The **3-tab shell** (GACHA | STORE | GIFTS). Only STORE routes to content. GACHA + GIFTS render
  in the tab bar but are **grayed/disabled** (no-op on tap; visually distinct from the active tab).
- The **STORE screen**: category filter row with **ALL / CLUBS / BALLS live**; TICKETS / CHARACTERS /
  ITEMS chips **grayed** (same treatment as the dead tabs). The curation row (POPULAR / OFFERS) is
  **grayed** for v1; only ALL is functional.
- A **card list** of purchasable items (balls + clubs), RP-priced, each with a gold **BUY**.
- **Purchase → RP spend → grant to inventory** via an extended `ShopTransaction` seam.
- The **club-ownership economy** (Phase A) — the fold-in that makes club sales real (§3).
- **Nav entry**: bottom-nav store icon → open the STORE tab.

### Out (deferred, named so they are not silently dropped)
- **Real money / IAP** — D2 is RP. The `$` prices in the mock become RP. Billing is a later swap.
- **GACHA tab / GIFTS tab** — grayed. GACHA is the next roadmap pillar; GIFTS is its own later pillar.
- **TICKETS / CHARACTERS / ITEMS** categories — grayed. Tickets/characters need the gacha system;
  ITEMS (repair kits: `itemQuantities["item_repair_*"]`) is a later economy tie-in.
- **POPULAR / OFFERS** curation views — grayed (no curation-flag data in v1).
- **Store History screen** (`13509:2978`) — DEFERRED. Needs a purchase-log persistence layer; not v1.
- **Cross-promotion banner** (`Cross Promotion Banner`, hidden in the history node, present in store) —
  render the slot but leave it a static placeholder / hidden; no live promo system in v1.

---

## 3. Architecture — the hard seam + sequencing (manage the two-features-in-one risk)

This order carries TWO concerns: a **club-ownership economy** (data/save) and a **shop UI**. They must
NOT tangle (that is the `tournament_signup_modal` failure mode). Build in two sealed phases, **A before B**,
A proven by EditMode tests before B depends on it.

- **Phase A — Club ownership economy** (pure data/save/logic; NO shop UI). Ships behind tests. When A is
  green, clubs are a real ownable/persisted/grantable thing.
- **Phase B — Shop UI** (shell + screen + cards + purchase wiring). Consumes A's grant API + the existing
  ball path. No save-schema work in B.

The seam between them is `ShopTransaction` (extended in B5) calling `ClubManager.GrantClub` (built in A)
and `SaveData.ballQuantities` (already exists). B never touches the save schema; A never touches UI.

---

## 4. Phase A — Club ownership economy (the fold-in)

**Why this exists.** Verified during the design pass: `ClubManager.InitializeClubs()` auto-seeds a
`PlayerClubData` for **every** club in `ClubDatabaseCSV` on Awake (rarity-based starting level via
`GetStartingLevel`), into a runtime-only `Dictionary<string,PlayerClubData> ownedClubs`. There is **no
persistence** (`ClubManager` has no Save/Load; `SaveData` has no club field) and **no acquisition API**
(no `AddClub`/`Grant`). So today every club is already owned and nothing survives relaunch. To *sell* a
club, ownership must become gated, persisted, and grantable. (This also resolves the P-006 /
`club_bag_population_concern` smell: club state not read from/written to save state.)

**A1 — Acquisition gate.** `InitializeClubs()` must stop auto-owning the full DB. On a fresh save it
seeds only a **starter set** (see A4 bag-safety); thereafter it hydrates owned clubs from the save (A2).
Non-owned clubs exist in the DB (catalog/templates) but are NOT in `ownedClubs`.

**A2 — Persistence.** Add a persisted club list to `SaveData`, mirroring the `PersistedCharacter` pattern.
New DTO `PersistedClub` carries the full mutable `PlayerClubData` state (source of truth =
`Assets/Scripts/UI/Inventory/ClubData.cs`):
`clubId, currentLevel, currentDurability, maxDurability, equippedBagSlot, totalSPEarned, spentPower,
spentAccuracy, spentLieResistance, spentDurability`. **Ownership = membership in the list.**
- `SaveData.ownedClubs = new List<PersistedClub>()` (name to be confirmed vs. existing conventions).
- Bump `schemaVersion` and add the field in the migrator (additive; absent list on old saves → handled
  by A3). Follow the existing v2→…→v5 additive migration convention exactly.
- `ClubManager` gains Save (write `ownedClubs` on grant/level/equip/repair changes) + Load (hydrate on
  Awake before/instead of the full-DB seed). Persist through the existing `Golfin.Save` host, not a new file.

**A3 — Migration policy (IMPLEMENTER FORK — flag for Cesar at build, do not guess silently).**
Existing saves have no club list, and today every player effectively owns every club. Two options:
- **(a) Grandfather-all:** on migrate, seed the full current DB set once (nobody loses their bag; the
  gate only affects *new* clubs added later). Safest for existing players.
- **(b) Starter-set reset:** on migrate, seed only the starter set (existing players lose non-starter
  clubs). Cleaner economy, but strips clubs players "had."
- **Recommendation: (a) grandfather-all.** It is non-destructive and still makes the gate + grant real
  for any club not in the current DB / future clubs. Confirm with Cesar before shipping the migration.

**A4 — Bag-safety invariant (hard).** The starter set + gate must NEVER produce an unplayable bag. The
game requires its default club types (wood/iron/putter/driver per `SelectorScreenshotHelper` bag order).
The starter set MUST guarantee a minimally playable bag on a fresh save. An EditMode test asserts a
fresh-save player has a playable bag (all required club types present/equippable).

**A5 — Grant API.** `ClubManager.GrantClub(string clubId)`:
- If already owned → return an already-owned result (clubs are unique; no stacking). B uses this to
  hide/disable BUY for owned clubs (§6).
- Else create `PlayerClubData` (starting level via existing `GetStartingLevel(rarity)`, full durability),
  add to `ownedClubs`, **persist**, fire `OnInventoryChanged`. Return success.
- Idempotent, side-effect-safe, no auto-equip (D5).

**A6 — Tests (EditMode, Phase A gate):** grant adds + persists + round-trips through save; grant of an
owned club is a no-op; migration (a) grandfathers all clubs; fresh save yields a bag-safe playable set;
hydrate-from-save restores levels/durability/equip. No UI, no Unity runtime needed.

---

## 5. Phase B — Shop UI

**B1 — Shell + nav entry.** New `ShopScreen` (Rewards Center) registered in `ScreenManager` (mirror
`ScreenId.TournamentSelection` registration). Header = standard Top UI (title "REWARDS CENTER" per Figma;
persistent RP pill `RPContainer` = the spend wallet, D2). Tab bar = GACHA | STORE | GIFTS; only STORE
routes; GACHA/GIFTS grayed + inert (D6). Wire the **bottom-nav store icon** → open `ShopScreen` (D3):
resolve the exact nav slot in the shipped `ShellScene` nav + the Figma `NavBarContainer` (`2098:7988`)
at step 0; if the shipped nav has no store slot, add one per the Figma.

**B2 — Catalog data layer.** New `Assets/Resources/Data/shop_catalog.csv` + a loader (reuse 517's
`ShopModel`/`ShopCatalog` CSV pattern). Columns (confirm against loader convention): `entryId, category
(club|ball), refId (clubId or ballId), rpCost, rarity, sortOrder, popular(bool, v1 unused), offer(bool,
v1 unused)`. v1 rows: the ball catalog (`ball_*` ids from `SaveData.ballQuantities` convention:
`ball_golfin`, `ball_pro`, …) + a club catalog subset (`clubId`s from `ClubDatabaseCSV`). **RP prices are
authored here** (the Figma `$` values are re-tokened to RP — pick RP costs at author time; not a 1:1 $→RP map).

**B3 — STORE screen (list).** Clone `StaminaShopSelectionScreenController` scaffold (scroll list,
`ScreenManager` registration, back nav) — it is itself the Tournament Selection clone. Bind the list to
`shop_catalog.csv` filtered by the active category chip. Category chips: ALL / CLUBS / BALLS **live**;
TICKETS / CHARACTERS / ITEMS **grayed**. Curation row POPULAR/OFFERS **grayed**; ALL functional.

**B4 — Card variants.** The Figma card ("Rankings Card" shell) has category-specific bodies:
- **Ball card:** icon/sprite + name + short desc + RP cost pill + gold BUY.
- **Club card:** club portrait + rarity background + `Lv x/y` + the 5 stat rows w/ parameter bars
  (reuse the Clubs-inventory stat-row pattern; matches `13509:9466`+ in the node) + RP cost pill + gold BUY.
Build the card shell once; two body variants. Node-exact geometry per Rule 21. **Verify the Figma→TMP
divisor per Lesson AK** (do not assume ÷1.4).

**B5 — Purchase wiring (extend `ShopTransaction`).** The 517 seam is RP-spend → stamina-grant with
`InsufficientRp`/`StaminaFull` enums. Extend it (its own class comment invites this):
- Generalize the grant: dispatch by category — **ball** → `SaveData.ballQuantities[ballId]++` (or set,
  respecting the `-1`=unlimited convention) + persist; **club** → `ClubManager.GrantClub(clubId)` (Phase A).
- Keep pre-check → `RewardPointsManager.SpendPoints(rpCost)` → grant → `onGranted` callback → result enum.
- Extend `PurchaseResult` with the general cases: `Success`, `InsufficientRp`, `AlreadyOwned` (clubs),
  `Invalid`. Drop/ignore the stamina-only `StaminaFull` path for the general shop (leave 517's usage intact).
- Success/deny → the existing `ToastController` pattern (as 517).

**B6 — Grant behavior (D5).** Grant to inventory, **no auto-equip**. Owned clubs (unique) → BUY hidden or
disabled (use `GrantClub`'s already-owned result / an `IsOwned` check). Balls are quantity-stacking, so
BUY stays enabled (increments quantity). No navigation to equip after purchase.

---

## 6. Element Reuse Map (Rule 22 — required; verified atoms in `UI_ELEMENT_PALETTE.md`)

The implementer MUST complete/verify this against the node at step 0; build atoms at node-exact geometry,
never null-sprite `Image` where the node shows a sprite (Rule 21 hard-fails it).

| Figma element | Reuse (palette atom / clone base) |
|---|---|
| Screen list scaffold + scroll + card list | `StaminaShopSelectionScreenController` (→ Tournament Selection clone base) |
| List card shell ("Rankings Card") | `TournamentSelectionCard.prefab` / stamina-shop card |
| Navy card / panel background | Navy card panel `Background - Next Hole.png` (`d162244f2dd5e8646afef2518d902a8e`) |
| RP cost pill (was `$` in mock) | RP value pill `RPContainer.png` (`9106f5ea…`) + RP coin `Reward Points Icon.png` (`aab2dfa3…`) |
| Gold BUY button | Gold button `Play Button.png` (`cff37a7f…`) |
| Back / cancel | Silver button `ButtonCancel.png` (`6021c639…`) |
| Rarity / discount badge | Two-layer badge pattern (`S_PillStadium.png` `bb07d102…` + dark inner fill + gradient TMP) |
| Club card stat rows + vertical divider | Clubs-inventory stat-row pattern + `DividerVertical.png` (`c9234f1f…`) |
| Section divider (horizontal) | `Divider.png` (`36b5ccd8…`) |
| Titles / body text | Rubik-SemiBold SDF (`39fb7824…`) / Rubik-VariableFont SDF (`0e84913c…`) |
| History icon (top-right filter) | present in node; DEFERRED with the History screen (render inert or omit v1) |

---

## 7. Currency (D2) — `$` → RP re-token

The mock prices in USD with strike-through discounts. v1 ships **RP**: author RP costs in
`shop_catalog.csv` (§B2) — this is a design re-token, NOT a $→RP conversion. The header `RPContainer`
pill (already on every screen) is the live spend wallet; `SpendPoints(rpCost)` debits it. The
strike-through "sale" affordance is **not** required for v1 (tie it to the deferred OFFERS flag later);
render a single RP cost. Real-money/IAP stays a future swap: the `ShopTransaction` seam is the single
point where a billing path would later replace the RP debit — keep it isolated so that swap is surgical.

---

## 8. Implementer forks (surface to Cesar; do not resolve silently)

1. **Migration policy (A3)** — grandfather-all (recommended) vs. starter-set reset. Confirm before shipping.
2. **Starter-set composition (A4)** — which clubs a fresh save owns. Must satisfy the bag-safety invariant.
3. **Already-owned club UX (B6)** — hide the card, or show BUY disabled + "OWNED". Recommend disabled+label.
4. **RP price authoring (B2)** — the actual RP numbers per catalog row (design values, not $-derived).
5. **Ball catalog scope (B2)** — which `ball_*` ids are for sale, and stacking vs. `-1` unlimited handling.
6. **Nav slot (B1)** — does the shipped `ShellScene` nav already have a store icon, or add one.
7. **Figma→TMP divisor (B4)** — verify per Lesson AK against the node render; do not assume ÷1.4.

---

## 9. Test gate + acceptance

**EditMode (Phase A, must pass before B):**
- Grant club → owned + persisted + save round-trip restores level/durability/equip.
- Grant already-owned club → no-op (no dup, no stat reset).
- Migration (a) grandfathers the full current DB; old save with no club list loads clean.
- Fresh save → bag-safe playable set (all required club types).

**Phase B / integration:**
- Purchase ball with sufficient RP → `ballQuantities` incremented + persisted + RP debited + success toast.
- Purchase club with sufficient RP → `GrantClub` fires, club owned + persisted, RP debited, BUY→owned state.
- Insufficient RP → deny toast, no grant, no debit.
- STORE category filter shows only live categories; grayed chips/tabs are inert.
- Nav store icon opens the STORE tab.

**UI fidelity (Rule 21):** `figma_node_to_spec.py` generates `spec.json` from `4079:28230` at step 6e;
`UIFidelityLinter` hard-gates node-exact geometry / no null-sprite Images. Demo capture = a normal-play
open of the shop from the nav icon + one ball buy + one club buy (normal play, per the video-capture rules).

---

## 10. Pipeline

Tier 3, FULL PIPELINE (implementer → self-review → reviewer → red-team → Cesar). Phase A is a save-schema
change — treat migration + bag-safety as red-team focus. Do NOT let Phase B UI work begin reporting DONE
until Phase A EditMode gate is green. On completion move this folder to `Docs/Specs/Completed/general_shop_ui/`.

**Kickoff (fenced, copy-ready):**

```
Use the implementer subagent on "general_shop_ui"
```
