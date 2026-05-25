# SPEC — `live_stat_provider_wiring`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

See `STATUS.md`. **SPEC_READY** as of 2026-05-25.

## Goal

Make every production gameplay shot use the player's actually-selected character + clubs + ball, sourced from `CharacterManager` / `BagManager` / `BallManager` / `ClubContext` / `BallContext`. Today every production shot uses hardcoded `DefaultStatProvider` defaults (default driver, neutral character, neutral ball) regardless of selection. This SPEC wires the live state into `ShotController.GetStatBundle()` while preserving the lab path's manual `InjectStatBundle()` override and defaulting gracefully when no selection exists.

## Problem statement (verified by grep, 2026-05-25)

- `ShotController.GetStatBundle()` (file: `Assets/Scripts/Gameplay/Input/ShotController.cs:338`) returns `DefaultStatProvider.BuildSwingBundle()` / `BuildPuttBundle()` unless `_statBundleOverridden == true`.
- Only callers of `InjectStatBundle`: `Physics/Viewer/PhysicsLabController.cs:555` and `:563` (both build per-club lab bundles).
- Grep for `CharacterManager.Instance` across `Gameplay/`, `Physics/Viewer/`, `UI/GameplayTransition/` returns **zero hits**.
- `GameSession.SelectedCharacterId` is set by matchmaking and persists across hole loads, but the gameplay path never reads it.
- `BagManager.Instance.EquippedBagSlot` + `BagManager.OnEquippedBagChanged` event are live and already consumed by HUD populators.
- `ClubContext.SelectedClubId` and `BallContext.SelectedBallId` are live and updated by `ClubContextPopulator` / `BallContextPopulator` from `BagManager` / `BallManager`.
- `DefaultStatProvider.cs` comment confirms intent: *"Until promoted, this always returns defaults — gameplay never breaks if inventory is absent."*

## Locked decisions

| # | Decision |
|---|---|
| L1 | **Static bus + assembly-CSharp-side population** pattern, mirroring `HoleContext` precedent. Named-asmdef code (`Golfin.Gameplay.Defaults`) exposes a `Func` resolver; Assembly-CSharp `MonoBehaviour` populates it on `Awake`. Per `tasks/lessons.md` "Unity asmdef — Cannot reference Assembly-CSharp from a named asmdef" — this is the only allowed direction. |
| L2 | **Per-shot live resolve** (resolver called inside `GetStatBundle()` every shot), NOT cached + invalidated. Cheap (one dictionary lookup per stat source) and avoids invalidation bugs on club/ball/character changes mid-hole. |
| L3 | **Graceful fallback to defaults.** If any of `SelectedCharacterId` / `ClubContext.SelectedClubId` / `BallContext.SelectedBallId` is empty OR the lookup fails, the resolver returns `null` and `StatProviderBus.Resolve()` falls through to `DefaultStatProvider`. Lab without a seeded inventory continues to function unchanged. |
| L4 | **Lab override unchanged in concept, but ownership moves.** ~~`PhysicsLabController.InjectStatBundle()` continues to win because `_statBundleOverridden` short-circuits before `StatProviderBus.Resolve()` is called. No lab changes required.~~ **AMENDED 2026-05-25 (architect Phase 2 review):** `PhysicsLabController.SetClub` was unconditionally injecting a neutral bundle on every call, which bypassed the bus for every committed shot in production flow (`Hole1Playthrough`, `LiveStatProviderVisualGate*`). Fix: `SetClub` no longer injects; lab callers (lab UI, putter cone smoke, smoke runner, putter green reader bot scenarios) MUST inject explicitly via a new `InjectLabBundleForCurrentClub()` method. Production-flow callers (BotDriver.PlayHoleToCup, auto-revert) MUST NOT inject. Full fix list in `ARCHITECT_REVIEW.md`. |
| L5 | **Existing CSV-first pattern.** `ClubDatabaseCSV`, `BallDatabaseCSV`, `CharacterDatabaseCSV` are the lookup sources. `CharacterManager.GetCharacter()` already handles the CSV-first-then-ScriptableObject-fallback per `tasks/lessons.md` "CSV-first pattern for character data". |

## Pre-flight findings (locked 2026-05-25)

| Check | Result |
|---|---|
| Where does the static bus live? | `Assets/Scripts/Gameplay/Defaults/` — alongside `DefaultStatProvider.cs`, same namespace `Golfin.Gameplay.Defaults`. **Don't break the existing asmdef.** |
| Where does the host live? | `Assets/Scripts/LiveStatProviderHost.cs` (Assembly-CSharp root) — needs to see `CharacterManager` / `BagManager` / `BallManager` / `CharacterDatabaseCSV` / `ClubDatabaseCSV` / `BallDatabaseCSV` / `ClubContext` / `BallContext`, all in Assembly-CSharp. |
| Scene placement for host | `ShellScene.unity` → `PersistentUI` GameObject (same parent as `GameplaySceneLoader`, per `feedback_never_manual_wiring.md` standard). Survives scene reloads via PersistentUI's `DontDestroyOnLoad`. |
| Character → CharacterStats mapping | `CharacterManager.GetCharacter(id)` returns `CharacterData?` (template). `CharacterManager.GetCharacterData(id)` returns `PlayerCharacterData?` (runtime: level, SP allocation, current Strength/ClubControl/Recovery/Stamina). Use `GetCharacterData()` for live stats. **Pre-flight Q1 for implementer:** verify `PlayerCharacterData` exposes the four stat values in the shape `CharacterStats` expects (`fp` types via `fp.FromInt`). |
| Club → ClubStats mapping | `ClubContext.SelectedClubId` (string) → `ClubDatabaseCSV.Instance.GetClub(id)` → club template → build `ClubStats`. **Pre-flight Q2 for implementer:** verify `ClubDatabaseCSV` has a `GetClub(string)` accessor (or equivalent) and that its returned shape contains `BaseVelocityMps`, `LoftDegrees`, `BaseBackspinRpm`, `Accuracy` (the four fields `ShotInputBuilder.Build` reads from `bundle.Club.Value`). If not, add the accessor as a small additive extension. |
| Ball → BallStats mapping | `BallContext.SelectedBallId` (string) → `BallDatabaseCSV.Instance.GetBall(id)` → ball template → build `BallStats`. Same pre-flight Q3 as above for `BallDatabaseCSV`. |
| Putter mapping | Putter is currently a special-cased `PutterStats` struct, not pulled from `ClubDatabaseCSV` per the `PhysicsLabController:550-560` precedent. **Pre-flight Q4 for implementer:** decide whether the player's putter has a single canonical `PutterStats.DefaultPutter` (same as lab) for v1, or whether the bag/inventory carries a separate putter selection. Probable answer: for v1, ship with `PutterStats.DefaultPutter` until Cesar wires a putter-selection UI; spec for that is out of scope. |
| Stamina / current values | `CharacterStats` struct's last two fields are stamina-related (PhysicsLab passes `fp.FromFloat(100f), fp.FromFloat(100f)` — `currentStamina, maxStamina`). **Pre-flight Q5 for implementer:** verify `PlayerCharacterData` exposes a runtime stamina value (or if stamina is meant to deplete shot-by-shot mid-hole and there's no runtime tracker yet, use `100f / 100f` for v1 as the lab does — note this as a follow-up). |

## Architecture

### New file 1: `Assets/Scripts/Gameplay/Defaults/StatProviderBus.cs`

In namespace `Golfin.Gameplay.Defaults` (existing asmdef). Static class. Single `Func` field + single resolve method:

```csharp
using System;
using Golfin.Physics.Stats;

namespace Golfin.Gameplay.Defaults
{
    /// <summary>
    /// Static bus that lets Assembly-CSharp register a live stat resolver. Named-asmdef
    /// code (e.g. ShotController) calls Resolve(isPutt) which forwards to the registered
    /// resolver, or falls through to DefaultStatProvider when nothing is registered.
    /// Matches the HoleContext static-bus precedent for cross-asmdef data flow.
    /// </summary>
    public static class StatProviderBus
    {
        /// <summary>
        /// Set by LiveStatProviderHost (Assembly-CSharp) on Awake. Returns null when
        /// the live state is incomplete (no character / club / ball selected), which
        /// causes Resolve() to fall through to the default bundle.
        /// </summary>
        public static Func<bool, StatBundle?> Resolver;

        /// <summary>
        /// Called by ShotController.GetStatBundle() every shot.
        /// </summary>
        public static StatBundle Resolve(bool isPutt)
        {
            var live = Resolver?.Invoke(isPutt);
            if (live.HasValue) return live.Value;
            return isPutt
                ? DefaultStatProvider.BuildPuttBundle()
                : DefaultStatProvider.BuildSwingBundle();
        }
    }
}
```

### New file 2: `Assets/Scripts/LiveStatProviderHost.cs`

In Assembly-CSharp root. `MonoBehaviour`. Lives on `ShellScene.unity → PersistentUI` GameObject. On `Awake`, registers `StatProviderBus.Resolver = ResolveLive`. On `OnDestroy`, unregisters.

```csharp
using UnityEngine;
using Golfin.Gameplay.Defaults;
using Golfin.Gameplay.Loop;       // GameSession
using Golfin.Physics.Math;
using Golfin.Physics.Stats;
// using <namespaces for CharacterDatabaseCSV, ClubDatabaseCSV, BallDatabaseCSV, ClubContext, BallContext>
// — verify exact namespaces during implementer pre-flight

public class LiveStatProviderHost : MonoBehaviour
{
    [SerializeField, Tooltip("Log one line per shot when the live path resolves a bundle. Helpful during wiring; turn off for ship.")]
    bool _enableDiagLog = true;

    void Awake()  => StatProviderBus.Resolver = ResolveLive;
    void OnDestroy()
    {
        if (StatProviderBus.Resolver == (System.Func<bool, StatBundle?>)ResolveLive)
            StatProviderBus.Resolver = null;
    }

    StatBundle? ResolveLive(bool isPutt)
    {
        // CHARACTER — read GameSession + CharacterManager runtime data.
        string charId = GameSession.SelectedCharacterId;
        if (string.IsNullOrEmpty(charId)) return null;
        var charData = CharacterManager.Instance?.GetCharacterData(charId);
        if (charData == null) return null;
        var characterStats = BuildCharacterStats(charData);

        // BALL — read BallContext + BallDatabaseCSV.
        string ballId = BallContext.SelectedBallId;
        if (string.IsNullOrEmpty(ballId)) return null;
        var ballData = BallDatabaseCSV.Instance?.GetBall(ballId);
        if (ballData == null) return null;
        var ballStats = BuildBallStats(ballData);

        if (isPutt)
        {
            // PUTTER — v1 uses canonical DefaultPutter; per-player putter selection is out of scope.
            var bundle = new StatBundle(
                PutterStats.DefaultPutter,
                ballStats,
                characterStats,
                fp.FromFloat(100f), fp.FromFloat(100f));
            if (_enableDiagLog) Debug.Log($"[LiveStatProvider] putt char={charId} ball={ballId} putter=DefaultPutter");
            return bundle;
        }

        // CLUB — read ClubContext + ClubDatabaseCSV.
        string clubId = ClubContext.SelectedClubId;
        if (string.IsNullOrEmpty(clubId)) return null;
        var clubData = ClubDatabaseCSV.Instance?.GetClub(clubId);
        if (clubData == null) return null;
        var clubStats = BuildClubStats(clubData);

        var swingBundle = new StatBundle(
            clubStats,
            ballStats,
            characterStats,
            fp.FromFloat(100f), fp.FromFloat(100f));
        if (_enableDiagLog) Debug.Log($"[LiveStatProvider] swing char={charId} club={clubId} ball={ballId}");
        return swingBundle;
    }

    // Mapping helpers — pre-flight Q1/Q2/Q3 govern exact field reads. Use
    // PhysicsLabController:555-572 as the structural reference for which fields
    // the physics stats structs expect.
    static CharacterStats BuildCharacterStats(PlayerCharacterData data) { /* TODO impl per pre-flight Q1 */ throw new System.NotImplementedException(); }
    static ClubStats      BuildClubStats(PlayerClubData data)           { /* TODO impl per pre-flight Q2 */ throw new System.NotImplementedException(); }
    static BallStats      BuildBallStats(PlayerBallData data)           { /* TODO impl per pre-flight Q3 */ throw new System.NotImplementedException(); }
}
```

The three `Build*Stats` helpers are the only places the implementer needs to map the player-data shape to the physics-stats shape. Use the existing `PhysicsLabController:555-572` lab bundles as the reference for which fields go where.

### Edited file: `Assets/Scripts/Gameplay/Input/ShotController.cs`

Single-line change in `GetStatBundle()` (line 338):

```csharp
// BEFORE:
private StatBundle GetStatBundle()
{
    if (_statBundleOverridden) return _statBundle;
    return IsPutt
        ? DefaultStatProvider.BuildPuttBundle()
        : DefaultStatProvider.BuildSwingBundle();
}

// AFTER:
private StatBundle GetStatBundle()
{
    if (_statBundleOverridden) return _statBundle;
    return StatProviderBus.Resolve(IsPutt);
}
```

### Scene edit: `ShellScene.unity`

Add `LiveStatProviderHost` MonoBehaviour to the `PersistentUI` GameObject. Drive this via MCP `gameobject-component-add` per `feedback_never_manual_wiring.md` — do not ask Cesar to wire by hand.

### Tests

Three new EditMode tests in `Assets/Scripts/Gameplay/Tests/StatProviderBusTests.cs`:

1. **`Resolve_WithNoResolverRegistered_ReturnsDefaultSwingBundle`** — sanity that defaults still work when host isn't present.
2. **`Resolve_WithResolverReturningNull_FallsThroughToDefault`** — sanity that null-returning resolver falls through.
3. **`Resolve_WithResolverReturningBundle_ReturnsLiveBundle`** — sanity that a registered resolver bundle propagates.

Plus one PlayMode test in `Assets/Scripts/Gameplay/Tests/LiveStatProviderHostPlayModeTests.cs` that loads a minimal scene, instantiates the host, seeds `GameSession` + `ClubContext` + `BallContext` with valid IDs, fires `StatProviderBus.Resolve(false)`, and asserts the returned bundle's club id matches the seeded id (proves end-to-end wiring).

## Q-LOCKS

| # | Question | Architect lean | Lock |
|---|---|---|---|
| Q1 | Stamina field — use `PlayerCharacterData.CurrentStamina` if it exists, or hard-code 100f for v1? | **100f for v1** unless `PlayerCharacterData` already tracks stamina runtime. Stamina depletion is a separate feature. | **LOCKED 2026-05-25 (Cesar):** Read `currentStaminaEnergy` / `maxStaminaEnergy` from `PlayerCharacterData` (the `[System.NonSerialized] float` runtime fields — NOT the `currentStamina` int stat). Both default to 100f today, so behavior is identical to lab's 100f/100f for v1, but the bus is wired the moment a depletion system lands. `BuildCharacterStats` does `fp.FromFloat(data.currentStaminaEnergy)` / `fp.FromFloat(data.maxStaminaEnergy)`. No spec/code change required when depletion ships. |
| Q2 | Putter selection — `PutterStats.DefaultPutter` for v1, or is there a putter-per-player concept in the inventory? | **DefaultPutter for v1.** Per-player putter is a future feature. | **LOCKED 2026-05-25 (Cesar):** Option B — read the player's equipped putter from the bag for v1. Pattern: filter `ClubContext.EquippedBag` for the entry with `LabClubIndex == 3` (the `ClubType.Putter` slot per `ClubContextPopulator.MapClubTypeToLabIndex`), then look up its `ClubDataRuntime` via `ClubDatabaseCSV.Instance.GetClub(entry.ClubId)` and the runtime `PlayerClubData` via `ClubManager.Instance.GetClubData(entry.ClubId)`. **Field mapping for `BuildPutterStats(PlayerClubData pcd, ClubDataRuntime tpl)`:** `Accuracy = pcd.GetAccuracy(tpl)`, `Control = pcd.GetLieResistance(tpl)` (off-center forgiveness ≈ lie resistance), `Weight = pcd.GetPower(tpl)` (power slot repurposed as putter "feel weight" since putter has no swing power concept), `Durability = pcd.currentDurability`, `LoftDegrees = fp.FromFloat(tpl.baseLoft)`, `BaseVelocityMps = fp.FromFloat(5f)` (canonical DefaultPutter value; CSV doesn't carry a putter-velocity field today; revisit if/when a putter-velocity stat lands). **Fallback:** if the equipped bag has NO `LabClubIndex == 3` entry, return `null` from the resolver (treat as partial-state missing per Q3). The implementer adds a 4th pre-flight check (Q4-pre) confirming `ClubManager.GetClubData(string)` returns `PlayerClubData?` with the `GetAccuracy/GetLieResistance/GetPower(template)` accessors as read above. |
| Q3 | When live state is *partially* missing (e.g. character selected but no ball), fall through to **full** defaults, or mix live + defaults? | **Full defaults.** Mixing creates surprising bugs ("why is my fast character hitting like a default?"). All-or-nothing keeps the seam clean. | **LOCKED 2026-05-25 (Cesar):** Option A — full defaults on any partial-state miss. Resolver returns `null`; bus falls through to `DefaultStatProvider.BuildSwingBundle()` / `BuildPuttBundle()`. No mixing. Rationale: keeps the diag-log signal binary (live OR default — no third "mixed" state); makes setup bugs loud rather than silent. **Related invariant (not enforced by this SPEC):** Cesar confirms the player cannot play a hole without a putter in the equipped bag — a future bag-arrange screen will tooltip + lock the screen when the bag has no putter. This means the Q2 "no putter in bag → return null → full defaults" branch is defense-in-depth for a state production should never produce; it's correct, but it should never fire in a shipping build. |
| Q4 | Should `LiveStatProviderHost` log a one-line `Debug.Log("[LiveStatProvider] using <char>/<club>/<ball>")` per shot when bundle resolves live, to aid testing? | **Yes**, gated by an `_enableDiagLog` SerializeField defaulting `true`. Helps Cesar verify wiring during play; turn off for ship. | **LOCKED 2026-05-25 (Cesar):** Option B — log on BOTH paths (live-success AND fallback). Gated by `[SerializeField] bool _enableDiagLog = true` on `LiveStatProviderHost`. **Logging contract:** Logging fires INSIDE `LiveStatProviderHost.ResolveLive`, NOT in `StatProviderBus.Resolve` — this keeps test scenes (no host registered) silent. Two log line shapes: live-success `[LiveStatProvider] LIVE <swing|putt> char=X club=Y ball=Z` (with `club=putter:<putterId>` on the putt branch); fallback `[LiveStatProvider] FALLBACK <swing|putt> reason=<R>` where R ∈ {`no-character`, `no-ball`, `no-club`, `no-putter`, `character-lookup-failed`, `ball-lookup-failed`, `club-lookup-failed`, `putter-lookup-failed`}. Implementer's choice: inline string literals or a small private enum — either is fine. Bus itself stays silent (no log on `Resolver == null`). |

## Definition of done

- [ ] `Assets/Scripts/Gameplay/Defaults/StatProviderBus.cs` exists, namespace `Golfin.Gameplay.Defaults`, static class, `Resolver` Func field, `Resolve(bool)` method with default-fallback semantics.
- [ ] `Assets/Scripts/LiveStatProviderHost.cs` exists (Assembly-CSharp root), MonoBehaviour, registers/unregisters resolver on Awake/OnDestroy, implements 3 mapping helpers (`BuildCharacterStats`, `BuildClubStats`, `BuildBallStats`).
- [ ] `LiveStatProviderHost` component added to `ShellScene.unity → PersistentUI` GameObject via MCP (no manual wiring asked of Cesar).
- [ ] `ShotController.GetStatBundle()` edited: single-line swap from `DefaultStatProvider.Build*Bundle()` direct call to `StatProviderBus.Resolve(IsPutt)`.
- [ ] All 5 pre-flight Q's (Q1–Q5 in §Pre-flight findings) resolved in `IMPLEMENTER_REPORT.md` with the field paths used.
- [ ] 3 EditMode tests for `StatProviderBus` + 1 PlayMode test for `LiveStatProviderHost` exist and pass; full project test gate stays green (current ~334 baseline per AI_CONTEXT).
- [ ] Lab unchanged: `PhysicsLab_Hole1` smoke run still injects per-club lab bundles via `InjectStatBundle` (the override path), behavior identical to today.
- [ ] **Visual gate (manual play):** Cesar runs a production hole, fires shots with two different characters of meaningfully-different stat levels (e.g. lv-10 power-build vs lv-1 default-build), and confirms the carry-distance / accuracy delta is visible. One-line description of the difference in `IMPLEMENTER_REPORT.md`.
- [ ] **Diag log:** at least one `[LiveStatProvider]` log line per shot captured during the visual gate, confirming the live path fires (not the default fallback). Paste a 3-shot log excerpt in the report.
- [ ] Per Lesson R: every new `.cs` file ships with its `.cs.meta`. The new component MonoBehaviour added to ShellScene must serialize cleanly across machine pulls.
- [ ] No regressions in EditMode tests; full project gate baseline maintained.

## Out of scope

- **Spin/fade/draw wiring** — separate spec at `Docs/Specs/Queued/spin_and_shot_shape_wiring/` (next in sequence).
- **Putter aim blue line** — separate spec at `Docs/Specs/Queued/putter_aim_blue_line/`.
- **Per-player putter inventory** — **NOW IN SCOPE** per Q2 lock. The bus reads the player's equipped putter from `ClubContext.EquippedBag` (`LabClubIndex == 3`) and builds `PutterStats` from its CSV row with the field mapping defined in Q2.
- **Bag-arrange screen putter-required enforcement** — future spec: bag-arrange UI must tooltip and lock when the equipped bag has no putter (Cesar lock 2026-05-25 alongside Q3). Out of scope here; the resolver's null-return on missing-putter is defense-in-depth only.
- **Stamina depletion mid-hole** — runtime stamina tracking is a future feature; v1 reads what `PlayerCharacterData` has (likely 100f static).
- **UI for switching active club** — `ClubContext.SelectedClubId` is already updated by the existing inventory UI; this SPEC consumes that state, doesn't build it.
- **Multi-bag-slot switching mid-hole** — once a bag is equipped, this SPEC reads its contents; switching bags during a hole is future scope.

## Pipeline

**Recommended: TIER 3 (FULL PIPELINE) — implementer → self-reviewer → architect-reviewer → Cesar.**

Reasoning:
- Stat resolution path has been a historical bug source (controls_g zero-init AeroConfig postmortem; physics-lab postmortem class).
- New static-bus pattern with cross-asmdef contract is "new architecture" per the Tier-3 trigger list.
- PlayMode test + visual gate justify the chain.

Tier 2 (TellCode) acceptable downgrade if Cesar wants faster turnaround — the structural pattern is simple, the failure modes are mostly catch-able by the 3 EditMode tests + manual visual gate.

Estimate: **~6 hours of Code time** (1–2 hours per Build*Stats mapping × 3 + ShellScene wiring + tests + visual gate) + pipeline chain.

## Sequencing

Fires after `puttpath_predictor_perf_and_design` (done 2026-05-25). Touches zero overlapping files with the queued `spin_and_shot_shape_wiring` (which edits `ShotController.CommitFlick` body + `ShotInputBuilder.Build`) or `putter_aim_blue_line` (which is rendering-only). Could run before or after `phone_build_smoke_test`. Per Cesar's call 2026-05-25: this trio fires first, phone test pushed after.

## Kickoff (fresh chat)

After Cesar locks Q1–Q4, paste this into the fresh architect chat to fire the pipeline:

```
Use the golfin-implementer subagent on "live_stat_provider_wiring"
```

Plus this context block alongside the line:

> **Tier 3 full pipeline.** Read order: `SPEC.md` → `tasks/lessons.md` ("Unity asmdef — Cannot reference Assembly-CSharp from a named asmdef" + "CSV-first pattern for character data").
>
> **Pre-flight required:** resolve §Pre-flight findings Q1–Q5 against the actual `PlayerCharacterData` / `PlayerClubData` / `PlayerBallData` shapes and the `ClubDatabaseCSV` / `BallDatabaseCSV` accessor signatures. Document each in `IMPLEMENTER_REPORT.md` under "Pre-flight". Reference the lab bundle path at `PhysicsLabController.cs:555-572` for the structural shape of `StatBundle` field assignments.
>
> **Lab compatibility is a hard gate.** After implementation, run smoke-bot Hole 1 in `PhysicsLab_Hole1.unity` and confirm the lab path still injects per-club bundles via `InjectStatBundle` (override wins over the bus). If lab behavior changes, that's a FAIL.
>
> **Visual gate is mandatory and manual.** Pipeline must include Cesar firing two production shots with meaningfully-different character builds and confirming the carry/accuracy delta. Self-reviewer cannot PASS without this in `IMPLEMENTER_REPORT.md`.
>
> Estimate: 6 hr Code time + pipeline chain.
