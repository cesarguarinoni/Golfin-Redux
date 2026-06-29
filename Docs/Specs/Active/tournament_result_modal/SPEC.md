# SPEC — tournament_result_modal (Prize / Claim screen)

**Created:** 2026-06-29 18:08 JST
**Tier:** FULL PIPELINE (Tier 3 — UI fidelity + new orchestration arch + runtime timing state machine)
**Epic:** Tournaments v1 → results/claim ("claim on result screen", EPIC 387b3e97)
**Figma frame:** `13498:2067` ("Prize" Pop-Up) — file key `5gEAHjl6xAtW8iYY7NMvWd`
**Replaces:** T6 §1 stopgap where last-hole submit routes back to the Leaderboard.

---

## 0. PRE-FLIGHT (Lesson AK — mandatory, do NOT skip)

Before authoring or reviewing any pixel value:

1. **Re-pull the node at step 0.** Run `get_design_context` (and `get_metadata`) on `13498:2067` with `clientLanguages=csharp`, `clientFrameworks=unity`. The token table in §4.3 below is **reconcile-against-node convenience, NOT source of truth** — diff every value against the fresh pull and fix drift.
2. **Verify the Figma→TMP divisor for THIS task.** Do **not** trust 1.4 blindly. The Prize modal shares its header font system with the **live** `TournamentSignupModal.prefab` (same Noto Sans JP / Rubik families, several identical Figma px sizes). Open that prefab, read the actual TMP `fontSize` on the shared header texts (e.g. the 42px title, the 22px venue line), and derive the true divisor from the ground-truth prefab values. Apply that divisor consistently.
3. **Flag vestigial nodes.** In the node, `📍` (`13498:2079`) is `hidden="true"` — do NOT author it. The "Hours + Map" row is date + dash + status word only.

---

## 1. Scope & boundary

**In scope:**
- **A.** A claim-only "Prize" modal (clone of the Signup modal) that shows the player's final rank + prize for a resolved tournament, with a single gold **CLAIM** button.
- **B.** An auto-present orchestrator that decides *when* the Prize modal appears, per Cesar's timing rules.
- **Two small infra seams** (ScreenManager screen-changed event; ModalController open-count + stack-emptied event) that B depends on.

**Out of scope:** leaderboard changes, prize-table edits, the round loop (T6, shipped), any backend logic change. The backend already exposes everything needed (§3).

**Form factor:** the epic's "result screen" is realized as this **modal/Pop-Up** (the Figma frame is a Pop-Up), auto-presented over Home/Tournament screens — not a full ScreenManager screen.

---

## 2. Deliverables

| # | Artifact | Type | Location |
|---|----------|------|----------|
| A1 | `TournamentResultModal.prefab` | Prefab (clone of `TournamentSignupModal.prefab`) | `Assets/Prefabs/UI/Modals/` |
| A2 | `TournamentResultModalController.cs` | New `ModalController` subclass | `Assets/Scripts/UI/Tournaments/` (same asmdef as `TournamentSignupModalController`) |
| B1 | `TournamentResultPresenter.cs` | New singleton MonoBehaviour (orchestrator) | `Assets/Scripts/UI/Tournaments/` |
| S1 | ScreenManager screen-changed event | Edit (additive) | `Assets/Scripts/UI/ScreenManager.cs` |
| S2 | ModalController open-count + stack-emptied event | Edit (additive) | `Assets/Scripts/UI/Modals/ModalController.cs` |

> A2/B1 co-locate with `TournamentSignupModalController.cs` so they inherit its asmdef and already-resolved references (`Golfin.Tournaments`, `Golfin.UI.Modals`, `GolfinRedux.UI`, `Golfin.UI.Toast`, `TMPro`). Namespace: `GolfinRedux.UI.Tournaments`.

---

## 3. Verified seam inventory (read from live repo 2026-06-29)

**Backend access — `Golfin.Tournaments` via the host singleton:**
- `TournamentService.Instance.Backend` → `ITournamentBackend` (DDOL singleton, ready after `Awake`). `Assets/Scripts/TournamentsRuntime/TournamentService.cs`.
- `TournamentService.Instance.SelectedTournamentId` (string?) — nav handoff hint.
- `TournamentService.Instance.GetTopPrizeRP(id)` — headline RP helper (not needed here; result carries the real prize).

**`ITournamentBackend` (`Assets/Scripts/Tournaments/ITournamentBackend.cs`):**
- `GetTournaments()` → `IReadOnlyList<TournamentDefinition>`
- `GetTournament(id)` → `TournamentDefinition`
- `GetMyEntry(id)` → `EntryState?` (null = not entered)
- `GetResults(id)` → `TournamentResult?` — **null until resolved** (`now ≥ endUtc + resolveDelay`) or if not entered. **This is the "tournament is over for me" gate.**
- `ClaimPrize(id)` → void — grants RP + item, sets `Claimed=true`, **idempotent (no-op if already claimed)**. The claim hook.

**`TournamentResult` (`Assets/Scripts/Tournaments/TournamentResult.cs`):**
`FinalRank` (int, 1-based) · `IsTie` (bool) · `PrizeRP` (long) · `ItemRewardId` (string?) · `Claimed` (bool).

**`TournamentDefinition` fields (confirmed via Signup `Populate`):** `Id`, `SponsorKey`, `NameKey`, `ClubId`, `HoleSet` (`.Count`), `StartUtc`, `EndUtc`, `EntryFeeRP`, `PrizeTableId`.

**`EntryStatus`** = `NotEntered | InProgress | Finished | DNF`. **`TournamentState`** = `Upcoming | Open | Playing | Ending | Closed | Ended` (`Ended` = window over + entered). `Assets/Scripts/Tournaments/TournamentEnums.cs`.

**Claim persistence:** `ITournamentEntryStore.IsClaimed/MarkClaimed`, backed by `SaveBackedEntryStore` (T5, persisted). `LocalTournamentBackend.ClaimPrize` guards on `_store.IsClaimed(id)`. **NOT exposed through `ITournamentBackend`** — the only claim signal available to the UI layer is `TournamentResult.Claimed`. See §5 memo-freshness note.

**Screen layer — `GolfinRedux.UI` (`Assets/Scripts/UI/ScreenManager.cs`):**
- `ScreenManager.Instance.CurrentScreen` → `ScreenId`
- `ScreenManager.Instance.ShowScreen(ScreenId, instant)`
- Eligible `ScreenId`s: `Home`, `TournamentSelection`, `TournamentHoleSelection`, `TournamentLeaderboard`.
- **GAP:** no screen-changed event today → adds in **S1**.

**Modal base — `Golfin.UI.Modals` (`Assets/Scripts/UI/Modals/ModalController.cs`):**
- `Show()` / `Hide()` / `IsVisible()`; virtual `OnShow()` / `OnHide()`. `Show()` does `transform.SetAsLastSibling()`.
- **GAP:** no central modal stack / no "any modal closed" event → adds in **S2**.

**Clone template & UI patterns — `TournamentSignupModalController.cs`** (same Figma family, the direct clone source for both prefab and controller).

---

## 4. PART A — Prize modal

### 4.1 Clone gate (HARD RULE)
- Clone `Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab` → `TournamentResultModal.prefab`. **Cite the source prefab GUID + the reused child GUIDs** (navy panel, separator sprite, RP-coin sprite, the **gold Main Buttons** instance) in the implementation report. No GameObject authored from scratch.
- Reuse from the clone: navy gradient panel, top + inner separators, RP-coin Image, and **one** gold `Main Buttons` instance.
- **Diff from Signup → Prize:**
  - Buttons: Signup has a gold/silver **pair** (Confirm/Cancel). Prize has a **single gold CLAIM** button. Keep the gold instance, relabel to `CLAIM`, drop the silver/cancel. **No close/X button** (claim-only — the node has none).
  - **New** middle block: a RANK band (`RANK #N`) sitting between two separators (the node has an extra separator + RANK `Upper`/`Header` that Signup lacks — `13498:2081`, `13498:2107/2108/2110`, `13498:2105`). Clone an existing separator instance for the second divider.
  - Reward line: bind to the *result* prize (PrizeRP + item), green `#73e080`, not the Signup's entry-fee framing.

### 4.2 Controller `TournamentResultModalController : ModalController`

SerializeFields (mirror Signup naming):
- Header: `_sponsorText`, `_titleText`, `_venueText`, `_dateLineText` (TMP)
- **New:** `_rankText` (TMP)
- Reward: `_rewardCoinIcon` (Image), `_rewardText` (TMP)
- `_claimButton` (Button)
- Optional `_panelsToHide` (default empty — see §4.4)

```
public void Open(string tournamentId):
    guard tournamentId non-empty
    guard TournamentService.Instance != null  (else warn + return)
    var def    = Backend.GetTournament(tournamentId)        // guard null
    var result = Backend.GetResults(tournamentId)           // guard null — must be resolved
    _tournamentId = tournamentId
    Populate(def, result)
    Show()

private void Populate(TournamentDefinition def, TournamentResult r):
    // Header — identical logic to Signup.Populate (copy verbatim, incl. the
    // "already embeds Holes" venue guard and the SPONSOR PRESENTS / NameKey / ClubId binds)
    sponsor → "{SPONSOR} PRESENTS"  (def.SponsorKey, fallback "GOLFIN PRESENTS")
    title   → LocalizationManager.Get(def.NameKey)
    venue   → "{venueName}  -  {def.HoleSet.Count} Holes"  (with already-has-Holes guard)
    // Date line — date RANGE + status word "Finished" (NO countdown; node shows "Finished")
    dateLine → $"{def.StartUtc:MMM d} – {def.EndUtc:MMM d} — Finished"
    // Rank
    rankText → $"RANK #{r.FinalRank}"          // tie display: see OPEN DECISION O-3
    // Reward (green): item suffix only when an item is awarded
    rewardText → r.ItemRewardId != null ? $"{r.PrizeRP:N0} + Trophy" : $"{r.PrizeRP:N0}"
    rewardCoinIcon.enabled = true

protected override void Awake():
    base.Awake()
    _claimButton?.onClick.AddListener(OnClaim)

private void OnClaim():
    guard TournamentService.Instance != null
    try { Backend.ClaimPrize(_tournamentId); }   // grants RP + item, idempotent
    catch { ShowToast("Claim failed."); return; }
    // optional: ShowToast("Prize claimed") / let RP bar refresh via its own event
    Hide()                                        // close → presenter re-evaluates (S2 event)
```

### 4.3 Token table — RECONCILE AGAINST NODE `13498:2067` (Lesson AK)
Figma px below; the Unity column is **provisional /1.4 — VERIFY divisor against the live Signup prefab per §0.2**.

| Element | node id | Figma px | font / weight | color | Unity (prov.) |
|---|---|---|---|---|---|
| GOLFIN PRESENTS | 13498:2073 | 24 | Rubik SemiBold | gradient white→#828fa1 via #d1d6e0@40% (see O-2) | ~17.1 |
| Lomond Championship (title) | 13498:2074 | 42 | Noto Sans JP Bold | #ffffff | ~30.0 |
| club · 18 Holes (venue) | 13498:2075 | 22 | Rubik Regular | #c7d6eb | ~15.7 |
| date range | 13498:2077 | 40 | Rubik SemiBold | #ffffff | ~28.6 |
| dash "—" | 13498:2078 | 40 | Rubik Regular | #c7d6eb | ~28.6 |
| "Finished" | 13498:2080 | 40 | Rubik SemiBold | #ffffff | ~28.6 |
| **RANK #N** | 13498:2110 | 64 | Noto Sans JP Bold | #ffffff | ~45.7 |
| reward "12,000 + Trophy" | 13498:2090 | 40 | Rubik Bold | **#73e080** | ~28.6 |
| **CLAIM** label | I13498:2095;2180:1003 | 66 | Rubik SemiBold | #321506, lineHeight 84, letterSpacing −0.78, text-shadow 0/1/0 rgba(255,255,255,0.3) | ~47.1 |

**Layout/containers (Figma px):**
- Panel `13498:2067`: 978×605, gradient **#133453 (top) → #091b33 (bottom)**, border **3px #ffffff**, radius **50**, drop-shadow 0/10/10 rgba(0,0,0,0.4). Inner Pop-Up `13498:2068`: border 1px #0a1d35, **pb 32**.
- Content container `13498:2070`: padding **48 / 32**, **gap 24** between major blocks.
- Header `Upper` `13498:2071`: internal gap 10; Header sub-block `13498:2072` gap 4. Hours+Map `13498:2076` gap 12.
- RP-amount row `13498:2088`: gap 8; RP icon `13498:2089` 40×40.
- CLAIM button container: h **120**, px **48**, radius **20**, gold gradient `rgb(252,241,149) → rgb(214,171,66)@59.9% → rgb(187,127,29)`, border **2px #ffe48b**, + sheen + ellipse highlight (all carried by the cloned Main Buttons instance — do not re-author).
- Vertical order (each gap 24): Header → Separator → RANK → Separator → Reward; then CLAIM below (in Pop-Up, pb 32).

### 4.4 Placement (scene)
The Prize modal + presenter must survive screen swaps and render over Home **and** all tournament screens → instantiate the prefab on the **persistent UI canvas** (where `PersistentUIManager` lives), **not** inside a single screen hierarchy. `Show()`'s `SetAsLastSibling()` keeps it on top, so `_panelsToHide` stays empty by default. **NOTE for implementer:** confirm the exact persistent parent + that a `GraphicRaycaster` is present on the modal's Canvas and `Raycast Target` is off for all non-interactive Images (project convention).

---

## 5. PART B — `TournamentResultPresenter` (auto-present orchestrator)

Singleton MonoBehaviour on the persistent UI object. Holds a serialized ref to the `TournamentResultModalController`.

### 5.1 Presentable predicate
```
bool TryFindPresentable(out string id):
    id = null
    if TournamentService.Instance?.Backend == null: return false
    string best = null; DateTime bestEnd = DateTime.MaxValue
    foreach def in Backend.GetTournaments():
        if _claimedThisSession.Contains(def.Id): continue          // session guard (§5.4)
        if Backend.GetMyEntry(def.Id) == null: continue            // player entered?
        var r = Backend.GetResults(def.Id)
        if r == null || r.Claimed: continue                        // resolved & unclaimed?
        if def.EndUtc < bestEnd: best = def.Id; bestEnd = def.EndUtc  // oldest first, deterministic
    id = best; return best != null
```

### 5.2 Eligible-screen test
```
bool IsEligibleScreen(ScreenId s) =>
    s == Home || s == TournamentSelection || s == TournamentHoleSelection || s == TournamentLeaderboard
```
(Exact set is **OPEN DECISION O-1** — Cesar said "Home or Tournament screens".)

### 5.3 Trigger state machine
Wired in `OnEnable` / `OnDisable` (event-driven UI convention):
- **S1** `ScreenManager.ScreenChanged += OnScreenChanged`
- **S2** `ModalController.ModalStackEmptied += OnModalsCleared`
- Plus a **coarse safety tick** (a tournament can resolve while the player idles on Home with no screen change / no modal activity): `InvokeRepeating(SafetyTick, 30f, 30f)` — interval is **OPEN DECISION O-4**.

All three funnel into one guarded routine:
```
void TryPresent():
    if _presenting: return                                   // a Prize modal is already up/in-flight
    if !IsEligibleScreen(ScreenManager.Instance.CurrentScreen): return
    if !TryFindPresentable(out var id): return
    if ModalController.OpenModalCount > 0: return            // another modal up — wait for ModalStackEmptied
    StartCoroutine(PresentAfterDelay(id))

IEnumerator PresentAfterDelay(string id):
    yield return new WaitForSecondsRealtime(_settleDelay)    // default 1.0s, unscaled (honors "wait a second after modals close")
    // RE-VALIDATE everything post-wait (player may have navigated / a modal popped / claimed elsewhere)
    if !IsEligibleScreen(ScreenManager.Instance.CurrentScreen): yield break   // "don't show if they changed screens"
    if ModalController.OpenModalCount > 0: yield break        // a modal popped during the wait → bail; ModalStackEmptied will retry
    if !TryFindPresentable(out var stillId) || stillId != id: yield break
    _presenting = true
    _resultModal.Open(id)                                     // count 0→1 (no emptied event)
```

`OnScreenChanged(s)` → `if IsEligibleScreen(s) TryPresent()`
`OnModalsCleared()` → `_presenting = false; TryPresent()`  ← fires after the Prize modal's own close too, chaining to the next unclaimed prize or stopping.
`SafetyTick()` → `TryPresent()`

### 5.4 Claim + re-present
`OnClaim` → `ClaimPrize(id)` → `Hide()`. The presenter must **not** re-show the same prize:
- On claim, add id to a session `HashSet<string> _claimedThisSession` (the presenter exposes a tiny hook the modal calls on successful claim, OR the presenter performs the claim — see O-5). Cross-session re-show is already blocked by persisted `IsClaimed` (fresh backend derives `result.Claimed=true`).
- **REQUIRED verification (memo-freshness):** confirm `Backend.GetResults(id)` returns `Claimed=true` immediately after `ClaimPrize(id)` within the same session. `LocalTournamentBackend` memoizes results (`_resultMemo`); if the memoized object is stale (`Claimed=false`), the predicate alone would re-present in a loop. The `_claimedThisSession` set is the guard that makes this safe **regardless** of memo behavior — do not remove it. If memo is confirmed fresh, the set is belt-and-suspenders; if stale, it is load-bearing.

---

## 6. Infra diffs (additive, non-breaking)

### S1 — `ScreenManager.cs`
```csharp
public static event System.Action<ScreenId>? ScreenChanged;
```
Fire at the **end** of `ApplyScreen(screenId)` (after `_currentScreen` is set and all SetActive calls): `ScreenChanged?.Invoke(screenId);`. One field + one line.

### S2 — `ModalController.cs`
```csharp
public static int OpenModalCount { get; private set; }
public static event System.Action? ModalStackEmptied;   // fires on 1→0 transition
```
- In `Show()` after `_isVisible = true;` → `OpenModalCount++;`
- In `Hide()` after `_isVisible = false;` → decrement once and fire on empty:
  ```csharp
  OpenModalCount = Mathf.Max(0, OpenModalCount - 1);
  if (OpenModalCount == 0) ModalStackEmptied?.Invoke();
  ```
- **Leak guard:** add a base `OnDisable()` — if `_isVisible`, treat as a hide (set `_isVisible = false`, decrement, fire-on-empty) so a force-deactivated modal can't strand the count. Ensure no double-decrement (the `_isVisible` flag gates it).

> Decrementing at `Hide()` (fade start) rather than fade end is intentional: the presenter's 1.0s `_settleDelay` (§5.3) comfortably absorbs the 0.2s fade-out, so "show a second after they're closed" is honored without coupling to animation completion.

---

## 7. OPEN DECISIONS (Cesar's veto)

- **O-1 — Eligible-screen set.** Spec includes Home + TournamentSelection + TournamentHoleSelection + TournamentLeaderboard. Confirm whether TournamentLeaderboard should be included (it's where last-hole submit currently lands — natural claim-on-arrival) or excluded.
- **O-2 — Gradient sponsor text.** Node renders "GOLFIN PRESENTS" as gradient-filled text (white→#828fa1 via #d1d6e0@40%). If the live Signup modal uses a flat color for the same element, match Signup (flat #d1d6e0) for consistency; gradient text needs a TMP shader. Default: **match Signup**.
- **O-3 — Tie rank display.** `result.IsTie` available. Node shows plain "RANK #1". Default v1: plain `RANK #N`, ignore tie marker. Alt: `RANK #N (T)`.
- **O-4 — Safety-tick interval.** Default 30s. (Covers resolve-while-idle-on-Home. Lower = snappier, higher = cheaper.)
- **O-5 — Who calls ClaimPrize.** Default: the modal's `OnClaim` calls `ClaimPrize` + notifies the presenter to add the session-claimed id. Alt: presenter owns the claim (modal raises a `Claimed` callback). Default keeps the modal self-contained like Signup.
- **O-6 — Multiple unclaimed prizes.** Spec presents oldest-EndUtc first, then chains to the next on close. Confirm one-at-a-time is desired (vs. a "you have N prizes" summary).

---

## 8. Acceptance criteria

1. After a tournament the player entered **resolves** (`GetResults != null`), the Prize modal auto-appears the next time the player is on an eligible screen — bound to the real `FinalRank`, `PrizeRP` (+ "+ Trophy" iff item), and the def's header.
2. If another modal is open, the Prize modal waits until it closes **+ 1.0s**, then shows — **only if** still on an eligible screen and no new modal opened.
3. Navigating to an **ineligible** screen during the wait **aborts** the show.
4. **CLAIM** grants the prize once (RP balance increases; `ClaimPrize` idempotent), modal closes, and it **never re-appears** that session or after restart.
5. Claim-only: the modal has **no** dismiss/close path other than CLAIM.
6. No regression to existing modals (Signup, Matchmaking): `OpenModalCount` stays balanced; `ScreenChanged` fires on every screen swap.
7. Visual fidelity to `13498:2067` within the verified divisor (per §0.2), human play-and-confirm (Lesson O — event captures alone do not prove visual fidelity).

## 9. Pipeline-hardening compliance
- Lesson AK: node re-pull at step 0 (§0.1), divisor verified against live prefab (§0.2), token table labeled reconcile-against-node (§4.3), vestigial `📍` flagged (§0.3).
- Clone gate: source + reused-child GUIDs cited; no from-scratch GameObjects (§4.1).
- Lesson O: acceptance #7 requires human visual confirm, not just dispatch captures.
- Synthetic-button / fabricated-claim auto-fail rules apply (CLAIM must be the real cloned Main Buttons instance reading a live sprite).

---

## Kickoff
```
Use the implementer subagent on "tournament_result_modal"
```
