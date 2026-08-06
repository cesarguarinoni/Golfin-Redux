# SPEC — `landing_surface_banner`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

`SPEC_READY` — see `STATUS.md`.

## Goal

After every shot settles, show a full-width banner announcing where the ball landed — **FAIRWAY**, **GREEN**, **FRINGE**, **SEMI-ROUGH**, **ROUGH**, **BUNKER**, **WATER**, **OB** — in ALL CAPS (EN) / Japanese, white text. Style and animation are an exact copy of the existing 1v1 turn/result banner (`TurnBannerWidget` — which already implements Figma node `4094:26052`). Fires in solo AND in 1v1 for the human player's shots only. All strings go through `LocalizationManager` with new EN+JP rows.

Scope decisions locked by Cesar 2026-08-06:
- Outcomes: Fairway, Green, Fringe (GreenCollar), Semirough, Rough, Bunker (Sand **and** BunkerLip), Water, OB. **No banner** for Tee, CartPath, InCup (Hole Complete modal owns InCup).
- Modes: solo + 1v1 human shots. Suppressed for the bot's shots.
- Wording: golf terms (FRINGE not COLLAR; BUNKER not SAND).

## Reference

- **Figma frame:** Golfin Game Redux / node `4094:26052` in file `5gEAHjl6xAtW8iYY7NMvWd` (in-game screen with FAIRWAY banner).
- **Reference PNG:** `reference/figma_4094-26052_landing_banner_fairway.png` (pulled at spec time).
- **Placeholder vs canonical:** the Figma text "FAIRWAY" is one sample value; the banner shows the mapped surface name per the table below. Everything else in the frame (course, character, HUD) is context, not part of this task.
- **Ground truth for visuals is the scene object, not the Figma numbers.** The existing `TurnBanner` GameObject in `LabScaffold.unity` is the shipped, already-approved implementation of this exact Figma banner (see its header comment citing node 4094:26052). This task **clones it at runtime** — do NOT rebuild the banner from Figma values.

## Figma Fidelity (Rule 18)

| Element | Figma node | Property → value |
|---|---|---|
| Banner band | `4094:26052` | 1170×210 full-width; translucent navy fill (scene: rgba 0.075,0.204,0.325,0.5 + gradient children); **top+bottom 3px #818EA1 borders** (children of TurnBanner); rest Y = −664 (top-anchored) |
| Label | `4094:26052` | Rubik Medium (TMP asset guid `39fb7824ee463ab408c7f2e76c362562`), auto-size 36–128, **white #FFFFFF** (`m_fontColor {1,1,1,1}` — clone inherits it; do NOT tint), centred, 318px horizontal padding each side |
| Animation | — | identical to turn banner: slide-in 0.25s ease-out-quad → hold 1.2s → fade 0.3s (serialized on the widget; clone inherits) |
| Content | — | localized surface name, EN rows are ALL CAPS in the CSV (no runtime ToUpper — JP has no caps) |

## Architecture context

- **Asmdef boundaries:** new controller lives in **`Golfin.Physics.Viewer`** (`Assets/Scripts/Physics/Viewer/`), same as `VersusMatchController` — that assembly already references `Golfin.Gameplay.UI` (TurnBannerWidget), `Golfin.Gameplay.Loop`, and the HUD contexts. Do NOT put it in `Golfin.Gameplay.UI` (would need a back-reference to `PhysicsLabController`).
- **Existing code referenced:**
  - `TurnBannerWidget` — `Assets/Scripts/Gameplay/UI/ShotUI/TurnBannerWidget.cs`, ns `Golfin.Gameplay.UI.ShotUI`. API: `Show(string text, bool fromLeft = true)`, `ShowPersistent`, `Hide()`. Reused as-is, **zero edits**.
  - `PhysicsLabController` — `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`. Public `BallSM` property; terminal handling in `HandleShotComplete(ShotResult)` (~line 1227) — not edited; we subscribe alongside it.
  - `BallStateMachine.OnShotComplete` — `event Action<ShotResult>`; subscribe pattern copied from `VersusMatchController.Start()` (wait for `GameSession`/`BallSM` readiness, then `+=`).
  - `ShotResult` — `Assets/Scripts/Gameplay/Loop/ShotResult.cs`: `TerminalState` (`BallState`), `OBReason?`, `EndSurface` (`SurfaceType`).
  - `SurfaceType` — `Assets/Scripts/Physics/Core/SurfaceType.cs` (Fairway, Green, GreenCollar, Semirough, Rough, Tee, Sand, BunkerLip, CartPath, Water, OOB).
  - `OBReason` — Water / OutOfBounds / ExitedWorldBounds.
  - `GameSession.IsVersus`, `MatchContext.ActiveIndex` (0 = local player) — at `OnShotComplete` time `ActiveIndex` is still the shooter (turn swap happens later in `MatchFlow`), so it is the correct suppression signal.
  - `LocalizationManager.Get(key)` — static, falls back to the key string, then EN if JP cell empty.
- **Existing assets referenced:** `TurnBanner` GameObject in `Assets/Scenes/Physics/LabScaffold.unity` (fileID `1436714829`, inactive child of `ShotUI_Canvas`, widget component fileID `1436714831`).
- **Localization pipeline:** `Assets/Localization/LocalizationText.csv` (`key,English,Japanese`) → `LocalizationTextImporter` (menu `Tools/Localization/Import Text CSV`; `LocalizationPlaymodeHook` auto-imports on entering play mode).

## Surface → key → text mapping

| Trigger | Key | English | Japanese |
|---|---|---|---|
| AtRest · Fairway | `LANDING_FAIRWAY` | FAIRWAY | フェアウェイ |
| AtRest · Green | `LANDING_GREEN` | GREEN | グリーン |
| AtRest · GreenCollar | `LANDING_FRINGE` | FRINGE | カラー |
| AtRest · Semirough | `LANDING_SEMIROUGH` | SEMI-ROUGH | セミラフ |
| AtRest · Rough | `LANDING_ROUGH` | ROUGH | ラフ |
| AtRest · Sand **or** BunkerLip | `LANDING_BUNKER` | BUNKER | バンカー |
| OB · OBReason == Water | `LANDING_WATER` | WATER | ウォーター |
| OB · OutOfBounds / ExitedWorldBounds | `LANDING_OB` | OB | OB |
| AtRest · Tee / CartPath | — | (no banner, by decision) | |
| InCup | — | (no banner — Hole Complete modal owns it) | |

NOTE (JP wording, flag if Cesar disagrees): カラー is the standard JP broadcast term for the fringe/collar; alternative フリンジ. ウォーター could also be ウォーターハザード (longer; auto-size will shrink it) — カラー / ウォーター chosen for brevity.

## Implementation

### 1. Localization rows

Append to the end of `Assets/Localization/LocalizationText.csv` (after the `GAMEPLAY_*` group):

```csv
LANDING_FAIRWAY,FAIRWAY,フェアウェイ
LANDING_GREEN,GREEN,グリーン
LANDING_FRINGE,FRINGE,カラー
LANDING_SEMIROUGH,SEMI-ROUGH,セミラフ
LANDING_ROUGH,ROUGH,ラフ
LANDING_BUNKER,BUNKER,バンカー
LANDING_WATER,WATER,ウォーター
LANDING_OB,OB,OB
```

No BOM/quoting needed (no commas in values). Run `Tools/Localization/Import Text CSV` once (or just enter play mode — the hook imports).

### 2. New script — `Assets/Scripts/Physics/Viewer/LandingBannerController.cs`

Runtime-clone approach: **no duplication of the banner subtree in scene YAML**. The controller holds a serialized reference to the existing (inactive) `TurnBanner` widget and `Instantiate`s a sibling clone at startup. The clone inherits the full visual + animation + white label. This keeps the LabScaffold diff to one added component + one wired reference (scene-serialization lesson from K12/K14).

```csharp
using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// landing_surface_banner: shows a localized FAIRWAY / GREEN / FRINGE / SEMI-ROUGH /
    /// ROUGH / BUNKER / WATER / OB banner when the ball settles.
    /// Visuals/animation = runtime clone of the 1v1 TurnBanner (Figma 4094:26052), white text.
    /// Solo: every shot. Versus: human shots only (ActiveIndex==0 at OnShotComplete time).
    /// No banner for InCup, Tee, CartPath (Cesar 2026-08-06).
    /// Lives on the [Session] GameObject in LabScaffold.unity.
    /// </summary>
    public class LandingBannerController : MonoBehaviour
    {
        [Header("Required references")]
        [Tooltip("The existing (inactive) TurnBanner under ShotUI_Canvas — used as template.")]
        [SerializeField] TurnBannerWidget _templateBanner;

        TurnBannerWidget _banner;      // runtime clone
        BallStateMachine _sm;

        /// <summary>True while the landing banner is on screen (clone active).
        /// Read by VersusMatchController to sequence the AnnounceTurn banner.</summary>
        public static bool IsBannerVisible { get; private set; }

        void Update()
        {
            IsBannerVisible = _banner != null && _banner.gameObject.activeInHierarchy;
        }

        IEnumerator Start()
        {
            if (_templateBanner == null)
            {
                Debug.LogError("[LandingBanner] _templateBanner not wired — no landing banners.");
                yield break;
            }

            // Clone the turn banner (template is inactive; clone starts inactive too —
            // TurnBannerWidget.Show() handles activation + off-screen pre-positioning).
            _banner = Instantiate(_templateBanner, _templateBanner.transform.parent);
            _banner.gameObject.name = "LandingBanner";

            // Wait for the BallStateMachine, mirroring VersusMatchController.Start().
            var controller = FindObjectOfType<PhysicsLabController>();
            float waited = 0f;
            while ((controller == null || controller.BallSM == null) && waited < 15f)
            {
                if (controller == null) controller = FindObjectOfType<PhysicsLabController>();
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
            if (controller == null || controller.BallSM == null)
            {
                Debug.LogWarning("[LandingBanner] BallSM unavailable after 15s — banners disabled this session.");
                yield break;
            }

            _sm = controller.BallSM;
            _sm.OnShotComplete += HandleShotComplete;
        }

        void OnDestroy()
        {
            if (_sm != null) _sm.OnShotComplete -= HandleShotComplete;
        }

        void HandleShotComplete(ShotResult result)
        {
            // Versus: only the local player's shots (ActiveIndex still == shooter here).
            if (GameSession.IsVersus && MatchContext.ActiveIndex != 0) return;

            string key = KeyFor(result);
            if (key == null) return;

            _banner.Show(LocalizationManager.Get(key), fromLeft: true);
        }

        static string KeyFor(ShotResult r)
        {
            if (r.TerminalState == BallState.OB)
            {
                return r.OBReason == Golfin.Gameplay.Loop.OBReason.Water
                    ? "LANDING_WATER"
                    : "LANDING_OB";   // OutOfBounds + ExitedWorldBounds
            }

            if (r.TerminalState != BallState.AtRest) return null;   // InCup etc.

            switch (r.EndSurface)
            {
                case Golfin.Physics.SurfaceType.Fairway:     return "LANDING_FAIRWAY";
                case Golfin.Physics.SurfaceType.Green:       return "LANDING_GREEN";
                case Golfin.Physics.SurfaceType.GreenCollar: return "LANDING_FRINGE";
                case Golfin.Physics.SurfaceType.Semirough:   return "LANDING_SEMIROUGH";
                case Golfin.Physics.SurfaceType.Rough:       return "LANDING_ROUGH";
                case Golfin.Physics.SurfaceType.Sand:
                case Golfin.Physics.SurfaceType.BunkerLip:   return "LANDING_BUNKER";
                default:                                     return null; // Tee, CartPath — silent by decision
            }
        }
    }
}
```

**Asmdef reference (VERIFIED REQUIRED):** `LocalizationManager` is a global-namespace static class in the `Golfin.Localization` asmdef. `Golfin.Physics.Viewer.asmdef` (at `Assets/Scripts/Physics/Viewer/`) does NOT currently reference it (checked 2026-08-06; asmdef refs are not transitive through `Golfin.Gameplay.UI`, which does reference it — see `Assets/Scripts/Gameplay/UI/ShotUI/Golfin.Gameplay.UI.asmdef` line 10). Add `"Golfin.Localization"` to the `references` array of `Golfin.Physics.Viewer.asmdef`. This is the only asmdef change.

### 3. Scene wiring (LabScaffold.unity — minimal diff)

- Add `LandingBannerController` to the **`[Session]`** GameObject (the one hosting `VersusMatchController` / `HoleCompletionBridge`).
- Wire `_templateBanner` → the existing `TurnBanner` object (fileID `1436714829`).
- **Do not** touch TurnBanner itself, its children, or anything else in the scene. Expected scene diff: one new MonoBehaviour block + one m_Component entry + the reference.

### 4. Versus sequencing (VersusMatchController.cs — one bounded edit)

In 1v1 the landing banner (≈1.75s) and the next `AnnounceTurn` banner occupy the same Y slot on different GameObjects — without sequencing they overlap. In `AwaitShot()`, after the existing settle pause:

```csharp
            // Small pause so settled ball frame is visible.
            // Debug capture mode: shorten pause to save time inside the 30s window.
            yield return new WaitForSeconds(_debugBothBots ? 0.1f : 0.5f);

            // landing_surface_banner: if the landing banner is still on screen (human shot),
            // hold AnnounceTurn until it clears so the two banners never stack. Bounded 2.5s.
            float bannerWait = 0f;
            while (LandingBannerController.IsBannerVisible && bannerWait < 2.5f)
            {
                bannerWait += Time.unscaledDeltaTime;
                yield return null;
            }
```

PACING NOTE (flag, don't decide): this adds up to ~1.3s before OPPONENT'S TURN after each human shot in 1v1. If Cesar finds it slow (K12 spirit), the dial is the clone's serialized `_holdDuration` — settable on the clone right after `Instantiate` — not the turn-flow code.

DEBUG NOTE: in `_debugBothBots` capture mode P1 is a bot but `ActiveIndex==0`, so its shots will banner. Accepted — debug-only path, keeps the gate simple.

### 5. Explicitly NOT changed

- `TurnBannerWidget.cs` — zero edits.
- `PhysicsLabController.HandleShotComplete` — zero edits (we subscribe to the same event).
- Banner visuals: fill, borders, font, **white** font color, timings — all inherited from the clone.
- Hard-coded EN strings "YOUR TURN" / "OPPONENT'S TURN" / "YOU WIN" / "YOU LOSE" / "TIE" — out of scope (localization follow-up candidate, noted for the Architect).

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Fairway landing shows **FAIRWAY**, white text, slide-in/hold/fade identical to turn banner (A/B vs `reference/figma_4094-26052_landing_banner_fairway.png` + the 1v1 turn banner)
- [ ] Green → **GREEN**; collar → **FRINGE**; semirough → **SEMI-ROUGH**; rough → **ROUGH**; sand → **BUNKER** (bunker-lip settle also BUNKER if reproducible)
- [ ] Water entry → **WATER** (visible during the splash camera hold); boundary OB → **OB** (visible during the OB hold)
- [ ] Holed out (InCup) → NO landing banner, Hole Complete flow unchanged
- [ ] `LocalizationManager.SetLanguage(Japanese)` → banners show フェアウェイ / グリーン / カラー / セミラフ / ラフ / バンカー / ウォーター / OB
- [ ] 1v1: bot (P2) shots produce NO landing banner; human shots banner first, then OPPONENT'S TURN — sequential, never stacked
- [ ] Solo HUD otherwise byte-identical (TurnBanner object untouched, still inactive in solo)
- [ ] LabScaffold scene diff limited to the [Session] component addition + reference (no stale-override reconciliation swept in — check `git diff` for MatchMakingModal drift and revert it if the scene save picked it up)
- [ ] All `[SerializeField]` references wired in the Inspector
- [ ] Unity Console has no errors related to this task
- [ ] Spec deviations flagged at the bottom of the report with justification

## Files / hierarchy this task touches

- `Assets/Localization/LocalizationText.csv` — +8 rows (`LANDING_*`)
- `Assets/Localization/LocalizationTextTable.asset` — regenerated by importer (commit the regen)
- `Assets/Scripts/Physics/Viewer/LandingBannerController.cs` — NEW (+ .meta)
- `Assets/Scripts/Physics/Viewer/VersusMatchController.cs` — bounded edit in `AwaitShot()` (§4)
- `Assets/Scenes/Physics/LabScaffold.unity` — [Session] gets the component + wired reference
- `Assets/Scripts/Physics/Viewer/Golfin.Physics.Viewer.asmdef` — add `"Golfin.Localization"` to references (verified missing)

## Smoke evidence

Editor-verifiable (state-machine + UI class; no device pass required). Human-in-the-loop per Lesson O: play LabScaffold on a real hole, land shots on fairway/green/rough/bunker, dunk one in water, drive one OB; describe in `IMPLEMENTER_REPORT.md` what the banner visually did each time (slide direction, hold, fade, no font-size jump — the R2-4 ForceMeshUpdate path must still hold on the clone). Repeat one landing in JP via the localization debug window. For 1v1, run the versus flow (debug force or matchmaking path) and describe the human-shot banner → turn banner sequence. Runtime event logs alone are dispatch evidence, not visual evidence.

## Out of scope (do NOT do these)

- No banners for Tee, CartPath, InCup.
- No banner for opponent/bot shots in 1v1.
- No localization of the existing turn/result banner strings.
- No visual restyling — no new colors, fonts, sizes, or a second banner design. White text comes from the clone; do not set colors in code.
- No edits to `TurnBannerWidget.cs`, `PhysicsLabController.HandleShotComplete`, or the TurnBanner scene object.
- No new prefab for the banner (runtime clone only, per scene-serialization lessons).
