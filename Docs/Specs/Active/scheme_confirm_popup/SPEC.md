# SPEC — `scheme_confirm_popup`

**Status:** SPEC_READY (2026-09-05). Control-schemes track, after all three schemes shipped (`scheme_freeswing` DONE `4ae3307d9` / `9ce9d0bb9`). Cesar 2026-09-05: "a pop-up explaining each control system when selected, with a Confirm and Cancel button; it can use images" → layout "B with C explanation" chosen; images must be centred; CONFIRM is the **gold** button.
**Figma:** file `5gEAHjl6xAtW8iYY7NMvWd`, page "Shot Controls — Schemes", section **5 — Scheme confirm pop-up (options)** (`14138:35577`): **PENDULUM (chosen layout D)** `14140:35361`, **TAP TIMING** `14145:36487`, **FREE SWING** `14145:37377`, **FLICK** `14145:39195`. A/B/C (`14138:35578`, `14138:104023`, `14138:104637`) are rejected explorations — do not build them. Renders in `reference/`.
**One line:** tapping a control scheme that is not the current one — in Settings › Controls or the in-game gear modal — opens a pop-up with the scheme's name, three step tiles, a HOW IT WORKS list and CANCEL / CONFIRM; the scheme only changes on CONFIRM.

---

## 1. Behaviour

1. **Trigger.** `ControlsSubmenu.OnSchemeSelected` and `InGameSettingsModalController.OnSchemeSegmentTapped` no longer call `ControlSchemeService.Set` directly. If `scheme == ControlSchemeService.Current` → no-op (no pop-up). Otherwise → `SchemeConfirmModal.Instance.Show(scheme, source)` where `source` is `"settings"` / `"ingame"` (the same string `Set` already takes). The row/segment highlight stays on the **current** scheme while the pop-up is open.
2. **CONFIRM** → `ControlSchemeService.Set(scheme, source)`, `Hide()`. The existing `OnSchemeChanged` repaint paths update both surfaces; the host swaps at the next Idle as today.
3. **CANCEL**, the backdrop, or the Android back → `Hide()`, nothing changes. No "don't show again" — Cesar asked for it on every selection.
4. **Content** (Figma D): gold title = the scheme's Settings label (`SETTINGS_CONTROLS_*`); three 314×340 tiles with captions `1 …` / `2 …` / `3 …`; gold `HOW IT WORKS` header + three numbered lines; muted footer "Works with every club. You can switch back any time in Settings."; silver CANCEL (`MODAL_CANCEL`) + **gold** CONFIRM (`MODAL_CONFIRM`, `Main Buttons / Gold, Enabled=Yes` — not Copper).
5. **In-game** the pop-up stacks above the gear modal (its own canvas sort order above `InGameSettingsModal`, like that modal's own QUIT confirm dialog); the gear modal stays open underneath and its segment row repaints on CONFIRM.
6. The scheme change still applies at the next Idle if a swing is in progress (host rule) — the pop-up does not need to know.

## 2. Non-goals
Per-scheme tutorials / first-shot hints (backlog); a "don't show again" toggle; changing the Settings row / segment visuals; any driver change.

## 3. Design

### 3.1 `SchemeConfirmModal` (`Assets/Scripts/UI/Modals/SchemeConfirmModalController.cs : ModalController`, prefab `Assets/Prefabs/UI/Modals/SchemeConfirmModal.prefab`)
- **Clone source (Rule 19):** `Assets/Prefabs/UI/Modals/StartingCharacterConfirmModal.prefab` — the shipping `ModalController` with a backdrop, a navy panel and a CANCEL/CONFIRM `Main Buttons` pair; re-skin the panel to the Figma `Pop-up` node (`4192:31365` style: navy gradient, silver 3 px stroke, r 40) and swap the CONFIRM button variant to Gold. Panel is vertical auto-layout, width 1086, hug height (Figma D measures 1180); centred; 48 px inner padding; rows: title, `Steps` (3 tiles + captions, centred, 36 px gap under the title separator, 12 px under the captions), `HowItWorks` (gold header 36 SemiBold, three `1|2|3` rows: gold Rubik Bold 36 index + white Rubik Medium 34 text, auto-height), footer (Rubik Medium 34 white 75 %), `Buttons` (450×120 each, gap 48).
- **Two instances:** one in `ShellScene` under the Settings canvas (the Settings screen's modal layer, above `SettingsController`'s panel), one in `LabScaffold` under `ShotUI_Canvas` with a sorting order above `InGameSettingsModal`. Both are the same prefab; `Instance` resolves to the one in the active scene (`FindObjectOfType` on `Show` is fine — two scenes are never loaded together; NOTE if they are).
- **Content table** `SchemeConfirmContent` (static, in the same file or `Controls/`): per `ControlScheme` → title key, three tile sprites (Resources path), three caption keys, three line keys. Everything player-facing is a `LocalizedText` key; the numbers `1 2 3` are typography, not strings.
- `Show(ControlScheme scheme, string source)`: bind title/tiles/captions/lines from the table, remember `(scheme, source)`, `base.Show()`. `OnConfirm` → `ControlSchemeService.Set(scheme, source)` → `Hide()`. `OnCancel`/`closeButton`/backdrop → `Hide()`.
- Telemetry: `controls_scheme_changed` already fires from `Set`; add `where: "settings_popup" | "ingame_popup"` by passing those as `source` — the dashboard filter already groups on the string (check it does not enumerate; if it does, add the two values to the `DICT`).

### 3.2 Tile images — captured from the running game, not exported from Figma
The Figma tiles are crops of design frames; the shipped tiles must show the **built** UI (the Flick frames in Figma are a static pose and don't show a pull, the arrows or the flick at all). Twelve PNGs, `Assets/Resources/UI/Controls/Tiles/T_<Scheme>_<1|2|3>.png`, **314×340 at the 1170×2532 reference** (628×680 @2× source, imported as a sprite, mip-off, like the other UI sprites), captured by a new Editor menu `GOLFIN ▸ Capture ▸ Scheme Confirm Tiles` built on the existing scheme verify bots (`PendulumSchemeVerify` / `NeedleSchemeVerify` / `FreeSwingSchemeVerify` drive the states already; add the same three states for Flick through `ClubHandleDragger`'s external-drag path + `ShotConeTestDriver`):

| Scheme | Tile 1 | Tile 2 | Tile 3 |
|---|---|---|---|
| Flick | PULL — handle 70 % down the cone | AIM & TIME — arrow inside the green band | FLICK UP — `Resolving`, ball just launched, handle gone |
| Pendulum | PULL — 100 % lane, club on the gold tick | TIME IT — marker on the pip | FLICK UP — JUST! pop over the bar |
| Tap Timing | PULL — 100 % ring, club on it, crescent visible | TAP — needle in the blue zone, TAP! hint | RESULT — PERFECT pop + pip on the arc |
| Free Swing | BACKSWING — club at 100 %, trace straight down | SWING UP — trace curving back up, IMPACT window | RESULT — analyzer chip (POWER 100 % · IMPACT 0 · STRAIGHT · GOOD) |

Crop rule (the one the Figma frames use): centre the crop on the **bounding box of the subject elements** measured off the live `RectTransform`s (lane/bar/arc/chip + club head + grade pop where present), scale to fit with a 10 % margin, then resample to the tile size. No HUD chrome (top bar, action buttons) may appear in a tile — assert on the crop bounds. Captures happen with the flick/scheme UI at its committed colours (no lint snapshot). The menu writes the PNGs and a `tiles_manifest.json` (scheme, step, source frame, crop rect) into the spec folder. **Re-run the menu whenever a scheme's UI changes** — note this in `controls.csv` header comments next to the scheme keys.

### 3.3 Strings — importer path, EN + JA in the same commit, then `Import Text CSV` with a forced CSV reimport
Shared:
| key | EN | JA |
|---|---|---|
| `SCHEME_POPUP_HOW` | HOW IT WORKS | 操作方法 |
| `SCHEME_POPUP_FOOTER` | Works with every club. You can switch back any time in Settings. | どのクラブでも使えます。設定からいつでも戻せます。 |

Captions (`SCHEME_POPUP_<S>_STEP<n>`) and lines (`SCHEME_POPUP_<S>_LINE<n>`), `<S>` ∈ FLICK / PENDULUM / NEEDLE / FREESWING:

| key | EN | JA |
|---|---|---|
| `…_FLICK_STEP1` | PULL | 引く |
| `…_FLICK_STEP2` | AIM & TIME | 狙い＆タイミング |
| `…_FLICK_STEP3` | FLICK UP | フリック |
| `…_FLICK_LINE1` | Pull the club back down the cone for power — the further, the harder. | コーンに沿ってクラブを引くほどパワーが上がります。 |
| `…_FLICK_LINE2` | Slide left or right inside the cone to fine-tune your aim. Arrows run up the cone: green is perfect timing. | コーン内で左右に動かして狙いを微調整。矢印が上がり、緑がベストタイミングです。 |
| `…_FLICK_LINE3` | Flick up to hit. Wait too long and your aim starts to wander. | 上にフリックで打ちます。待ちすぎると狙いがブレます。 |
| `…_PENDULUM_STEP1` | PULL | 引く |
| `…_PENDULUM_STEP2` | TIME IT | タイミング |
| `…_PENDULUM_STEP3` | FLICK UP | フリック |
| `…_PENDULUM_LINE1` | Pull the club straight back — further is more power, past the gold line is overpower. | クラブをまっすぐ引くほどパワーが上がり、金線を越えるとオーバーパワーです。 |
| `…_PENDULUM_LINE2` | A marker swings across the bar. Better Club Control makes it slower. | マーカーがバーを往復します。クラブコントロールが高いほど遅くなります。 |
| `…_PENDULUM_LINE3` | Flick up when it hits the red centre. Green = JUST, amber = GOOD, outside = MISS. | 赤い中心に来た瞬間に上へフリック。緑＝ジャスト、黄＝グッド、外＝ミス。 |
| `…_NEEDLE_STEP1` | PULL | 引く |
| `…_NEEDLE_STEP2` | TAP | タップ |
| `…_NEEDLE_STEP3` | RESULT | 結果 |
| `…_NEEDLE_LINE1` | Pull the club back inside the circle for power and let go — past the gold ring is overpower. | 円の中でクラブを引いて離します。金の輪を越えるとオーバーパワーです。 |
| `…_NEEDLE_LINE2` | A needle sweeps across the arc. Tap anywhere to stop it; higher Club Control makes it slower. | 針がアークを一度だけ動きます。どこでもタップで止めます。クラブコントロールが高いほど遅くなります。 |
| `…_NEEDLE_LINE3` | Blue zone = PERFECT. Early hooks left, late slices right. No tap before the end = shank. | 青＝パーフェクト。早いとフック、遅いとスライス。止めないとシャンクです。 |
| `…_FREESWING_STEP1` | BACKSWING | バックスイング |
| `…_FREESWING_STEP2` | SWING UP | 振り上げ |
| `…_FREESWING_STEP3` | RESULT | 結果 |
| `…_FREESWING_LINE1` | Drag the club straight down — deeper is more power, past the gold line is overswing. | クラブをまっすぐ下へ。深いほどパワーが上がり、金線を越えるとオーバースイングです。 |
| `…_FREESWING_LINE2` | Drag back up through the IMPACT line; the shot fires as you cross it. A straight path flies straight, an angled one shapes a draw or fade. | インパクト線を上へ通過した瞬間に打ちます。まっすぐならストレート、斜めならドローやフェードになります。 |
| `…_FREESWING_LINE3` | Tempo counts: a smooth, quick upswing keeps full power. Slow or wobbly costs distance and accuracy. | テンポが大事。速く滑らかに振り上げればフルパワー。遅い・ブレると飛距離と精度が落ちます。 |

Titles reuse `SETTINGS_CONTROLS_FLICK / PENDULUM / TAPTIMING / FREESWING`; buttons reuse `MODAL_CANCEL` / `MODAL_CONFIRM`. Zero hardcoded `.text`.

## 4. Files (expected)
- `Assets/Scripts/UI/Modals/SchemeConfirmModalController.cs` + `SchemeConfirmContent.cs` (new); `Assets/Prefabs/UI/Modals/SchemeConfirmModal.prefab` (new, cloned)
- `Assets/Scripts/UI/ControlsSubmenu.cs`, `Assets/Scripts/UI/Modals/InGameSettingsModalController.cs` (route through the modal)
- `ShellScene.unity`, `LabScaffold.unity` (one instance each)
- `Assets/Editor/ShotUI/SchemeConfirmTilesCapture.cs` (new menu), 12 PNGs under `Assets/Resources/UI/Controls/Tiles/`, `tiles_manifest.json` in the spec folder
- `LocalizationText.csv` (26 keys EN+JA); dashboard `DICT` only if the `where` filter enumerates values
- Art: panel/button sprites are the clone source's; nothing else new

## 5. Tests (EditMode)
1. `SchemeConfirmModalTests`: `Show(Pendulum)` binds the four content slots from the table (title key, 3 sprites non-null, 3 caption keys, 3 line keys); CONFIRM calls `Set(scheme, source)` exactly once and hides; CANCEL / close / backdrop hide without `Set`; `Show` for the current scheme is never reached (the callers no-op — tested on `ControlsSubmenu` and the in-game controller with a fake service).
2. Content table completeness: every scheme has 3 sprites that resolve from Resources and 12 keys that exist in the localisation table (guards a missing tile or key at build time).
3. Tile capture: the manifest lists 12 crops, each 628×680, each crop rect inside the shot area (no HUD chrome).

## 6. Acceptance
- Settings › Controls: tapping a different scheme opens the pop-up over the Settings panel; the row highlight stays on the current scheme; CANCEL leaves it; CONFIRM moves it and the game uses the new scheme on the next shot. Tapping the current scheme does nothing.
- In-game gear modal: same, stacked above the modal; on CONFIRM the segment row repaints and the swap lands at the next Idle.
- All four pop-ups match Figma section 5 frames D / TAP TIMING / FREE SWING / FLICK (measured: panel width 1086, tile row centred 48/48, 36 px gap under the separator, gold CONFIRM tint read off the live Image); the tiles are the in-game captures, centred on their subject, no HUD chrome.
- `--check` clean + table read-back; zero hardcoded `.text`; `controls_scheme_changed` carries `where=settings_popup|ingame_popup`.
- 1170×2532 and 16:9: the panel fits with the buttons reachable (Free Swing has the longest copy — measure it).
- Device pass (Cesar) on both surfaces.

## 7. Out of scope → backlog
Per-scheme first-shot hint/tutorial (already listed); "don't show again".
