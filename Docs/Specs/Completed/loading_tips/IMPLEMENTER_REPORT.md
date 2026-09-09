# IMPLEMENTER_REPORT — `loading_tips`

**Iteration shape:** `loading_screen_tips:catalog-and-art`
**Iteration:** iter-1 (single pass; one Cesar interrupt handled in-flight, §Rejection follow-up)
**Canonical screenshot:** `screenshots/loading_tip_view_en_1170x2532.png` (1170×2532, real hole-load path)
**Baseline:** `HEARTBEAT.log` — HEAD `544758d5b`, working tree clean at kickoff.

---

## What was built

`ProTipCard` no longer owns the tip list. It reads `Assets/Resources/Data/LoadingTips.csv`
(34 rows), hands them to a pure `LoadingTipSequencer`, and shows whatever that returns.
The two-pool rule (§2.1) — first pool in fixed order twice, then a random draw from the
general pool that excludes the last five keys — lives in one testable class with an
injectable RNG. Position survives a quit in `PlayerPrefs` (`loadingtips.state`).

All 34 tip diagrams are exported from Figma's `Authored — OFFICIAL` section; the eight
2025 `Tip *.png` are gone. Strings are rewritten EN+JA and published (`texts` v50).
`§3.3a` polish is on the card: cross-fade, eased height, press feedback, arrival rise and
a looping "TAP FOR NEXT TIP" pulse — all through the shared `UiMotion` atoms.

---

## Files modified or created

| File | 1-line summary |
|---|---|
| `Assets/Resources/Data/LoadingTips.csv` (new) | 34-row catalog: key, pool, order, sprite, active — 8 `first`, 2 `active=0`. |
| `Assets/Scripts/UI/LoadingTipCatalog.cs` (new) | Parses the CSV to `LoadingTip[]`; `#` comments skipped, a malformed row warns once and is dropped. |
| `Assets/Scripts/UI/LoadingTipSequencer.cs` (new) | The pool rules: first pool ×2 in `order`, then a uniform draw minus the 5-key recent ring. |
| `Assets/Scripts/UI/LoadingTipStore.cs` (new) | `PlayerPrefs` JSON round-trip of `{firstPass, firstIndex, recentKeys}`; corrupt → fresh install. |
| `Assets/Scripts/UI/ProTipCard.cs` (edit) | Bound to the sequencer; `string[] tipKeys` + index-matched `Sprite[]` → name-keyed `TipSprite[]`; `CrossfadeToTip`/`textFadeDuration` deleted; §3.3a motion. |
| `Assets/Tests/EditMode/LoadingTipSequencerTests.cs` (new) | 9 tests: the two passes, 2 000 seeded draws with no repeat inside 6, 3-row pool, inactive rows, index clamp, empty pool, ring round-trip, resumed-run. |
| `Assets/Tests/EditMode/LoadingTipCatalogTests.cs` (new) | 7 tests holding the CSV, the 34 PNGs, the texts CSV and the ShellScene wiring to each other. |
| `Assets/Scripts/UI/Polish/Tests/PressFeedbackCoverageTests.cs` (edit) | New `EnumeratedTapTargets_HavePressFeedback` — the prefab audit cannot see a scene `IPointerClickHandler`, so `ProTipCard` is listed explicitly. |
| `Assets/Localization/LocalizationText.csv` (edit) | 8 rewrites + 26 new tips (EN+JA, `<color=#EEDC9A>` spans), `TIP_TIMING` → `false`. |
| `Assets/Localization/LocalizationTextTable.asset` (edit) | Bundled floor rebuilt — 1168 rows, 37 `TIP_*` keys. |
| `Assets/Resources/Data/content_version.txt` (edit) | `texts=49` → `texts=50` after publish. |
| `Assets/Art/LoadingScreen/Tip_<NAME>.png` ×34 (new) | Authored-diagram exports, 806 px wide, transparent margins, Sprite (2D and UI), no mipmaps. |
| `Assets/Art/LoadingScreen/Tip *.png` ×8 (deleted) | The 2025 set, incl. the orphan `Tip Leaderboard.png`. |
| `Assets/Scenes/ShellScene.unity` (edit) | `ProTipCard`: `TipContent` wrapper (VLG + CanvasGroup) over TipText/TipImage, `ButtonPressFeedback`, `tipsCsv`, 34 named sprites. |

---

## Acceptance checklist

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | `LoadingTipSequencerTests` | **PASS** | 9/9. `FreshState_WalksTheFirstPoolTwice_ThenDrawsGeneral` asserts the exact 16-tip sequence; `GeneralDraws_NeverRepeatWithinSixConsecutive` runs 2 000 seeded advances asserting no key inside any 6-window; `ThreeActiveRows_StillDrawSomething` runs 200 draws on a 3-row pool (exclusion shrinks from the oldest end); `InactiveFirstPoolRow_IsNeverShown`, `PersistedIndexPastTheEnd_Clamps`, `EmptyGeneralPool_IsNullSafe`, `RecentRing_IsCappedAtFive_AndRoundTripsThroughTheStore`. Run: `RUN FINISHED passed=296 failed=0`. |
| 2 | Different tips each run (3 screens, 3 tips) + cold boot opens on `TIP_SWING` | **PASS** | Cleared `loadingtips.state`, real path Home→PLAY→hole card→ACTION. Run 1 state after the screen opened: `{"firstPass":0,"firstIndex":0,"recentKeys":["TIP_SWING"]}` — **TIP_SWING, not TIP_ACCURACY**: the double-advance guard holds. Quit, re-enter, same real path → `{"firstPass":0,"firstIndex":1,...["TIP_SWING","TIP_ACCURACY"]}` — opened on TIP_ACCURACY. A third seeded run opened on TIP_VIEW (`firstIndex 3`). Three screens, three tips. |
| 3 | "TAP FOR NEXT TIP" alpha trace | **PASS** | `screenshots/tip_swap_trace.csv`, 181 frames @ ~60 fps of a REAL `OnPointerClick`: `tapAlpha` sweeps **0.550 … 1.000** and **restarts on the tap** (snaps back to 0.550 at t=0.761). Settles at 0.550 on disable — measured live: `tapGroup=TapNextText alpha=0.550` with the card inactive. |
| 4 | `LoadingTipCatalogTests` | **PASS** | 7/7. 34 rows / 8 `first` / 2 `active=0` (`TIP_STORE`, `TIP_REPAIR`); every `sprite` == `Tip_` + key-minus-prefix; `Assets/Art/LoadingScreen/Tip_*.png` enumerates to **exactly 34** and the two lists match element-for-element; the ShellScene card's `tipSprites` has 34 named entries each pointing at the file its name claims. |
| 5 | Editor run: cold boot shows `TIP_SWING`; +3 advances → `firstIndex 3`; past 16 → general | **PASS (2 of 3 directly, 1 by test)** | Cold boot → `TIP_SWING` and `firstIndex 0` (row 2). Auto-cycle advanced live to `firstIndex 3` = `TIP_VIEW` (quoted in-flight), then wrapped into pass 2 (`{"firstPass":1,"firstIndex":3,...}`) — the first pool walked a second time on screen. The pass-2→general transition at advance 16 is covered by test, not by a 2-minute live hold. |
| 6 | Hole load opens on the NEXT tip from the persisted position | **PASS** | This IS the path used throughout — `GameplaySceneLoader` via the real `HoleCard` ACTION button. Seeded `{firstPass:0,firstIndex:2,recentKeys:["TIP_GRADES"]}` → the screen opened on `firstIndex 3` / `TIP_VIEW`, i.e. one step on, not tip 1 and not the previous tip. |
| 7 | JA renders, no raw keys | **PASS** | `screenshots/loading_tip_view_ja_1170x2532.png` — header `プロのヒント`, tip `ホールカードのマップをタップして…`, footer `タップして次へ`, bar `読み込み中`. Runtime read confirms `lang=Japanese`. |
| 8 | `export_content.py --check` clean; zero new hardcoded `.text` literals | **PASS** | Plan `texts 28 add / 7 change / 1133 same / 0 conflict` → `--apply` → `content_publish` → **texts v50** → `--check: clean — no file would change, no catalog has drifted`. `ProTipCard` writes `.text` in exactly one place, the pre-existing fallback when the object has no `LocalizedText`; every real string goes through `loc.SetKey(key)`. |
| 9 | `TIP_TIMING` false; 8 old PNGs gone; no scene ref to their GUIDs | **PASS** | `RetiredTipTiming_IsInactiveInTexts_NotDeleted` asserts the row exists and ends `,false`. `OldTipArt_IsGone` asserts no `Tip *.png` besides the card background. `grep 02879563e1f454841bb4c79963031842 Assets/Scenes/ShellScene.unity` → no match (all 7 old sprite GUIDs dropped). |
| 10 | §3.3a: fade text+image, eased height, `ButtonPressFeedback`, `Rise`, `CrossfadeToTip` gone, coverage test | **PASS** | `tip_swap_trace.csv`: `contentAlpha` 1.000→0.000 over 0.15 s (`UiMotion.FadeDur`), sprite rebinds at the trough, 0.298→1.000 back in — one `CanvasGroup` on `TipContent` covering **both** TipText and TipImage. `cardScaleY` 1.000→**0.950**→1.000 over 0.12 s, **never above 1.0**. Height eased **1077.5 → 937.5 px over 0.24 s** (`UiMotion.EntryDur`), monotone. `Rise` armed on enable (`_rise` handle observed). `CrossfadeToTip`, `textFadeDuration`, `_tipTextCanvasGroup` all deleted. `EnumeratedTapTargets_HavePressFeedback` passes. |
| 11 | Console clean on the loading screen, both targets | **PARTIAL** | `HoleLoad` exercised repeatedly with zero errors in the console (`console-get-logs` Error count 0 across the session; compile clean). `LegacyBootHome` was **not** reached: on this machine, with `DevAutoSignIn` already authenticated, boot goes `Logo → Splash → Home` and never shows the Loading screen. Flagged below rather than claimed. |
| 12 | Spec deviations flagged | **PASS** | § Deviations below. |

---

## Rejection follow-up (Cesar, in-flight 2026-09-09)

> *"The outline around the image is wrong (thick on the sides, thin on top and bottom)"*

**Verdict: RESOLVED.** Same-angle A/B at full res: `screenshots/tip_image_outline_before_after.png`.

- **Reproduced numerically first, not by eye.** Every one of the 34 exports carried an
  **opaque** slate band on the left and right (10 px on 28 of them, 11/19/27 px on three)
  and **0 px** top and bottom. Sampled per file, not spot-checked.
- **Root cause:** `download_assets` renders a node **as it sits on the canvas**. The
  `Authored diagram` frame has no fill of its own and its content `Frame` is inset
  (`x=10, w=786`), so the export baked the tip card's own frosted `Pop-up` panel into
  those gutters. Not a Figma authoring bug, not a Unity layout bug — my export call.
- **Fix:** re-exported all 34 through `get_screenshot` with `contentsOnly: true`
  (the spec already said "transparent background" — I had not honoured it). Verified per
  file: left/right gutters now `alpha 0`, opaque pixel count 200k–486k, all still 806 px wide.
  No PNG was hand-edited and nothing in the Figma file was changed.
- **Same-angle re-shoot:** the canonical screenshot was re-taken through the same real
  hole-load path at the same tip (`TIP_VIEW`) — the band is gone.

---

## UI fidelity

| Element | Node | Built | Verdict |
|---|---|---|---|
| Tip diagram, all 34 | `Authored diagram` frames, §2.2 | 806 px wide at 1×, per-tip heights 260–640 px, transparent margins | PASS — width exact on all 34; contact sheet `screenshots/all_34_tip_diagrams.png` |
| Image slot geometry | Figma `Image` x=86 w=806 in a 978 card | Unity: 978 card, VLG padding 48 → 882 content, sprite letterboxed to 806 → **effective margin 86** | PASS — matches the node exactly |
| Card height | fitter-driven | 341.54 px empty → 1018.9 (`Tip_SWING` 560) → 1097.5 (`Tip_ACCURACY` 580) → 937.5 (`Tip_FORECAST` 420) | PASS — tracks each diagram |
| `TipContent` wrapper | n/a (implementation detail) | card prefH **341.54 before == 341.54 after** introducing it; TipContent prefH 82.66 = 58.66 + 24 gap + 0 | PASS — layout-neutral |
| Header / tip / footer | `PRO TIP` / tip copy / `TAP FOR NEXT TIP` | `TIP_HEADER` / `TIP_*` / `TIP_NEXT`, `#EEDC9A` spans rendering | PASS — EN and JA screenshots |

---

## Deviations from the spec (flagged, not silently taken)

1. **`ButtonPressFeedback` path.** Spec says `Assets/Scripts/UI/Polish/ButtonPressFeedback.cs`;
   it actually lives at `Assets/Scripts/UI/ButtonPressFeedback.cs` (namespace `Golfin.UI.Polish`
   as specced). No change made.
2. **`PressFeedbackCoverageTests` is prefab-only.** `PressFeedbackScope.Roots` walks
   `Assets/Prefabs/UI` and `Assets/Resources/Prefabs` and counts `Button`s. `ProTipCard` is a
   **scene** object and an `IPointerClickHandler`, so it is structurally invisible to that audit.
   Added a second test with an explicit `TapTargets` list (scene path + object name) rather than
   widening the audit to every `IPointerClickHandler` — most of those are scroll views and drag
   routers where a press pulse would be wrong.
3. **CanvasGroup placement.** Spec offered "wrapper, or one on each of the two". Took the
   **wrapper** (`TipContent`), so the swap is literally one `UiMotion.Fade` on one group. Cost:
   one new scene GameObject; proven layout-neutral (table above).
4. **Highlight spans in Japanese.** §2.2's JA column carries `[..]` markers and §3.5 says
   "`<color=#EEDC9A>` around every `[..]` span" without qualifying the language, so JA got the
   spans too. Existing JA rows have none — this is a deliberate reading, easy to reverse.
5. **Tests reach the tip types by reflection.** `LoadingTipSequencer` et al. are Assembly-CSharp
   per spec, and a named test assembly cannot reference a predefined one. Used the project's own
   idiom (`Probe` in `UiMotionTests`) via a `Tips` shim.
6. **`minLoadingTime` widened at runtime for capture.** The `HoleLoad` screen closes as soon as
   the scene is ready. Raised the live controller's `minLoadingTime` (a runtime field write, no
   scene change, reset by exiting play) purely to hold the REAL screen still. The screen, its
   navigation and its content are the real ones.
7. **ShellScene save churn isolated.** Saving the scene rewrote **158 unrelated RectTransforms**
   to zeroed anchors (the known `project_scene_save_bakes_layout_churn` scar). Spliced only the
   five modified + five new YAML documents onto HEAD's file and restored the four layout-driven
   rects to values Unity itself recomputed. Final diff: **13 hunks, +168/−22**, all inside the
   `ProTipCard` subtree.

## Fix 2 (Cesar, 2026-09-09) — `Tip_LEVELUP` cost corrected

> *"Fix 2. Anything needed on the admin side?"*

**Verdict: RESOLVED. Nothing is needed on the admin side.**

**The number.** `CharacterLevelUpDatabase.GetLevelUpCost(toLevel)` is documented and used as *the
cost to level up TO a level* — every call site passes `nextLevel` (`CharacterManager.cs:700`,
`:734`, `LevelUpModalController.cs:302`, `:440`). So `Lv 80 → Lv 81` costs the **level 81** row of
`Assets/Data/LevelUpCosts.csv`, which is `cost_r 41`, not the 805 the panel drew. `+1 SP` was
already right (`sp_reward` 1). Corrected on the Figma text node `14204:33499` (Rubik SemiBold,
`WIDTH_AND_HEIGHT` auto-resize, so the box shrank 142 → 108 px and the left-aligned line stayed at
x=48), then re-exported through `get_screenshot` `contentsOnly: true` like the other 33.

A/B: `screenshots/tip_levelup_cost_before_after.png`.

**Verified after re-import** — the asset, not just the file: `Tip_LEVELUP` imports at 806×480 with
its **GUID unchanged** (`d5d2fde29c38419199ac23245a4c05d6`), so no scene reference moved; card slot
17 (`name = Tip_LEVELUP`) still resolves to that exact object; and reading the pixels back off the
imported texture, the strip the old `805 RP` tail occupied (x 160–200 on the cost line) is now
clear. Gate re-run after the change: **296 passed / 0 failed**. I did not re-shoot an in-game frame
for this one — the change is text inside a sprite whose binding and compositing path were already
proven on screen for seven other tips; what I checked is that Unity is serving the corrected file.

**Why the admin needs nothing** (checked against `content_rows`, not assumed):

| Thing | State | Action |
|---|---|---|
| `TIP_LEVELUP` string | Unchanged by this fix — the tip copy quotes no number | none |
| `TIP_TIMING` | Already `is_active=false`, `min_build=0` **server-side** — the importer carried the CSV's fourth column through the publish | none — §2.4's "Cesar deactivates the row in the admin" is already done |
| `TIP_STORE`, `TIP_REPAIR` | `is_active=true` in `texts`, correctly: the *strings* should exist. What is gated off is the **tip row** (`active=0` in `LoadingTips.csv`), which is bundled and client-only | none — flip the CSV when those systems ship |
| The diagram PNG | A bundled sprite, not a content catalog | none |
| `texts` catalog | Published at **v50**, `export_content.py --check` clean | none |

The only admin-side work this task ever needed was the `texts` publish, and that is done.

## Daily-report video, and the defect recording it found

`videos/loading_tips.mp4` — 63 s, 1170x2532, 30 fps. Also copied to
`Docs/Reports/Media/loading_tips/`. Built by a new `LoadingTipsDemoRecorder`
(`Assets/Scripts/UI/Editor/`), modelled on `LoanDemoRecorder`: Unity Recorder, **Game View** source
(a camera source drops the Overlay HUD under URP), nothing touching `CaptureCore` while it runs (a
backbuffer read mid-recording flips Recorder frames on Metal), and the caption sidecar
`build_bot_video.py --mode captionsjson` burns off timestamps the run itself recorded.

Every tap is a real widget: the Splash `StartButton`, the Home mode card's `PlayButton`, the hole
card's `ActionButton`, and `ProTipCard.OnPointerClick` — which IS the card's handler, since it is an
`IPointerClickHandler` and not a `Button`. Two runtime instruments, both disclosed in the file's
header and both dead on play-mode exit: `minLoadingTime` widened so the real loading screen stays
up long enough to read, and `autoCycleInterval` widened so the TAP is the only thing that advances.

### The defect: every tap advanced the sequencer TWICE

The first take's per-beat log read `SWING, GRADES, CLUB, RARITIES` — tips **1, 3, 5, 7**. Tips 2, 4,
6 and 8 were unreachable by tapping.

`NextTip` was `UiMotion.Run(ref _swap, Then(fadeOut, () => { SwapTo(_seq.Advance()); Run(ref _swap,
fadeIn); }))`. `Then` runs its tail in **two** places — at the routine's end, and again from the
finalizer it registers, because an interrupted sequence still owes its tail. Re-entering `Run` on
the **same handle** from inside that tail settles the routine currently running it; the settle fires
the finalizer; the finalizer runs the tail again. It is the same shape as the self-re-arming `Then`
tail that took the Editor down in `DailyMissionPillController.StartGlow`, and that file's own note
is what named it.

**Nothing I had measured could see it.** The alpha trace, the height trace and the press-feedback
trace were all correct — both advances land in the same frame, so only the second is ever drawn.
The stills were correct. What exposed it was asking the clip to show the tutorial *in order* and
logging which tip was actually on screen at each beat.

Fixed by making the swap ONE routine that yields the two `UiMotion.Fade` sweeps in sequence
(`SwapRoutine`), the idiom `TapPulseLoop` and `DailyMissionPillController.GlowLoop` already use.
That routine registers no finalizer, which exposed a second, quieter bug: `UiMotion.Run` settles a
routine's registered final state when motion is off or outside play mode, so with the kill switch on
the swap would have done **nothing at all** rather than being instant. `NextTip` now takes its
instant path when `!UiMotion.Enabled || !Application.isPlaying`.

**Evidence after the fix** — the recorder logs the tip at every beat, and the run behind the shipped
clip reads: `SWING, ACCURACY, GRADES, VIEW, CLUB, FORECAST, RARITIES, CONTROLS`, then pass 2 opens
on `SWING` again in Japanese. That is §2.1's first pool, in `order`, walked twice. Gates re-run
after the fix: `GolfinRedux.Tests.EditMode` **296 / 0**, `Golfin.UI.Polish.Tests` **162 / 0**.

**The regression guard here is the recorder's beat log, not a unit test** — stated plainly because
it is weaker than a test: it only runs when someone records a clip. A play-mode test asserting
"one tap, one advance" is the right follow-up.

### Three caption defects, all caught by looking at decoded frames

1. `build_bot_video.py --title` **defaults to a stale `"Loop v2 — Stage F / Button Press Feedback"`**,
   which the first build burned over the logo as a centred title card. Now passed explicitly; the
   recorder no longer writes a title caption of its own (the script prepends one).
2. Captions landed **on top of the `NOW LOADING` label**. Added `--caption-y-offset` to
   `build_bot_video.py` (default 0 — existing clips unchanged) and passed 300.
3. Captions **clipped at both frame edges**: the default caption size is `h // 32` = **79 px**, which
   overflows 1170 px at ~33 characters. Built at `--caption-fontsize 54 --caption-wrap 34`, and the
   one 37-character line was shortened in the recorder so the sidecar and the source agree.

A fourth was a content defect, not a rendering one: with one caption spanning two beats, "Six shot
grades now, not three" sat over the *map* tip for half its window. Every beat now carries its own
caption, so a caption can never outlive the frame it describes.

## Findings for the Architect (not fixed here — out of scope)

- ~~`Tip_LEVELUP` diagram quotes a wrong number.~~ **FIXED** — see § Fix 2 below.
- **`LegacyBootHome` loading screen appears unreachable on a signed-in dev boot** (item 11).
  Worth confirming whether a real first-run player still sees it.
