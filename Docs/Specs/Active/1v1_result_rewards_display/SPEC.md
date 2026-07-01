# SPEC — 1v1_result_rewards_display (Order 347)

**Type:** FULL PIPELINE (Tier 3 — UI visual fidelity + match-flow wiring)
**Priority:** P2 (bumped from P3 — Cesar activated 2026-07-01)
**Effort:** M (1–2 days across stages)
**Phase:** Loop v2 / Matchmaking
**Surfaces (NEW):** `VersusResultScreen.prefab` + `VersusResultScreenController.cs` + `VersusResultScreenBuilder.cs` (editor)
**Touches (later stages):** `VersusResultHandler.cs`, ShellScene (screen wiring)

**Figma (file `5gEAHjl6xAtW8iYY7NMvWd`):**
- Win state (local player wins): node **`13274:877`**
- Lose state (local player loses): node **`13275:2628`**

> **⚠ Figma-first / Lesson AK — MANDATORY step 0:** before writing ANY builder code, run
> `get_design_context` on the **RESULTS panel component node** (not the full frame) for both
> nodes and take the token values as literals. The token table in §2 is a *reconcile-against-node
> convenience, NOT source of truth*. **Derive the TMP divisor per-file** — for the sibling
> Tournament modal it was **÷1.3125** (title 42px→32), NOT the default ÷1.4. Do not assume.
> (Frame `get_design_context` returns too much — target the panel component. Figma `get_metadata`
> was timing out on 2026-07-01; retry, or drill via `get_screenshot` on child nodes.)

---

## §0 Reuse mandate (duplicate-and-modify, NEVER rebuild)

The versus portrait pair + rank display **already exists**. Do not re-invent it.

- **`MatchMakingModal.prefab` / `MatchmakingModalController.cs`** — the closest structural analog:
  it already lays out a **You-vs-Opponent** portrait pair and resolves the opponent's
  **portrait + rarity + level + username + rank** from the shared `LeaderboardManager` roster
  (the same `LeaderboardEntry` the matchmaking flow seeds into `MatchContext.Players[1]`). **Study
  and reuse its binding path** for the two result cards.
- **`CharacterThumbnailCard.prefab` / `CharacterThumbnailCard.cs`** — the portrait card
  (rarity letter + `Lv` badge + portrait + name banner). This IS the WINNER/LOSER card in the
  mockup. Reuse it; do not author a new portrait card.
- **`TournamentResultModalBuilder.cs`** — the **exact Stage-0 builder pattern** to mirror:
  `PrefabUtility.LoadPrefabContents` → restructure → `WireField` via `SerializedObject` paths →
  `SaveAsPrefabAsset`, exposed as a re-runnable `[MenuItem]`. Copy this shape.
- **Reward system (Stage 2): reuse the hole-complete flow, do NOT invent one** — `HoleData.RewardType`
  / `HoleReward`, `HoleDatabaseLoader`'s (type,amount)-pair CSV parsing, and
  `HoleCompleteModalController.GrantRewards`'s grant switch (`RewardPointsManager.EarnPoints` /
  `ItemManager.AddItems("repairkit_common")` / `BallManager.AddBalls`). See D1 (§5).
- Navy rounded panel bg + gold CTA button sprites: reuse from `TournamentResultModal.prefab` /
  `HoleCard.prefab` / the shared Main Button. Separator sprite guid
  `9e62d8f4ffd01e7468d07912ccba967a` (same one the Tournament builder uses).

---

## §1 Why (the gap)

Verified against live code 2026-07-01: `VersusResultHandler.HandleMatchComplete` grants the
win reward **silently** (`RewardPointsManager.Instance.EarnPoints(reward)`) then unloads gameplay
and returns home after 0.5s. The player sees only the persistent WIN/LOSE/DRAW banner
(`TurnBannerWidget.ShowPersistent`) — **the reward is never shown, and there is no result screen.**
No versus result/reward prefab exists (only `TournamentResultModal.prefab` + legacy
`MissionResultCard.prefab`). This spec builds the missing screen.

**Sequencing rule (Cesar, hard):** the WIN/LOSE/DRAW **banner plays first**, THEN the RESULTS
screen appears. Banner → screen, never simultaneous.

---

## §2 Figma design breakdown (both nodes)

It is a **full-screen** RESULTS screen: persistent **TopBar** shows `RESULTS` + RP balance + gear
+ podium icon; the persistent **bottom nav** is visible; a central **navy rounded RESULTS panel**
holds the content. TopBar + bottom nav are existing Shell chrome — **not part of this prefab.**

**Central panel, top → bottom:**
1. `RESULTS` header — white, bold, centered.
2. Two column labels above the portraits: **WINNER** (green) / **LOSER** (red-orange).
3. Two `CharacterThumbnailCard` portraits with **`Vs.`** between them.
   **LEFT = local player, RIGHT = opponent** (Olivia left / Elizabeth right in both mockups).
4. Under each card: `USERNAME` line + `RANK: #NNN` — **green for the winner, red for the loser**.
5. Separator.
6. `HOLE` label (gold) + course/hole line: `Lomond Country Club  - Hole 5`.
7. Separator.
8. **Reward row** — three items, each `icon ×N`: coin/RP, item (scissors glyph), ball.
   **Bright/gold when local player WON; desaturated/greyed when local player LOST.**
9. Gold **`NEW MATCH`** button.

**Two states = one layout, mirrored:**
| | Win node `13274:877` | Lose node `13275:2628` |
|---|---|---|
| Left column (local) | WINNER (green) | LOSER (red) |
| Right column (opp.) | LOSER (red) | WINNER (green) |
| Reward row | bright/active | greyed/inactive |

**DRAW: no Figma exists → see D2.**

*(Token values — colors/sizes/spacings — are NOT transcribed here on purpose. Pull them from the
node at step 0 per the Lesson AK banner above.)*

---

## §3 Delivery stages

**Stage 0 — PREFAB ONLY (Cesar checks this before any wiring).** ← *this is the kickoff target*
Build `VersusResultScreen.prefab` (the central RESULTS panel + contents) via a re-runnable editor
`VersusResultScreenBuilder.cs`, mirroring `TournamentResultModalBuilder`. Reuse
`CharacterThumbnailCard` for both portraits + real navy/gold/separator sprites. Bind **sample data**
so both visual states are demonstrable (a `[MenuItem]` toggle or two build variants: WIN preview +
LOSE preview). **No ShellScene wiring, no `VersusResultHandler` change, no reward-logic change.**
Deliverable = the prefab, openable/previewable, that Cesar eyeballs against `13274:877` /
`13275:2628`. Provide a real-render still or short editor clip (NOT a hand-stitched slideshow —
Rule 20).

**Stage 1 — Present in the Shell (after banner).**
Add a `VersusResultScreen` screen (rule D4) to the Shell; `VersusResultScreenController` binds the
two players from the live `MatchContext` + roster (reuse Matchmaking's binding). Re-route
`VersusResultHandler`: instead of auto-unloading home, it lets the banner play, THEN presents this
screen with the real outcome/players/hole. Bind local + opponent rank from `LeaderboardManager`.

**Stage 2 — CSV-driven multi-reward grant + display + NEW MATCH.** (D1 RESOLVED; needs D3)
Replace the flat `versus_1v1.rewards=200` grant: define versus rewards as CSV (type,amount) pairs,
parse to `List<HoleReward>`, grant via the shared `RewardGranter` (extracted from
`HoleCompleteModalController.GrantRewards`), bind the result-screen reward row to that list (RP/
repair/ball, N-slot). Wire `NEW MATCH` (D3).

**Stage 3 — Polish.** Win/lose reward brightness states, draw variant (D2), transitions.

---

## §4 Stage 0 acceptance (the checkable deliverable)

- [ ] Step 0 done: `get_design_context` re-pulled on the panel node for BOTH states; divisor
      derived per-file (recorded in the implementer report).
- [ ] `VersusResultScreenBuilder.cs` exists as a re-runnable `[MenuItem("GOLFIN/...")]`, mirrors
      the `TournamentResultModalBuilder` load→restructure→save pattern, editor-only (`Editor/`).
- [ ] `VersusResultScreen.prefab` renders the central panel matching the Figma layout: header,
      WINNER/LOSER labels, two `CharacterThumbnailCard` portraits + `Vs.`, USERNAME + RANK lines,
      two separators, HOLE line, 3-item reward row, gold NEW MATCH button.
- [ ] **Both visual states demonstrable** (win: left green + bright rewards; lose: left red + greyed
      rewards) — via preview toggle or two build variants.
- [ ] Portraits reuse `CharacterThumbnailCard` (NOT a re-authored card); sprites reused from real
      prefabs (no new panel/button art invented).
- [ ] **Non-persistence / no scene mutation:** the builder only writes the new prefab asset; Shell
      scene + all reused source prefabs are byte-identical afterward (md5). No `VersusResultHandler`
      diff, no reward-logic diff.
- [ ] `script-execute` compiles clean; no new console errors.
- [ ] Real-render still/clip provided for Cesar's eyeball check (Rule 20 — no slideshow).

---

## §5 DECISIONS NEEDED (Cesar — before Stage 2; NOT blocking Stage 0)

- **D1 — Reward model. ✅ RESOLVED (Cesar 2026-07-01):** 1v1 wins pay **multiple rewards — RP + balls
  + repair kits** (and future types like gacha tickets), and the payout **must come from a CSV, not
  a flat int**. The flat `versus_1v1.rewards=200` int in `modes.csv` is the outlier to replace.
  **REUSE the existing hole-complete reward system** (do not invent one): `HoleData.RewardType
  { Points, RepairKit, Ball }` + `HoleReward` (list), CSV-parsed as **(type,amount) pairs** exactly
  like `HoleDatabaseLoader` (cols 7–12 play / 13–18 replay; `ParseRewardType(str)` → `AddReward`),
  granted via the `HoleCompleteModalController.GrantRewards` switch
  (`RewardPointsManager.EarnPoints` / `ItemManager.AddItems("repairkit_common")` /
  `BallManager.AddBalls`), and displayed as the same coin/repair/ball row. Figma's 3 slots ARE
  Points/RepairKit/Ball. **Extensibility:** the reward row + binding must iterate a `List<HoleReward>`
  (N slots), NOT hardcode 3 fields, so `GachaTicket` (add to `RewardType`) drops in later.
  **Refactor note:** the grant switch is currently private in `HoleCompleteModalController` — extract
  a shared `RewardGranter.Grant(List<HoleReward>)` so versus + hole-complete use ONE grant path (DRY;
  one place to add gacha tickets). Confirm CSV shape with Cesar at Stage 2: per-outcome rows (win pays,
  lose/draw pay 0 → greyed row) either as new `modes.csv` reward-pair columns or a dedicated
  `match_rewards.csv`. **Stage 0 still just renders the Figma 3-slot row with placeholder counts.**
- **D2 — DRAW visual.** No Figma. Proposed default: both columns neutral (no green/red), reward row
  greyed, header/banner reads DRAW. Confirm or provide a node.
- **D3 — NEW MATCH behavior.** Requeue same mode via the matchmaking flow, re-open the Matchmaking
  modal, or return to Mode Select? (Current post-match behavior is auto-home.)
- **D4 — Screen vs modal.** Proposed: a real Shell **screen** (persistent TopBar `RESULTS` + bottom
  nav, per Figma), integrated like `RankingsScreen`. Alternative: full-screen modal on the
  persistent canvas. Affects Stage 1 only — Stage 0 builds the panel prefab either way.

---

## §6 Out of scope / guardrails

- No server / real-player matchmaking (bot opponent from roster stands).
- Stage 0: **zero** gameplay/flow/reward-logic change — prefab asset only.
- Do NOT rebuild the portrait card or the versus layout — reuse Matchmaking + `CharacterThumbnailCard`.
- Do NOT raw-edit prefab/scene YAML — sanctioned MCP / `SerializedObject` only.
- Bot-video/real-render gate applies (Rule 20): no hand-stitched slideshow for the visual proof.

---

## Kickoff (Stage 0)
```
Use the implementer subagent on "1v1_result_rewards_display"
```
