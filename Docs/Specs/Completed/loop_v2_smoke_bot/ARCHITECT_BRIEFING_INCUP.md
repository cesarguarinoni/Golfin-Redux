# Architect briefing — loop_v2_smoke_bot InCup decision

**For:** Architect Claude (claude.ai chat with full repo access)
**From:** Cesar
**Status of task:** STATUS=ARCHITECT_REVIEW_ESCALATE after iter-3. Both prior FAIL items resolved; one open question that needs an architectural call.

---

## What loop_v2_smoke_bot is

A reusable **bot framework** that drives the production app (ShellScene → Home → PLAY → matchmaking → gameplay → fire shot → result modal) like a real player would. Goal: replace per-stage manual playthrough (~30 min × every visual gate × every iter) with composable scenarios (30–50 lines each) that produce honest screenshot evidence.

Two layers:
- **Driver** (`BotDriver.cs`) — primitives: `Click(name)`, `WaitForScreen(...)`, `WaitForModalVisible`, `FireShot(target, power)`, `Capture(label)`, etc.
- **Scenarios** (`Scenarios.cs`) — thin coroutines that compose primitives.

Three scenarios shipping with this task:
1. **Hole 1 Playthrough** — Stage C1 visual gate (Home → PLAY → matchmaking → gameplay → fire → result modal).
2. **Settings Round Trip** — Stage A surviving flow smoke.
3. **Hole Selection Browse** — Stage E visual gate.

Pattern reuses `SmokeRunner2fHost` lifecycle (armed-flag + self-destruct + `CaptureCore.SnapPlayModeSafe`).

---

## What the SPEC's "two-seam ceiling" means

SPEC §"Files POTENTIALLY EDITED" (line 374) explicitly authorizes only **two** test seams beyond the public API surface:
1. `MatchmakingModalController.State` public getter (if missing).
2. `PhysicsLabController` public putt-fire seam (if missing).

> "Everything else MUST go through existing public APIs. If the implementer finds itself adding test seams beyond these two, **escalate** before continuing."

Iter-1/2 used both pre-authorized seams. Option B below would be a **third** seam — hence the escalation.

---

## What iter-1 → iter-3 produced

### Resolved (verified by reviewer)

| Item | Resolution |
|---|---|
| ShellScene contamination (5 stale `[LoopV2SmokeBot]` GOs baked in iter-1) | `git diff main -- Assets/Scenes/ShellScene.unity` = 0 bytes. Root cause was `Destroy(this)` vs `Destroy(gameObject)` + pre-play `SaveScene()`. Both fixed; launcher uses `[DidReloadScripts]` + `playModeStateChanged` injection now, never saves. |
| FindCupPosition fuzzy-match catching `SpinButton` | Replaced with reflection read of `Golfin.Gameplay.UI.HUD.HoleContext.PinWorld`. Log confirms correct pin coords `(-230.50, 10.18, -72.48)`. |
| FireShot polling race (ball-state machine cycled Aiming→Flying→Rolling→AtRest→Aiming in one frame; bot polled every 0.5s and missed terminal window) | Bot now follows §2f scaffolding: `SetClub(PutterIndex)` → `SetBallAnimatorPlayRate(float.MaxValue)` (Instant) → `PlaceBallAt(nearCup, 1)` → `SetCameraYawRadians(yaw)` → gate on Aiming → subscribe `OnShotComplete` BEFORE `Fire(preset)` → frame-poll the event flag. Log: `FireShot OK: OnShotComplete fired after 0.009s — terminal=AtRest`. |
| HoleSelection s02/s03 byte-identical (false PASS) | Scenario reworked. CardTapButton was ambiguous (18 matches, first was wrong). New flow: home → tee nav → hole selection grid → home. MD5s: `4e39` / `6305` / `4e39` (s03==s01 by round-trip closure; s02 distinct). |
| EditMode tests | 305/305 PASS via `AllEditModeTestRunner` (delegates to TestRunnerApi). |
| SPEC §DoD inconsistency (7/5/5 vs 6/4/4) | SPEC updated to match actual scenario output. |

### Persistent open issue — InCup capture

The Stage C1 visual gate per SPEC §DoD requires a **result modal capture** (HoleCompleteWidget visible after ball sinks). Current state:

- Bot fires `putt_flat_3m` preset from 3m placement near the cup on Hole 1's green.
- Ball physics runs; terminal state observed is `AtRest`, not `InCup`.
- s06 in the playthrough is labelled `result_modal` but shows no modal (because the ball didn't sink, HoleCompleteWidget never showed).
- The capture lies about its content, which undermines the framework's reusability-as-acceptance-evidence promise.

Bot mechanics are sound (OnShotComplete fires reliably; ball moves; turn advances). It's the **outcome shape** that's wrong for this specific gate.

---

## The three options

### Option A — Tighten placement to 30cm to force InCup

**Change:** Bot calls `PlaceBallAt(cup + 0.3 * dir, 1)` instead of `PlaceBallAt(nearCup_3m, 1)`. Same preset, near-certain InCup.

**Pro:** Fastest fix. No SPEC change. No new seams. Result modal captured honestly.

**Con:** Contradicts the standing rule Cesar added on 2026-05-19: *"bot must drive ANY UI like a real player, not just play-through-to-cup."* A 30 cm tap-in is not realistic; future contributors will copy this pattern and the "real player" contract erodes. Stage D/E/F scenarios that need ball-in-cup will all reach for sub-foot placement.

**Reviewer's note:** rejected because it breaks the contract.

### Option B — Add `ForceShotCompleteForBot(InCup)` test seam *(reviewer's recommendation)*

**Change:** Add a new public method on `PhysicsLabController` that drives the ball-state machine straight to InCup (or invokes `HandleShotComplete(InCup)`) without running full physics. Bot calls this instead of `Fire(preset)` for scenarios that need result-modal evidence.

**Pro:**
- Cleanly verifies the Stage C1 modal-subscription wiring (HoleCompleteWidget subscribes to InCup; that's what the C1 gate is actually testing).
- Real-physics `Fire(preset)` stays available for any scenario that needs real ball motion (e.g., Stage D variance demos).
- Bot stays honest about what each scenario captures: `Fire` proves shot mechanics; `ForceShotCompleteForBot(InCup)` proves modal wiring.
- Mirrors how unit tests would force a terminal state — the bot is, in spirit, an integration test.

**Con:**
- Third pre-authorized test seam (SPEC §"Files POTENTIALLY EDITED" caps at two).
- Adds production code that exists solely for the bot. Has to be `#if UNITY_EDITOR` or carefully gated so it can't be invoked at runtime.
- Sets a precedent: future bot scenarios will request more seams. Need a clear principle for when a seam is justified.

**Reviewer's recommendation rationale:** the C1 gate is about modal wiring, not putt physics. Real-physics putt evidence is a different gate. Conflating them via 30cm placement is the wrong abstraction; a clean seam is honest about which thing each capture proves.

### Option C — Defer C1 modal capture; ship the framework as-is

**Change:**
- Drop s06 (`result_modal`) from Hole1Playthrough or relabel to `s06_shot_terminal_atrest`.
- Update SPEC §DoD to reflect the relabel.
- Mark Stage C1 visual gate as needing a follow-up task (either real-physics-with-tuned-preset that actually sinks, or a separate ForceShotComplete seam task).
- Manual Cesar play covers C1 result-modal evidence until that follow-up ships.

**Pro:**
- Framework ships honestly. No false captures. No new seams.
- Iter-3 already produces 5 verifiable captures of real production-flow navigation through gameplay.
- Stages A, D, E, F can use the framework immediately; C1 is the only deferred gate.

**Con:**
- The original justification for inserting this task between C0 and C1 ("bot pays for itself by Stage D") loses some urgency if C1's gate still needs manual play.
- We accept that the framework-as-shipped doesn't reach the cup — the very thing the SPEC originally promised.

---

## Other relevant context

- The "behaves like a real player" contract was added 2026-05-19 specifically because Cesar revised the SPEC mid-task to broaden it from a single-purpose script to a framework that drives ANY UI.
- SPEC line 454 already acknowledges: *"Bot can't drive a UI element (e.g. drag-to-aim shot) | Scenario falls back to test-seam direct fire; flag the missing primitive for a future spec."* This is a precedent for seam-fallback being acceptable when a primitive is missing.
- Pre-existing seams used: `SetBallAnimatorPlayRate` (internal), `SetCameraYawRadians` (public), `PlaceBallAt` (public), `SetClub` (public), `BallSM.OnShotComplete` (internal access). None added in iter-3.
- The bot framework is otherwise complete and reusable. The framework decision will likely propagate to every Stage D/E/F scenario that involves shot outcomes.

---

## Question

Which option do we take, and if (B), what's the principle for when a future bot scenario gets a new seam vs. has to use existing public APIs?

Files relevant to the decision:
- `Docs/Specs/Active/loop_v2_smoke_bot/SPEC.md` — original SPEC + 2026-05-19 revision.
- `Docs/Specs/Active/loop_v2_smoke_bot/ARCHITECT_REVIEW.md` — full iter-3 reviewer verdict with mechanism trace.
- `Docs/Specs/Active/loop_v2_smoke_bot/IMPLEMENTER_REPORT.md` — iter-3 PASS/FAIL grid.
- `Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs` — current FireShot implementation (lines 444–603).
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — Fire/HandleShotComplete pipeline.
- `tasks/loop_v2_smoke_bot/hole1_playthrough/screenshots/` — current captures + history.log.
