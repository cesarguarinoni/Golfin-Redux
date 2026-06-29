# SPEC — `tournament_round_loop` (T6)

**Phase:** Tournaments · **Tier:** FULL PIPELINE (Tier 3 — new modal UI fidelity + runtime gameplay integration)
**Authored:** 2026-06-28 (JST) · Architect
**Kickoff:** `Use the implementer subagent on "tournament_round_loop"`
**Unblocks:** first playable end-to-end tournament. Next task after this = dedicated results/claim-prize screen.

---

## 0. REUSE MANDATE (read first — non-negotiable)

> **Clone-and-modify existing GameObjects. Author ZERO new panels, buttons, separators, or sprites.**
> Cesar (2026-06-28): *"All elements for the Signup modal exist already (buttons, panel, separator)."*

Every visual piece of the Signup modal already ships in the project's modal family
(`HoleCompleteModalController`, `MatchmakingModalController`, the inventory modals — all under
`Assets/Scripts/UI/Modals/` + `Assets/Scripts/UI/{Matchmaking,Inventory}/`). All extend
`ModalController` (`Assets/Scripts/UI/Modals/ModalController.cs`).

**Step 0 (implementer, before building anything):** open an existing centered confirm modal prefab
(start with `HoleCompleteModal`, fallback `MatchmakingModal`) and identify the reuse sources:
- the **navy gradient panel** (rounded-50, 3px white border) → Signup panel
- the **gold + silver "Main Buttons"** GO pair → CONFIRM (gold) + CANCEL (silver)
- the **separator line** GO → both Signup separators
- the **RP coin icon** Image (imageHash `d7b5d07acf45a459f8117adbc96d7ae0368c95c1`) → ENTRY + reward icons

Duplicate those GOs into the Signup prefab and re-bind text/positions per §3. Do **not** rebuild geometry from the Figma React dump.

---

## 1. Scope — ONE task (Cesar: "One")

T6 delivers the whole enter→play→finish loop as a single pipeline run:

1. **Signup modal** (new prefab + `TournamentSignupModalController : ModalController`) — Figma `13480:2479`.
2. **Selection CTA redirect** — "SIGN UP" states open the Signup modal (today they jump straight to Hole Selection with no Register).
3. **Hole-Selection data-binding** — bind the static scaffold cards to `entry.PerHole` (sequential Finished / Next / Locked); "Next" launches that hole. *(This was never built — see §5.)*
4. **Tournament round boot** — launch a hole with a `GameSession.IsTournament` flag + an active `TournamentRoundContext`.
5. **Stat seam** — `LiveStatProviderHost.ResolveLive` branches: tournament-active → `CharacterStats` from the frozen `CharacterSnapshot` + stamina from the entry pool (not live `charData`).
6. **Stamina pool** — own pool, runtime-only, flat placeholder depletion per shot (§8).
7. **Hole-complete submit** — mirror the `IsVersus` early-return: build `HoleResult` → `SubmitHoleResult` → return to Hole Selection.
8. **Finish** — last hole submitted → entry `Finished` → route to Leaderboard.

**Out of scope (next task):** dedicated results/claim-prize screen; real stamina-depletion economy + disk persistence; remote backend.

---

## 2. Flow

```
Selection card tap (TournamentSelectionScreenController.HandleCtaClicked)
 ├─ Open / Ending  (NOT entered, CTA="SIGN UP")  → Signup modal 13480:2479
 │     ├─ CANCEL  → close, stay on Selection
 │     └─ CONFIRM → backend.Register(id, EntryFeeRP, characterId)   ← RP debit + snapshot freeze
 │                  → ScreenManager.ShowScreen(TournamentHoleSelection)
 ├─ EnteredActive  (CTA="CONTINUE")              → TournamentHoleSelection           (unchanged)
 └─ EnteredFinished / Ended (CTA="LEADERBOARD")  → TournamentLeaderboard             (unchanged)

TournamentHoleSelection (binds entry.PerHole)
 └─ "Next" hole card tap → BeginTournamentHole(holeIndex)
        → GameSession.IsTournament = true
        → TournamentRoundContext.BeginRound(entry)            (snapshot + stamina pool)
        → SeedSession(snapshot.CharacterId, equipped ball/club) + BeginGameplayLoad(holeIndex)
           → play hole (stats from snapshot via §7; stamina depletes §8)
           → hole sinks → HoleCompletionBridge (IsTournament branch §9)
                → GameSession.OnTournamentHoleComplete(strokes)            (event → Assembly-CSharp)
                → TournamentRoundHandler: SubmitHoleResult(id, result)
                → if !Finished → ShowScreen(TournamentHoleSelection)   (next card now "Next")
                → if  Finished → ShowScreen(TournamentLeaderboard)     (real score ranked)
```

---

## 3. Signup modal — `13480:2479` (clone-and-modify)

**Controller:** new `TournamentSignupModalController : ModalController` at
`Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs`. Mirror `MatchmakingModalController`
for show/hide + backdrop + prior-active-state restore (its `OnShow`/`OnHide` panel-state capture is the
pattern to copy so closing the modal never resurrects a stale home panel).

**Prefab:** `Assets/Prefabs/UI/Tournaments/TournamentSignupModal.prefab` (new file, assembled from §0 clones).

### Geometry & tokens (Figma px; TMP size = Figma ÷ 1.4)

Panel `Pop-Up` **978×531** · gradient top `#133453` → bottom `#091b33` · border **3px** white ·
radius **50** · drop-shadow 0,10,10 rgba(0,0,0,0.40) · inner border `#0a1d35` · bottom-pad 32.
Content container: flex-col, **gap 24**, padding x **48** / y **32**.

| Element | Node | Text / binding | Font (Figma → TMP) | Color |
|---|---|---|---|---|
| Sponsor | `13480:2575` | `"{SPONSOR} PRESENTS"` (`def.SponsorKey`↑, fallback `GOLFIN PRESENTS`) | Rubik SemiBold 24 → **17.1** | gradient white→`#828fa1` via `#d1d6e0`@40% |
| Title | `13480:2576` | tournament name `LocalizationManager.Get(def.NameKey)` | Noto Sans JP Bold 42 → **30** | `#FFFFFF` |
| Venue | `13480:2577` | `"{venue}  -  {N} Holes"` (`tourn.venue.{clubId}` / `def.HoleSet.Count`) | Rubik Regular 22 → **15.7** | `#c7d6eb` |
| Date range | `13480:2579` | `"MMM d – MMM d"` (`def.StartUtc`/`def.EndUtc`, en-dash) | Rubik SemiBold 40 → **28.6** | `#FFFFFF` |
| Dash | `13480:2580` | `—` | Rubik Regular 40 → **28.6** | `#c7d6eb` |
| Countdown | `13480:2582` | `"Ends in {d}d {hh}h"` (reuse `BuildDateLine` countdown logic from `TournamentSelectionScreenController`) | Rubik SemiBold 40 → **28.6** | `#FFFFFF` |
| ENTRY pill | `13480:2618` | bg rgba(250,199,77,0.18) · border 1px `#fac74d` · radius 22 · pad l14 r16 y6 · gap 8 | — | — |
| ↳ "ENTRY" | `13480:2620` | static | Rubik SemiBold 22 → **15.7** | `#fac74d` |
| ↳ RP icon | `13480:2621` | coin `d7b5d07…` · **30×30** | — | — |
| ↳ amount | `13480:2622` | `def.EntryFeeRP` (e.g. `500`) | Rubik SemiBold 22 → **15.7** | `#fac74d` |
| Reward icon | `13480:2624` | coin `d7b5d07…` · **40×40** | — | — |
| Reward | `13480:2625` | `"{topPrizeRP:N0} + Trophy"` (`TournamentService.GetTopPrizeRP(def.Id)`) | Rubik Bold 32 → **22.9** | `#73e080` |

Separators `13480:2484` (top) + `13480:2637` (mid): reuse the existing separator GO, full content width, 2px.

### Buttons — row `13480:2530`, flex gap **48**

- **CANCEL** `13480:2532` — clone **silver** Main Buttons. 359×120, radius 20, border 2px `#f7f8f9`,
  gradient `#FFFFFF`→`#d1d5db`→`#818ea1`. Label "CANCEL" Rubik SemiBold 66 → **47** `#1e293b`.
  `onClick` → `Hide()`.
- **CONFIRM** `13480:2534` — clone **gold** Main Buttons. 391×120, radius 20, border 2px `#ffe48b`,
  gradient `#fcf195`→`#d6ab42`→`#bb7f1d`. Label "CONFIRM" Rubik SemiBold 66 → **47** `#321506`.
  `onClick` → `OnConfirm()` (§4).

### Controller behavior
- `Open(string tournamentId)` — caches id, populates header/entry/reward from `backend.GetTournament(id)` + `TournamentService.GetTopPrizeRP(id)`, shows over a dimmed backdrop.
- `OnConfirm()`:
  - `characterId` = current roster selection (**NOTE-API:** `CharacterManager.Instance.SelectedCharacterId`; confirm exact accessor).
  - `var entry = TournamentService.Instance.Backend.Register(id, def.EntryFeeRP, characterId);`
    (Register debits RP **and** freezes the `CharacterSnapshot` internally — both already live; do not re-implement.)
  - On success → `Hide()` → `ScreenManager.Instance.ShowScreen(ScreenId.TournamentHoleSelection)`.
  - **Insufficient RP:** Register's RP debit is the guard. If RP < fee, do not enter — show the existing `ToastController` "Not enough RP" and keep the modal open. (**NOTE-API:** confirm whether `Register` throws / returns null on insufficient RP, or whether to pre-check `RewardPointsManager` balance before calling. Pre-check is safer — mirror the entry-fee block UX from `mode_select_system`.)

---

## 4. Selection CTA redirect — `TournamentSelectionScreenController.HandleCtaClicked`

Current `switch (card.State)` sends `Open`/`Ending`/`EnteredActive` all to `ShowScreen(_holeSelectionTarget)`.
**Change only the not-entered arms:**

```csharp
case TournamentSelectionCard.CardState.Open:
case TournamentSelectionCard.CardState.Ending:
    // NOT entered → confirm-and-register first
    _signupModal.Open(card.TournamentId);
    break;

case TournamentSelectionCard.CardState.EnteredActive:   // "CONTINUE" — already registered
    ScreenManager.Instance?.ShowScreen(_holeSelectionTarget);
    break;
// EnteredFinished / Ended / Upcoming: UNCHANGED
```

Add `[SerializeField] private TournamentSignupModalController _signupModal;`. Keep the existing
`SelectedTournamentId` write. Modal is wired in the scene, not instantiated per-tap.

---

## 5. Hole-Selection binding — `TournamentHoleSelectionScreenController` (CURRENTLY UNBUILT)

The controller is still the **Stage-1 scaffold**: nav-only (podium + Close), hole cards are **static
placeholder instances, not bound, not tappable**. Screen-bind only did Selection + Leaderboard. T6 builds the binding.

On `OnEnable` (after a frame, mirror Selection's `RebuildNextFrame`):
- `var entry = TournamentService.Instance.Backend.GetMyEntry(TournamentService.Instance.SelectedTournamentId);`
- `var def = backend.GetTournament(id);` — `def.HoleSet` is the ordered hole list.
- For each ordinal `i` in `def.HoleSet`, derive card state **sequentially**:
  - `i < entry.PerHole.Count` → **Finished** (show strokes vs par; reuse the Stage-0 `TournamentHoleCard_Finished` Result block, Figma `13414:5549`).
  - `i == entry.PerHole.Count` → **Next** (playable; the only tappable card).
  - `i > entry.PerHole.Count` → **Locked**.
- "Next" card `onClick` → `BeginTournamentHole(def.HoleSet[i])` (§6).

**Reuse:** the three card visual states already exist as Stage-0 prefabs in the Hole-Selection scene
(`tournament_screens` Stage 0). Bind to them; do not author new cards. **Do NOT** inherit
`HoleSelectionScreenController` (it wipes scroll content on enable — see the existing class comment).

---

## 6. Round boot — `BeginTournamentHole(int holeIndex)`

Reuse the practice boot path; add the tournament context. (**NOTE-API:** confirm exact
`GameSession.SeedSession(...)` + `GameplaySceneLoader.BeginGameplayLoad(holeIndex)` signatures — mirror
`HoleSelectionScreenController.HandleActionClicked` / the 1v1 launch.)

```
GameSession.IsTournament   = true;                       // new flag, parallels IsVersus
GameSession.TournamentId   = id;                          // new
TournamentRoundContext.BeginRound(entry);                // snapshot + stamina pool (§7,§8)
SeedSession(character: entry.Snapshot.CharacterId,        // LOCKED snapshot character
            ball/club: player's equipped gear);          // gear stays live (snapshot froze character only)
BeginGameplayLoad(holeIndex);
```

`GameSession.ResetSession()` must clear `IsTournament`/`TournamentId` and call `TournamentRoundContext.EndRound()`
(parallels the IsVersus reset) so solo/practice stays byte-identical.

---

## 7. Stat seam — `LiveStatProviderHost.ResolveLive`

At the **top** of `ResolveLive(bool isPutt)`, before the live character lookup, branch:

```csharp
if (TournamentRoundContext.IsActive)
{
    var snap = TournamentRoundContext.Snapshot;
    var characterStats = new CharacterStats(
        strength:    snap.Strength,
        clubControl: snap.ClubControl,
        recovery:    snap.Recovery,
        stamina:     snap.Stamina);          // the STAT, from the frozen snapshot
    // ball + club/putter resolved from the live contexts exactly as today
    // staminaEnergy from the entry pool, NOT charData:
    //   current = fp.FromFloat(TournamentRoundContext.StaminaEnergyRemaining)
    //   max     = fp.FromFloat(TournamentRoundContext.StaminaEnergyMax)
    // build + return StatBundle(...) with these three swaps; null-fallback rules for ball/club unchanged
}
// else: existing live path, untouched
```

Everything else (ball/putter/club build helpers, fallback-on-missing) is reused verbatim. The only swap is
character stats + stamina-energy source. **Solo path bit-identical when `IsActive == false`.**

---

## 8. Stamina pool (runtime-only v1)

New static `TournamentRoundContext` (place beside `LiveStatProviderHost` in Assembly-CSharp so the seam
has no asmdef hop; or in `Golfin.TournamentsRuntime` if it needs `EntryState` — **NOTE:** pick whichever
keeps the `LiveStatProviderHost` reference one-way):

```csharp
static bool   IsActive;
static string TournamentId;
static CharacterSnapshot Snapshot;
static float  StaminaEnergyMax;        // = 100f placeholder (charData.maxStaminaEnergy default)
static float  StaminaEnergyRemaining;  // starts at Max each round
BeginRound(EntryState entry)  // IsActive=true, Snapshot=entry.Snapshot, Remaining=Max
EndRound()                    // IsActive=false, clears
DepleteStamina(float amount)  // Remaining = Mathf.Max(0, Remaining - amount)
```

- **Starts full** at round start, **carries hole→hole within the session** (BeginRound seeds once; do not
  reset per hole — only on EndRound).
- **Flat placeholder depletion:** subtract a constant per shot at the single commit point
  (**NOTE-API:** `ShotController.CommitFlick` — confirm it's the one place a shot is committed; mirror where
  strokes increment). Cost is a CSV-tunable constant — add `tournament_stamina_cost` to an existing tournaments
  CSV (default e.g. `5` → ~20 shots). Putts deplete too (flat).
- **Runtime-only** — NOT persisted to `Golfin.Save`; quitting mid-round resets the pool on resume. Disk
  persistence + a real depletion curve = the deferred economy task (Cesar-approved split).
- **No hard gate v1** (GDD §17.7): hitting 0 does NOT block play. It only drives the existing red
  stamina-bar / low-stamina bolt indicator if/when surfaced. (Hard-gate revisit = polish.)

---

## 9. Hole-complete submit — mirror the `IsVersus` branch

`HoleCompletionBridge` lives in `Golfin.Physics.Viewer` and must **not** call `TournamentService` directly
(Lesson W asmdef boundary). Mirror the proven `OnMatchComplete → VersusResultHandler` /
`OnHoleComplete → HoleCompleteModalController` event pattern:

- In `HoleCompletionBridge.HandleShot` (the hole-complete hook): add an `if (GameSession.IsTournament)`
  early-return branch (parallel to the existing `IsVersus` early-return) that fires a new
  `GameSession.OnTournamentHoleComplete(holeIndex, strokes)` event **instead of** the solo result modal.
- New Assembly-CSharp `TournamentRoundHandler` (subscribes in ShellScene, like `VersusResultHandler`):
  ```
  var result = new HoleResult(holeIndex, strokes, rngSeed, inputLog);
  var entry  = TournamentService.Instance.Backend.SubmitHoleResult(id, result);
  GameSession.IsTournament = false;            // round paused between holes
  if (entry.Status == EntryStatus.Finished) {
      TournamentRoundContext.EndRound();
      ScreenManager.Instance.ShowScreen(ScreenId.TournamentLeaderboard);
  } else {
      ScreenManager.Instance.ShowScreen(ScreenId.TournamentHoleSelection);  // next card → "Next"
  }
  ```
- The solo `HoleCompleteModalController` must **not** fire in tournament mode (the early-return guarantees this).

**`HoleResult` fields:** `holeIndex`, `strokes` (the real scoring field, from the completion event),
`rngSeed`, `inputLog`. **v1 = minimal capture** for rngSeed/inputLog (anti-cheat replay is a server
concern). **NOTE-API:** confirm the `HoleResult` ctor + how strokes are surfaced at hole-complete (the solo
modal already receives them); if the sim's per-hole seed / shot log aren't readily exposed, populate
`rngSeed = 0` + empty `inputLog` with a `// TODO server-replay` and flag in the report — do NOT block.

---

## 10. Backend calls (signatures confirmed from `ITournamentBackend`)

- `EntryState Register(string id, long entryPaymentRP, string characterId)` — idempotent; RP debit + snapshot freeze internal.
- `EntryState? GetMyEntry(string id)` — `.PerHole`, `.Status`, `.Snapshot`. null = not registered.
- `EntryState SubmitHoleResult(string id, HoleResult result)` — appends; sets `Finished` when all holes in.
- `TournamentDefinition GetTournament(string id)` — `.HoleSet`, `.EntryFeeRP`, `.NameKey`, `.SponsorKey`, `.ClubId`, `.StartUtc`, `.EndUtc`.
- `TournamentService.Instance.GetTopPrizeRP(id)` / `.SelectedTournamentId` — already live.

Backend accessed via `TournamentService.Instance.Backend`. **No backend code changes in T6** — it's all consumed.

---

## 11. Decisions / flags (stated for veto)

- **D1 Stamina = runtime-only v1** (§8). Veto → adds a v3→v4 save migration; defer recommended.
- **D2 `HoleResult` rng/input = minimal v1** (§9). Veto → needs sim seed/shot-log plumbing; defer recommended.
- **D3 No character preview on Signup** — snapshot freezes silently from the current roster pick on CONFIRM (matches the Figma, which has no picker). Veto → add a character chip to the modal.
- **D4 Sequential holes** — only the next unplayed hole is "Next"; rest Locked (derived from `entry.PerHole`). Free-order is not v1.
- **D5 Gear stays live** — snapshot froze the character only; ball/club come from the player's equipped bag at play time.

---

## 12. Acceptance gate

1. **Full normal-play video, 1170×2532** (per `feedback_gameplay_video_use_normal_play`): ShellScene →
   Tournaments → tap an Open tournament → Signup modal → CONFIRM (RP visibly debited) → Hole Selection
   (card 1 = "Next") → play hole 1 → returns to Hole Selection (card 1 Finished, card 2 "Next") → play a
   second hole → leaderboard reflects real strokes. No synthetic buttons — drive the real card `onClick`.
2. **EditMode tests:** Register-from-modal debits RP + freezes snapshot; `ResolveLive` returns snapshot
   stats when `TournamentRoundContext.IsActive`; stamina depletes per shot + carries hole→hole + resets on
   EndRound; SubmitHoleResult advances Next→Finished; last-hole submit flips `Finished` + routes to Leaderboard;
   solo path bit-identical when `IsActive == false` (regression).
3. **CANCEL** closes the modal with no Register, no RP change, no stale-panel resurrection.
4. No `Assets/Scripts/Physics/` sim diffs beyond the additive `IsTournament`/stamina hooks.

---

## 13. NOTE-API confirmations for the implementer (do not guess — read the file, then inline)

- `CharacterManager.Instance.SelectedCharacterId` (or equivalent roster-selection accessor).
- `GameSession.SeedSession(...)` + `GameplaySceneLoader.BeginGameplayLoad(int)` exact signatures (mirror practice/1v1 launch).
- `GameSession.IsVersus` set/reset sites — add `IsTournament`/`TournamentId` in lockstep.
- `HoleCompletionBridge.HandleShot` — exact hole-complete hook + how `strokes` is surfaced (solo modal already gets it).
- `ShotController.CommitFlick` — the single shot-commit point for the stamina hook.
- `HoleResult` ctor field order + `EntryState.PerHole` type.
- `RewardPointsManager` balance accessor for the insufficient-RP pre-check.

---

## 14. Files touched

**New:**
- `Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs`
- `Assets/Prefabs/UI/Tournaments/TournamentSignupModal.prefab` (cloned GOs)
- `TournamentRoundContext.cs` (Assembly-CSharp beside `LiveStatProviderHost`, or `TournamentsRuntime`)
- `TournamentRoundHandler.cs` (Assembly-CSharp, ShellScene-subscribed)

**Modified:**
- `Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs` (CTA arms + `_signupModal` ref)
- `Assets/Scripts/UI/Tournaments/TournamentHoleSelectionScreenController.cs` (card binding + `BeginTournamentHole`)
- `Assets/Scripts/LiveStatProviderHost.cs` (tournament branch)
- `GameSession.cs` (`IsTournament`/`TournamentId` + reset + `OnTournamentHoleComplete`)
- `HoleCompletionBridge.cs` (IsTournament early-return → event)
- `ShotController.cs` (stamina-deplete hook, gated on `TournamentRoundContext.IsActive`)
- a tournaments CSV (+`tournament_stamina_cost`)
- `ShellScene.unity` (place Signup modal instance + wire refs) — **Code/Unity owns the scene; Architect never edits it.**
