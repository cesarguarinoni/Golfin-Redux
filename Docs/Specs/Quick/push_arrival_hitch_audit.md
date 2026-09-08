# `push_arrival_hitch` — the P0 pair × order audit, and what is still owed

Written 2026-09-08 alongside the implementation. **Everything here is derived from the repo, with
the Unity Editor untouched** (another session owns it): the sibling order comes from
`Assets/Scenes/ShellScene.unity`, the backdrop identity from each screen's own chrome `Image`,
and the pushable set from `LayeredPush.CanPush` + `ScreenManager.PillarOf` read as code.

## 1 · `ScreensRoot` order (ShellScene, depth 1)

```
 0 LogoScreen      5 InventoryScreen   10 ModeSelectionScreen        15 StaminaShopDetailScreen
 1 SplashScreen    6 RankingsScreen    11 TournamentHoleSelection…   16 GeneralShopScreen
 2 LoadingScreen   7 HoleSelection…    12 TournamentLeaderboard…     17 GachaHistoryScreen
 3 HomeScreen      8 MissionSelection… 13 TournamentSelection…       18 GachaPrizesScreen
 4 RosterScreen    9 MatchMakingModal  14 StaminaShopSelection…      19+ GPS / auth screens
```

`ScreenId.Leaderboard` resolves to `RankingsScreen` (index 6) — read off
`ScreenManager._leaderboardScreen`'s serialized reference, not assumed from the name.

## 2 · Backdrop identity — the chrome sprite GUID, per screen

Read from each screen's depth-1 chrome child (`Background` / `BG`, per `LayeredPush.LayerMap`),
in the scene for scene-authored screens and in the source prefab for the five prefab instances.
This reproduces the three groups the code's own comments name, independently:

| screen | chrome child | sprite GUID | group |
|---|---|---|---|
| ModeSelection, HoleSelection, MissionSelection, TournamentHoleSelection | `Background` | `2e5476ee…` | Play |
| TournamentSelection, TournamentLeaderboard, Leaderboard | `BG` | `0d425c0a…` | Rankings |
| GeneralShop, GachaHistory, GachaPrizes | `BG` / `Background` | `5ec22d10…` | Gacha |
| Inventory | `BG` | `44d64d73…` | Inventory (no pair) |

## 3 · Every pushable ordered pair, before and after P0

40 ordered pairs are pushable. **12 of them drew the arriver UNDERNEATH the leaver's opaque
backdrop for the whole 250 ms** — the arriver is the earlier sibling and the backdrops are
identical, so `SetAsLastSibling` never ran. All 40 are on top after.

### The 12 that were occluded (the defect)

| leaver | arriver | ScreensRoot | before | after |
|---|---|---|---|---|
| ModeSelection | MissionSelection | 10 → 8 | **occluded** — Cesar's "janky, not smooth" | on top |
| GachaPrizes | GeneralShop | 18 → 16 | **occluded** — Cesar's "empty screen on the left" | on top |
| ModeSelection | HoleSelection | 10 → 7 | occluded | on top |
| MissionSelection | HoleSelection | 8 → 7 | occluded | on top |
| TournamentHoleSelection | HoleSelection | 11 → 7 | occluded | on top |
| TournamentHoleSelection | MissionSelection | 11 → 8 | occluded | on top |
| TournamentHoleSelection | ModeSelection | 11 → 10 | occluded | on top |
| TournamentLeaderboard | Leaderboard | 12 → 6 | occluded | on top |
| TournamentSelection | Leaderboard | 13 → 6 | occluded | on top |
| TournamentSelection | TournamentLeaderboard | 13 → 12 | occluded | on top |
| GachaHistory | GeneralShop | 17 → 16 | occluded | on top |
| GachaPrizes | GachaHistory | 18 → 17 | occluded | on top |

### The 28 that were already correct (listed, per PIPELINE_HARDENING §22 — a shape audit
enumerates the sites that were fine too)

Same-backdrop, arriver already the later sibling (12): HoleSelection→MissionSelection /
→ModeSelection / →TournamentHoleSelection; MissionSelection→ModeSelection /
→TournamentHoleSelection; ModeSelection→TournamentHoleSelection; Leaderboard→TournamentLeaderboard
/ →TournamentSelection; TournamentLeaderboard→TournamentSelection; GeneralShop→GachaHistory /
→GachaPrizes; GachaHistory→GachaPrizes.

Cross-backdrop, which already re-ordered because `crossFadeChrome` was true (16): every
Play↔Rankings pair in both directions (HoleSelection / MissionSelection / ModeSelection /
TournamentHoleSelection × TournamentSelection / TournamentLeaderboard).

**Why a's invariants never caught this:** `chromeAlphaMin` and `seamWorstCover` sample CanvasGroup
*alphas*, and an arriver drawn under an opaque leaver has perfectly correct alphas on every frame.
Who is on top was not a number anywhere. It is now (`arriverOnTop`, sampled from the live
transform every frame, asserted by the probe and by `LayeredPushArrivalTests`).

## 4 · Still owed — needs the Unity Editor

Blocked while another session drives it. None of these change the code; they are the evidence.

- `game_polish_a_invariants.json` regenerated (`GOLFIN ▸ Game Polish ▸ Probe — push`): `fail = 0`,
  and every record now carries `arriverOnTop`, `arriverChromeAlphaMax`, `arrivalFrameMs`,
  `maxStepFrac`, `parallaxFactor`.
- Per-frame content-X log for `ModeSelection → MissionSelection`, before/after.
- `arrivalFrameMs` before/after for the four Play-pillar screens (fix 4's number).
- Before/after 5-frame strips for `ModeSelection → MissionSelection` and `GachaPrizes → GeneralShop`.
- The A/B parallax clip (0.3 vs 1.0) for Cesar to overrule; 1.0 ships unless he does.
- P1 evidence: `Rebind x10` with a different first prize than the previous pull; `ShowPrizes`
  logging `instant`; the Prizes arrival under the modal fade.
- Test run: `LayeredPushTests` (+ the new `LayeredPushArrivalTests`), `GpsPolishMotionTests`
  (+ `StaggerUnderPushTests`), then the full EditMode sweep.

Compile status at the time of writing: **Assembly-CSharp, Assembly-CSharp-Editor and
Golfin.UI.Polish.Tests all build clean (0 errors)**, checked with Unity's own Roslyn against the
generated `.csproj` reference sets — read-only, Editor never touched.
