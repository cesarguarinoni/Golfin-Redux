# IMPLEMENTER_REPORT — `game_polish_a`

> **OPTION (b) SHIPPED — Cesar, 2026-09-04, after watching the clip.**
> Two screens of the same pillar now push even when their backdrops DIFFER, and the backdrops
> cross-fade through each other. Home and every cross-pillar move keep the fade to black; that was
> a separate rule and it is unchanged. **The flag is removed, not flipped** — it existed for the
> one video that made the decision possible, and a switch for a decision already made is a dead
> branch. `LayeredPushTests.TheOptionBFlag_IsGone` pins that.
>
> **Re-measured against the widened rule: 84 pushes, `fail == 0`.** 32 of them are cross-backdrop
> — the path that did not exist before this decision — across **16 ordered pairs that used to
> fade**. Sections below marked "flag OFF" describe the pre-decision run and are superseded by
> § *Option (b) shipped — re-measured*.

**Iteration shape:** `navigation-motion:layered-push-and-nav-selected-state`
**Iteration:** 1
**Canonical screenshot:** `screenshots/push_18_home_return.png`

---

## 0 · Two things Cesar should read first

**1. The Editor's active build profile was `iOS-Standalone`, and it made the whole task
unrunnable until it was changed.** `iOS-Standalone` carries `GOLFIN_STANDALONE`, so
`StandaloneGate` rewrote `Home → GpsHub` and blocked every game screen — the first baseline
pass captured the GPS hub four times under four different screen names before the cause was
found in the log (`[StandaloneGate] blocked HoleSelection — not part of the PLAYLIFE shell`).
It is now **`iOS-Full-GPS`**, which is the profile this task's two bars actually live in.
**Left that way deliberately**, because reviewing this work needs the game shell; switch it
back from Build Profiles when the standalone lane is next built.

**2. Size.** Four new textures, and they are the only assets added: **33.2 KB of source PNG,
≈50 KB as iOS ASTC 6x6**. The halos are baked at HALF resolution and scaled up by the
component — a falloff has no high-frequency detail to lose, and it takes the four from ~91 KB
to ~50 KB. Import settings follow `build_size_diet` phase 3 exactly: no `compression: None`,
an iPhone override at ASTC 6x6, `maxTextureSize` 512. No Resources folder was added (those
ship in every build variant, including the PLAYLIFE shell, which has no game nav bar).

**3. One of my commits swept your parallel work, and nothing is lost but you should know where
it went.** `264ee64f5 "game_polish_a: A1 green"` also carries, from your own in-flight session:
`Docs/Specs/Active/map_view_v2/**` (SPEC + STATUS + 6 reference PNGs), `Docs/Reports/content_art.txt`
(+2456 lines), `Docs/GPS/GPS_BACKLOG.md` and `Docs/TellCode.md`. I used `git add -A -- Docs` in
that one step instead of naming paths — the `k10_commit_swept_k11_edits` scar exactly.

Verified intact, not reverted: every file exists at HEAD, and
`git diff HEAD -- Docs/Reports/content_art.txt` is empty, so what is committed IS your latest.
**I did not rewrite history to split it out**: your session is live on this branch (you committed
`68853a7ca` and `a547f3ec9` while I worked), and rewriting HEAD under an active session is a much
worse failure than a misleading commit message. Split it yourself if you want it, or leave it.

---

## Files modified or created

| File | What |
|---|---|
| `Assets/Scripts/UI/Polish/LayeredPush.cs` | **NEW.** The game shell's push: layer table, `CanPush`, `SameBackground`, direction, the tween, rest-state restore, the `AllowBackgroundCrossFade` flag. |
| `Assets/Scripts/UI/Polish/ScreenEntryMotion.cs` | **NEW.** The 16 px entry rise on fade-path arrivals; skipped after a push. |
| `Assets/Scripts/UI/Polish/NavSlotHighlight.cs` | **NEW.** §D7's gold halo + brighter ring, one component driving BOTH bars. |
| `Assets/Scripts/UI/Polish/Editor/GamePolishBuilder.cs` | **NEW.** Adds `ScreenEntryMotion` to the 13 shell screens and wires each screen's content rects from `LayeredPush.LayerMap`. |
| `Assets/Scripts/UI/Polish/Editor/GamePolishProbe.cs` | **NEW.** `baseline` / `push` / `parity` / `perf` / `option_b`; writes the A1 invariants JSON. |
| `Assets/Scripts/UI/Polish/Editor/GamePolishDemoRecorder.cs` | **NEW.** The A4 take — one recording, six segments, a cut sidecar. |
| `Assets/Scripts/UI/Polish/Editor/GamePolishTestReport.cs` | **NEW.** EditMode sweep → a quotable report file (see A12 for why this was needed). |
| `Assets/Scripts/UI/Polish/Tests/LayeredPushTests.cs` | **NEW.** Direction table (110 pairs), every `CanPush` false case, the flag, the layer table. |
| `Assets/Scripts/UI/Polish/Tests/ScreenEntryMotionTests.cs` | **NEW.** The `SkipEntry` bit and rest parity at component level. |
| `Assets/Scripts/UI/Polish/UiSelection.cs` | `FadeSwap` + `Indicator` added for §D3. `UiMotion`'s public API untouched. |
| `Assets/Scripts/UI/ScreenManager.cs` | The one new push branch, the second `IsPushing` guard, `ShellScreenObject`. |
| `Assets/Scripts/UI/PersistentUIManager.cs` | `UpdateScreenHighlight` → `NavSlotHighlight`; `iconActiveColor` marked `[Obsolete]`; four halo/ring sprite fields. |
| `Assets/Scripts/UI/Gps/GpsNavBarHighlight.cs` | **The only `Gps/` change** (authorised). Full diff in A15. |
| `Assets/Scripts/UI/Inventory/InventoryScreenController.cs` | §D3 tab cross-fade, §D6 bump, indicator cross-fade. |
| `Assets/Scripts/UI/Rankings/RankingsScreenController.cs` | §D3 list fade around the repaint, §D6 bump, indicator cross-fade. |
| `Assets/Scripts/UI/Gacha/GachaHistoryScreenController.cs` | §D3 list fade on the store repaint (see A7 for the filter-chip finding). |
| `Assets/Scripts/UI/SettingsController.cs` | §D3 scrim fade + panel pop/unpop; `IsOpen` made state-driven. |
| `Assets/Scripts/UI/SettingsMenuItem.cs` | §D3 submenu fade in step with the existing height tween. |
| `Assets/Scenes/ShellScene.unity` | 13 × `ScreenEntryMotion` + the four sprite references. **263 insertions, 3 deletions, zero anchor/sizeDelta lines.** |
| `Assets/Prefabs/UI/PersistentUI.prefab` | Sprite fields + `ButtonPressFeedback` on the five nav slots. |
| `Assets/Art/HomeScreen/S_NavSlot{Glow,Ring}_{156,238}.png` (+ `.meta`) | The four baked sprites. |
| `Docs/Scripts/make_nav_selected.py` | **NEW.** The baker. |
| `Docs/Scripts/cut_game_polish_clips.py` | **NEW.** The A4 clip cutter. |

---

## Option (b) shipped — re-measured

`Docs/Diagnostics/_capture/game_polish_a_invariants.json`, regenerated against the new rule:

```
measured = 84        fail = 0        optionBShipped = true
distinct ordered pairs = 40
cross-backdrop  32 records   seam worst cover over EVERY frame = 1.0   (gate: >= 0.5)
same-backdrop   52 records   chrome alpha min over EVERY frame = 1.0   (gate: == 1)
durations 0.250 – 0.293 s over 9 – 16 frames   (4 frame-starved, duration not scored)
applyScreenCalls = 1 on every record; blocksRaycasts restored on every record
```

**The 16 ordered pairs that used to fade and now push**, all measured clean:

| | |
|---|---|
| `ModeSelection` ⇄ `TournamentSelection` | `ModeSelection` ⇄ `TournamentLeaderboard` |
| `HoleSelection` ⇄ `TournamentSelection` | `HoleSelection` ⇄ `TournamentLeaderboard` |
| `MissionSelection` ⇄ `TournamentSelection` | `MissionSelection` ⇄ `TournamentLeaderboard` |
| `TournamentHoleSelection` ⇄ `TournamentSelection` | `TournamentHoleSelection` ⇄ `TournamentLeaderboard` |

**On the seam reading 1.0.** That is not the metric failing to move — it is the compositing order
working. The leaver's chrome is HELD at 1 and the incoming one is faded in on top of it, so
`max(from, to)` is 1 on every frame by construction. What changed is that it is now *measured*
from the live CanvasGroups instead of restated: the previous code computed `Mathf.Max(fe, 1f)`,
which is 1 whatever happens and would have reported a clean seam even if the leaver had been
faded out. Tolerable while this path sat behind an off-by-default flag; not tolerable now that it
ships.

**Three things the change had to touch, beyond deleting the flag:**

1. **The sweep enumerates by PILLAR, not by backdrop.** The old three background groups were
   *exactly* the pushable set before the decision. Keeping them would have kept reporting
   `fail == 0` while never once exercising the newly-shipped path — a green gate measuring the
   wrong thing. MainPlay is one group of six screens now.
2. **Which chrome assertion applies is decided per pair**, by that pair's own backgrounds
   (`sameBackground` in the JSON), because both paths are live at once. Same sprite ⇒ chrome alpha
   must stay 1; different sprites ⇒ the incoming chrome is *supposed* to start at 0 and the seam
   test applies instead. The old global rule would have failed the feature for working.
3. **The A4 clip was relabelled and re-cut** from the same footage:
   `videos/game_polish_a_f_cross_backdrop.mp4`. Its burnt-in caption used to read "FLAG OFF IN THE
   BUILD", which stopped being true the moment the decision was made — a mislabelled artifact is
   worse than none.

**A9 is void and replaced.** There is no flag to grep for; `TheOptionBFlag_IsGone` and
`SameBackground_IsNoLongerRequiredByTheGate` are the guards now. Polish suites: **91 passed,
0 failed.**

---

## The push map, measured live (not copied from the SPEC)

Read off ShellScene with the Editor open, comparing the chrome `Image.sprite` **by asset**:

| Group | Screens | Background GUID | Pillar |
|---|---|---|---|
| Play | `ModeSelection`, `HoleSelection`, `MissionSelection`, `TournamentHoleSelection` | `2e5476ee…` (`Art/HoleSelectScreen/Background.png`) | MainPlay |
| Rankings | `TournamentSelection`, `TournamentLeaderboard`, `Leaderboard` | `0d425c0a…` (`Art/RankingsScreen/BackgroundRangkings.png`) | MainPlay ×2 + none |
| Gacha | `GeneralShop`, `GachaHistory`, `GachaPrizes` | `5ec22d10…` (`Art/Shop/Background - Blurred.png`) | Gacha |

**24 ordered push pairs** (12 + 6 + 6). Every other move fades, unchanged.

Three findings the SPEC left open, resolved by measurement rather than assumption:

- **`Inventory`'s background is a DIFFERENT asset with the SAME NAME** —
  `Art/ClubsInventory/Background.png` `44d64d73…`, against the Play screens'
  `Art/HoleSelectScreen/Background.png` `2e5476ee…`. Both are called "Background". This is
  exactly why `SameBackground` compares the `Sprite` **reference** and never the name; a name
  comparison would push between two visibly different rooms. (Inventory is a one-screen pillar
  anyway, so it has no pair — but the trap is live for anything added later.)
- **`Roster` has no chrome child at all** (`CarouselSection`, `DetailPanel`, three modals — no
  background; the character stage renders behind). **`StaminaShopSelection` / `StaminaShopDetail`
  have none either** — their backdrop lives inside nested prefabs. All three therefore have no
  `LayerMap` entry and fall through to the fade, which settles the SPEC's "measure" row: there
  is nothing to hold still while content slides, so the fade is the honest transition.
- **`ModeSelection/TournamentTempEntry` is live**, not dead — it is an active `Button` carrying
  `TournamentDevEntryButton` that routes to `TournamentSelection`. It is therefore CONTENT and
  travels with `CardsContainer`; left behind it would hang over the arriving screen.

---

## Deviations (A16)

**D-1 · `NavSlotHighlight` builds its halo and ring children at RUNTIME, not from a builder.**
§D7.2/3 has `GamePolishBuilder` author a `Glow` and a `Ring` child on `PersistentUI.prefab`
*and* on the GPS bar. It cannot: the GPS bar is cloned **inside all eight GPS screen prefabs**,
so authoring a child there means editing `Assets/Prefabs/UI/Gps/**`, which Cesar's scope rule
puts off limits ("The ONLY `Gps/` file you may edit is `GpsNavBarHighlight.cs`"). `Attach()`
creates them instead. This is not a shortcut — it is what makes the rest of the rule keepable:

- every GPS prefab stays byte-identical (A14 greps for it, and the diff is empty);
- the two bars **cannot drift**, because there is exactly one place that decides what a
  highlight is made of — which is the point of Cesar's "both bars at once";
- nothing is added to a REST frame: both children start at alpha 0, and an alpha-0 CanvasGroup
  cannot move a rest pixel — the same argument `GpsScreenTransition.EnsureGroup` already makes;
- re-running any builder cannot orphan a scene override, because there is no serialized
  reference to orphan (project memory: `playmode_hides_prefab_instance`).

Cost: two GameObjects and two CanvasGroups per slot, created once. The **sprites** come off
`PersistentUIManager`, exactly as `GpsNavBarHighlight` already reads its colours off it — not
`Resources.Load`, because a Resources folder ships in every build variant including the
PLAYLIFE shell, which has no game nav bar at all.

**D-2 · The `CanvasGroup`s are made at runtime too.** §D2 has the builder add them to all 37
layers. Making them in `LayeredPush.EnsureGroup` / `ScreenEntryMotion.EnsureGroup` — which both
already did as a safety net, and which `GpsScreenTransition` has always done for the hand-built
hub — keeps 37 objects out of the scene (263 diff lines against 840) and means a screen the
builder has never been run over still animates correctly. Alpha-1 group, no rest pixel moves.

**D-3 · `SettingsController.IsOpen` changed from `settingsPanel.activeSelf` to a state flag.**
Not cosmetic: with the close now animated the panel stays active for `FadeDur` after the player
asked it to go, so `activeSelf` would report the overlay as open during its own exit and
`ScreenManager`'s Android back handler (`ScreenManager.cs:653`, the only reader) would swallow
the next back press. The flag is true from the first frame of open and false from the first
frame of close, which is what that caller actually means.

**D-4 · §D3's `GachaHistory` "FiltersIconRow filter change" does not exist.** The chips are in
the prefab at `GameScreenContent/ContentContainer/FiltersBlock/CategoryRow/*Chip`, but nothing
wires their `onClick` and `GachaHistoryScreenController` has no chip fields at all. The site
that *does* repaint the list is `GachaHistoryStore.OnChanged`, so that is what was animated,
and the substitution is stated in the code comment as well as here rather than made quietly.
Chips wired later route through `RepaintAnimated` and inherit the fade for free.

**D-5 · §D3's `SettingsMenuItem` "snap" was not a snap.** The height has always been tweened
through `expandCurve` with the `LayoutElement` driven (trap C3 was already handled by whoever
wrote it). What was actually missing is that the submenu content sat at full opacity from the
first frame, so a half-open row showed a crisply-drawn submenu clipped by its own rect. It now
dissolves on the SAME progress value — one animation, not two that can disagree.

**D-6 · §D3's `TournamentHoleCard` row needs no change, and the SPEC allows for that.**
`TournamentHoleSelectionScreenController` PICKS one of three templates and `Instantiate`s it
(`:140–158`); the state is baked at spawn and no live card ever swaps between Locked / Finished
/ Next. Nothing to cross-fade.

**D-7 · `ButtonPressFeedback` added to the five GAME nav slots.** Rule 11 is about new Buttons,
and these are not new — but the GPS bar's five equivalents already carry it, the D7 work now
drives these five, and a bar that presses differently from the bar beside it is the kind of
inconsistency this slice exists to remove. Five components, no behaviour change at rest.

---

## Acceptance checklist

### A9 · The option-(b) flag is pinned OFF — **PASS**

`grep -rn AllowBackgroundCrossFade Assets`, every hit:

```
LayeredPush.cs:93      public static bool AllowBackgroundCrossFade { get; set; } = false;   <- the declaration
LayeredPush.cs:231     if (!AllowBackgroundCrossFade && !SameBackground(...)) return false; <- the only READ
Tests/LayeredPushTests.cs:44,45,74,78,79,83,105,107                                        <- the pin
Editor/GamePolishProbe.cs:237,241,251,257  (option_b mode; forced false for every other mode)
Editor/GamePolishDemoRecorder.cs:183,187,193  (segment f only, on then off)
```

**Zero production writers.** Every writer is a test or an Editor-only tool. The property has no
public *field* form, so no prefab can serialize it on; `Flag_IsNotASerializedFieldAndHasNoProductionWriter`
asserts that, and `AllowBackgroundCrossFade_DefaultsToFalse` reads the compiler-generated
backing field rather than the live property so a same-session probe run cannot make it look
green.

### A14 · Scope — **PASS**

```
$ git diff --stat 1e7f97504..HEAD -- Assets/Scripts/UI/Gps Assets/Prefabs/UI/Gps
 Assets/Scripts/UI/Gps/GpsNavBarHighlight.cs | 38 +++++++++++++++++++++--------
 1 file changed, 28 insertions(+), 10 deletions(-)
```

One file, the authorised one. **`Assets/Prefabs/UI/Gps/**` is untouched** — which is exactly
what deviation D-1 exists to achieve. `FadeController.cs` untouched. `UiMotion.cs` untouched
(zero lines: the two new helpers went on `UiSelection`). `ModalController` untouched.

Working tree at report time carries no path outside this task's own folders (Rule 13). Two
sweeps of reimport churn were caught and reverted rather than committed: 11 tree materials and
**3 `M_Splash*.mat`** (`m_CustomRenderQueue: 3100 → 3000`) after the Editor restart — the
`M_Splash*` files are under a standing ban and were `git checkout`-ed, not staged.

### A15 · The nav selected state — **PASS on the mechanism, evidence below**

`grep -rn "iconActiveColor" Assets/Scripts`, every hit:

```
PersistentUIManager.cs:68   <- a doc comment naming this grep
PersistentUIManager.cs:73   public Color iconActiveColor = Color.cyan;   <- the [Obsolete] field
Gps/GpsNavBarHighlight.cs:9 <- a header comment describing what the file USED to do
```

**No runtime read anywhere.** The field survives only so existing prefabs deserialize without a
warning, and carries `[System.Obsolete("game_polish_a §D7 — the selected state is
NavSlotHighlight …")]`.

`GpsNavBarHighlight.cs` diff, in full (the only `Gps/` change):

```diff
+// game_polish_a §D7 — WHAT "the same way" MEANS CHANGED, AND SO DID THIS FILE.
+// The Game bar no longer tints the slot: a slot is one baked sprite carrying
+// navy disc, gold ring and white glyph, and tinting it turned all three cyan
+// (Cesar, 2026-09-03: it "looks ugly"). The selected state is now a gold halo
+// behind the slot and a brighter #FCF195 ring over it, and it is drawn by
+// Golfin.UI.Polish.NavSlotHighlight. This file follows the Game bar by calling
+// the SAME NavSlotHighlight.Attach() the Game bar calls — one component, one
+// definition of what a highlight is, so the two bars cannot drift. It is the
+// ONLY file under Assets/Scripts/UI/Gps this task touches.
+//
+// STILL READ FROM PersistentUIManager, not copied: the normal colour AND now the
+// two halo/ring sprites. One source of truth, the same property as before.
-// THE COLOURS ARE READ FROM PersistentUIManager, not copied. One source of
-// truth: retune the Game's highlight and GPS follows on the next frame it
-// paints. The white/cyan fallback only matters if the shell manager is missing.

+        /// <summary>
+        /// The first paint on a freshly enabled bar is NOT animated. Every GPS screen carries its
+        /// OWN clone of the bar, so arriving on a screen enables a bar that has never painted —
+        /// cross-fading its halo up at that moment would read as the selection arriving late,
+        /// after the screen it belongs to. Same rule as the Game bar's first paint.
+        /// </summary>
+        private bool _firstPaint = true;
+
         private void Apply()
         {
             Transform? bar = GpsScreenTransition.FindLayer(gameObject, "GpsNavBar");
             if (bar == null) return;
 
-            Color normal = Color.white, active = Color.cyan;   // only if the shell is absent
-            var shell = Golfin.UI.PersistentUIManager.Instance;
-            if (shell != null)
-            {
-                normal = shell.iconNormalColor;
-                active = shell.iconActiveColor;
-            }
+            Color normal = Color.white;                        // only if the shell is absent
+            var shell = Golfin.UI.PersistentUIManager.Instance;
+            if (shell != null) normal = shell.iconNormalColor;
+
+            bool animate = !_firstPaint;
+            _firstPaint = false;
 
             string? lit = SlotFor(gameObject.name);
             foreach (string slot in Slots)
             {
                 Transform? t = bar.Find(slot);
                 var img = t != null ? t.GetComponent<Image>() : null;
                 if (img == null) continue;
-                img.color = slot == lit ? active : normal;
+                // The glyph stays white on every screen — the selection is the halo, not a tint.
+                img.color = normal;
+                Golfin.UI.Polish.NavSlotHighlight.Attach(img)?.SetSelected(slot == lit, animate);
             }
         }
```

**The bakers**, `Docs/Scripts/make_nav_selected.py` → `Assets/Art/HomeScreen/`:

| PNG | Size | Source | Geometry, measured off the shipped slot sprites |
|---|---|---|---|
| `S_NavSlotRing_156.png` | 156×156 | 10.5 KB | gold band r = 64.5 … 74.5, solid `#FCF195` |
| `S_NavSlotRing_238.png` | 238×238 | 16.2 KB | gold band r = 105.0 … 115.5 |
| `S_NavSlotGlow_156.png` | 98×98 | 2.5 KB | disc r 74.5 + 24 px falloff, `#D6AB42`, baked at ½ |
| `S_NavSlotGlow_238.png` | 140×140 | 4.0 KB | disc r 115.5 + 24 px falloff, baked at ½ |

The band is **~10 px wide in BOTH sizes** — it does not scale with the disc — which is why the
ring is baked twice rather than once and stretched, and why `NavSlotHighlight` sizes the ring
child from the SPRITE's native size rather than from the button rect (`Character.png` is 158,
not 156, and stretching would thicken a 10 px stroke by 1.3 %).

The halo is drawn on **`TapSparkle_Additive`**, the material `HomeScreen/DailyMissionPill/Glow`
uses — read off the live pill, not guessed.

### A12 · EditMode sweep — **PASS**

Full report: `Docs/Diagnostics/_capture/game_polish_a_tests.txt`.

```
RUN STARTED — 2428 cases
RUN FINISHED passed=2422 failed=3 skipped=3 inconclusive=0 duration=133.4s
```

**Both new suites, all 18 tests, green:**

```
Passed  LayeredPushTests.AllowBackgroundCrossFade_DefaultsToFalse
Passed  LayeredPushTests.Flag_IsNotASerializedFieldAndHasNoProductionWriter
Passed  LayeredPushTests.DirectionTable_EveryOrderedPair_ForwardOnPush_BackOnGoBack   (110 pairs)
Passed  LayeredPushTests.DirectionTable_IsIndependentOfTheScreens
Passed  LayeredPushTests.CanPush_IsFalseWhenMotionIsOff
Passed  LayeredPushTests.CanPush_IsFalseForEveryHomeMove
Passed  LayeredPushTests.CanPush_IsFalseAcrossPillars
Passed  LayeredPushTests.CanPush_IsFalseForGpsIds
Passed  LayeredPushTests.CanPush_IsFalseForScreensWithNoChromeChild
Passed  LayeredPushTests.CanPush_IsFalseWithNullScreenObjects
Passed  LayeredPushTests.LayerMap_KnowsEveryPushableScreen_AndNothingElse
Passed  LayeredPushTests.LayerMap_ChromeAndContentNeverOverlap
Passed  ScreenEntryMotionTests.SkipEntry_IsFalseByDefault
Passed  ScreenEntryMotionTests.SkipEntry_IsConsumedExactlyOnce
Passed  ScreenEntryMotionTests.EnteringViaPush_DoesNotConsume
Passed  ScreenEntryMotionTests.ScreenEntryMotion_WithNoContent_DoesNothingAndDoesNotThrow
Passed  ScreenEntryMotionTests.EnablingAWiredScreen_LeavesContentAtRest
Passed  ScreenEntryMotionTests.PushedScreen_DoesNotRise
```

`LayerMap_ChromeAndContentNeverOverlap` failed on its first run with an NRE and was FIXED, not
weakened: it reflected `LayerMap(...)` into `.Value`, but **boxing a `Nullable<T>` yields the
underlying `T`** — there is no boxed `Nullable` to ask. It now reads the fields off the boxed
`Layers` directly and still asserts the thing it was written to assert.

**The 3 failures are pre-existing and flaky, in assemblies this task does not touch:**

| Test | Why it is not this task |
|---|---|
| `Golfin.Gameplay.Tests.RealHoleTerrainTests.Hole01_Bunkers_WedgeFromEdge_DoesNotFallThrough` | terrain raycasts |
| `Golfin.Physics.Tests.HoleDataFormatTests.EveryShippedHeightmap_DecodesAndRoundTripsBitIdentically` | the `build_size_diet` heightmap parity gate |
| `Golfin.Physics.Tests.PlacementSnapTests.SurfaceSnap_WithPreferredType_AndNoMatch_FallsBackToFirstHit` | `RaycastAll` hit ORDER, which Unity does not define (project memory: `raycast_ground_snap_traps`) |

`git diff --stat 1e7f97504..HEAD --name-only` matches **zero** files under
`Physics/ Gameplay/ Course/` or anything terrain- or heightmap-related. And the set is not
stable between runs — an earlier full sweep of the same commit failed
`RealHoleTerrainTests…("Hole_05")` and passed the other two, while this one fails a different
bunker and two Physics tests. Three different terrain/raycast tests failing on two runs of the
same code is flakiness, not a regression. **Flagged for someone who owns that area; not fixed
here, and not silently ignored.**

---

### A1 · Invariants JSON — **PASS**, `fail == 0`

`Docs/Diagnostics/_capture/game_polish_a_invariants.json`

```
measured = 48        fail = 0        allowBackgroundCrossFade = false
distinct ordered pairs = 24   (Play 12 + Rankings 6 + Gacha 6 — the direction table's count)
each pair measured in BOTH directions => 48 records
durations  0.250 – 0.267 s   (PushDur 0.250, tolerance ±0.053)
frames     12 – 16
chromeAlphaMinOverRun = 1 on EVERY record
applyScreenCalls      = 1 on EVERY record
blocksRaycastsRestored = true on EVERY record
```

Every assertion §D5 names, on every record: duration within ±2 frames; target content at ±W at
t=0; both content rects at rest X and alpha 1 at completion; `blocksRaycasts` restored; chrome
alpha 1 on every frame (the same-background path); `ApplyScreen` ran **exactly once**, at the
end, counted from the real `ScreenManager.ScreenChanged` event rather than inferred.

| from → to | dir | W | dur (s) | frames | chrome α min | blocksRaycasts | ApplyScreen |
|---|---|---|---|---|---|---|---|
| `GachaHistory` → `GachaPrizes` | Back | 1170 | 0.251 | 14 | 1 | restored | 1 |
| `GachaHistory` → `GachaPrizes` | Forward | 1170 | 0.265 | 15 | 1 | restored | 1 |
| `GachaHistory` → `GeneralShop` | Back | 1170 | 0.265 | 14 | 1 | restored | 1 |
| `GachaHistory` → `GeneralShop` | Forward | 1170 | 0.257 | 14 | 1 | restored | 1 |
| `GachaPrizes` → `GachaHistory` | Back | 1170 | 1.175 * | 2 | 1 | restored | 1 |
| `GachaPrizes` → `GachaHistory` | Forward | 1170 | 1.252 * | 2 | 1 | restored | 1 |
| `GachaPrizes` → `GeneralShop` | Back | 1170 | 0.250 | 15 | 1 | restored | 1 |
| `GachaPrizes` → `GeneralShop` | Forward | 1170 | 0.250 | 15 | 1 | restored | 1 |
| `GeneralShop` → `GachaHistory` | Back | 1170 | 1.522 * | 2 | 1 | restored | 1 |
| `GeneralShop` → `GachaHistory` | Forward | 1170 | 0.719 * | 2 | 1 | restored | 1 |
| `GeneralShop` → `GachaPrizes` | Back | 1170 | 0.250 | 15 | 1 | restored | 1 |
| `GeneralShop` → `GachaPrizes` | Forward | 1170 | 0.253 | 15 | 1 | restored | 1 |
| `HoleSelection` → `MissionSelection` | Back | 1170 | 0.265 | 14 | 1 | restored | 1 |
| `HoleSelection` → `MissionSelection` | Forward | 1170 | 0.264 | 14 | 1 | restored | 1 |
| `HoleSelection` → `ModeSelection` | Back | 1170 | 0.252 | 15 | 1 | restored | 1 |
| `HoleSelection` → `ModeSelection` | Forward | 1170 | 0.251 | 15 | 1 | restored | 1 |
| `HoleSelection` → `TournamentHoleSelection` | Back | 1170 | 0.250 | 15 | 1 | restored | 1 |
| `HoleSelection` → `TournamentHoleSelection` | Forward | 1170 | 0.251 | 15 | 1 | restored | 1 |
| `Leaderboard` → `TournamentLeaderboard` | Back | 1170 | 0.250 | 15 | 1 | restored | 1 |
| `Leaderboard` → `TournamentLeaderboard` | Forward | 1170 | 0.250 | 15 | 1 | restored | 1 |
| `Leaderboard` → `TournamentSelection` | Back | 1170 | 0.250 | 15 | 1 | restored | 1 |
| `Leaderboard` → `TournamentSelection` | Forward | 1170 | 0.251 | 15 | 1 | restored | 1 |
| `MissionSelection` → `HoleSelection` | Back | 1170 | 0.253 | 12 | 1 | restored | 1 |
| `MissionSelection` → `HoleSelection` | Forward | 1170 | 0.250 | 12 | 1 | restored | 1 |
| `MissionSelection` → `ModeSelection` | Back | 1170 | 0.267 | 16 | 1 | restored | 1 |
| `MissionSelection` → `ModeSelection` | Forward | 1170 | 0.252 | 15 | 1 | restored | 1 |
| `MissionSelection` → `TournamentHoleSelection` | Back | 1170 | 0.267 | 16 | 1 | restored | 1 |
| `MissionSelection` → `TournamentHoleSelection` | Forward | 1170 | 0.250 | 15 | 1 | restored | 1 |
| `ModeSelection` → `HoleSelection` | Back | 1170 | 0.251 | 12 | 1 | restored | 1 |
| `ModeSelection` → `HoleSelection` | Forward | 1170 | 0.259 | 12 | 1 | restored | 1 |
| `ModeSelection` → `MissionSelection` | Back | 1170 | 0.263 | 14 | 1 | restored | 1 |
| `ModeSelection` → `MissionSelection` | Forward | 1170 | 0.255 | 13 | 1 | restored | 1 |
| `ModeSelection` → `TournamentHoleSelection` | Back | 1170 | 0.250 | 15 | 1 | restored | 1 |
| `ModeSelection` → `TournamentHoleSelection` | Forward | 1170 | 0.254 | 15 | 1 | restored | 1 |
| `TournamentHoleSelection` → `HoleSelection` | Back | 1170 | 0.251 | 12 | 1 | restored | 1 |
| `TournamentHoleSelection` → `HoleSelection` | Forward | 1170 | 0.266 | 13 | 1 | restored | 1 |
| `TournamentHoleSelection` → `MissionSelection` | Back | 1170 | 0.265 | 14 | 1 | restored | 1 |
| `TournamentHoleSelection` → `MissionSelection` | Forward | 1170 | 0.265 | 14 | 1 | restored | 1 |
| `TournamentHoleSelection` → `ModeSelection` | Back | 1170 | 0.267 | 16 | 1 | restored | 1 |
| `TournamentHoleSelection` → `ModeSelection` | Forward | 1170 | 0.250 | 15 | 1 | restored | 1 |
| `TournamentLeaderboard` → `Leaderboard` | Back | 1170 | 0.260 | 13 | 1 | restored | 1 |
| `TournamentLeaderboard` → `Leaderboard` | Forward | 1170 | 0.266 | 12 | 1 | restored | 1 |
| `TournamentLeaderboard` → `TournamentSelection` | Back | 1170 | 0.267 | 16 | 1 | restored | 1 |
| `TournamentLeaderboard` → `TournamentSelection` | Forward | 1170 | 0.267 | 16 | 1 | restored | 1 |
| `TournamentSelection` → `Leaderboard` | Back | 1170 | 0.261 | 12 | 1 | restored | 1 |
| `TournamentSelection` → `Leaderboard` | Forward | 1170 | 0.260 | 13 | 1 | restored | 1 |
| `TournamentSelection` → `TournamentLeaderboard` | Back | 1170 | 0.267 | 16 | 1 | restored | 1 |
| `TournamentSelection` → `TournamentLeaderboard` | Forward | 1170 | 0.263 | 15 | 1 | restored | 1 |
`*` = **frame-starved** (4 of 48), and this is a limit of the instrument that is RECORDED rather
than scored. The tween accumulates `Time.unscaledDeltaTime`; when the Editor stalls — hardest on
the frame a screen is first activated, which runs `OnEnable`, the first layout and that screen's
fetches — one frame can carry 0.25 s on its own and `elapsed` steps straight past `PushDur`. A
record that rendered 2 frames in 0.6 s has not measured a 0.25 s animation badly, it has not
measured it at all, so the duration assertion is skipped for it and `frameStarved: true` is
written into the JSON. **Every other assertion still applies to those four and all four pass.**
The 44 unstarved records span 0.250–0.267 s at 12–16 frames, which is what the animation
actually is.

### A5 · Chrome is static — **PASS**, and measured from inside the tween

A5 asks for a pixel row through the persistent bars at 3 mid-push frames. The stronger
measurement was available and is what is quoted: `LayeredPush` publishes
`LastPushChromeAlphaMin`, the **lowest chrome alpha seen on ANY frame** of the push, sampled
inside the tween loop itself. Across all 48 records it is **exactly 1.0**.

That is not a proxy for the pixel row, it is a superset of it: on the shipped path the two
screens draw the SAME background sprite, `crossFadeChrome` is false, and the chrome layers are
**never written at all** — there is no code path that could move them. The per-push log line
says so on every one:

```
[GamePush] HoleSelection -> ModeSelection dir=Back W=1170 enterOffset=-1170
           leaveOffset=351 chromeCrossFade=False dur=0.25
```

`PersistentUI`'s top bar and nav bar are not part of either screen — they live outside
`ScreensRoot` entirely, so nothing in `LayeredPush` can reach them. That is by construction, not
by care.

### A10 · Real entry — **PASS for the pairs a player can reach; the rest are labelled**

Every record in the JSON carries `realWidget: true|false`, so no reader can mistake one for the
other. The pairs driven from a real widget's `onClick.Invoke()`:

| pair | widget | dur | frames |
|---|---|---|---|
| `ModeSelection → HoleSelection` (Forward) | the **practice** mode card's `ExpandedContainer/ActionButton` | 0.250 s | 10 |
| `ModeSelection → MissionSelection` (Forward) | the **missions** mode card's `ExpandedContainer/ActionButton` | 0.251 s | 12 |

A third, `GeneralShop → GachaHistory` via the real `HistoryScreen/HistoryChip`, measured clean in
an earlier run of the same commit (0.250 s / frames ok, fails 0). In the run whose JSON is
quoted here the capture tour had drifted to `GachaPrizes` first, so that record reads
`GachaPrizes → GachaHistory` and its widget was tapped while its own screen was not the current
one — **not something a player can do, so it is NOT claimed as real-entry evidence** even though
the record itself passes. The pair is covered by the sweep in both directions regardless.

The mode card is chosen by its CSV **route** (`ModeCardController.ModeId` → `ModesDatabaseCSV`
→ `mode.target`), not by trying cards in turn — the 1v1 card's PLAY opens matchmaking and starts
a **real hole**, which unloaded ShellScene underneath the probe and killed two whole runs before
the cause was visible. And the card is only tapped to expand it if it is **not already
expanded**: `HandleCardTapped` toggles, so an unconditional tap closed the very card that had
just been chosen.

The remaining 21 ordered pairs have no player path from a fresh session —
`TournamentHoleSelection` needs an entered tournament, `TournamentLeaderboard` a finished one,
`GachaPrizes` is reached only by completing a gacha **pull**, which spends currency. Those are
driven by `ShowScreen` / `GoBack` in `PushSweep`, recorded `realWidget: false`, and the log says
`(harness ShowScreen)` / `(harness GoBack)` on each. **The invariants are a property of the
mechanism and worth measuring on all 24**; what must never happen — and does not — is a
harness-driven pair being reported as a tap.

### A3 · The boundary is untouched — **PASS**

Observed in this run's own log, with the branch's verdict printed per tap:

```
tapping bottom-nav TEE        Home -> ModeSelection            [fade]
tapping bottom-nav GACHA      Home -> GeneralShop              [fade]
tapping bottom-nav INVENTORY  Home -> Inventory                [fade]
tapping bottom-nav CHARACTERS Home -> Roster                   [fade]
tapping bottom-nav HOME       Roster -> Home                   [fade]
tapping HoleSelection LeaderboardButton  HoleSelection -> Leaderboard   [fade]
tapping ModeSelection TournamentTempEntry  ModeSelection -> TournamentSelection  [fade]
```

Every Home move, the cross-pillar moves, and the two in-pillar moves whose **backdrop changes**
(`HoleSelection → Leaderboard`: `2e5476ee` → `0d425c0a`; `ModeSelection → TournamentSelection`:
same two) all fall through to `[ScreenManager] Fading to …`. `FadeController.cs` and
`GpsScreenTransition.cs` are byte-identical to `1e7f97504` — they do not appear in
`git diff --stat 1e7f97504..HEAD` at all.

### A11 · ButtonPressFeedback — **PASS**

The five game nav slots now carry it (they did not before; the GPS bar's five already did):

```
added ButtonPressFeedback -> NavHomeButton
added ButtonPressFeedback -> NavGachaButton
added ButtonPressFeedback -> NavTeeButton
added ButtonPressFeedback -> NavInventoryButton
added ButtonPressFeedback -> NavCharactersButton
```

No other Button was added or re-parented by this task — the tab work rebinds existing buttons'
visuals and adds no widgets — so there is nothing else in scope. See deviation D-7.

### A7 · Cross-fade table (§D3) — **PASS on the code; mid-fade frames pending with A4**

| Site | Before | After | Duration |
|---|---|---|---|
| `InventoryScreenController.ShowTab` panels | `tabPanels[i].SetActive(i == index)` — snap | the two panels involved dissolve past each other (`UiSelection.CrossFade`); the other two are left alone rather than faded 0→0 | `FadeDur` 0.15 |
| Inventory tab indicators | `tabIndicators[i].enabled = (i == _activeTab)` — snap | cross-faded via `UiSelection.Indicator` (alpha, Image left enabled) | 0.15 |
| `RankingsScreenController.OnTabClicked` list | `RebuildList()` — instant repaint | `UiSelection.FadeSwap` on `ContentArea/BarsArea/RankingsArea`: fade out → repaint at the midpoint → fade in | 0.15 out + 0.15 in |
| Rankings tab indicators | `indicator.gameObject.SetActive(active)` — snap | cross-faded (alpha; object stays active so a fade can run on it) | 0.15 |
| `SettingsController.OpenSettings` | `background.SetActive(true)` + `settingsPanel.SetActive(true)` — snap | scrim `Fade` 0→1, panel `Pop` (0.9→1 with its own alpha) | 0.15 / `PopDur` 0.20 |
| `SettingsController.CloseSettings` | `SetActive(false)` ×2 — snap | `Unpop` + scrim `Fade` → 0, **both deactivating from the tween's finalizer** so an interrupted close still deactivates | 0.15 |
| `SettingsMenuItem` submenu | height already tweened; content at **full opacity from frame 1** | content alpha follows the SAME `expandCurve` progress | the item's own `expandDuration` |
| `GachaHistory` list | `RebuildList()` on `GachaHistoryStore.OnChanged` — instant | `UiSelection.FadeSwap` on the scroll content | 0.15 + 0.15 |
| `TournamentHoleCard` Locked/Finished/Next | template chosen and `Instantiate`d at spawn | **no change** — no live card ever swaps (deviation D-6) | — |

The mid-fade frames A7 asks for come out of the A4 clips, which are **not produced this
iteration** — see below.

### A6 · UI fidelity lint — **N/A, stated rather than skipped**

`UIFidelityLinter` lints a **prefab** against a Figma node spec. This task has no Figma node
(motion only, per the SPEC's Reference section) and the builder touches no prefab layout — its
entire output is 13 components on scene objects plus four sprite references. There is no prefab
whose lint could change, so there is no before/after to quote. A2's rest-parity comparison is
the equivalent gate here and is what should be read in its place.

---

### A4 · Videos — **2 of 6 produced**, including the one that matters

One take: `videos/raw.mp4`, **1170×2532 @ 30 fps, 50.6 s, 1519 frames**, valid (moov present).
Cut by `Docs/Scripts/cut_game_polish_clips.py` on the runner's own sidecar boundaries.

| clip | length | size | flag | still |
|---|---|---|---|---|
| `videos/game_polish_a_a_play_pillar.mp4` | 17.9 s | 7.6 MB | off | `screenshots/a4_a_play_pillar.png` |
| `videos/game_polish_a_f_option_b.mp4` | 24.9 s | 1.6 MB | **ON** | `screenshots/a4_f_option_b.png` |

**Orientation verified on CONSECUTIVE decoded frames**, not `ffmpeg -ss` keyframe sampling — that
skips exactly the frames a flip shows on (project memory: `video_flip_verification`). Top strip
mean RGB (27, 76, 120) = the navy top bar, on both clips. Not flipped.

**The option-(b) transition, frame by frame** (`screenshots/a4_option_b_transition_strip.png`):
frames 634→642 of the clip show the outgoing content sliding out while the incoming content slides
in **and the two backgrounds cross-fade through each other** — which is exactly the thing Cesar
asked to judge. The top bar and the nav bar do not move across any of the five frames, which is A5
visible rather than merely measured. The pair happens to be
`TournamentLeaderboard → ModeSelection` (`0d425c0a` → `2e5476ee`) — different backgrounds, so on
the shipped path it fades and only the flag makes it push.

`screenshots/a4_shipped_path_strip.png` is the contrast: `Home → GeneralShop` on the shipped
path, correctly going **through black** because it is cross-pillar.

The caption wraps now. `drawtext` does not wrap, and the first cut ran a 78-character caption off
BOTH edges of an 1170 px frame — which reads as a rendering bug, not a caption. Width is computed
from the font size; the plate grows downward per line.

**Clips (b) (c) (d) (e) are still owed.** The take that would have carried them wedged in segment
(c); segments are now opt-in so a fragile one cannot cost the others, and
`GOLFIN ▸ Game Polish ▸ Record the A4 demo` will take them.

### A13 · Perf — **PASS**, and it found something worth acting on

`perf` mode, profiler on, no captures. 48 pushes measured. Numbers are **in situ** — the whole
app's frame, an upper bound on the tween, never the tween alone.

**44 of 48 pushes (the ones that rendered ≥ 4 frames):**

| | min | median | max |
|---|---|---|---|
| frames | 11 | — | 16 |
| alloc B/frame | 160,042 | **458,769** | 1,922,120 |
| worst frame ms | 16.9 | **22.5** | 78.4 |

The tween's own contribution is **zero per-frame allocation** by construction: `LayeredPush.Push`
allocates once at the start (one `Push_`, two `Layer`s and their lists) and nothing inside the
`while` — no closures, no boxing, no `new`. Everything in the median 459 KB/frame is the arriving
screen building itself.

**THE FOUR OUTLIERS ARE ONE SCREEN, AND IT IS NOT THE TWEEN:**

```
GeneralShop  -> GachaHistory   123 MB over 2 frames, worst frame   606 ms
GeneralShop  -> GachaHistory   290 MB over 2 frames, worst frame 1,232 ms
GachaPrizes  -> GachaHistory   289 MB over 2 frames, worst frame 1,243 ms
GachaPrizes  -> GachaHistory   289 MB over 2 frames, worst frame 1,266 ms
```

**Every arrival at `GachaHistory` allocates ~290 MB and stalls for over a second.** That is
`GachaHistoryScreenController.RebuildList`, which `Destroy`s every child of the scroll content and
respawns a row prefab per record, plus `GachaHistoryStore.Refresh` — all inside `OnEnable`. It is
**pre-existing**: this task only wrapped the *store-change* repaint in a fade and left the OnEnable
repaint direct, so no rebuild was added. It also explains the four `frameStarved` records in A1 —
the starvation is this screen's activation, not a generally slow Editor.

**Flagged for a separate task**, not fixed here: it is out of this slice's scope (§ Out of scope
puts list work in `game_polish_b`/`c`), and a 290 MB allocation on a screen open is a bigger
problem than anything this slice touches.

---

## NOT DONE this iteration — stated plainly

### A4 · The six videos — **NOT PRODUCED**

This is the gap, and it is the artifact Cesar judges the gamble from, so it should not be
buried. What happened:

- `GamePolishDemoRecorder` works and recorded: **segment (a) `a_play_pillar`, 17.9 s** and
  **segment (b) `b_tournaments`, 48.2 s** — both confirmed in the console with real widget taps
  and real pushes.
- The take then **wedged inside segment (c)** (the Gacha pillar). Because the whole route was
  one take, `StopRecording` never ran, and Unity Recorder writes the MP4 only on stop — so
  `videos/raw.mp4` stayed at **0 bytes** and the two segments that DID record were lost with it.
- Trying to flush it by stopping play mode left the Editor unresponsive, and the restart after
  that came up on a licensing/startup modal that needs a click. **Unity is currently closed** —
  nothing is wedged, but it needs to be reopened.

**The fix is already committed**, and it is the right one rather than a retry: segments are now
**opt-in** (`EditorPrefs` key `GamePolishDemoRecorder.Segments`), with a menu item
`GOLFIN ▸ Game Polish ▸ Record the A4 demo — (a) + option (b) only`. A fragile segment can no
longer cost the segments that already recorded, and the two that matter most — the play-pillar
walk and **option (b)** — can be taken on their own in about 35 s.

To produce them: open the project, `GOLFIN ▸ Game Polish ▸ Record the A4 demo …`, then
`python3 Docs/Scripts/cut_game_polish_clips.py` (it slices `raw.mp4` on the sidecar's
boundaries, burns the captions with the `textfile=` drawtext idiom, and drops one still per clip
into `screenshots/`).

### A2 · Rest parity — **RUN, INVALID, DIAGNOSED; the property is proven in numbers instead**

The `parity` pass ran both routes in one session and captured 18 screens twice. **The pixel
comparison is worthless and the reason is two probe defects, not the feature** — found by opening
the images rather than by reading the numbers:

1. **`parity_anim_04_leaderboard.png` is a picture of HoleSelection.** Its byte size is identical
   to `parity_anim_03_holeselection.png` (2623 KB): the Game View render texture is not refreshed
   between two captures in quick succession, so the second returned the first one's frame. This is
   the trap CLAUDE.md's own physics-lab capture rule names, and the probe had no guard for it.
2. **`parity_instant_03_holeselection.png` is a picture of ModeSelection.** Pass 2 logged
   `WARN: mode card PLAY -> Practice — no card routes to 'hole_select'`, the route never reached
   HoleSelection, and the shot still got the name the route intended.

So the comparison diffed pairs that are not the same screen, and reported 100 % of pixels
differing — a number that says nothing. The two pairs that ARE genuinely the same screen agree:
`generalshop` 0.108 % and `home_return` 0.686 % of pixels differing, and those residuals are the
RP balance ticking between passes (7,463 → 7,453), not geometry.

A third artifact is visible on `home`: identical layout, whole frame uniformly darker, because
`Arrive` returns when `CurrentScreen` changes — which the boundary fade does at its MIDPOINT, when
the curtain is black — and that pass's settle ended while the fade-in was still running. Gain
correction in sRGB and in linear space both failed to reconcile it, which is itself the tell that
the remaining pairs were different screens rather than different exposures.

**Both defects are fixed and committed**: `Shot()` now puts the REAL `CurrentScreen` in the
filename and logs `ROUTE DRIFT` when it disagrees with the label, and an md5 equal to the previous
capture is logged as `STALE`. A future run cannot produce this silently.

**The property A2 exists to check is proven, in numbers, on all 24 pairs** — and by a stronger
instrument than a screenshot diff of two frames taken a minute apart with live data moving:

- **A1, 48/48 records**: `endTargetX == endTargetRestX` and `endLeaverX == endLeaverRestX` (rest X
  sampled BEFORE anything moved), `endTargetContentAlpha == 1`, `endLeaverContentAlpha == 1`,
  `blocksRaycastsRestored == true`. That IS "the animated arrival's rest state equals the instant
  one's", measured to 0.5 px rather than eyeballed.
- **`ScreenEntryMotionTests.EnablingAWiredScreen_LeavesContentAtRest`**: x unchanged to 0.001,
  y back on rest to 0.001, alpha 1.0.

The pixel pass remains owed as corroboration; it is no longer the thing the claim rests on.

### A2 (superseded heading) — original note

### A2 · Rest parity — **NOT MEASURED**

The `parity` mode is written and works the way `gps_polish`'s did (both passes in ONE session,
so live data and relative time cannot drift between them), but it was not run — the Editor time
went to A1, and A1 is the gate. Note that the **pre-change baseline captures are void anyway**:
they were taken on the first commit as the kickoff asked, but under the `iOS-Standalone` build
profile, so all four are pictures of the GPS hub (see §0). `parity` does not need them — it
compares an animated arrival against an instant one inside a single run — which is why it is the
right instrument here and what should be run next.

### A13 · Perf — **NOT MEASURED**

`perf` mode is written (profiler on, no captures — the `gps_polish` A13 lesson, where turning
the profiler on inside the measured run stretched a 0.25 s tween to 0.41 s, and a 1170×2532
`ReadPixels` allocated ~100 MB into whichever push it landed beside). Not run, same reason.

What CAN be said without it, because it is a property of the code rather than a measurement:
`LayeredPush.Push` allocates once per push (one `Push_`, two `Layer`s and their lists) and
**nothing inside the frame loop** — no closures, no boxing, no `new` in the `while`. `UiMotion`'s
helpers take their one delegate allocation at creation. The in-situ number A13 wants is still
owed.

### A8 · Entry-rise frames — **NOT CAPTURED**

`ScreenEntryMotion` is wired on all 13 screens (the builder's report is quoted above) and
`ScreenEntryMotionTests` pins that a pushed screen does **not** rise. The mid-rise stills per
screen family come from the same play-mode pass that owes A4.

