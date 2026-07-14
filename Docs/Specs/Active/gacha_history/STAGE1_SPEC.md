# gacha_history — STAGE 1 SPEC (Screen + store + ticket types + ball stats)

**Read this together with `SPEC.md` §6 (Stage 1), `FORK_DECISIONS.md`, and `STAGE1_NOTES.md`.**
This addendum resolves everything Stage 1 was ambiguous about. **Every decision below is Cesar's,
recorded 2026-07-14. Do not re-litigate them and do not invent alternatives.**

Stage 0 is APPROVED and COMMITTED (`da877efa7`). The prefabs are Cesar-tuned. **Stage 1 reads the
LIVE prefabs and applies minimal diffs. It does NOT rebuild them.** Rebuilding a Stage-0 prefab from
scratch is an automatic FAIL at every gate.

---

## 0. Standing control (Cesar, 2026-07-14) — why this spec is so explicit

> *"Do the chain but exhort better control. I don't want to see things recreated from zero and
> blatant disregard to design."*

Three hard consequences:

1. **Nothing is built from scratch that already exists.** Every new visual element must cite a clone
   source (Rule 19 `## Clone provenance` table). If a mandated source cannot be found, set
   `IMPLEMENTER_BLOCKED` and SURFACE — never hand-roll (Cesar's standing rule).
2. **The Figma node is the design.** Re-pull `4079:18306` and row `13622:21105` with
   `get_design_context` at step 0 (Rule 9). Geometry comes from the node, not from memory.
3. **Where the node has NO design, this spec is the design.** The node contains a CLUB card only —
   there is **no ball card in Figma at all**. The ball card's treatment is specified in §3 below and
   is derived from shipped code. Inventing a different ball treatment is a FAIL.

---

## 1. Ticket types — REAL types now (Cesar's call)

**The problem:** today there is exactly one undifferentiated ticket — `SaveData.gachaTickets` (an
`int`, schema v7) behind `GachaTicketManager`. There is no ticket type, no currency enum, nothing.
The history row must show **which ticket was used** and **whether the item came from a 1-pull or a
10-pull**, and more ticket kinds are coming.

**Cesar chose: build real ticket types now**, including the save migration. Not a display-only fudge.

### 1a. The type

Add `TicketType` (new file, `Assets/Scripts/UI/Gacha/TicketType.cs`, namespace `GolfinRedux.UI.Gacha`):

```csharp
/// Ticket kinds. ENUM ORDER IS FROZEN — the int value is persisted in SaveData.
/// Adding a kind = append a new member. NEVER reorder or renumber.
public enum TicketType
{
    Standard = 0,   // the only kind that exists today (SaveData.gachaTickets, schema v7)
}
```

Only `Standard` exists today — do NOT invent `Premium`/`Gold`/etc. just to make the enum look fuller.
The point of the enum is that adding kind #2 later costs one line and no migration.

Display metadata (name + icon) is **data-driven, CSV-first** per project convention. Add
`Assets/Resources/Data/tickets.csv` (mirrors `gacha_banners.csv`):

```
ticketType,nameKey,iconSprite
0,TICKET,S_Store_Ticket_02
```

`iconSprite` resolves the SAME sprite the Stage-0 row already uses:
`Assets/Art/Shop/S_Store_Ticket_02.png` (Cesar re-imported it at the correct angle — use AS-IS, no
rotation). Loader mirrors `GachaBannerCatalog` (which already parses `gacha_banners.csv` — clone that
parser's shape, including the `internal` seam + `InternalsVisibleTo` so tests hit the PRODUCTION type,
not a local copy — that circular-coverage trap already bit us once on gacha_screen Stage 2).

### 1b. The wallet — SaveData v7 → v8

`JsonUtility` cannot serialize a `Dictionary`. Use a list of pairs, exactly like the existing
`PersistedClub` / `PersistedCharacter` pattern in `SaveData.cs`:

```csharp
[Serializable]
public class PersistedTicketBalance
{
    public int ticketType;   // (int)TicketType — enum order frozen
    public int count;
}
```

- `SaveData` gains `public List<PersistedTicketBalance> ticketBalances = new();`
- `CurrentSchemaVersion` → **8**.
- **`SaveSchemaMigrator` v7 → v8 MUST preserve the existing balance.** Move `gachaTickets` into a
  `ticketBalances` entry for `TicketType.Standard`, then zero the legacy field. A migration that
  drops a player's tickets is a hard FAIL.
- **Keep the `gachaTickets` field** on `SaveData` (do not delete it) so the v7→v8 migration can still
  read it off an old file. Mark it `[Obsolete]`/commented as migration-only and stop writing it.

**Do NOT silently change the test grant.** `SaveSchemaMigrator` v6→v7 and `GachaTicketManager.Awake`
BOTH seed 10 tickets as a dev test grant, and both carry a `TODO: revert to 0 before ship` that says
they must be reverted **together**. Carry that same paired TODO forward into the v8 code. Reverting
one and not the other silently refills emptied balances — the existing comments say so explicitly.

### 1c. GachaTicketManager — per kind

Rework to a per-kind API:

```csharp
int  GetTickets(TicketType type);
bool CanAfford (TicketType type, int amount);
void AddTickets(TicketType type, int amount);
bool SpendTickets(TicketType type, int amount);
event Action<TicketType,int> OnTicketsChanged;   // (kind, new balance)
```

**Update every existing call site** — do not leave a compatibility shim that quietly defaults to
Standard and rots. Known call sites (verify by grep, this list may be incomplete):
`Assets/Scripts/UI/PersistentUIManager.cs` (top-bar ticket counter),
`Assets/Scripts/UI/RewardGranter.cs`,
`Assets/Scripts/UI/Gacha/GachaTabController.cs`,
`Assets/Scripts/Save/Tests/GachaTicketTests.cs`.

**Top-bar counter:** `Standard` is the only kind today, so the shared top bar shows the Standard
balance. Do not redesign the top bar for multiple kinds — that is a future order. Leave a comment
saying so.

---

## 2. The history record + store (mock data — fork 2)

History rows are **mock** this order (real pulls do not exist yet). The store is NOT persisted to
save — only the ticket WALLET is. Do not add history records to `SaveData`.

`GachaHistoryRecord` (plain C#, `Assets/Scripts/UI/Gacha/GachaHistoryRecord.cs`):

| Field | Type | Feeds | Notes |
|---|---|---|---|
| `rewardType` | `GachaRewardType` enum | which row prefab to spawn | `Club, Ball` this order. `Character, Item, Ticket` are declared but NOT built (fork 4 — club + ball only). |
| `rewardId` | `string` | COL1 card | a `club_*` id (→ `ClubDatabaseCSV`) or a `ball_*` id (→ `BallDatabaseCSV`). |
| `quantity` | `int` | ball card `x99` badge | balls only; clubs are always 1. |
| `bannerId` | `string` | COL2 source line | resolves via `GachaBannerCatalog` → `nameKey` (e.g. `STANDARD CLUB 1`). Do NOT hardcode banner names in the row. |
| `ticketType` | `TicketType` | **COL3** icon + label | §1. This is the "what ticket was used" half of Cesar's answer. |
| `pullCount` | `int` | **COL3** `PULLS: N` | **1 or 10** — the "1-pull or 10-pull" half. |
| `pulledUtc` | `string` (ISO-8601) | COL2 date/time lines | rendered `PULLED yyyy/MM/dd` + `hh:mm:ss tt`, matching the Stage-0 rows. |

`GachaHistoryStore` — newest-first ordering, and a filter predicate by reward type (the sub-filter
chips are wired in **Stage 2**, but the predicate lands now so Stage 2 is a wiring job, per SPEC §7).

**Mock set:** ~12 records, a mix of clubs and balls across several rarities, newest first — enough to
make the list scroll and to exercise both row variants. Only real ids: `Clubs.csv` has the clubs;
`Balls.csv` has exactly TWO balls (`ball_golfin`, `ball_putt_ace`) — do not invent ball ids.

---

## 3. The ball card gets STATS (Cesar's call) — and there is NO Figma for it

**Cesar moved `Docs/Specs/Queued/gacha_history_ball_stats.md` INTO Stage 1.** That queued file is now
superseded by this section; mark it as absorbed.

**Critical:** the Figma row (`13622:21105`) contains a **club card only**. Confirmed by re-pulling the
node — its COL1 holds a `Clubs` instance, rarity frame, and a 6-row `Parameters` block. **There is no
ball card in the design.** So the ball card is DERIVED, and here is exactly how. Do not improvise.

### 3a. Ball stats are NOT club stats

| | Club | Ball |
|---|---|---|
| Stats | power, accuracy, lieRes, loft, durability | **power, rebound, windResistance, roll, spin** |
| Range | 0..100 | **−10 .. +10 (SIGNED)** |
| Extra rows | `180 yd` distance row; durability split bar | none |
| Rarity | yes | **NO rarity in the data model** |
| Level | yes | **NO level in the data model** |

Source of truth: `Assets/Scripts/UI/Inventory/BallData.cs` (`BallDataRuntime`: `power, rebound,
windResistance, roll, spin`; `PlayerBallData`: `ballId` + `quantity` only).

### 3b. Geometry = the club card's stat block. Bar = the shipped BALL bar.

- **Geometry:** reuse the club card's `Parameters` block verbatim from node `13622:21105` — the
  157×120 block, 20px rows, each row `HLayout gap 8` = `[icon 20×20][bar flex h-10 rounded-20][value
  20px white w-34]`. **5 rows** (one per ball stat). **No distance row, no durability split bar.**
- **Bar:** `Golfin.Inventory.BallSegmentedBar` — the EXACT component the shipped `BallDetailPanel`
  uses. `SetValue(value, maxValue: 10)` → 20 segments, centre = 0, positive fills right in blue,
  negative fills left in orange-red. It get-or-adds a `HorizontalLayoutGroup` and disables the smooth
  fill Image, so drop it onto the cloned `Bar` Image and call `SetValue`. **Do NOT normalise −10..+10
  onto a 0..1 fill** — Cesar explicitly rejected that (it hides the sign; a −6 rebound would read as a
  weak positive).
- **Stat icons:** reuse whatever `BallDetailPanel` / the Inventory Balls tab already uses. If a ball
  stat has no icon anywhere in the project, **surface it — do not draw one**.

### 3c. The ball row's metadata line 2 (Cesar's call)

The Stage-0 ball row's 2nd metadata line reads `MYTHIC · Lv 1`. **That is fake text I posed — balls
have no rarity and no level.** Cesar's call: **show the QUANTITY instead**, using
`PlayerBallData.quantity` / `BallManager.GetQuantityDisplay()` (which already returns `"x99"` / `"∞"`).
Keep the row's shape identical to a club row. Club rows keep `RARE · Lv 999`.

---

## 4. Screen + wiring

- `GachaHistoryScreenController` — spawns rows from the store into the LIVE
  `GachaHistoryScreen.prefab` content, picking `GachaHistoryRow.prefab` (club) or
  `GachaHistoryRowBall.prefab` (ball) by `rewardType`.
- **CLOSE** → back to the Rewards Center GACHA tab (`ScreenId.GeneralShop`).
- **The History chip → opens this screen.** It is currently a `Debug.Log` stub at
  `Assets/Scripts/UI/Gacha/GachaTabController.cs` line ~161, inside `WireHistoryChip()`
  (`HistoryChipPath = "HistoryChip"`, a direct child of the `GeneralShopScreen` root, with a `Button`
  whose onClick is empty in the prefab and wired at runtime). Replace the stub listener with
  `ScreenManager.Instance.ShowScreen(ScreenId.GachaHistory)`.
  **Cesar authorised touching the completed `gacha_screen` work for THIS AND NOTHING ELSE.** Any other
  edit to `GeneralShopScreen.prefab` or the gacha screen's scripts is out of scope and a FAIL.
- `ScreenId.GachaHistory` + the `ScreenManager` serialize field + the inactive ShellScene instance
  **already exist and are committed** — do not re-add them.

## 5. Prefab hygiene (from Stage 0 — read `STAGE1_NOTES.md`)

1. **Port the ball-card offsets into `GachaHistoryRowBall.prefab`** (`STAGE1_NOTES.md` §0). The
   Stage-0 fix lives ONLY on `GachaHistoryScreen.prefab`'s embedded copy, because edits to the row
   prefab do NOT propagate into it. Once rows are spawned at runtime from the row prefabs, delete the
   statically-posed rows from `GachaHistoryScreen.prefab` so the embedded copies cannot drift.
   **Gate:** club card and ball card must each sit 6.0px below their own row's top edge, and the
   `x99` badge must be fully inside the card bounds.
2. **Remove the hardcoded `#3380E6` stat-bar colour** from the static rows (`STAGE1_NOTES.md` §1). It
   was a Stage-0 stand-in for the unbound authoring default; the runtime binder owns bar colour, and
   leaving the hardcode masks the red low-durability state. Do NOT touch the shared `BagClubCard` /
   `ItemUseClubCard` / `GeneralShopCard` prefabs — Cesar confirmed they are NOT broken.

---

## 6. Test gate (EditMode, must be green)

Against the **production** types — not local copies (the gacha_screen Stage 2 circular-coverage trap:
tests exercised a local `ParseCsvDirect`, so they proved nothing. Use `internal` + `InternalsVisibleTo`).

1. **Save migration v7 → v8 preserves the balance.** A v7 save with `gachaTickets = 7` migrates to a
   v8 save whose `Standard` balance is 7. Non-negotiable.
2. `TicketType` enum values are frozen (`Standard == 0`).
3. `tickets.csv` parses via the production loader; ticket 0 resolves name + `S_Store_Ticket_02`.
4. `GachaTicketManager` per-kind add / spend / CanAfford round-trip through save.
5. Store: newest-first ordering; filter predicate per reward type.
6. Row bind: a record → card + metadata + ticket column **without throwing**, for every rarity and for
   BOTH row variants (club and ball).

## 7. Definition of done (Stage 1)

- EditMode tests green (§6), run via `tests-run`, output cited.
- Rule 21 UI-fidelity lint JSON cited with `fail == 0` for every prefab touched.
- Rule 19 `## Clone provenance` table — every reused element cites a real prefab path / asset path /
  GUID read back off the LIVE object.
- Rule 18 `## Figma fidelity` per-element table vs node `4079:18306` / `13622:21105`, **including font
  WEIGHT and rendered-size-vs-reference for every text element**. The ball card's rows are exempt from
  the node diff (no design exists) but MUST be diffed against §3 of this file instead.
- A real-flow screenshot: boot → Rewards Center → tap the History chip → History screen with live rows.
  **Not a synthetic harness** (Rule 2, real-entry rule).
- Editor left clean: exit play mode, ShellScene reloaded from disk, no scene residue.

**Stage 1 hard-gates on Cesar.** Surface the screenshot + a one-line "what changed", then WAIT.
