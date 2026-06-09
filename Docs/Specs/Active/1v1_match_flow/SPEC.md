# SPEC — `1v1_match_flow` (1v1 Phase 2)

**Notion:** Order 344 (P2, Loop v2) · **Follows:** `1v1_ingame_ui` Phase 1 (Order 343, shipped `756ab280`, in `Docs/Specs/Completed/1v1_ingame_ui/`)
**Tier:** FULL PIPELINE (runtime AI + new state machine + touches shipped gameplay)
**Prepared:** 2026-06-09 15:58 JST (Architect)
**This SPEC scopes Phase 2a only** (turn-flow + win/tie/draw + winner banner + a basic runtime bot). Phase 2b (the tunable difficulty model) is described in §13 and shipped as a follow-on once 2a lands. This mirrors the Phase-1→Phase-2 handoff pattern.

---

## 1. Goal — the match itself

Phase 1 built the versus HUD. Phase 2a makes a 1v1 hole actually **play and resolve**: alternating shots between the human (P1) and a runtime bot opponent (P2), one shared random hole, first-to-sink wins with a one-shot courtesy tie attempt, ending on a WIN / LOSE / DRAW banner with the RP reward granted on a win.

**Match shape (Cesar):**
```
banner "YOUR TURN" → P1 shoots (P2 card dimmed, P1 input live)
banner "OPPONENT'S TURN" → bot shoots (P1 input locked, bot drives the shot)
… strict alternation, P1 always shoots first each round …
→ first player to sink wins; trailing player gets exactly ONE courtesy shot, and only to tie
→ WIN / LOSE / DRAW banner → RP grant on win → return path
```

---

## 2. Decisions locked (Cesar, 2026-06-09)

1. **One ball, two persisted lies.** No dual live balls, no ball-spawn infra. The single `PhysicsLabController` ball is teleported to the active player's stored lie at the start of their turn; its resting position is written back to that player's lie after the shot resolves. (Solo already plays successive shots from successive lies — Phase 2a is bookkeeping two of them.)
2. **Draw on equal strokes.** No sudden-death. Banner reads WIN / LOSE / DRAW. Sudden-death is a later enhancement.
3. **Strict alternation, P1 (human) shoots first** every round. Not golf "honors/farthest-plays." This makes the courtesy rule self-consistent (see §10).
4. **Courtesy shot auto-resolves** from the alternation: it is always exactly one count-matching shot and is only ever taken by the player who shoots second when the first-shooter sinks. No separate "tie mathematically impossible" branch is needed (§10 proves it).
5. **Camera follows the active player**; inactive player's shot controls are locked and their card is dimmed (Phase-1 0.50 opacity already does the dim). One ball + one camera already frames the ball between shots in solo, so re-placing the ball at the active lie gives "camera on active player" for free.
6. **Difficulty (Phase 2b)** derives from the **opponent character's level** via a CSV table (level → error bands), randomized within the band per shot, higher level = tighter. **Phase 2a ships a basic bot with NO error injection** (plays competent, straight shots toward the cup) so a full match resolves end-to-end before difficulty is tuned to feel.
7. **Win reward = the 1v1 card's existing `Reward` value**, read from `modes.csv` (`versus_1v1.rewards = 200`) — NOT a new number. Loss and draw grant 0.

**Architect sub-calls baked here (override any and I amend before kickoff):**
- **A — Per-player stroke/lie model lives in `MatchContext`** (extended additively), NOT in `GameSession`. Keeps `GameSession`'s single-player model and the solo path byte-identical.
- **B — RP grant + result presentation cross the asmdef boundary via a `GameSession` event** (`OnMatchComplete`), handled by a ShellScene-resident controller — exactly the shipped `HoleCompletionBridge → GameSession.OnHoleComplete → HoleCompleteModalController` pattern. The match-flow controller (in `Golfin.Physics.Viewer`) must NOT call `RewardPointsManager` directly (it's not referenceable from Viewer — Lesson W asmdef trap).
- **C — Phase 2a ends on a held WIN/LOSE/DRAW banner** (`TurnBannerWidget`, persistent variant) + RP grant + return-to-home. A dedicated 1v1 result modal (your strokes vs opponent's, side-by-side) is a **separate UI spec (2c)** so 2a doesn't balloon into new modal art.
- **D — Safety stroke cap** (CSV, default par+5) prevents a pathological infinite match if neither side holes; primary resolution is always first-sink + courtesy. With a competent bot this rarely triggers (§11).
- **E — Phase 2b (difficulty) is written after 2a ships**, so the error model is tuned against the real bot in motion.

---

## 3. Step-0 reuse gate (HARD REJECT if violated)

No screen/widget/data GameObject may be authored from scratch. The implementer must cite the source surfaces it builds on:

- **`MatchContext`** (static data layer) — `Assets/Scripts/Gameplay/UI/ShotUI/HUD/MatchContext.cs`. EXTEND the `Player` struct additively; do NOT rewrite it.
- **`TurnBannerWidget`** — `Assets/Scripts/Gameplay/UI/ShotUI/TurnBannerWidget.cs`. REUSE `Show(text, fromLeft)`; ADD a persistent variant for the winner banner (additive method, §8.3).
- **`VersusHudController`** — `Assets/Scripts/Gameplay/UI/ShotUI/VersusHudController.cs`. The Phase-1 DEBUG turn driver (`DebugSwapTurn`/`DebugForceVersus`/`DebugShowBanner`) is REPLACED as the driver of record by the new turn-flow; the debug methods may remain `#if UNITY_EDITOR`-only or be removed (§8.1).
- **`PlayerCardWidget`** — already binds per-index name/level/portrait + 1.0/0.50 opacity off `MatchContext.SetActive`. Do NOT touch its bind logic; the flow just calls `SetActive`.
- **Bot shot-driving** — reimplement the PROVEN production path from `BotDriver` (`Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs`: `PlayHoleToCup` / `FireDriverShot` / `SelectShot`) in a SHIPPABLE class (no `#if UNITY_EDITOR`, no `ForceShotCompleteForBot` seam). Cite `BotDriver` as the behavioral reference.
- **Hole-out branch** — extend the production `HoleCompletionBridge` (`Assets/Scripts/Physics/Viewer/HoleCompletionBridge.cs`); do NOT touch the deprecated `HoleCompleteDriver`.

---

## 4. Architecture & placement

| Piece | Lives in | Why |
|---|---|---|
| `MatchContext` extensions | `Golfin.Gameplay.UI.HUD` (existing file) | data layer already there |
| `VersusMatchController` (turn-flow SM) | **`Golfin.Physics.Viewer`** | needs internal `PhysicsLabController.BallSM` + `SetCameraYawRadians` (both `internal` to Viewer); same home as `HoleCompletionBridge` |
| `VersusBot` (runtime opponent) | **`Golfin.Physics.Viewer`** | same internal-seam need; `BotDriver` already lives here |
| `TurnBannerWidget.ShowPersistent` | `Golfin.Gameplay.UI.ShotUI` (existing file) | additive |
| `GameSession.OnMatchComplete` + `MarkMatchComplete` | `Golfin.Gameplay.Session` (existing file) | event bridge across asmdef boundary |
| `VersusResultHandler` (RP grant + return) | **ShellScene / Assembly-CSharp** (wherever `RewardPointsManager` is reachable) | Viewer can't reference `RewardPointsManager`; mirrors `HoleCompleteModalController` |
| Human-input gate | `Golfin.Physics.Viewer` flow toggles `ClubHandleDragger` | lock human during bot turn |

**Boundary rule:** the flow controller fires `GameSession.MarkMatchComplete(outcome)`; the ShellScene handler (which can see `RewardPointsManager` + the modes DB) grants RP and drives the return. No cross-asmdef direct calls from Viewer upward.

---

## 5. Data model — `MatchContext.Player` (additive)

Add to the existing `struct Player`:
```csharp
public Vector3 Lie;        // this player's current ball resting position (world). Tee at match start.
public int     Strokes;    // shots taken this hole by this player (replaces per-player TurnCount use)
public bool    HoledOut;   // true once this player's ball reached InCup
public int     HoleOutStroke; // the stroke count on which they holed (0 until holed)
```
Add static helpers (additive, do not alter existing API):
```csharp
public static void ResetMatchState(Vector3 tee); // both players: Lie=tee, Strokes=0, HoledOut=false, HoleOutStroke=0, ActiveIndex=0
public static int Other(int i) => 1 - i;
```
> NOTE: store the lie as `Vector3` for the visual ball transform; the sim's native fixed-point (`fp3`) is only needed inside `BallSimulation` — `PhysicsLabController.PlaceBallAt(Vector3)` already accepts world `Vector3`, and `ShotResult.EndPosition` (`fp3`) → `Vector3` conversion uses the existing `.ToFloat()` pattern seen in `BotDriver` (`firstHit.Position.x.ToFloat()` etc.). Confirm the converter helper at implementation.

`GameSession` is **not** extended for per-player state. `GameSession.TurnCount`/`ShotHistory` remain single-player; the versus model is entirely in `MatchContext`.

---

## 6. Code anchors (verified 2026-06-09)

| Need | Anchor |
|---|---|
| Versus gate | `GameSession.IsVersus` — `Session/GameSession.cs:27` |
| Shot resolved (turn ends) | `BallStateMachine.OnShotComplete` (`Action<ShotResult>`) — `Loop/BallStateMachine.cs:30` |
| Holed out | `ShotResult.TerminalState == BallState.InCup` — `Loop/ShotResult.cs`; SM terminal `BallStateMachine.cs:199` |
| New lie after a shot | `ShotResult.EndPosition` (`fp3`) or `PhysicsLabController.BallPosition` (`Vector3`, public, `PhysicsLabController.cs:127`) |
| Place ball at lie | `PhysicsLabController.PlaceBallAt(Vector3, int? surface)` — public, `:651` |
| Aim toward cup | `PhysicsLabController.SetCameraYawRadians(float)` — **internal**, `:754`; yaw = `Mathf.Atan2(flat.z, flat.x)` toward cup |
| Cup world pos | `Golfin.Gameplay.UI.HUD.HoleContext.PinWorld` (static) — read as `BotDriver.FindCupPosition()` does |
| Club select | `PhysicsLabController.SetClub(int)` public `:546`; `PutterIndex` public static `:535`; clubs 0=Driver 1=Iron7 2=Wedge 3=Putter |
| Live-stat path for bot | `ShotController.ClearStatBundleOverride()` (so the bus resolves the opponent's live stats via `StatProviderBus` using `MatchContext.Players[1]`'s runtime) |
| Human shot path / commit | `ShotController.BeginExternalDrag()` `:76` → `SetExternalPower(power01, cone)` `:84` → `EndExternalDrag()` `:94` → CommitFlick. `ShotController.State` (`ShotState`) `:40`, gate on `ShotState.Idle` then `BallState.Aiming` |
| Human input component (to lock) | `ClubHandleDragger` — `Assets/Scripts/Gameplay/UI/ShotUI/ClubHandleDragger.cs` |
| Hole par | `HoleContext.Par` |
| Production hole-out fire | `HoleCompletionBridge.HandleShot(ShotResult)` — `Viewer/HoleCompletionBridge.cs` (fires `GameSession.MarkHoleComplete` on InCup / stroke-cap) |
| RP grant | `RewardPointsManager.Instance.EarnPoints(int)` — `UI/Roster/Managers/RewardPointsManager.cs:105` |
| Reward value | `ModesDatabaseCSV` → `ModeData.rewards` for id `versus_1v1` (=200) — `UI/ModeSelect/{ModesDatabaseCSV,ModeData}.cs` |
| Bot reference (editor-only) | `BotDriver.PlayHoleToCup` / `FireDriverShot` / `SelectShot` — `Viewer/Bot/BotDriver.cs` |

---

## 7. `VersusMatchController` — turn-flow state machine

New MonoBehaviour in `Golfin.Physics.Viewer`, on the `[Session]` host in `LabScaffold.unity` (alongside `HoleCompletionBridge`). Active only when `GameSession.IsVersus`; on `!IsVersus` it is a hard no-op (solo untouched). Owns the match; subscribes `BallStateMachine.OnShotComplete`.

**States:**
```
MatchStart → AnnounceTurn → AwaitShot → ResolveShot → (Decide) → AnnounceTurn | MatchEnd
```

1. **MatchStart** — read tee/start lie (current ball position at hole load). `MatchContext.ResetMatchState(tee)`. `ActiveIndex = 0` (P1). → AnnounceTurn.
2. **AnnounceTurn(active)** — `MatchContext.SetActive(active)` (drives card opacity). `PlaceBallAt(Players[active].Lie)`. `SetCameraYawRadians` toward `HoleContext.PinWorld`. `_banner.Show(active==0 ? "YOUR TURN" : "OPPONENT'S TURN", fromLeft: active==0)`. → AwaitShot after the banner's in/hold (do NOT block the whole hold if it feels sluggish — tune live with Cesar).
3. **AwaitShot(active)** —
   - **active == 0 (human):** enable human input (`ClubHandleDragger.enabled = true`); the player takes the shot through the normal path. Wait for `OnShotComplete`.
   - **active == 1 (bot):** disable human input (`ClubHandleDragger.enabled = false`); `VersusBot.TakeShot()` drives the production shot path toward the cup. Wait for `OnShotComplete`.
   - On `OnShotComplete(result)`: `Players[active].Strokes++`.
4. **ResolveShot(active, result)** — `Players[active].Lie = result.EndPosition→Vector3`. If `result.TerminalState == InCup`: `Players[active].HoledOut = true; HoleOutStroke = Strokes`. → Decide.
   - If `result.TerminalState == OB`: keep play on the same player path as solo (OB penalty handled by existing pipeline); the ball returns to its drop, `Strokes` already incremented. (Do NOT swap turns mid-OB-recovery beyond the normal one shot — one shot = one turn; OB just means the next turn starts from the drop lie.) → Decide (no hole-out).
5. **Decide** — apply the win/tie truth table (§10):
   - Match resolved → MatchEnd(outcome).
   - Courtesy shot owed → set `ActiveIndex` to the courtesy player, AnnounceTurn (this is their single tie attempt).
   - Otherwise → `SetActive(Other(active))`, AnnounceTurn.
   - Safety cap reached (§11) → MatchEnd(outcome).
6. **MatchEnd(outcome ∈ {P1Win, P2Win, Draw})** —
   - `_banner.ShowPersistent(outcome == P1Win ? "YOU WIN" : outcome == P2Win ? "YOU LOSE" : "DRAW", fromLeft: true)`.
   - `GameSession.MarkMatchComplete(outcome, Players[0].Strokes, Players[1].Strokes)`.
   - Lock human input. Hand off to the ShellScene handler (§9) for RP + return.

**Serialized fields:** `ClubHandleDragger _humanInput; TurnBannerWidget _banner; VersusBot _bot;` (+ resolve `PhysicsLabController` like `HoleCompletionBridge` does). Reuse the `VersusHudController`'s already-wired `_banner` if cleaner than a second reference — implementer's call, cite which.

---

## 8. Other components

### 8.1 Replace the Phase-1 debug driver
`VersusHudController` keeps its **layout** responsibilities (P2 card activation, mini-map reposition, opening-banner-on-activate). The turn SWAP + per-turn banner are now owned by `VersusMatchController`. Guard or remove `DebugSwapTurn`/`DebugForceVersus`/`DebugShowBanner` so they can't drive a shipped build (keep `#if UNITY_EDITOR` if retained for inspection). The opening "YOUR TURN" should fire from the flow's first AnnounceTurn, not from both — de-dupe so the banner plays exactly once at match start.

### 8.2 `VersusBot` (runtime, shippable — Phase 2a = basic)
New class in `Golfin.Physics.Viewer`. Reimplements `BotDriver`'s production path **without** the editor gate or capture/logging:
- `TakeShot()` coroutine: read cup (`HoleContext.PinWorld`) and ball (`BallPosition`); compute `dist` and `yaw`; pick club+power via the `SelectShot(dist, isFirstStroke)` model (port it); `SetClub` + `ClearStatBundleOverride`; `SetCameraYawRadians(yaw)`; gate `ShotState.Idle` → `BallState.Aiming`; `BeginExternalDrag()` → ramp `SetExternalPower(power01, 0f)` over ~0.85s → `EndExternalDrag()`.
- **Phase 2a: no error injection.** `coneFinetune = 0`, no aim/power noise. The bot plays a competent straight line so matches reliably resolve. (Phase 2b layers the error model — §13.)
- MUST NOT call `ForceShotCompleteForBot` and MUST NOT be `#if UNITY_EDITOR`.
- Bot stats resolve through the live path (`StatProviderBus`) using `MatchContext.Players[1]`'s character runtime, same as a human's stats resolve.
> NOTE: `SelectShot`'s first-stroke flag — the bot tracks its own `Players[1].Strokes==0` as "first stroke" (Driver), matching the reference.

### 8.3 `TurnBannerWidget.ShowPersistent(string text, bool fromLeft)`
Additive method: slide-in identical to `Show`, then **hold indefinitely** (no auto fade-out, GameObject stays active). A `Hide()`/`Dismiss()` call (already-present deactivate path) clears it when the return flow runs. Do not alter `Show`.

### 8.4 `HoleCompletionBridge` versus branch
In `HandleShot`, when `GameSession.IsVersus` is true, **do not** fire the solo `GameSession.MarkHoleComplete` path (which drives the single-player result modal). Versus hole-outs are owned by `VersusMatchController`. Simplest seam: early-return in `HoleCompletionBridge.HandleShot` when `IsVersus`. Confirm this fully suppresses `HoleCompleteModalController` in versus (no solo result modal flashes). Solo path unchanged.

### 8.5 `GameSession` event bridge
Additive:
```csharp
public enum MatchOutcome { P1Win, P2Win, Draw }
public static event System.Action<MatchOutcome,int,int> OnMatchComplete; // outcome, p1Strokes, p2Strokes
public static void MarkMatchComplete(MatchOutcome o, int p1, int p2) => OnMatchComplete?.Invoke(o, p1, p2);
```
Clear subscribers/append nothing to solo paths. `ResetSession()` leaves this untouched (event, not state).

### 8.6 `VersusResultHandler` (ShellScene / Assembly-CSharp)
Subscribes `GameSession.OnMatchComplete`. On fire:
- If `P1Win`: `RewardPointsManager.Instance.EarnPoints(reward)` where `reward` = modes DB `versus_1v1.rewards`. Loss/draw: grant 0.
- Drive the return-to-home path (reuse whatever the solo result "continue"/close uses to leave the gameplay scene).
> NOTE: confirm the asmdef that can see both `RewardPointsManager` and the modes DB; if the ShellScene controller can't read `ModesDatabaseCSV`, pass the reward int through the `OnMatchComplete` payload instead (resolve it in `VersusResultHandler`'s reachable scope, or have the flow read it and include it). Flag the cleanest seam rather than guessing.

---

## 9. Human-input lock

During the bot's turn, the human must not be able to take a shot. Toggle `ClubHandleDragger.enabled` (false on bot turn, true on human turn). Confirm there is no other live human shot entry point that bypasses `ClubHandleDragger` (e.g. a tap-to-aim path); if so, gate that too. The inactive card already renders at 0.50 via Phase-1 `MatchContext.SetActive`.

---

## 10. Win / tie / draw truth table (the courtesy logic)

Strict alternation, P1 first each round. When a shot resolves InCup:

| Who just sank | State of the other | Resolution |
|---|---|---|
| **P1** (shoots first in the round) | P2 has had `P1.Strokes − 1` shots | **P2 gets ONE courtesy shot** (their `P1.Strokes`-th). After it: P2 holed at equal count → **DRAW**; else → **P1 WIN**. |
| **P2** (shoots second in the round) | P1 already took their round shot this round (not holed) at equal count | **P2 WIN** immediately. P1 cannot tie by taking more shots (would exceed P2's count) → no courtesy. |

This is the entirety of the rule — the courtesy shot is always exactly one shot and always count-tie-able, so no "tie impossible" branch exists. Implement as: on P1 InCup, flip to P2 for a flagged courtesy turn; evaluate after that single shot. On P2 InCup, end immediately.

> Edge: if P1 sinks and P2 was ALREADY holed earlier in the same round — impossible under alternation (P2 shoots after P1; if P2 had holed, the match would have ended on P2's shot). No handling needed.

---

## 11. Safety stroke cap (guard, not main path)

CSV-tunable `versusStrokeCapOverPar` (default 5; can reuse `HoleCompletionBridge._strokeCapOverPar` semantics). If a player reaches `par + cap` strokes without holing:
- If the opponent has already holed → opponent already won (normal path).
- If neither has holed and BOTH reach the cap → **DRAW**.
- If one caps and the other has not yet holed → the capped player can no longer win; play continues only for the other until they hole (then they win) or also cap (DRAW).

With the basic bot (which holes within a few strokes) this rarely fires; it exists only to prevent an infinite match. Keep it simple — do not build elaborate "lowest strokes at cap" comparison logic for 2a.

---

## 12. Out of scope (do NOT build in 2a)

- **Bot difficulty / error model** — aim/power error bands, club-choice noise, level→band CSV table. That is **Phase 2b** (§13).
- **Dedicated 1v1 result modal** (side-by-side strokes, opponent portrait) — separate UI spec (2c). 2a ends on the WIN/LOSE/DRAW banner.
- **Sudden-death** on a draw.
- **Real matchmaking / networked opponent** — opponent stays a local bot; matchmaking is faked (Order 342, shipped).
- **Any change to solo/Practice** play, HUD, or result modal.

---

## 13. Phase 2b — difficulty model (deferred; written after 2a ships)

Layer onto `VersusBot` from 2a:
- **CSV difficulty table** (new `Assets/Resources/Data/bot_difficulty.csv` or columns appended to an existing CSV): keyed by character level (or level bracket) → `{ aimErrorDegMax, powerErrorMax, clubNoiseChance }`. Higher level = tighter bands.
- **Per-shot error injection:** before commit, perturb `yaw` by `±Random(0, aimErrorDeg)`, `power01` by `±Random(0, powerError)`, and with `clubNoiseChance` pick an adjacent non-optimal club — so the bot plays like a person chasing the cup, not a solver.
- **Source:** the opponent character's real level (decision #6) → CSV table → bands. The P2 card already shows that level (Phase 1).
- **Demo:** full-match bot videos at a low level vs a high level showing wider vs tighter dispersion / more vs fewer wasted shots.

A `HANDOFF_2b.md` will be written at 2a close-out with the tuned-against-real-bot starting values.

---

## 14. Acceptance checklist (implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] A 1v1 match plays end-to-end: alternating turns, P1 first, banner each turn, bot drives its own shots via the production `ShotController` external-drag path (NOT `ForceShotCompleteForBot`, NOT `#if UNITY_EDITOR`).
- [ ] During the bot's turn, human shot input is locked (`ClubHandleDragger` disabled); restored on the human's turn.
- [ ] One ball only; it teleports to the active player's stored lie each turn and its resting position is written back to that player's `MatchContext.Players[i].Lie`.
- [ ] Camera orients toward the cup / active ball each turn.
- [ ] First-to-sink resolves per §10: P1 sinks → P2 one courtesy shot → DRAW or P1 WIN; P2 sinks → P2 WIN immediately.
- [ ] WIN / LOSE / DRAW banner shows on match end via `TurnBannerWidget.ShowPersistent` and holds.
- [ ] On P1 win, `RewardPointsManager` is credited the `versus_1v1.rewards` value (200) via the `GameSession.OnMatchComplete` → ShellScene handler bridge; loss/draw grant 0. RP grant is NOT called from inside `Golfin.Physics.Viewer`.
- [ ] `MatchContext.Player` extended additively (Lie/Strokes/HoledOut/HoleOutStroke); existing API and `PlayerCardWidget` bind untouched.
- [ ] **SOLO regression:** launch Practice → a solo hole plays and resolves through the existing `HoleCompletionBridge → OnHoleComplete → HoleCompleteModalController` result modal exactly as before; no versus controller activity; HUD byte-identical to Phase-1 ship; no WIN/LOSE banner.
- [ ] `IsVersus` true only on the 1v1 route; the `VersusMatchController` is a hard no-op on `!IsVersus`.
- [ ] Safety cap (§11) prevents an infinite match; default par+5, CSV-tunable.

---

## 15. Visual gate

Per `feedback_prefer_bot_videos` + `feedback_record_bot_video_full_size`: a **bot-recorded video at full 1170×2532** of a complete 1v1 match resolving — opening banner, P1 shot, OPPONENT'S TURN banner, bot shot, alternation, a sink, the courtesy shot, and the WIN/LOSE/DRAW banner. Manual play is not the gate. (Banner *timing/easing* is reviewed live by Cesar in Unity, per the Phase-1 rule that Game-View recording resizes the view — but match *flow* and *resolution* are bot-video-gated.)

---

## 16. Tier & kickoff

**FULL PIPELINE** — runtime AI + new state machine + touches shipped gameplay. Heaviest 1v1 spec; the 2a/2b split keeps each pass demoable.

Kickoff (Cesar pastes into Claude Code):
```
Use the implementer subagent on "1v1_match_flow"
```
