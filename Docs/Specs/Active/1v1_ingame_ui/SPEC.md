# SPEC — `1v1_ingame_ui` (Phase 1: UI)

**Tier:** FULL PIPELINE (visual fidelity + new scene structure + new data layer)
**Notion:** Order 343
**Status:** SPEC_READY — see `STATUS.md`
**Handoff file:** `Docs/Specs/Active/1v1_ingame_ui/SPEC.md`
**Authored:** 2026-06-08 15:33 JST (Architect)

> Scope note: This is **Phase 1 — the UI only.** The bot AI, turn-flow state machine, and win/tie + winner banner are **Phase 2** (see "Out of scope"). Phase 1 ships an intermediate, verifiable state: the two-player HUD + a versus data layer, with the active-player toggle and banner driven by a debug control until Phase 2's flow drives them.

---

## Goal

In **1v1 mode only**, the in-game HUD shows two player cards — **human top-left** (active-styled), **opponent top-right** (mirrored) — where the **active** player's card is fully opaque and the **inactive** is at 0.50; the solo **mini-map relocates** from top-right to lower-right beside the bottom action buttons; and a **turn-announcement banner** (styled on the surface banner) plays between turns. **Solo / Practice HUD behavior is unchanged.**

---

## Hard constraints (Cesar — non-negotiable)

1. **CLONE-AND-FLIP for P2.** The Player 2 card MUST be a **clone** of the existing Player 1 player-card GameObject (`PlayerCardWidget`, script GUID `c9b16932b3e429543aa96a954ce0ccbf`, in `LabScaffold.unity`), then mirrored (portrait to the right). **Do NOT build the P2 data card from scratch.** (Step-0 clone gate; violation = automatic reject.)
2. **1v1-ONLY.** Every change is gated behind a versus flag. The solo/Practice in-game HUD must be unchanged: P2 card inactive, banner hidden, mini-map at its current top-right position, P1 card reads `PlayerContext` exactly as today.

---

## Reference

- **Figma 1v1 HUD frame:** "In-Game - 1v1" / id `13177:1937` · file `5gEAHjl6xAtW8iYY7NMvWd`
- **Player blocks (token source):** `13177:1943` (First Row). Active block = `13177:1944` ("Cards - In-Game"); inactive block = `13177:1947` ("Left").
- **Turn banner model:** "In-game Banners" / id `4094:26052` (band sub-nodes `4094:25986` / `25990`).
- **Reference PNGs:** drop `screenshots/ingame_1v1.png` + `screenshots/turn_banner.png` before review.

### Extracted tokens (LITERALS — write these in; do NOT pull from Figma live)

**Player block (both cards share):**
- Portrait ("In-game Portrait"): **180×180**, cornerRadius **8**, stroke `#F3ECC2` ("In-Game Button Stroke"). Holds Rarity Background + Character image (inset −1px), overflow-clip.
- Parameters: vertical stack, gap **8**, 3 chip rows.
- Chip (Figma frame name "Strenght" is vestigial): **298×48**, bg `#001E39` ("Game_Dark_Blue"), padding-x **10**. Text **Rubik Medium 33px**, white `#FFFFFF`, lineHeight **39**, letterSpacing **0.18**, right-aligned ("EN/Caption_2_Medium").
- **Active card (P1, left):** portrait LEFT → parameters. Content opacity **1.0**.
- **Inactive card (P2, right):** parameters → portrait RIGHT (mirror). Content opacity **0.50**.
- Both wrappers: backdrop blur 2px. NOTE: URP UI backdrop-blur is non-trivial; if not cheaply available, **skip the blur, match opacity only**, and flag in report.
- Top-row container **1074** wide, `justify-between` (P1 flush left, P2 flush right), at frame (48,158).
- Chip order top→bottom: **Name** / `Lv {level}` / `TURN {n}`.

**Turn banner band:**
- Full width **1170 × 210h**, positioned **~664px from top** of the 2532 canvas (center-upper).
- Fill: vertical gradient `rgba(19,52,83,0.5)` (top) → `rgba(9,27,51,0.5)` (bottom) — translucent; gameplay shows through.
- Borders: top + bottom **3px** solid `#818EA1`.
- Text: **Rubik Medium 128px**, white, centered, letterSpacing **−2.56px**.

---

## Architecture context

- **HUD scene** (additive gameplay host): `Assets/Scenes/Physics/LabScaffold.unity`. Loaded by `GameplaySceneLoader` (`GAMEPLAY_SCENE_NAME`) alongside `Hole_{NN}_Geo.unity`.
- **Player card:** `Scripts/Gameplay/UI/ShotUI/PlayerCardWidget.cs` — binds static `PlayerContext` + `GameSession.TurnCount`. Namespace `Golfin.Gameplay.UI.ShotUI`.
- **Human card data source:** `Scripts/UI/HUD/PlayerContextPopulator.cs` — sets `PlayerContext.DisplayName/Level/Portrait/RarityBackground` from the selected character's `CharacterDataRuntime` + `PlayerCharacterData.currentLevel`.
- **Session:** `Scripts/Gameplay/Loop/Session/GameSession.cs` — static; single-player today. Add the versus flag here.
- **1v1 launch route** (set flag TRUE here): `Scripts/UI/ModeSelect/ModeSelectScreenController.cs` ~L161 `case "matchmaking_1v1"` AND `Scripts/UI/ModeSelect/ModeCarouselController.cs` ~L479 `case "matchmaking_1v1"` → just before `MatchmakingModalController.Open(randomHole)`.
- **Solo route** (set flag FALSE here): `Scripts/UI/HoleSelection/HoleSelectionScreenController.cs` `HandleActionClicked` (Practice direct seed). Also clear in `GameSession.ResetSession()`.
- **Opponent identity capture:** `Scripts/UI/Matchmaking/MatchmakingModalController.cs` — opponent chosen from `_opponentPool` (`List<CharacterDataRuntime>`), frozen at "OPPONENT FOUND" (~L370). Populate P2 data here.
- **Reusable overlay infra:** `FadeController` (`Scripts/UI/FadeController.cs`), `ModalController`, `ToastController` — for the banner fade.

---

## Implementation

### 1. Versus flag (gates everything)
`GameSession.cs`: add `public static bool IsVersus;`. Clear to `false` in `ResetSession()`. (Static survives the additive scene load.)
- **1v1 routes:** set `GameSession.IsVersus = true;` immediately before `MatchmakingModalController.Open(...)` in BOTH ModeSelect handlers.
- **Solo route:** set `GameSession.IsVersus = false;` on the Practice direct-seed path in `HoleSelectionScreenController.HandleActionClicked`.

### 2. `MatchContext` (new, 1v1-only data model)
New `Scripts/Gameplay/UI/ShotUI/HUD/MatchContext.cs` (namespace `Golfin.Gameplay.UI.HUD`) — static, two slots, mirrors `PlayerContext` shape:
```csharp
public static class MatchContext {
    public struct Player { public string DisplayName; public int Level; public Sprite Portrait; public Sprite RarityBackground; public int TurnCount; }
    public static readonly Player[] Players = new Player[2];
    public static int ActiveIndex = 0;            // 0 = human (left), 1 = opponent (right)
    public static event System.Action OnChanged;       // data changed
    public static event System.Action OnActiveChanged; // active player switched
    public static void SetActive(int i){ ActiveIndex = i; OnActiveChanged?.Invoke(); }
    public static void Raise() => OnChanged?.Invoke();
    public static void Reset(){ Players[0] = default; Players[1] = default; ActiveIndex = 0; OnChanged?.Invoke(); OnActiveChanged?.Invoke(); }
}
```
- **Slot 0 (human):** populate wherever `PlayerContext` is populated (`PlayerContextPopulator`). When `IsVersus`, mirror the SAME character data into `MatchContext.Players[0]`.
- **Slot 1 (opponent):** populate at "OPPONENT FOUND" in `MatchmakingModalController` from the chosen `CharacterDataRuntime`: DisplayName (`ToUpperInvariant`), Portrait (in-game portrait sprite), RarityBackground (same rarity-path lookup `PlayerContextPopulator` uses).
  - NOTE: opponent **Level/TurnCount** — Level's real source is the bot level (Phase 2). For Phase 1 display use the opponent character's available level or `1` and **FLAG it** in the report. TurnCount starts at 1.

### 3. `PlayerCardWidget` — additive index + opacity (NO rebuild)
Extend `PlayerCardWidget` (keep solo path byte-identical):
- Add `[SerializeField] int _playerIndex = 0;` and `[SerializeField] CanvasGroup _canvasGroup;` (CanvasGroup on the card root).
- `Refresh()`:
  - If `!GameSession.IsVersus`: **existing behavior verbatim** (`PlayerContext` + `TURN {GameSession.TurnCount}`, alpha 1).
  - If versus: read `MatchContext.Players[_playerIndex]` for name/level/portrait/rarity; turn text `TURN {Players[_playerIndex].TurnCount}`; `_canvasGroup.alpha = (MatchContext.ActiveIndex == _playerIndex) ? 1f : 0.5f`.
- `OnEnable`/`OnDisable`: also subscribe/unsub `MatchContext.OnChanged` + `MatchContext.OnActiveChanged` (guarded so the solo path doesn't depend on them).

### 4. P2 card — clone & flip (in `LabScaffold.unity`)
- **Clone** the existing P1 player-card GameObject (the one carrying `PlayerCardWidget`) → `PlayerCard_P2`.
- Set cloned `_playerIndex = 1`. Mirror layout to Figma `13177:1947`: parameters on the left, **portrait on the right edge** (width 515, flush right in the 1074 row). Chips stay right-aligned.
- Default `SetActive(false)` — the controller (step 6) activates it only in versus.
- Re-point the cloned `_canvasGroup`, `_portrait`, `_rarityBackground`, `_nameText/_levelText/_turnText` to the cloned children.

### 5. `TurnBannerWidget` (new, 1v1-only)
New `Scripts/Gameplay/UI/ShotUI/TurnBannerWidget.cs` + a banner GameObject in `LabScaffold.unity` HUD (built to the band tokens above; net-new, no Unity source to clone → reproduce tokens exactly; human LOOK pass verifies fidelity).
- Root: full-width band **1170×210**, gradient image (2-stop vertical, alphas 0.5), top+bottom 3px `#818EA1` borders, centered TMP Rubik Medium 128px white. `CanvasGroup` for fade. Starts hidden (alpha 0, inactive).
- API: `public void Show(string text)` → set text; animate **in** (slide from edge + fade 0→1, ~0.25s), **hold** ~1.2s, animate **out** (fade 1→0 + slide). Coroutine-driven; all timings serialized fields.
- Phase 1: the debug control (step 6) calls `Show("PLAYER 1'S TURN")` etc. so style + animation are verifiable.
- NOTE: final easing/timing approved by Cesar **live in Unity** — do NOT record UI animations (Game-View recording resizes the view and breaks layout).

### 6. `VersusHudController` (new, 1v1-only orchestrator) + mini-map reposition
New `VersusHudController` MonoBehaviour on the HUD root in `LabScaffold.unity`. `Start()`:
- If `!GameSession.IsVersus`: ensure P2 card inactive, banner hidden, mini-map at its DEFAULT (solo) anchoredPosition. Return — **solo untouched.**
- If versus: `SetActive(true)` the P2 card; move the mini-map RectTransform to the 1v1 anchoredPosition (lower-right, beside bottom buttons, per Figma); ensure banner present.
- Serialized fields: `RectTransform _miniMap; Vector2 _miniMapVersusPos; GameObject _p2Card; TurnBannerWidget _banner;`. Capture the solo mini-map pos as the scene default (read at Start before moving).
  - NOTE: the mini-map is the **same widget currently anchored top-right in solo play**, relocated (Cesar). Confirm the GameObject in `LabScaffold.unity` before moving.
- **DEBUG (Phase 1 only, inspector bool / `#if UNITY_EDITOR`):** controls to toggle `MatchContext.SetActive(0/1)` and fire `_banner.Show(...)`, so layout + translucency + banner are verifiable without the turn-flow. Guard/remove before ship.

---

## Out of scope (Phase 2 — do NOT build now)

- Bot AI / opponent shot-playing (difficulty, level→error band, club choice).
- Turn-flow state machine (who shoots when, control lock on the inactive player, camera/ball ownership).
- Win/tie resolution + winner banner.
- Driving the real active-player toggle + per-turn banner from gameplay (Phase 1 uses the debug control).
- Any change to the solo/Practice HUD.

---

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] 1v1 HUD matches Figma `13177:1937`: P1 card top-left (portrait left), P2 card top-right (portrait right, mirrored), tokens per spec.
- [ ] Inactive player's card renders at **0.50** opacity, active at **1.0**; `MatchContext.SetActive` swaps them.
- [ ] P2 card is a **CLONE** of the P1 card GameObject (cite source GUID `c9b16932b3e429543aa96a954ce0ccbf` + cloned object name) — not built from scratch.
- [ ] Mini-map sits lower-right by the bottom buttons in versus; unchanged (top-right) in solo.
- [ ] Turn banner matches band tokens; animates in / hold / out on `Show()`.
- [ ] **SOLO regression:** launch Practice → in-game HUD identical to current (P2 inactive, no banner, mini-map top-right, single card reads `PlayerContext`).
- [ ] `IsVersus` true only on the 1v1 route; false on Practice + `ResetSession`.
- [ ] No white-box placeholders; all `[SerializeField]`s wired; no console errors related to this task.
- [ ] Spec deviations (if any) flagged at the bottom of the report with justification.

---

## Smoke evidence

Visual fidelity (Lesson O): **human play-and-confirm required.** Bot/screen videos at full **1170×2532** (`feedback_record_bot_video_full_size`). Capture: (a) 1v1 launch showing both cards + active/inactive opacity, (b) debug-toggle swap, (c) banner `Show` animation, (d) Practice launch proving solo HUD unchanged. EditMode test: assert `PlayerCardWidget` solo path unchanged when `IsVersus=false` (alpha 1, `PlayerContext`-bound) and versus path reads `MatchContext`.

**Step-0 clone gate:** `IMPLEMENTER_REPORT.md` MUST cite the P1 card source GUID + the cloned P2 GameObject name. Missing citation = automatic reject.
