# RP_REBALANCE — game economy → shared GPS scale

**Status:** ✅ APPROVED by Cesar 2026-08-12 — with one amendment: **welcome grant REMOVED** (testing-only; admin-set balances replace it). Level-up formula ceil(level/2), stamina global rounding rule, and §3 caps all approved as drafted. These numbers are binding for the Slice 2 kickoff.
**Author:** Architect (Claude, Cowork session 2026-08-12)
**Rule of thumb:** every RP value ÷10, round half-up, minimum 1 for non-zero originals; 0 stays 0. Ratios between prices and rewards are preserved exactly, so the economy *feels* identical — only the digits shrink to sit beside the GPS anchors (daily_login 5 · game_play 10 · gps_checkin 30 · vote_hit 30 · screenshot 50).
**Sanity check at this scale:** a steady-state GOLFIN day (18 replays ≈ 90 + a few versus wins ≈ 40–100) lands at ~130–190 pts/day; an active GPS user lands at ~50–125/day. Same order of magnitude, game slightly ahead — reasonable for the product doing the retention work. Fine-tune post-cutover with real data.

---

## 1. Earns

### HoleDatabase.csv (`Assets/Data/HoleDatabase.csv`) — Points rows only; RepairKit/Ball item amounts are NOT RP and don't change

| Value | Current | New |
|---|---|---|
| First-clear hole reward (17 holes) | 100 | **10** |
| First-clear Hole 6 (special) | 200 | **20** |
| Replay reward (all holes) | 50 | **5** |

*Note the anchor alignment: a hole completion = 10 = exactly the GPS `game_play` action. Replay = 5 = `daily_login`.*

### modes.csv (`Assets/Resources/Data/modes.csv`)

| Value | Current | New |
|---|---|---|
| versus_1v1 win reward (`rewards` + `reward1Amount`) | 200 | **20** |
| practice `rewards` display value | 50 | **5** |
| missions `rewards` display value (locked mode) | 200 | **20** |

*NOTE for Code: confirm whether `modes.csv.rewards` is display-only or granted anywhere; either way ÷10 keeps it consistent with the real grants.*

### tournament_prizes.csv

| Prize table | Current (rank bands) | New |
|---|---|---|
| prize_small | 3000 / 1500 / 500 | **300 / 150 / 50** |
| prize_medium | 5000 / 3000 / 1000 | **500 / 300 / 100** |
| prize_major | 20000 / 12000 / 5000 / 1000 | **2000 / 1200 / 500 / 100** |

*Item rewards (ticket_gold, trophy_major) unchanged. A major win stays a ~200-hole jackpot, same as today.*

### Welcome grant — ❌ REMOVED (Cesar, 2026-08-12)

The client's `DEFAULT_STARTING_POINTS = 50,000` was testing-only and is not
ported. New accounts start at **0 RP**. Test balances are granted manually by
the admin — dashboard points panel once it exists, Supabase table editor/SQL
until then. Slice 2 removes the seed from `RewardPointsManager.Awake` at
cutover. (This also kills the ranking/avatar-distortion concerns the draft
flagged — no grant, no distortion.)

## 2. Spends (sign-up fees & prices)

### Mode entry fees (modes.csv)

| Mode | Current | New |
|---|---|---|
| practice `entryFee` | 100 | **10** |
| versus_1v1 / tournaments / driving_range / missions | 0 | **0** |

### Tournament sign-up fees (tournaments.csv `entryFeeRP`)

| Tournament | Current | New |
|---|---|---|
| kasumigaseki_open | 100 | **10** |
| gotemba_masters | 500 | **50** |
| all others | 0 | **0** |

*Fee-to-prize shape preserved: a 50-pt major entry against a 2,000-pt first prize is the same 1:40 as today's 500:20,000.*

### Character level-ups (`Assets/Data/LevelUpCosts.csv` — also used by club level-ups via `CharacterLevelUpDatabase`)

Current formula: `cost_r = level × 5` (5, 10, 15 … 1,200 at lv240; cumulative 144,600).

**New formula: `cost_r = ceil(level / 2)`** → 1, 1, 2, 2, 3 … 120 at lv240; cumulative **14,520** — ≈10% of today's total (the draft prose claimed 14,460 = exact ÷10; the formula actually sums to 14,520, a 60-pt drift over 240 levels. Caught by Claude Code at implementation, corrected 2026-08-12 — the twice-approved FORMULA is what ships; the prose was wrong). `sp_reward` column unchanged.

*Alternative if you prefer cleaner per-level numbers: `cost_r = level` (1, 2, 3 … 240) — but that's effectively ÷5, making level-ups twice as expensive relative to everything else. My recommendation is ceil(level/2).*

### Gacha (gacha_banners.csv)

| Banner | Current x1 / x10 | New x1 / x10 |
|---|---|---|
| banner_standard_club1, banner_test_a, banner_inactive | 500 / 4500 | **50 / 450** |
| banner_test_b | 750 / 6750 | **75 / 675** |

### Shop (shop_catalog.csv `rpCost` / `saleRpCost`)

| Entry | Current | New |
|---|---|---|
| club_iron9_klyro | 2000 / 1500 | **200 / 150** |
| club_awedge_fyloe | 4000 / 3000 | **400 / 300** |
| club_pwedge_royal | 6000 / 6000 | **600 / 600** |
| club_driver_gf | 1000 / 800 | **100 / 80** |
| ball_putt_ace | 500 / 350 | **50 / 35** |

### Stamina shop (stamina_shop_items.csv `rp_cost`, 30 rows)

Apply the global rule (÷10, round half-up): 65→7, 85→9, 95→10, 110→11, 115→12, 140→14, 145→15, 155→16, 165→17, 200→20, 225→23, 235→24, 250→25, 255→26, 260→26, 285→29, 300→30, 305→31, 315→32, 365→37. Tier feel survives: LIGHT ~7–16, MEDIUM ~11–32, HIGH ~12–37.

### Debug panel (`RewardPointsDebugPanel.cs` — dev-only, flag-off guarded in Slice 2 anyway)

±1,000 / ±10,000 → **±100 / ±1,000**; "Set 50k" → **"Set 5k"**.

## 3. Server seed — final `game_point_actions` values (replaces Phase A placeholders)

| action | pts (fixed) | max_per_event | daily_cap | once_per_user |
|---|---|---|---|---|
| hole_complete | NULL (client amount — holes vary) | 20 | 400 | no |
| hole_replay | NULL | 5 | 100 | no |
| versus_win | 20 | 20 | 200 | no |
| tournament_prize | NULL (rank-band amount) | 2000 | — | no |

*(No `golfin_welcome` or `legacy_balance_migration` actions — removed with the welcome grant; test balances are admin-set directly.)*

## 4. What does NOT change

RepairKit / Ball / item reward amounts (not RP) · SP rewards · stamina *values* (only their RP prices) · ticket items · prize item rewards · entry-fee-free modes · `earn_activity_pts` and all PLAYLIFE-side amounts.

## 5. Flags — all resolved 2026-08-12

1. ~~Welcome grant bucket~~ → grant **removed entirely** (Cesar; admin-set balances instead).
2. Level-up formula → **ceil(level/2)** approved.
3. Stamina rounding → **global rule** approved.
4. §3 caps → approved as drafted.

These numbers go verbatim into the Slice 2 kickoff (CSV/code edits + client-seed removal in GolfinRedux, `game_point_actions` seed in playlife) and ship together with the `PointsBackendEnabled` flip as one cutover.
