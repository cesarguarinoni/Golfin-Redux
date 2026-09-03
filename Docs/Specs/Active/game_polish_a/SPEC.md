# SPEC — `game_polish_a` (navigation & structure motion)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Notion **2111** `game_polish`, slice **a** of three (b = content & modals, c = sweep). The approved map is `Docs/Specs/Queued/game_polish/MAP.md` (Cesar, 2026-09-03) — this spec implements its G1, G2, G9 and the tab / overlay cross-fades, with the G10 gates. **Runs AFTER `design_consistency_audit` and its approved Quick fixes** — we do not animate inconsistencies.

## Status

See `STATUS.md`.

## Goal

Make moving around the GAME shell feel like the GPS surface already does: a **layered push** between screens of the same pillar whose background does not change, a 16 px **rise** on every fade-path arrival, **cross-fades** where a tab, filter or overlay currently snaps (`SetActive`), and **selection bumps** on tabs and chips, and a **new bottom-nav selected state** (gold halo + brighter ring, replacing the cyan tint — on the game bar AND the GPS bar, Cesar 2026-09-03). Nothing about what a screen *does* changes; every screen is pixel-identical at rest (0 px parity). No haptics, no package, no `Animator`.

Cesar's calls, baked in:

- **Fade-to-black stays for every cross-pillar move and for every in-pillar move whose background changes.** The push only runs when the two screens' backgrounds are the same sprite.
- **One 5-second video of the alternative** — push-with-background-cross-fade on `ModeSelection → TournamentSelection` — recorded behind a flag that **ships OFF** (`LayeredPush.AllowBackgroundCrossFade`, default `false`, pinned by test). Cesar judges it from the video; nothing in the shipped path depends on it.
- No haptics (`haptics_option`, 2130). GPS code untouched (`Assets/Scripts/UI/Gps/**`, `Assets/Prefabs/UI/Gps/**`) — **with ONE authorised exception**: §D7 rewires `Assets/Scripts/UI/Gps/GpsNavBarHighlight.cs` (and the GPS nav-bar prefab/builder it drives, if the halo needs a child) to the shared `NavSlotHighlight`, because Cesar asked for the selected state to change on both bars at once. Nothing else under `Gps/` may change; `git diff --stat -- Assets/Scripts/UI/Gps Assets/Prefabs/UI/Gps` is quoted in the report and must list only those files.

## Reference

- **No Figma nodes** — motion only. Rest state at HEAD is the visual reference; the audit's crop sheets (`Docs/Specs/Active/design_consistency_audit/screenshots/`) and `Docs/Reports/DESIGN_CONSISTENCY_AUDIT.md` are the ground truth for "what a screen looks like at rest" AFTER the Quick fixes land.
- **The model to copy:** `Docs/Specs/Completed/gps_polish/` — SPEC §D2/§D3, IMPLEMENTER_REPORT A1/A2/A5/A13, `Assets/Scripts/UI/Gps/GpsScreenTransition.cs` (read it, do not edit it, do not call it from game code), `Assets/Scripts/UI/Gps/Editor/GpsPolishProbe.cs` (the probe shape: `baseline` / `polished` / `push` / `parity` modes, real navigation, invariants JSON).
- **Motion vocabulary:** `Assets/Scripts/UI/Polish/UiMotion.cs` — `Fade` (`FadeDur` 0.15), `Rise` (`EntryDur` 0.25, `RiseDy` 16), `Slide` (`PushDur` 0.25), `Bump` (1.06 / 0.10), `Run(host, ref handle, routine)`, `UiMotion.Enabled`; `UiSelection` (bump + two-Image cross-fade). **Do not change `UiMotion`'s public API in this slice** (slice b adds `Ease`; the GPS_BACKLOG row exists for that one).
- **Navigation:** `Assets/Scripts/UI/ScreenManager.cs` — `Navigate(screenId, instant, push)`, the GPS push branch (find the comment `gps_polish §D2 — the ONE branch`; the sequence is `IsPushing → CompleteActiveNow` guard → instant path → `CanPush` → `FadeController.FadeOutThenIn`; line numbers move while the GPS track edits this file, so anchor on the comments, not on numbers), `PillarOf`, `RootOf`, `IsShell`, `ApplyScreen` (the `SetActive` wall), the static `ScreenChanged` event. `NavigateToPillar` — a pillar reset, stays on the fade.

### The screens and their layers (measured from the YAML at HEAD `85b2365fb` — Code re-measures live)

| ScreenId | Root (ShellScene object or prefab) | Chrome layer(s) | Content layer | Background sprite GUID |
|---|---|---|---|---|
| `ModeSelection` | `ModeSelectionScreen` | `Background` | `CardsContainer` (+ `TournamentTempEntry` — confirm dead, else it is content) | `2e5476ee…` |
| `HoleSelection` | `HoleSelectionScreen` | `Background` | `Content` (+ `LeaderboardButton` = content) | `2e5476ee…` |
| `MissionSelection` | `MissionSelectionScreen` | `Background` | `Content` | `2e5476ee…` |
| `TournamentHoleSelection` | `TournamentHoleSelectionScreen` | `Background` | `Content` (+ `LeaderboardButton`) | `2e5476ee…` |
| `TournamentSelection` | `TournamentSelectionScreen.prefab` | `BG` | `ContentArea` | `0d425c0a…` |
| `TournamentLeaderboard` | `TournamentLeaderboardScreen` | `BG` | `ContentArea` (+ `BackButton`) | `0d425c0a…` |
| `Leaderboard` (Rankings) | `RankingsScreen.prefab` | `BG` | `ContentArea` (+ `BackButton`) | `0d425c0a…` |
| `GeneralShop` | `GeneralShopScreen.prefab` | `BG` | `ContentArea` (+ `HistoryChip`) | `5ec22d10…` |
| `GachaHistory` | `GachaHistoryScreen.prefab` | `Background` | `GameScreenContent` (+ `FiltersIconRow`) | `5ec22d10…` |
| `GachaPrizes` | `GachaPrizesScreen.prefab` | `Background` | `GameScreenContent` | `5ec22d10…` |
| `Roster` | `RosterScreen` | — (no background child; the stage renders behind) | `CarouselSection` + `DetailPanel` | — |
| `StaminaShopSelection` / `StaminaShopDetail` | prefabs | inside nested prefabs — **measure** | — | ? |
| `Inventory` | `InventoryScreen` | `BG` + `Rim` | `TabBar` + `ContentArea` | `44d64d73…` |
| `Home` | `HomeScreen` | `Background` | everything else | `c230d900…` |

**Push pairs that follow from the table** (both directions; `push` → Forward, `GoBack` → Back):
`ModeSelection ⇄ HoleSelection`, `ModeSelection ⇄ MissionSelection`, `ModeSelection ⇄ TournamentHoleSelection` (if reachable), `HoleSelection ⇄ MissionSelection` (if reachable) · `TournamentSelection ⇄ TournamentLeaderboard`, `TournamentSelection ⇄ Leaderboard`, `TournamentLeaderboard ⇄ Leaderboard` (if reachable) · `GeneralShop ⇄ GachaHistory`, `GeneralShop ⇄ GachaPrizes`, `GachaHistory ⇄ GachaPrizes` (if reachable) · `Roster ⇄ StaminaShop*` ONLY if Code proves the same backdrop (else fade). Everything else — every Home move, `ModeSelection ⇄ TournamentSelection`, `TournamentSelection ⇄ TournamentHoleSelection`, `TournamentHoleSelection ⇄ TournamentLeaderboard`, `HoleSelection ⇄ Leaderboard`, any move to Login/Loading/GPS — **fade, unchanged**. The direction table (every ordered pair of shell ScreenIds → Push-Forward / Push-Back / Fade) is pinned by `LayeredPushTests` (D1.4).

## Design

### D1 · `LayeredPush` — the game's push (`Assets/Scripts/UI/Polish/LayeredPush.cs`, Assembly-CSharp)

The shape of `GpsScreenTransition`, made screen-agnostic. **Do not copy-paste the file** — write it against the layer map above and cite which GPS design points you kept (the review compares the two).

1. **Layer discovery** — a static `LayerMap` table `ScreenId → (chromeNames[], contentNames[])` from the table above (names, not paths; `transform.Find` at depth 1). `HasSplit(go)` true iff at least one chrome and one content child exist. Persistent bars (`PersistentUI`) are not part of either screen — they stay put by construction; A5 measures it.
2. **`CanPush(from, to, fromGo, toGo)`** — true iff: `UiMotion.Enabled`; both ids are shell screens (`ScreenManager.IsShell`) in the same pillar (`PillarOf(a) == PillarOf(b)`, non-null) OR both are in the `{TournamentSelection, TournamentLeaderboard, Leaderboard}` set (Leaderboard has no pillar — it rides history; treat the three `0d42…` screens as one push group); both have the split; **`SameBackground(fromGo, toGo)`** — the chrome layers' first `Image.sprite` are the same asset (compare `sprite == sprite`, not names) — unless `AllowBackgroundCrossFade` is true (the video flag, D4); neither is Home; neither end is a GPS screen (`GpsGate.IsGpsScreen` false for both — the GPS branch runs first anyway).
3. **`Push(fromGo, toGo, dir, apply)`** — `PushDur` 0.25 s; target content enters from `±W` (W = the content layer's `rect.width`, measured — GPS D-2 notes why it is not always 978), current exits to `∓W × 0.3`; chrome layers cross-fade in place ONLY when backgrounds differ (flag path) — when they are the same sprite the chrome does not animate at all (alpha stays 1 on both; the seam invariant is trivially true); both content `CanvasGroup.blocksRaycasts = false` for the duration; `apply()` (= `ApplyScreen`) at the END; then every moved rect / CanvasGroup on both screens reset to rest. `IsPushing`, `CompleteActiveNow()` (snap to rest + run the deferred apply), one push at a time, no queue — same contract as GPS. `SkipEntry` one-shot flag for D2.
4. **Direction** — `DirectionFor(from, to, push)`: `push` → Forward; `!push` (GoBack) → Back. No nav-slot ordering (game pillars have no in-screen nav bar). Pinned table in `LayeredPushTests`.
5. **`ScreenManager.Navigate`** — ONE new branch immediately after the GPS branch (the `gps_polish §D2 — the ONE branch` block), same shape (`fromGo`/`toGo` from a new `ShellScreenObject(ScreenId)` accessor mirroring `GpsScreenObject`; `isActiveAndEnabled`; `StartCoroutine(LayeredPush.Push(...))`; `return`). The `IsPushing` guard above the instant path becomes `if (GpsScreenTransition.IsPushing) …; if (LayeredPush.IsPushing) …`. The `Fading to` log line stays the fallback for everything else.

### D2 · Entry rise

On every shell screen's activation through the FADE path (fade-to-black arrival, pillar reset, boot into Home), the content layer(s) `Rise` (`EntryDur`, 16 px). After a push, `LayeredPush.SkipEntry` is consumed and nothing rises. Implementation: ONE component, `Assets/Scripts/UI/Polish/ScreenEntryMotion.cs` (`MonoBehaviour`, `OnEnable` → if `!LayeredPush.ConsumeSkipEntry()` run `Rise` on each configured content rect), added to each shell screen root by an Editor builder (`GamePolishBuilder.Apply(root)` — `Assets/Scripts/UI/Polish/Editor/GamePolishBuilder.cs`, new; it ADDS `CanvasGroup`s and the component, edits nothing else; re-runnable; scene objects via `SerializedObject` with `RecordPrefabInstancePropertyModifications` — trap C1). Home rises on boot too (after `Loading → Home`). Roster: `DetailPanel` only (the character stage does not move). Inventory: `ContentArea` only (`TabBar` and `Rim` are chrome).

### D3 · Cross-fades where things snap

| Site | Today | After |
|---|---|---|
| `InventoryScreenController.ShowTab(int)` (`tabPanels[i].SetActive(i == index)`, then `RefreshTabVisuals()`) | snap | outgoing panel `Fade` 1→0 → `SetActive(false)`; incoming `SetActive(true)` + `Fade` 0→1, overlapping (`FadeDur`); the active-tab indicator (`tabIndicators[]`, one per tab) becomes one `Slide`-ing indicator OR — if the four indicators are separate images by design — cross-fade them (choose the one that keeps 0 px rest parity, say which) |
| `RankingsScreenController` tabs (`DailyTab/WeeklyTab/MonthlyTab/HistoryTab`) | list repaints on tap | list `Fade` out/in around the repaint (`FadeDur`); tab label/underline change cross-fades |
| `GachaHistoryScreenController` `FiltersIconRow` filter change | repaint | same as Rankings |
| `SettingsController.OpenSettings/CloseSettings` | `SetActive` snap of `background` + `settingsPanel` | scrim `Fade` 0→1 (the `ModalScrim`-applied background), panel `Pop` (`PopDur`); close = reverse then `SetActive(false)` on completion; `IsOpen` stays state-driven (true from the first frame of open) |
| `SettingsMenuItem.Expand/Collapse` (accordion) | snap | content `Fade` + height `Tween` (`FadeDur`) — `LayoutGroup`-driven, so tween a `LayoutElement.preferredHeight`, not the rect (trap C3) |
| `TournamentHoleCard_Locked/Finished/Next` state swap (if the controller swaps prefabs/children at runtime) | snap | cross-fade the two states (`FadeDur`) — only if a swap happens at runtime; if the state is baked at spawn, no change and say so |

### D4 · The (b) video — push-with-background-cross-fade, flag OFF

`LayeredPush.AllowBackgroundCrossFade` (`public static bool`, default `false`; **pinned false by test**; NOT a Settings entry, NOT a `[SerializeField]`). When true, `CanPush` no longer requires `SameBackground` and `Push` cross-fades the chrome layers (`Fade` 1→0 / 0→1 in place, GPS D2.2). The probe (D5) turns it on for ONE run, records `ModeSelection → TournamentSelection → back`, turns it off. That clip is `videos/game_polish_a_b_option_mode_to_tournaments.mp4` (≈ 5 s, captioned "OPTION (b) — flag OFF in the build"). Nothing else in the task runs with the flag on; the invariants JSON (A1) is produced with it OFF.

### D5 · Probe and gates (`Assets/Scripts/UI/Polish/Editor/GamePolishProbe.cs`)

Same modes as `GpsPolishProbe`: `baseline` (motion off, pre-change captures of every shell screen — take these on the FIRST commit, before any change), `push` (drives every pair in the direction table through REAL navigation — `ShowScreen` / `GoBack` from the real buttons' `onClick.Invoke()`, Rule 2 — and writes `Docs/Diagnostics/_capture/game_polish_a_invariants.json`), `parity` (A2), `perf` (A13, profiler on, no screenshots — the gps_polish A13 lesson), `option_b` (D4). Invariants per push: duration within ±2 frames of `PushDur`; target content X at t=0 == ±W; both content X == 0 and alpha == 1 at completion; `blocksRaycasts` restored; chrome alpha == 1 on every frame (same-background path) — and for the `option_b` run, the seam test (never both chrome alphas < 0.5); `ApplyScreen` ran exactly once at the end (the `ScreenChanged` event count).

### D6 · Selection bumps (G9, the navigation subset)

`UiSelection` on: Inventory tab buttons, Rankings tabs, GachaHistory filter icons. Selected element `Bump`s; if the tab has an unselected/selected sprite pair, cross-fade two Images (no tinting — palette rule). Mode cards / hole cards selection are slice b.

### D7 · Bottom-nav selected state (game bar + GPS bar) — Cesar, 2026-09-03

**Today.** Each nav button is ONE `Image` holding a baked sprite with disc + gold ring + white glyph in a single PNG (`Assets/Art/HomeScreen/Home.png` `005773ab…`, `Hole Selection.png` `654eadc4…`, …; `PersistentUI.prefab` → `BottomNavBar/Nav{Home,Gacha,Tee,Inventory,Characters}Button`). `PersistentUIManager.UpdateScreenHighlight()` sets `Image.color` to `iconActiveColor = Color.cyan` on the active slot — the whole disc, ring and glyph go cyan. `GpsNavBarHighlight` (`Assets/Scripts/UI/Gps/`) reads `iconNormalColor`/`iconActiveColor` off the live `PersistentUIManager` and does the same to the GPS bar. Figma has **no selected variant** (`New Nav Bar Buttons`, `2098:8164`, is `Property 1=Default` only; `Nav Bar Container` `2098:7988` shows five identical slots), so there is no node to match — the palette is the constraint: gold `#FCF195 → #D6AB42 @0.6 → #BB7F1D` (the project's gold stroke, `UI_ELEMENT_PALETTE.md`), navy disc, white glyph.

**After — decided: gold halo + brighter ring, white glyph.**

1. **Two baked sprites** from a new baker `Docs/Scripts/make_nav_selected.py` (the `make_daily_pill_panel.py` pattern — edit the script, never the PNG; 2× with bleed; `TextureImporterType.Sprite` forced):
   - `Assets/Art/HomeScreen/S_NavSlotGlow.png` — the disc silhouette (radius read from the `New Nav Bar Buttons` default SVG via `download_assets`, 128 px button; the Tee slot is the larger 210 px disc — bake both, `S_NavSlotGlow_128` / `_210`) in border-gold, blurred outward ~24 px, flat colour with alpha carrying the falloff — a halo, not a second outline (the pill baker's lesson, its header comment). Drawn on the SAME additive material as `S_DailyPillGlow` (`TapSparkle_Additive.mat` or the glow's own — cite which the pill uses).
   - `Assets/Art/HomeScreen/S_NavSlotRing.png` (`_128` / `_210`) — the ring only, at the ring's exact radius and width from the SVG, solid `#FCF195`, transparent elsewhere. Drawn OVER the button at alpha 1 when selected; it is what "brighter ring" means — the baked ring in `Home.png` stays untouched underneath.
2. **`NavSlotHighlight`** (`Assets/Scripts/UI/Polish/NavSlotHighlight.cs`, one per slot, added by `GamePolishBuilder`): owns a `Glow` child (behind the button Image — sibling index 0 under the button, or a child with the Image drawn first; verify draw order by capture) and a `Ring` child (after the Image), both `CanvasGroup`-driven, both at alpha 0 at rest. `SetSelected(bool, animate)` → `Fade` both to 1 / 0 over `FadeDur`; the glow additionally does ONE `Pulse` cycle (`PulseDur`, 0.7 → 1.0 → 0.7… settle at 1) on select so the change draws the eye once. `animate:false` on the first paint after boot (no motion on a cold screen). The button `Image.color` is **never** tinted any more: `UpdateScreenHighlight` calls `NavSlotHighlight.SetSelected` per slot and sets `Image.color = iconNormalColor` (white) on all five; `iconActiveColor` stays as a serialized field for prefab compatibility but is unused — mark it `[Obsolete("game_polish_a §D7 — the selected state is NavSlotHighlight")]` and say so in the report.
3. **GPS bar follows.** `GpsNavBarHighlight` stops reading `iconActiveColor`; it resolves a `NavSlotHighlight` per GPS slot (added by the GPS bar's builder path — `GpsPolishBuilder.Apply`/the hub builder — via a call into the SAME `NavSlotHighlight.Attach(Button)` helper the game builder uses, so the two bars cannot drift) and calls `SetSelected`. Rest-state parity for every GPS screen must stay 0 px EXCEPT the selected slot, whose delta is exactly the halo + ring (A2 quotes the bounding box of the diff and shows it is the slot).
4. **Colour maths on device vs Editor:** additive materials read brighter on the phone than in the Editor's Game view at some quality tiers — capture the halo at the shell's gamma/linear setting and state which; flag the on-device check for the device pass.

## Localization

No new strings. Quote `git status -- Assets/Localization` empty and the importer PLAN `add 0` if anything forced a string.

## Architecture context

- **New:** `Assets/Scripts/UI/Polish/LayeredPush.cs`, `ScreenEntryMotion.cs`, `NavSlotHighlight.cs`, `Editor/GamePolishBuilder.cs`, `Editor/GamePolishProbe.cs`, tests in `Assets/Scripts/UI/Polish/Tests/` (`LayeredPushTests`: direction table, `CanPush` false for Home / cross-pillar / different backgrounds / GPS ids / `Enabled=false`, `AllowBackgroundCrossFade` default false; `ScreenEntryMotionTests`: `SkipEntry` consumed once).
- **Touched:** `ScreenManager.cs` (the branch + `ShellScreenObject` + the second `IsPushing` guard), `InventoryScreenController.cs`, `RankingsScreenController.cs`, `GachaHistoryScreenController.cs`, `SettingsController.cs`, `SettingsMenuItem.cs`, the shell screen roots (components + `CanvasGroup`s via the builder — scene + prefabs listed in the report with `git diff --stat`).
- **Touched (D7):** `PersistentUIManager.cs` (`UpdateScreenHighlight` → `NavSlotHighlight`), `PersistentUI.prefab` (five `NavSlotHighlight` + Glow/Ring children via the builder), `Assets/Scripts/UI/Gps/GpsNavBarHighlight.cs` (authorised exception), `Docs/Scripts/make_nav_selected.py` + two/four baked PNGs.
- **Untouched:** `FadeController`, `UiMotion` public API, everything else under `Gps/`, `ModalController`.
- No asmdef change. Editor tools in `Assets/Scripts/UI/Polish/Editor/` need an Editor asmdef or an `Editor` folder rule — check how `Assets/Scripts/UI/Gps/Editor/` compiles and mirror it.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] **A1 · Invariants JSON** `Docs/Diagnostics/_capture/game_polish_a_invariants.json`, `fail == 0`, one record per push pair in the direction table (count quoted = table count), flag OFF.
- [ ] **A2 · Rest parity 0 px** on EVERY shell screen (all 14 rows of the layer table + Home + Roster + Inventory tabs ×4 + Settings open): animated arrival vs `ShowScreen(id, instant:true)` — and vs the `baseline` captures from the first commit. Non-zero → quoted with the reason.
- [ ] **A3 · Boundary untouched:** Home → any, ModeSelection → TournamentSelection, HoleSelection → Leaderboard, any → GpsHub log `[ScreenManager] Fading to`; `FadeController.cs` and `GpsScreenTransition.cs` unchanged (`git diff --stat` quoted).
- [ ] **A4 · Videos** (≥ 50 KB, drawtext-captioned, one still each in `screenshots/`): (a) Play pillar walk ModeSelection → HoleSelection → back → MissionSelection → back, then nav-bar taps Home → Gacha → Inventory → Characters → Play (the D7 selected state cross-fading); (b) TournamentSelection → TournamentLeaderboard → Leaderboard → back → back; (c) Gacha pillar GeneralShop → History → Prizes → back → back; (d) Inventory four tabs + Rankings four tabs + GachaHistory filters; (e) Settings open → expand two items → close; (f) **option (b)** ModeSelection → TournamentSelection → back with the flag ON (≈ 5 s, captioned as OFF-in-build). Cesar judges (a)–(c) as the gamble and (f) as the option.
- [ ] **A5 · Chrome is static:** on (a)–(c), a row of pixels through `PersistentUI`'s top bar and one through the nav bar, at 3 mid-push frames, mean |ΔRGB| ≤ 2 vs rest; on the same-background pushes the chrome layers' alpha logged as 1.0 every frame.
- [ ] **A6 · UI fidelity lint** delta zero on every prefab the builder touched (`fail`/`warn` before/after quoted per prefab).
- [ ] **A7 · Cross-fade table** (D3): per site — before (snap) / after (routine + duration), one mid-fade frame each.
- [ ] **A8 · Entry rise:** one mid-rise frame per screen family (Play / Tournaments / Gacha / Inventory / Roster / Home), and a log line proving a push arrival did NOT rise (`SkipEntry` consumed).
- [ ] **A9 · Flag pinned:** `AllowBackgroundCrossFade` default false by test; grep shows no production caller sets it (`grep -rn AllowBackgroundCrossFade Assets` quoted — only the test and the probe).
- [ ] **A10 · Real entry:** every transition in A1 driven from the real widget's `onClick.Invoke()` — the widget path quoted per pair (Rule 2).
- [ ] **A11 · ButtonPressFeedback** present on every Button the builder or the tab work touched (Rule 11, grep quoted).
- [ ] **A12 · EditMode** full sweep green + new suites by name.
- [ ] **A13 · Perf:** `perf` mode over video (a): isolated per-frame allocation of `LayeredPush` routines ≤ 32 B (test), in-situ worst frame and alloc quoted as an upper bound on the app, not the tween.
- [ ] **A14 · Scope:** `git status` shows no `Gps/` path, no `FadeController`, no `UiMotion.cs` signature change (`git diff Assets/Scripts/UI/Polish/UiMotion.cs` empty or comment-only).
- [ ] **A15 · Nav selected state (D7):** the two bakers' outputs in `Assets/Art/HomeScreen/` with the script quoted; a still of each pillar selected on the game bar (5) and the GPS hub selected slot (1); `grep -rn "iconActiveColor" Assets/Scripts` shows NO runtime read (only the obsolete field + its tooltip); `Image.color` on all five nav Images logged as white on every screen; the cross-fade + single pulse visible in video (a); `GpsNavBarHighlight` diff quoted in full (the only GPS-folder change).
- [ ] **A16 · Deviations** flagged with justification.

## Smoke evidence

Videos (a)–(f) + stills; A1 JSON; A2 parity table; A5 numbers; A7 table.

## Out of scope (do NOT do these)

- Modal pops, the three retrofits, count-ups, staggers, shimmer, `PendingSpend`, card selection bumps — **`game_polish_b`**.
- Scroll-feel, safe-area, Rubik Medium, `ButtonPressFeedback` sweep beyond touched buttons — **`game_polish_c`**.
- Shipping option (b); any Settings toggle for motion; haptics; `UiMotion` API changes; anything under `Gps/`; `FadeController`; new screens.
