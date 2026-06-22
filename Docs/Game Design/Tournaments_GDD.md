# Tournament System — Game Design Document

> **Status: v1 — DECISIONS LOCKED (approved for implementation 2026-06-22).** Implementation plan: `Tournaments_Implementation_Plan.md`.
> Scope: simple, time-boxed, leaderboard stroke-play tournaments. Bots first, real async players later (architecture mapped now).

---

## 1. Design Pillars

1. **Simple stroke play.** One course, one score (total strokes), one leaderboard. No brackets, no elimination, no real-time play in v1.
2. **Time-boxed & async.** A tournament is a course open for a UTC window. Everyone plays the same holes during the window; ranked at close.
3. **Bots now, players later — same pipe.** All score data flows through one backend interface. v1 fills the field with deterministic bots; the server impl swaps in without UI changes.
4. **Resume anywhere.** Progress persists per hole through the existing `Golfin.Save` layer. Quit, relaunch, finish later — as long as the window is open.
5. **Forward-compatible by construction.** Because the sim is deterministic, a round result carries enough data (seed + input log) for a future server to re-simulate and verify the score. We design that shape in now even though v1 doesn't use it.

### Non-goals (v1)
Real-time/synchronous play · single-elimination brackets · leagues/divisions · near-pin sub-challenge · OS push notifications · in-game mailbox · server backend · per-region local-time windows (UTC only).

---

## 2. Core Loop

```
Home ──(banner / menu)──► Tournament Selection
                              │
                              ▼
                    Tournament Detail ──► Sign up (Free or RP fee, lock character)
                              │
                              ▼
                    Play course holes  ◄── resume across sessions
                    (one attempt / hole, stamina-gated, saved after each)
                              │
                    (window endUtc reached)
                              ▼
                    Results computed ──► Leaderboard (final) ──► Result screen ──► Claim prizes
```

1. Player enters from Home (menu entry or notification banner).
2. **Selection screen** lists tournaments by state (Active / Upcoming / Ended) with course, countdown, entry cost, prize preview, entrant count.
3. **Sign up**: free or RP fee, lock character/loadout for the entry (§ Decision S1). RP debited once via `RewardPointsManager` → `SaveDataHost`. One entry per player per tournament.
4. **Play** the course's holes in order. Same shot mechanics as Story / 1v1. **Each hole consumes stamina** (existing system) — if the locked character is out of stamina, the player waits for regen / uses items, which is the natural driver of multi-session play. Score = strokes; per-hole time also recorded (tie-break + future near-pin).
5. **Save** after each completed hole (`SaveDataHost.MarkDirty()` → debounced atomic write). Leave and resume freely.
6. At **`endUtc`**, the entry is frozen (auto-submitted as-is; DNF if unfinished). Leaderboard finalizes after a short `resolveDelay`.
7. **Result screen** on next open shows final rank + prizes → Claim.

---

## 3. Tournament Lifecycle (state machine)

| State | Meaning | Player can… |
|---|---|---|
| **Upcoming** | `now < startUtc` | View, see prize table, (optional pre-register) |
| **Active** | `startUtc ≤ now < endUtc` | Register, play, resume, see **provisional** leaderboard |
| **Resolving** | `endUtc ≤ now < endUtc + resolveDelay` | See "computing results"; no more play |
| **Resolved** | leaderboard final, prizes claimable | View final leaderboard, claim prize |
| **Archived** | after retention window | View final standings only |

State is **derived from `now` vs. the window** (plus a `resolved` flag once prizes are committed) — never hand-set. This is the mechanism behind "show results only at a valid time": the Result/Leaderboard-final UI is gated behind `now ≥ endUtc + resolveDelay`. Before that, the leaderboard is explicitly labelled **Provisional / Live**.

---

## 4. Time Authority

- **All windows are UTC**, stored as ISO-8601 in the tournament definition.
- v1 has no server clock, so time goes through an **`ITournamentClock`** abstraction:
  - **v1 impl** = device `DateTime.UtcNow`. (Spoofable, but with bot-only fields and modest RP prizes the competitive-integrity risk is low.)
  - **Later impl** = NTP / server time. Swapping it hardens every window check at once.
- Result reveal, "ends soon" warnings, state transitions, and **bot reveal pacing** (§7) all read `ITournamentClock.UtcNow` — never `DateTime.UtcNow` directly. One seam, future-proofed.

---

## 5. Scoring & Ranking

- **Primary key:** total strokes across all holes, ascending. Displayed relative to par (`-3`, `E`, `+5`) for UX; raw strokes drive the sort.
- **One attempt per hole — strokes stand.** No replays / mulligans within the window (Decision #4).
- **Did-not-finish:** players who didn't complete all holes before `endUtc` rank **below all finishers**, ordered by holes completed (desc) then strokes (asc).
- **Ties** are a first-class case — see **§6** for the full tiebreak ladder and prize handling.

---

## 6. Ties & Tiebreakers

Stroke play produces ties routinely (short courses + bots make them common), so ties are designed for explicitly, not patched. Two separate questions: how to **order** tied entries, and how to **pay** them.

### 6.1 Tiebreak ladder (ordering)
Applied in sequence; the first step that separates two entries decides. Every step but the last is a skill/effort signal:

1. **Total strokes** — the primary score.
2. **Countback** — fewest strokes over the closing holes: back-9 → back-6 → back-3 → back-1, then front-9 → front-6 → front-3 → front-1 if still level. The standard golf method; rewards finishing strong. Defined over the tournament's ordered `holeSet` (works for 9- or 18-hole sets).
3. **Total completion time** — sum of per-hole timers; faster wins.
4. **Submission timestamp** — earliest finish. Deterministic final fallback so the sort is always total.

Reaching step 4 means two entries posted **identical strokes, identical countback, and identical total time** — genuinely indistinguishable play. (Bots carry seeded times + timestamps, so resolution is fully deterministic and reproducible.)

### 6.2 Prize handling for genuine ties (payout)
Entries still level after steps 1–3 **share the placement** (e.g., two tie for 2nd → both show **T2**, the next entry is 4th). Prizes are then **split-pool**:

- **Pool** the prizes for every position the tie spans (a 2-way tie for 2nd spans ranks 2 and 3 → pool the rank-2 and rank-3 rewards).
- **RP:** split evenly, **rounded up** (player-favorable).
- **Indivisible items** (a club, a single ticket): **grant a copy to each tied player.** True ties this deep are rare enough that the cost is negligible, and it avoids a "lost the club by a timestamp" feel-bad.
- The **step-4 timestamp only fixes display order** — it never changes a tied player's prize value.

**Why split-pool:** it's the sports-authentic model *and* it cleanly handles the nasty edge case where a tie **straddles a prize-band boundary** (e.g., spanning rank 10 and 11 where top-10 and 11+ pay differently) — both bands' prizes for the spanned positions go into the pool and split, so no one is arbitrarily shoved across the boundary by a tiebreaker.

### 6.3 Edge cases
- **DNF ties:** same ladder over completed holes (countback / time / timestamp), but always ranked below finishers.
- **Player vs bot tie:** no special case — bots participate in the ladder with their seeded strokes / time / timestamp.
- **Multi-way ties:** the pool spans all occupied positions; split N-ways, RP rounded up.

### 6.4 Decision needed
- **D-Tie — indivisible-item rule.** Default = **duplicate to each tied player** (generous, simple). Alternative = **award by step-4 timestamp order** (tighter economy). *Rec: duplicate for v1; revisit only if the prize economy needs control. All ordering steps 1–4 are locked.*

*Implementation note: countback needs per-hole strokes, which `perHole[]` already stores (§12) — no extra data. Tie ordering + split-pool live in `LocalTournamentBackend` ranking/prize resolution (Order T4).*

---

## 7. Bots — Pre-Rolled, Revealed Organically (Decision #3)

The field is generated **deterministically at tournament creation** from a seed (`tournamentId`-derived), so the same tournament always yields the same bots/scores.

- **Pre-roll the card:** each bot's full result (per-hole strokes + total) is rolled at creation from the seed, using skill-bracket distributions anchored to course par (reuses the `bot_difficulty` bracketing concept).
- **Seeded pace schedule:** each bot also gets a deterministic schedule — a start offset plus per-hole completion timestamps spread across the window.
- **Organic reveal:** `GetLeaderboard()` **projects each bot's card through its schedule at `ITournamentClock.UtcNow`** → bots visibly progress (`thru 3`, `thru 7`, `thru 9`) and scores trickle onto the board over the window instead of all appearing at t=0. By `endUtc` every bot has completed.
- This is a **pure function of (seed, now)** — reproducible, needs no background process and no server. The "live" leaderboard is computed on read.

*(Sim-played bots — running the real versus bot through the deterministic sim — remain a future option when we want provably sim-consistent bot scores. Not needed for v1.)*

---

## 8. Real-Players-Later Architecture (mapped now)

Single seam: **`ITournamentBackend`**.

```
ITournamentBackend
  GetTournaments()                  → list of TournamentDefinition + derived state
  GetTournament(id)
  Register(id, entryPayment, characterId)  → debit RP if needed, create entry, lock character
  GetMyEntry(id)                    → resumable progress
  SubmitHoleResult(id, holeResult)  → append + persist (local) / POST (remote)
  GetLeaderboard(id)                → provisional (projected) or final
  GetResults(id)                    → final rank + prize grant (gated by state)
  ClaimPrize(id)
```

- **v1: `LocalTournamentBackend`** — tournaments from CSV, bot field generated locally, player entry stored in `Golfin.Save`, leaderboard merged (projected bots + local player) on read.
- **Later: `RemoteTournamentBackend`** — same interface over REST. UI, ScreenManager flow, and screens are unchanged.
- **Anti-cheat shape designed in now:** `HoleResult` / round submission carries `rngSeed` + an `inputLog` (shot commands). v1 ignores it; a future server **re-simulates** the deterministic sim from the log and rejects mismatches. Designing the DTO now means no save-schema break later.
- **Tournament definitions:** v1 from bundled/remote-config CSV; later server-issued. Same `TournamentDefinition` DTO either way.

---

## 9. Data Model (CSV-first)

**`tournaments.csv`** — one row per tournament (this is the "CSV with data for all tournaments" from Decision #2; hole count varies per row):

| col | notes |
|---|---|
| `id` | stable string id (seeds bot field) |
| `nameKey` | localization key (JP/EN) |
| `courseId` | existing Country-Club/Course id |
| `holeSet` | which holes, per tournament (e.g. `1-9`, `1-18`, `1,4,7`) |
| `startUtc` / `endUtc` | ISO-8601 UTC |
| `resolveDelayMinutes` | grace before results reveal |
| `entryType` | `Free` \| `RP` |
| `entryFeeRP` | int, 0 if free |
| `maxEntrants` | optional cap |
| `botFieldId` | → bot field config |
| `prizeTableId` | → prize table |

**`tournament_bot_fields.csv`** — `botFieldId`, bot count, skill-bracket weights, pace-spread params (for organic reveal).

**`tournament_prizes.csv`** — see §10.

Authoring a tournament is **data-only**: one CSV row + a prize-table id + a bot-field id. No code.

---

## 10. Prize Logic & "a coherent way to populate it"

Prizes are **rank-band tables**, keyed by `prizeTableId`. Reward types **reuse existing inventory systems** so nothing new has to be invented to grant them:

**Reward types:** `RP` · `GachaTicket` (gold/blue) · `Consumable` (balls, repair kits, boosters) · `Club` / `Bag` · `CharacterShard` (future) · `Cosmetic` (title/badge).

**`tournament_prizes.csv`:**

| col | example |
|---|---|
| `prizeTableId` | `major_9hole` |
| `bandType` | `Rank` \| `Percentile` |
| `bandFrom` / `bandTo` | `1`/`1`, `2`/`3`, `4`/`10`, or `0`/`10` (top-10%) |
| `rewardType` | `RP` |
| `rewardId` | (item id, blank for RP) |
| `quantity` | `5000` |

**Percentile bands** (top 10% / 25% / finisher) let one table scale across any field size — solving the "coherent population" problem. Ship **3 templates** so authoring is a pick-list:

- **Small** (free entry): finisher consolation + modest top-3 RP.
- **Medium** (small RP fee): RP curve + a gacha ticket for top bands.
- **Major** (larger fee): big RP top-end + club/booster + tickets, percentile consolation.

**Grant flow:** at **Resolved**, the backend computes each entrant's band → prize (tie spans handled by split-pool, §6.2). **Player claims on the Result screen** (Decision #5) → granted via `RewardPointsManager` / inventory. A `claimed` flag (save in v1, server later) guarantees one-time grant. Cancelled tournament → RP entry fee refunded. No mailbox in v1.

---

## 11. Screens

The requested screens plus supporting hooks. **Bind to existing UI patterns** (`ScreenManager` for full screens, `ModalController` for overlays, event-driven `Action` binding) — don't rebuild hierarchies. **Full element breakdown, tokens, and states are in §16 (UI Design Specification).**

1. **Tournament Selection** *(full screen)* — filter tabs **All / Open / Active / Closed** (§16.2); card per tournament: image, name, club + holes, countdown, fee, prize, sign-up area, rankings icon; **expand-in-place** for flavor text (§16.3–16.6).
2. **Tournament Detail → expand-in-place** *(no separate screen in v1 — §16 U1)* — tapping a card expands it for flavor/description; rules, prize preview and the **character-lock** picker (at sign-up) ride on the expanded card / sign-up modal.
3. **Leaderboard** *(full screen)* — ranked rows (rank, name/flag, score-to-par, thru X/N, time); **Provisional** banner while Active, **Final** after Resolved; sticky highlighted player row. Tied entries share a placement (T2, T2…) per §6.2.
4. **Result** *(full screen, sequenced)* — your final rank + score + prize(s) + Claim. Only reachable when state = Resolved.

**Supporting:**
- **Home notification banner** — reuse/extend the existing Home banner for: *Registration open* · *Entered — ends in T* · *Ends soon (<X h) & unfinished* · *Results ready* · *Prize unclaimed*. *(NOTE: confirm the banner component in spec phase.)*
- **In-round HUD** — same as Story/1v1, plus "Hole X / N" + running total (+ optional live position) + stamina readout (existing).

---

## 12. Save State — Resumable Round (concrete)

Plugs straight into `Golfin.Save` (no new persistence tech):

- New flat DTO `PersistedTournamentEntry` added to `SaveData`; bump `schemaVersion 2 → 3` with a `SaveSchemaMigrator` v2→v3 step that seeds an empty tournament list (fail-hard-on-newer behavior preserved).

```
PersistedTournamentEntry {
  tournamentId
  registeredUtc
  characterId            // locked at registration (Decision S1)
  currentHoleIndex
  perHole[]      { holeIndex, strokes, timeMs, rngSeed, inputLog? }
  totalStrokes
  status         InProgress | Submitted
  claimed
}
```

- **After each completed hole:** write into `perHole[]`, advance `currentHoleIndex`, `SaveDataHost.MarkDirty()` → debounced atomic save. Crash/quit-safe by design.
- **Resume:** load entry, jump to `currentHoleIndex`, restore locked character.
- **Window closes mid-round:** entry auto-marked `Submitted` (DNF if `perHole.Count < holeCount`).
- `perHole[]` also powers countback tiebreaks (§6.1) — no extra data needed. `inputLog` is written now (forward-compat for server verification) — can be empty/cheap in v1.

---

## 13. Notifications / "About to End" Warning

Driven entirely by `ITournamentClock.UtcNow` + the player's entries — **no push in v1**, local banner only, with a hook reserved for OS push later. Banner priority (highest first): *Prize unclaimed* → *Results ready* → *Ends soon & unfinished* → *Entered* → *Registration open*.

---

## 14. Decisions — LOCKED

| # | Decision | Ruling |
|---|---|---|
| 1 | Stamina interaction | **Tournament holes consume stamina** (same as normal play). Drives multi-session + item use. |
| 2 | Course / hole count | **Varies per tournament**, set in `tournaments.csv` (`holeSet` column). |
| 3 | Bot scores | **Pre-rolled at creation, revealed organically** across the window to simulate play times (§7). |
| 4 | Re-attempts | **None** — strokes stand, no mulligan. |
| 5 | Prize claim | **Claim on the Result screen.** No mailbox in v1. |
| 6 | Hole order | Sequential, complete a hole to advance (default). |
| 7 | Registration cutoff | Register any time before `endUtc` (default). |
| 8 | Field structure | Single global field; divisions later (default). |
| 9 | Cancellation | RP entry fee refunded on cancel (default). |
| 10 | Time authority | Device-UTC for v1; server/NTP when remote backend lands (default). |
| Tie | Tiebreak ladder | **Strokes → countback → time → submit-timestamp** (§6.1). Locked. |

### Still-open sub-decisions
- **S1 — Character lock vs swap.** Stamina consumption (Decision #1) means a single locked character can be stamina-gated out of finishing a short-window tournament. **Default: lock character/loadout at registration**, size windows so stamina regenerates across the hole set, allow stamina items. Alternative: permit character swap between holes. *Confirm before the play-integration phase.*
- **D-Tie — indivisible-item rule (§6.4).** Default: **duplicate the item to each tied player**. Alternative: award by submission-timestamp order. *Rec: duplicate for v1.*

---

## 15. Success Criteria (v1)

- A tournament can be authored entirely from CSV (row + prize table + bot field) with zero code.
- Player can enter, play part of a course, quit, relaunch, and resume with no progress loss.
- Provisional leaderboard visibly fills in over the window (organic bot reveal); reads "Final" only after `endUtc + resolveDelay`.
- Ties resolve deterministically (countback → time → timestamp); tied placements pay by split-pool with no band-boundary unfairness.
- Prizes grant exactly once, via existing inventory/RP systems.
- Swapping `LocalTournamentBackend` → `RemoteTournamentBackend` requires **no UI changes**.

---

## 16. UI Design Specification (Figma)

> Grounded in live Figma frames (file `5gEAHjl6xAtW8iYY7NMvWd`): Hole Selection page `12885:87551`, Shop v5.1 Selection `13167:317`, Shop v5 Detail `13131:1071`, Rankings `4003:7960`, 1v1 Result `13274-877` / `13275-2628`. Tokens below are read from those frames, not estimated.

### 16.0 Locked UI decisions (2026-06-22)
- **U1 — Detail = expand-in-place.** Tapping a tournament card expands it in place to reveal flavor/description text; there is **no separate Tournament Detail screen** in v1. Supersedes §11 #2 and the "Tournament Detail" node in the §2 loop — sign-up, prize preview and the character-lock picker are delivered on the expanded card / sign-up modal.
- **U2 — Leaderboard = single board.** One ranked list per tournament; **no Daily/Weekly/Monthly period tabs**. Reuses the built `RankingsScreen` pattern (Top3 podium + scroll list + pinned player row). Metric = **total strokes ascending** (lower = better), shown to-par.
- **U3 — Sponsors = single presenting-sponsor mark** for v1 (one logo, not a sponsor row).

### 16.1 Shared tokens (read from frames)
| Token | Value |
|---|---|
| Canvas | 1170 × 2532, 48px side margins → content 1074 |
| Top bar | instance, gradient `#082540→#1F527F` |
| Back panel | 1074w, radius 40, gradient `#133453→#091B33` |
| Card | 978w × 360h, radius 50, gradient `#133453→#091B33` |
| Card list | vertical auto-layout, **gap 24** |
| Filter pill | rounded pill radius 28, gradient `#133453→#091B33`, segments split by vertical line dividers, padding 0/28 |
| Fonts | Rubik (Bold/SemiBold/Medium/Regular), Noto Sans JP Bold |
| Gold accents | `#EBD170` · `#EEDC9A` · `#FAC74D` |
| Prize green `#73E080` · Link blue `#8CD1FF` · RP-pill bg `#001E39` | |
| Reusable instances | Nav bar, scrollbar, arrows |

### 16.2 Tournament Selection (full screen)
Hole-Selection scaffold: top bar → **filter strip** → back panel containing the **card list** → bottom nav; scrollbar + arrows.
**Filter tabs:** `All` (all open + closed) · `Open` (open only) · `Active` (player is entered) · `Closed` (past). Single pill, 4 segments + dividers.

### 16.3 Tournament card — collapsed
| Element | Source pattern | Token |
|---|---|---|
| Time-left badge (top-right, absolute) | Shop "★ FEATURED" pill | radius 22, solid fill, label 20px Rubik Bold `#0A1A30` |
| Tournament image (left bleed) | Shop storefront thumb | 260×360 image fill, clipped by card radius |
| Club name + holes (eyebrow) | Shop category line | 24px Rubik SemiBold, gradient `#FFFFFF→#D1D6E0→#828FA1` |
| Tournament name | Shop title | 42px Noto Sans JP Bold / Rubik Bold, white |
| Presenting sponsor (single mark) | Shop logo chip | single logo image ~36×29 |
| Dates – countdown | Shop "Hours + Map" row | 22px Rubik SemiBold white, `#C7D6EB` separators |
| Prize (+ fee) | Shop "+STA" + RP pill | prize 32px Rubik Bold `#73E080`; RP pill 200×52 radius 40 `#001E39`; fee small label |
| Sign-up area | Main Buttons instance (resize, not rescale) | see §16.5 |
| Rankings icon | podium icon (Figma `12885-89938`) | small icon-button → that tournament's board |
| Expand affordance | Shop chevron | `›` 80px `#3E7CA8` |

### 16.4 Time-left badge — states
| State | Fill |
|---|---|
| Soon (upcoming) | muted blue |
| Open | green |
| Short time left | amber |
| Closing | red |
| Closed | grey |

Same pill; fill + label swap only.

### 16.5 Sign-up area — states
| Condition | Control |
|---|---|
| Open & not entered | **SIGN UP** button (Main Buttons) |
| Entered, in progress | **PLAY** / **CONTINUE** |
| Entered, finished | **DONE** indicator |
| Closed | **CLOSED** chip |
| Upcoming | **STARTS IN …** chip |

### 16.6 Tournament card — expanded (U1)
Tapping expands the card; adds the **flavor/description paragraph** (30px Rubik Medium, white) below the info block — mirrors the Hole-Selection expanded pattern. Single-expanded invariant (expanding one collapses the others). No separate Detail screen.

### 16.7 Leaderboard (full screen, U2)
Reuse the built `RankingsScreen` prefab pattern: Top3 podium → scrolling rows → pinned player row. **Single board** (no period tabs). Rows: rank, name, score-to-par, `thru X/N`, time. **Provisional** banner while Active; **Final** after Resolved. Ties share placement (`T2, T2…`, §6.2). Sort = strokes ascending.

### 16.8 Result (full screen, sequenced)
Align to the 1v1 result frames (`13274-877` / `13275-2628`): final rank + score + prize(s) + **Claim**. Reachable only when state = Resolved; appears after the result banner.

### 16.9 Figma conventions
- **Auto-layout everywhere** — no absolute positions except the top-right badge overlay.
- **8px-multiple** gaps/paddings (8/16/24/32); list gap 24, side margins 48.
- **Shared tokens** — same gradients, fonts, panel radii, gold accents as Shop/Hole/Rankings; **Main Buttons** real component instances (resize, never rescale); rarity/icon assets via existing helpers, never hardcoded.
