# Tournament Bot Field (T3) — Spec

> **Order:** EPIC 500 · depends on **T1 ✓** (contracts `BotFieldConfig`, `BotCard`, `TournamentDefinition`, `TournamentLeaderboardEntry`). **Logically independent of T2** — consumes the `BotFieldConfig` *type*, not its CSV loader; build & test against POCO fixtures. Feeds **T4** (`LocalTournamentBackend.GetLeaderboard` calls the projection).
> **Design source:** `Docs/Game Design/Tournaments_GDD.md` §7 (Bots — Pre-Rolled, Revealed Organically), §6 (ties), §5 (scoring), §4 (clock).
> **Tier:** FULL PIPELINE — pure deterministic logic, gated by an **invariant test suite** (`PIPELINE_HARDENING` Rule 3), *not* visuals. No `§1` clone table → Rule 8 N/A.

---

## 0. What T3 is
The deterministic bot-field **generator + reveal projector**. Pure C# in the `Golfin.Tournaments` asmdef (**System-only, no UnityEngine dependency** → headless NUnit-testable). Two jobs:
1. **Pre-roll** the whole field at creation from a seed: `RollField(def, cfg, holePars) → IReadOnlyList<BotCard>` — each bot's per-hole strokes + total + start offset + per-hole completion timestamps, all deterministic from `def.Id`.
2. **Project** a card at read time: `Project(card, now) → (thru, revealedStrokes, complete)` — the pure `(seed, now)` function GDD §7 mandates; T4's `GetLeaderboard` uses it to build `TournamentLeaderboardEntry` rows.

No background process, no `UnityEngine.Random`, no server. Same `(def.Id, now)` ⇒ same board, forever.

---

## 1. Reuse handles (verified on disk 2026-06-25)
| Need | Reuse — concrete handle | Note |
|---|---|---|
| Bot identities (`id/username/characterId/level`) | `Assets/Scripts/UI/Rankings/LocalFakeLeaderboardProvider.cs` → `Assets/Resources/Data/fake_players.csv` (120 rows) | same roster Rankings/1v1 use. **Do not re-parse the CSV** — reuse the provider. `BotCard.BotId` must be a fake_players `id`. |
| Per-hole par (RollField input) | `Assets/Scripts/UI/HoleDatabaseLoader.cs` → `HoleData.par` | **RollField takes `IReadOnlyList<int> holePars` as a param** (keeps T3 pure). Caller (T4) resolves it from `def.CourseId` + `def.HoleSet`. |
| Worst-hole stroke cap | existing `versusStrokeCapOverPar` (`ModesDatabaseCSV` / `GameSession.VersusStrokeCapOverPar`) | reuse the "cap over par" concept for the bot pickup clamp (D3). |
| Bracket tiers | `Assets/Resources/Data/bot_difficulty.csv` minLevels `1 / 10 / 25 / 50 / 100 / 180` | reuse the **bracket ids only**. The per-shot columns (`aimErrorDeg…`) are 1v1 *sim-play* (`VersusBot.cs`) and are **NOT** used by pre-roll. |
| Contracts | `Golfin.Tournaments`: `BotFieldConfig`, `BotCard`, `TournamentDefinition.HoleSet`, `TournamentLeaderboardEntry` (T1) | shapes fixed; T3 fills them. `BotCard` already carries every §6 tiebreak input — **no T1 amendment needed**. |

---

## 2. Determinism — seed model (CORRECTNESS TRAP)
- Seed source = `def.Id` (GDD §7 "tournamentId-derived").
- **MUST use a platform-stable hash.** `System.String.GetHashCode()` is **randomized per process** in modern .NET / IL2CPP → using it = non-reproducible fields across launches & devices = silent GDD violation. **Define an explicit stable hash in T3** (FNV-1a 64 or xxHash). Pin a known-vector test (`StableHash("abc") == <fixed const>`).
- Per-bot stream seed = `StableHash($"{def.Id}|{botIndex}")`; split concerns with suffixes (`":ident"`, `":strokes"`, `":pace"`) so adding one roll doesn't shift the others.
- **D0:** ship an explicit ~15-line PRNG (PCG / xorshift) seeded by the stable hash — **rec**, removes all doubt about `System.Random` algorithm stability on IL2CPP.

---

## 3. Bracket → strokes distribution (THE design gap — propose + flag)
GDD §7 says "skill-bracket distributions anchored to course par," but **no stroke distribution exists** (bot_difficulty.csv is sim-play params). Proposed model:
- New CSV **`Assets/Resources/Data/bot_score_brackets.csv`** (CSV-first, tunable), keyed by the bot_difficulty minLevels:
  `minLevel,meanDeltaPerHole,stdevPerHole`
- **D1 — proposed starting values (tune):**
  | bracket (minLevel) | meanΔ/hole | stdev | feel |
  |---|---|---|---|
  | 1 | +1.3 | 0.9 | high-handicap |
  | 10 | +0.9 | 0.8 | |
  | 25 | +0.6 | 0.7 | |
  | 50 | +0.35 | 0.6 | |
  | 100 | +0.15 | 0.5 | strong |
  | 180 | −0.05 | 0.45 | scratch (occasional birdie) |
- Per hole `h`: `strokes = clamp( round( par[h] + Normal(meanΔ, stdev) ), 1, par[h] + cap )`, Normal via seeded Box-Muller. `cap` = **D3** (rec: reuse `versusStrokeCapOverPar`, e.g. +4 ⇒ "pickup at par+4").
- `TotalStrokes = Σ strokes`.

---

## 4. Identity selection (reconcile `BracketWeights` vs roster `level`)
Each `fake_players` row's natural bracket = highest `minLevel ≤ level`. `BotFieldConfig.BracketWeights` sets the desired field mix.
- **D2 — proposed (rec):** for each of `cfg.BotCount` slots, sample a target bracket from `BracketWeights` (seeded), then pick (seeded, no-repeat) a roster identity whose natural bracket == target; nearest-bracket fallback if a tier is exhausted. The bot's **distribution params come from the slot's bracket** (§3).
- Guarantees: no duplicate identity in a field; `|field| == BotCount`; observed bracket mix ≈ weights at large N.
- Alt: ignore weights, sample `BotCount` identities and use each one's level-bracket. (Loses authoring control.)

---

## 5. Pace schedule (organic reveal)
- `startOffset = Uniform(cfg.StartOffsetMinSec, cfg.StartOffsetMaxSec)` seeded ⇒ `botStart = startUtc + startOffset`.
- Spread `H` holes across `[botStart, endUtc]`: nominal step `= (endUtc − botStart) / H`; each completion `= prev + step ± Uniform(0, PerHoleSpreadSec)`, **strictly increasing**, final clamped so the last completion `≤ endUtc` (compress if jitter overruns — GDD "by endUtc every bot has completed").
- Store as `BotCard.PerHoleCompletionUtc[h]`; `StartOffsetSeconds = startOffset`.

---

## 6. Projection (read-time pure fn — consumed by T4)
`Project(card, now)`:
- `thru = count( PerHoleCompletionUtc[i] ≤ now )`
- `revealedStrokes = Σ PerHoleStrokes[0..thru)`
- `complete = (thru == H)`
- T4 maps → `TournamentLeaderboardEntry { BotId→identity, Strokes = complete ? Total : revealedStrokes, Thru = thru }`.
- **Invariants:** `now1 < now2 ⇒ thru1 ≤ thru2`; `now < botStart ⇒ thru 0`; `now ≥ endUtc ⇒ thru == H (complete)`.
- **Ranking + tie resolution = T4** (§6 ladder). T3 only guarantees the inputs exist: per-hole strokes (countback ✓), total time `= lastCompletion − botStart`, submission ts `= lastCompletion`.

---

## 7. Acceptance — invariant test suite (THE gate; Rule 3)
Headless NUnit in `Golfin.Tournaments.Tests`. Pass = all green (this is the gate, not eyeballing a board):
- **Determinism:** `RollField` twice (same args) → deep-equal.
- **Stable-hash vector:** `StableHash("abc")` == pinned constant (guards the GetHashCode trap).
- **Field size:** `|field| == cfg.BotCount`.
- **Identities:** every `BotId ∈ fake_players` ids; no duplicate within a field.
- **Bracket mix:** observed ≈ `BracketWeights` within tolerance at `BotCount = 500`.
- **Strokes bounds:** ∀ hole `1 ≤ strokes ≤ par + cap`; `Total == Σ`.
- **Pace:** `PerHoleCompletionUtc` strictly increasing; all ∈ `(startUtc, endUtc]`.
- **Projection purity + monotonicity:** depends only on `(card, now)`; `thru` non-decreasing in `now`; `thru(startUtc⁻) == 0`; `thru(endUtc) == H`.
- **Reveal trickle:** at `now = startUtc + window/2`, `0 < Σ thru < H·BotCount` (partially filled, not all-or-nothing).

---

## 8. Staging
- **Stage 1** — stable hash + explicit PRNG + per-bot seed streams + hash-vector & determinism tests.
- **Stage 2** — `bot_score_brackets.csv` + bracket→strokes roll + identity selection + `RollField` (strokes / identity / bracket-mix tests).
- **Stage 3** — pace schedule + `Project` + pace / projection / trickle tests.
- *(T4 later: wire `Project` into `GetLeaderboard`, merge the local player, apply §6 ranking.)*

---

## 9. Decisions for Cesar
- **D0 — PRNG:** explicit PCG/xorshift seeded by stable hash *(rec)* vs `System.Random` + stable seed.
- **D1 — strokes distribution numbers** (§3 table): tune the per-bracket meanΔ/stdev. The model + `bot_score_brackets.csv` shape is the real ask.
- **D2 — identity selection:** weighted-by-`BracketWeights` *(rec)* vs roster-level-only.
- **D3 — worst-hole cap:** reuse `versusStrokeCapOverPar` *(rec, e.g. +4)*.
- **D4 — in-progress provisional ranking** (sort partially-revealed bots by revealed strokes? hide `thru 0` bots?) — likely a **T4** concern; confirm so the `Project` output carries what T4 needs.

---

## Source links
GDD: `Docs/Game Design/Tournaments_GDD.md` §7 (lines ~117–126), §6, §5, §4. Contracts: `Assets/Scripts/Tournaments/{BotFieldConfig,ITournamentBackend,HoleResult}.cs`. CSVs: `Assets/Resources/Data/{bot_difficulty,fake_players}.csv`.
