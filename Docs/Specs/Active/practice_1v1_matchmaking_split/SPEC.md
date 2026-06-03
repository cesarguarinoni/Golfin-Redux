# SPEC — Practice / 1v1 Matchmaking Split

**Slug:** `practice_1v1_matchmaking_split`
**Tier:** TELLCODE (behavioral re-route of shipped C0 code; small, targeted) — promote to FULL PIPELINE if the Practice seed relocation proves non-trivial.
**Status:** SPEC_READY
**Pairs with:** `mode_select_system` (which mode's PLAY invokes which route).

---

## Goal

Today the fake matchmaking sits on the **Practice** path. Move it: **Practice becomes solo (no matchmaking)**, **1v1 owns matchmaking** with a **random hole (1-18) + random opponent from the roster** (Cesar's directive + answer this turn).

---

## Current state (verified)

- `Scripts/UI/HoleSelection/HoleCardController.cs:94` — tapping a hole card forwards to `MatchmakingModalController.Open(holeIndex)`.
- `Scripts/UI/Matchmaking/MatchmakingModalController.cs` — `Open(...)` captures the session seed and runs `OpponentScanRoutine`, which on "OPPONENT FOUND" calls `GameSession.SeedSession(hole, charId, bagSlot)` (loop_v2 Stage B) then hands off to `GameplaySceneLoader` (Stage C0). `_opponentPool` (List<CharacterDataRuntime>) is the random opponent source.
- So **Practice currently runs through matchmaking** — exactly what moves to 1v1.

## Target flows

**Practice (solo, no matchmaking):**
- Hole Select tap (`HoleCardController`) -> seed the session directly: `GameSession.SeedSession(holeIndex, selectedCharId, equippedBagSlot)` -> `GameplaySceneLoader.BeginGameplayLoad(...)`. **No matchmaking modal.**
- CONSEQUENCE (the real work): the seed currently lives *inside* `MatchmakingModalController`. Relocate a seed call onto the Practice launch path so the result-modal / PLAY-NEXT loop (C1) still works solo. Extract the seed into a tiny shared helper (e.g. `GameSession.SeedSession` already exists — just call it from the Practice path) so both paths use one seeding entry point.

**1v1 (matchmaking + random):**
- Mode Select 1v1 PLAY -> pick `holeIndex = Random 1..18`, then `MatchmakingModalController.Open(holeIndex)` (random opponent already comes from `_opponentPool`). No hole-select screen.
- Confirm `Open` signature accepts an externally-chosen hole; if it currently derives the hole from a tapped card, add an overload/param for the random-hole entry.

---

## Changes (surgical)

1. **Practice path** — `HoleCardController` (or the Practice launch in `mode_select_system`) calls `SeedSession` + `GameplaySceneLoader.BeginGameplayLoad` directly; remove the `MatchmakingModalController.Open` forward from the Practice/hole-select route.
2. **1v1 path** — Mode Select 1v1 PLAY -> random hole -> `MatchmakingModalController.Open(randomHole)`. Random opponent from `_opponentPool` (already random).
3. **Seed ownership** — ensure exactly one seed point per path; matchmaking keeps seeding for 1v1, Practice seeds on launch. No double-seed.

---

## Acceptance gates (loop_v2_smoke_bot)

1. Practice: Hole Select -> tap hole -> **no matchmaking modal** -> gameplay at that hole; hole-out -> result modal SUCCESS -> PLAY NEXT works (solo loop intact).
2. 1v1: Mode Select 1v1 PLAY -> matchmaking modal shows random opponent -> gameplay at a random hole (1-18) -> result loop intact.
3. No regression to C0/C1 (305/305 EditMode + existing smoke scenarios); session seed present in both paths exactly once.

---

## Risks / notes
- The seed relocation is the only non-trivial bit — it's why C0/C1 assumed matchmaking always ran first. Verify nothing downstream reads "opponent present" as a precondition for the result modal on the solo path.
- Future **1v1 in-game UI** (opponent HUD, turn order, versus scoring) is OUT of scope here — separate roadmap item, Cesar's upcoming Figma.
