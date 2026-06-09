# HANDOFF — `1v1_match_flow` (1v1 Phase 2)

**Notion:** Order 344 (Queued, P2) · **Follows:** `1v1_ingame_ui` Phase 1 (Order 343, shipped commit `756ab280`, in `Docs/Specs/Completed/1v1_ingame_ui/`)
**Purpose:** arm a fresh Architect chat to spec Phase 2. This is a brief, NOT the spec.
**Prepared:** 2026-06-08 (Architect)

> New-chat startup discipline still applies: `conversation_search` this 1v1 thread → `AI_CONTEXT.md` headline → Notion Order 344 → `git pull --ff-only` before reading repo files. Then read this file.

---

## What Phase 2 delivers
Three pieces (the Phase-1 SPEC "Out of scope" list):
1. **Production bot opponent** — difficulty random + tied to bot level; plays like a person chasing the cup, not a solver.
2. **Turn-flow state machine** — alternating shots, inactive-player control lock, camera/ball ownership, banner per turn.
3. **Win/tie resolution** — first to sink wins; trailing player gets one courtesy shot to tie; winner banner.

The match shape Cesar described: banner → P1 shoots (P2 watches) → banner → P2 shoots (P1 watches) → … → first to sink wins; the other gets exactly one more shot only if it can tie.

---

## What Phase 1 already built (Phase 2 plugs into these — do NOT rebuild)
- **`GameSession.IsVersus`** — versus gate. `Scripts/Gameplay/Loop/Session/GameSession.cs`.
- **`MatchContext`** (static) — `Scripts/Gameplay/UI/ShotUI/HUD/MatchContext.cs`. `Players[2]` {DisplayName, Level, Portrait, RarityBackground, TurnCount}, `ActiveIndex`, `SetActive(i)`, `OnActiveChanged`, `OnChanged`. **Phase 2's flow drives `SetActive` + per-player `TurnCount`.**
- **`TurnBannerWidget.Show(string)`** — `Scripts/Gameplay/UI/ShotUI/TurnBannerWidget.cs`. Slide/fade band (slides from LEFT for the left player, RIGHT for the right, by index). **Phase 2 calls it on each turn change + reuses the band for the winner banner.**
- **`PlayerCardWidget`** — per-index bind + 1.0/0.50 opacity, already swaps on `MatchContext.SetActive`.
- **`VersusHudController`** — `Scripts/Gameplay/UI/ShotUI/VersusHudController.cs`. Versus orchestrator; currently a `_debugForceVersus` / runtime debug toggle drives the active swap + banner. **Phase 2 replaces the debug driver with the real turn-flow.**
- Opponent identity already captured into `MatchContext.Players[1]` at "OPPONENT FOUND" (`MatchmakingModalController`).

---

## Code anchors for the three pieces

### Bot (production runtime — NOT the editor smoke bot)
- **Shot-commit path the bot drives:** `ShotController.BeginExternalDrag()` → `SetExternalPower(powerNormalized, coneFinetune)` → `EndExternalDrag()` → `CommitFlick()` — `Scripts/Gameplay/Input/ShotController.cs:76–99`. The human path (`ClubHandleDragger`) uses the same calls. Aim is set via the cone/aim system before commit.
- ⚠️ The existing smoke bot (`BotDriver`, `LoopV2SmokeBot` under `Scripts/Physics/Viewer/Bot/`) is **`#if UNITY_EDITOR`** and uses the `ForceShotCompleteForBot` `_ForBot` seam — **NOT shippable.** Phase 2's opponent must be a **new runtime bot** that drives the SAME `ShotController` external-drag entry points but ships in build (no `UNITY_EDITOR` gate, no `_ForBot` seam). Treat the smoke bot as a behavioral reference only.
- **Difficulty model (Cesar):** random + tied to bot level; aim-error band + power-error band + club-choice noise, higher level = tighter. Spec as an explicit, tunable error model. Bot stats resolve through the same live-stat path (`StatProviderBus`) using `MatchContext.Players[1]`'s `CharacterDataRuntime`.

### Turn boundary / hole-out
- **Shot resolved (turn ends):** `BallStateMachine.OnShotComplete` (`Action<ShotResult>`) — `Scripts/Gameplay/Loop/BallStateMachine.cs:30`.
- **Holed out:** terminal `BallState.InCup` (`BallStateMachine.cs:199`) via `ICupDetector`. `HoleCompleteDriver` (implements `IHoleOutTrigger`) already fires the **solo** hole-complete card on InCup — Phase 2 must branch this in versus to run win/tie instead of the solo card.
- `GameSession.ShotHistory` / `RecordShot` / `TurnCount` are **single-player**. Phase 2 needs a per-player stroke + turn model (extend `GameSession` or move into `MatchContext`).

### ⚠️ Central architecture question — ball ownership
Today there is a **single ball** (no `BallController`/spawn exists; physics is batch `BallSimulation.Simulate`). Real 1v1 = each player has their own ball at their own lie. Phase 2 must decide the model: two persistent balls swapped each turn (own position/lie/stroke), or reset-per-shot. This drives camera, scene state, and the physics seed — **biggest design + risk item; settle it first.**

---

## Open design decisions to settle with Cesar (at spec time)
1. **Ball/lie model** — two persistent balls swapped per turn, or reset-per-shot? (drives camera + scene + physics seed)
2. **Tie semantics on equal strokes** — declared draw, or sudden-death extra hole?
3. **Courtesy shot** — does the trailing player still take the one tie-attempt shot when a tie is mathematically impossible, or does the match end immediately?
4. **Bot level source** — opponent character's level, or a separate bot-difficulty value? (also resolves the Phase-1 NOTE: P2 card level currently shows char level or 1)
5. **Win reward** — any RP on a 1v1 win? (entry fee is 0 today)
6. **Camera** — follow the active player's ball each turn? (likely yes)

---

## Files to read first (new chat)
1. `Docs/Specs/Completed/1v1_ingame_ui/SPEC.md` — Phase 1 spec; its "Out of scope" list = Phase 2 scope.
2. `Docs/Specs/Completed/1v1_ingame_ui/IMPLEMENTER_REPORT.md` — exactly how `MatchContext` / `TurnBannerWidget` / `VersusHudController` got wired.
3. `Scripts/Gameplay/UI/ShotUI/HUD/MatchContext.cs`, `VersusHudController.cs`, `TurnBannerWidget.cs`.
4. `Scripts/Gameplay/Input/ShotController.cs` (external-drag), `Scripts/Gameplay/Loop/BallStateMachine.cs` (OnShotComplete / InCup), `HoleCompleteDriver` (Golfin.Physics.Viewer).
5. `Scripts/Physics/Viewer/Bot/BotDriver.cs` — reference for driving shots programmatically (editor-only; the production bot is new).

## Tier
**FULL PIPELINE** — runtime AI + new state machine + touches shipped gameplay. Heaviest of the 1v1 specs; consider phasing within Phase 2 (e.g. turn-flow + win/tie first with a trivial bot, then the real difficulty model) if scope runs large.

## Kickoff (paste into the new Architect chat)
```
Spec 1v1 Phase 2 — `1v1_match_flow`, Notion Order 344. Read Docs/Specs/Queued/1v1_match_flow/HANDOFF.md first, then follow session startup (conversation_search the 1v1 thread, AI_CONTEXT, Notion 344, git pull --ff-only). Phase 1 (the UI) already shipped. Let's settle the open design decisions, then write the spec.
```
