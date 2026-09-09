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

> **UPDATE 2026-09-09 (`polish_regressions_0909` close-out).** The Editor is free; the probe and the
> test run are DONE and quoted below. Two capture items remain and are named at the end.

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

### 4a · DONE — the probe (`GOLFIN ▸ Game Polish ▸ Probe — push`), 2026-09-09

`Docs/Specs/Quick/media/polish_regressions_0909/push_invariants_f7800caa8.json`
(+ `push_probe_run.log`). **`measured = 87`, `fail = 0`, zero records carrying any `fails`.**
All five fields §4 asked for are present on every record:

| invariant | result across all 87 pushes | what it settles |
|---|---|---|
| `arriverOnTop` | **false on 0 records** | P0. The arriver is the last sibling on every frame of every push — the 12 occluded pairs of §3 are gone. |
| `maxStepFrac` | max **0.1333** | Fix 1's cap, exactly `MaxTweenStep / PushDur` = (1/30)/0.25. No frame advanced more than two frames' worth of travel. |
| `parallaxFactor` | **1.0 on all 55 same-backdrop pushes, 0.3 on all 32 cross-fade pushes** | Fix 3, with no pair on the wrong side of the split. |
| `arriverChromeAlphaMax` | 0 → 1, `fails = 0` | The arriver's chrome is off wherever the rule requires it (an identical backdrop underneath). |
| `arrivalFrameMs` | 9.3 – 136.6 ms; same-backdrop 9.3 – 79.9 | Fix 4's number. Worst is `TournamentLeaderboard → HoleSelection` at 136.6 ms. |

⚠️ **A stale run nearly passed as this one.** The first attempt was launched while the Editor still
held the assembly from a `git checkout 36dc3d480` made for the console sweep; it reported
`measured=87 fail=0` and looked fine, and the ONLY thing that gave it away was that its records
carried the OLD field set. `fail = 0` from a build without the fix is not evidence of the fix. The
JSON was deleted and re-run against main's assembly, and the timestamp (`utc 2026-09-09 00:59:03Z`)
belongs to that second run.

### 4b · DONE — the test run

Full EditMode sweep at `f7800caa8`: **2911 total, 2908 passed, 0 failed, 3 skipped** — including
`LayeredPushTests`, `LayeredPushArrivalTests`, `GpsPolishMotionTests` and `StaggerUnderPushTests`.

`LayeredPushArrivalTests.NoSingleFrameAdvancesMoreThanTwoFramesOfTravel` had thrown a
NullReferenceException on every run since `98e2fd3d5` — `UiMotion.PushDur` is a `const` field and the
fixture read it with `GetProperty`, which returns null — so the guard had never once run. Fixed in
`b6ef935b6`; it is green now, and `maxStepFrac ≤ 0.1333` above is the same property measured over 87
real pushes.

### 4c · DONE — the strips and the A/B parallax clip, 2026-09-09

Three recordings of the SAME two pushes at 1170×2532 / 60 fps
(`Assets/Editor/PushStripRecorder.cs`, left untracked across the checkouts so all three ran the
identical harness): `before_36dc3d480`, `after_head_parallax1`, and `ab_parallax03` — the last with
`SameBackdropParallaxFactor` temporarily edited to `0.3f`, since it is a `const` and cannot be
flipped at runtime. **The const was reverted and `git diff` on `LayeredPush.cs` is empty.**

The five frames are chosen from the MEASURED motion window, not from the sidecar timestamp: each
clip is decoded frame by frame around the mark and the window is where the frame-to-frame delta
clears a tenth of its peak. That is 11–16 frames per push at ~57 fps, which is a 250 ms tween — so
the five frames really do span the push rather than a guess at where it was.

| artifact | what it shows |
|---|---|
| `media/…/strip_modesel_missionsel.jpg` | **ModeSelection → MissionSelection, through the REAL MISSIONS card `ExpandedContainer/ActionButton`** (re-recorded 2026-09-09 — the first take drove the PRACTICE card and was mislabelled). BEFORE: the title flips to MISSIONS immediately while ModeSelection's content just drifts 0.3·W and the arriver is nowhere — it is underneath — and then the screen HARD-CUTS. That is §3's defect made visible. AFTER: both contents travel together and MissionSelection is on top for the whole slide. |
| `media/…/strip_gachaprizes_generalshop.jpg` | The same, for the pair with no player path (both ends re-seated, labelled on the strip). BEFORE: five frames of the same empty Prizes panel while the title swaps underneath. AFTER: the banner carousel slides in and settles. |
| `media/…/strip_ab_parallax.jpg` + `media/…/ab_parallax_clip.mp4` | Fix 3's A/B, 4× slow and side by side. At **0.3** the leaver lags and ModeSelection is still sitting there behind the arriving panel — two speeds over a fixed backdrop, which reads as a stutter. At **1.0** the pair moves as one rigid strip. |

Sidecars (`push_strip_*.json`) carry each push's window and the `LastPushParallaxFactor` the run
actually measured: `null` at `36dc3d480` (fix 3 did not exist yet), `0.30`, and `1.00`.

**1.0 ships unless Cesar overrules it.**

### 4d · DONE — the per-frame content-X log, and P1

**Per-frame content X** (`media/…/contentx_before_36dc3d480.tsv`, `contentx_after_head.tsv`,
written by `Assets/Editor/PushContentXLogger.cs`). One row per frame of the push, driven by the
REAL **MISSIONS** mode-card `ExpandedContainer/ActionButton`. Two things make the numbers
trustworthy, and both exist because the first version of this got them wrong:

- the card is chosen BY NAME. Taking "the first expanded ActionButton" gets PRACTICE, so the push
  was `ModeSelection → HoleSelection` while every artifact said MissionSelection;
- the arriver and leaver rects are read from `LayeredPush`'s OWN collected layers
  (`_active.To.Content[0]`), never resolved by name — so a rect that is not in the animation cannot
  be measured and reported as motionless. `PushStripRecorder` records `actualTarget` in its sidecar
  for the same reason.

Sampled from `LateUpdate`, not from the logger's coroutine: coroutines resume in START order and
this one starts first, so reading there reported every value one frame stale.

| | frames | arriver travel | leaver travel | worst single frame | `dArriver == dLeaver` |
|---|---|---|---|---|---|
| before `36dc3d480` | 11 | 962 px | **289 px** | **72.2 %** (arriver) | **no** — ratio ≈ 3.3 = 1 / 0.3 |
| after HEAD | 16 | 1170 px | **1170 px** | **38.0 %** | **YES, every frame** |

Both fixes are in that table, and the last column is the better proof of fix 3 than the A/B clip is:

**Fix 3.** Before, the leaver crawled 289 px while the arriver crossed 962 — two speeds over one
fixed backdrop, which is the stutter. After, `dArriver` and `dLeaver` are IDENTICAL on every single
frame (−38.0, −15.0, −13.1, −10.6, −8.6 …). That is what "one rigid strip" means, measured per frame.

**Fix 1.** Before, frame 1 was a 98.7 ms hitch that carried the arriver **72.2 %** of the content
width in one draw — the teleport, measured. After, the same class of hitch (101.8 ms; the arriving
screen still costs what it costs) can only spend `MaxTweenStep` = 1/30 s of the tween, which an
ease-out turns into 38 %. And frame 0 before shows the arriver already at 962 of 1170 — the build
frame had eaten 18 % of the travel before the first sampled frame; after, frame 0 reads exactly
1170.0, untouched, which is the held frame doing its job.

> **Correction of record.** An earlier revision of this section reported that the arriver's rect
> never moves in either build and filed it as a defect the invariant gate could not see. That was a
> harness bug, described above, not a product defect — the arriver moves correctly in both builds.
> The one suggestion that survives it on its own merit: an empty `p.To.Content` would pass every
> arriver assertion vacuously, because `endTargetX` and `endTargetRestX` are both 0, so a
> `p.To.Content.Count > 0` check in the probe is cheap insurance. Nothing is empty today.

**P1** (`media/…/gacha_p1_*.jpg`). Two REAL x10 pulls against the live server, the second made FROM
the Prizes screen:

```
[GachaPullService] Pulled 'banner_test_b' -> ok x10 … tickets=1690
[GachaPullFlow] Opening GachaPrizes instant (under the reveal scrim).
[GachaPrizesScreenController] Rebind x10 first=item:repairkit_common entrance=True

[GachaPullService] Pulled 'banner_test_b' -> ok x10 … tickets=1015
[GachaPrizesScreenController] Rebind x10 first=club:club_iron7_mireo entrance=True
```

The first arrival is `instant` — under the scrim, revealed by the modal's fade, which is what
`ShowPrizes`' own comment always claimed. The second pull rebinds to a DIFFERENT first prize
(`repairkit_common` → `club_iron7_mireo`, and a different ten under it), and logs NO
`Opening GachaPrizes` line and no `Already on … ignoring` — it did not navigate at all. That absence
is the fix, and it is the line to look for if this regresses. `entrance=True` on both, so the card
pop still plays, under the fade.

Compile status at the time of writing: **Assembly-CSharp, Assembly-CSharp-Editor and
Golfin.UI.Polish.Tests all build clean (0 errors)**, checked with Unity's own Roslyn against the
generated `.csproj` reference sets — read-only, Editor never touched.
