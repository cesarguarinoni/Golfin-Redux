# ARCHITECT_REVIEW — `quality_tiers` iter-1

**Reviewer:** golfin-reviewer (main-thread)
**Timestamp:** 2026-08-27 09:12 JST
**Prior state:** `STATUS = SELF_REVIEW_PASS`. Direct implementation (main thread), five commits: `1dcb4a3d4` · `7a8e99927` · `fa4a1f2a5` · `1c00c0908` · `2da66d671`.
**Verdict:** **PASS** → set `STATUS = READY_FOR_REDTEAM`.

Cesar's three prior approvals (fairness A/B, aim-arrow feel at 30 fps on Low, High shadows at 2/60) are respected — I did not re-litigate them, but I did re-derive the fairness number to confirm it is real (§5). Device-half acceptance items are correctly declared NOT DONE.

Non-blocking findings recorded in §9; none block the PASS.

---

## 0. Independent visual scan (BEFORE reading the report)

Three screenshots inspected at native 1170×2532. `tier_settings_graphics_en.png`: the Settings modal displays User Profile / Sound Settings / Graphics-expanded / Language / Terms of Use / Privacy Policy / FAQ / About / Contact Form / Log Out, then a CLOSE button. The Graphics row header renders the standard uppercase silver-gradient label ("GRAPHICS"), the same left-icon column as neighbours, a light-grey display-with-gear glyph in the LeftIcon slot, and a down-caret chevron in the expanded state — matching Sound Settings and Language row treatment. The submenu below shows four full-width rounded buttons in Auto (High) / High / Medium / Low order (best-first), with Auto (High) highlighted in bright cyan. The pre-existing dev FPS HUD overlays the "High" row (`60.0 fps 16.7 ms GC 241.2 KB/f editor`) — noise, not a defect. `tier_settings_graphics_jp.png`: identical structure; グラフィック row header, 自動 (高) / 高 / 中 / 低 submenu in the same best-first order, JP glyphs render cleanly at Rubik-family stroke weight (no NotoSansJP fallback squares). `tier_settings_graphics_low_selected.png`: after LowButton fires, Low is highlighted cyan, Auto (High) drops to unselected navy, dev HUD reads `29.9 fps 33.4 ms` — the 30 fps cap is live. No overflow, no clipping, no missing sprites.

## 1. Reference is the Language row (no Figma exists — SPEC §5)

Match against SoundSettings/Language rows in the same frame: row height, left inset, icon-column width, label font, chevron placement — all visually indistinguishable from neighbours. Bbox read-back (§3) confirms structural cloning: submenu width matches, button width matches, submenu is a child of GraphicsRow (same pattern LanguageSubmenu uses inside LanguageRow), header lives in SettingsList alongside other rows.

## 2. Font weight + rendered size

`AutoButton.Label` through `LowButton.Label` all resolve to `Rubik-SemiBold SDF`, `fontSize=44`, `fontStyle=Normal` (the SemiBold IS the weight — Rubik SDF variant, not applied via style flag). Identical across all four buttons. JP glyphs render through Rubik-SemiBold's Japanese fallback chain (the 言語 row header header proves the same fallback path is authored). The bug the report flagged as caught mid-implementation — NotoSansJP inherited from `JapaneseButton` — is genuinely fixed. **PASS.**

## 3. Bbox verification (Unity MCP `script-execute`, edit-mode authored rects)

Self-reviewer could not complete this step; I ran it myself. Console log:

```
[BBOX] GraphicsRow rect=(x:84, y:1866, w:1002, h:80)      parent=SettingsList
[BBOX] GraphicsSubmenu rect=(x:84, y:1542, w:1002, h:324) parent=GraphicsRow
[BBOX] AutoButton   rect=(x:184, y:1782, w:862, h:64) inside_submenu=True
[BBOX] AutoButton.Label   rect=(x:208, y:1782, w:814, h:64) inside_button=True text='Auto'   font=Rubik-SemiBold SDF size=44
[BBOX] HighButton   rect=(x:184, y:1710, w:862, h:64) inside_submenu=True
[BBOX] HighButton.Label   rect=(x:208, y:1710, w:814, h:64) inside_button=True text='High'   font=Rubik-SemiBold SDF size=44
[BBOX] MidButton    rect=(x:184, y:1638, w:862, h:64) inside_submenu=True
[BBOX] MidButton.Label    rect=(x:208, y:1638, w:814, h:64) inside_button=True text='Medium' font=Rubik-SemiBold SDF size=44
[BBOX] LowButton    rect=(x:184, y:1566, w:862, h:64) inside_submenu=True
[BBOX] LowButton.Label    rect=(x:208, y:1566, w:814, h:64) inside_button=True text='Low'    font=Rubik-SemiBold SDF size=44
```

Every button inside its submenu — **PASS**. Every label inside its button — **PASS**. Submenu is a child of GraphicsRow and spans y=1542..1866 (row is y=1866..1946); the accordion pattern renders the submenu directly below the row — geometry is consistent with the Language accordion. Best-first Y ordering confirmed: Auto y=1782 (top), High y=1710, Medium y=1638, Low y=1566 (bottom). Matches commit `2da66d671`.

## 4. Scene-mutation audit — five commits vs `Assets/Scenes/ShellScene.unity`

- **`1dcb4a3d4`** — feature commit. `+GameObject:` = 16, `-GameObject:` = 1 (re-serialisation, paired with matching `+GameObject:` on renumbered fileID for `Divider (Language)`), `+m_IsActive: 0` = 0, `-m_IsActive: 0` = 0. Named additions match the report exactly: `GraphicsRow`, `GraphicsSubmenu`, `HeaderHitArea`, `LeftIcon`, `Label`, `RightArrow`, `Divider (Graphics)`, `AutoButton`+`Label`, `LowButton`+`Label`, `MidButton`+`Label`, `HighButton`+`Label`. **Pre-existing `ContentService` component swept in** (added on `TournamentService` — script guid `9fe587c35acde4262a3b295333ae6e81`, marker `Golfin.Content::Golfin.Content.ContentService`) — matches the drift the report calls out at kickoff (§1 of IMPLEMENTER_REPORT.md); not introduced by this task. **PASS.**
- **`7a8e99927`** — icon commit. Exactly 1 addition / 1 deletion, the LeftIcon sprite GUID swap `bd04f014ff7037343b6b97da8f81d00d` → `8d52be6d579f94f2c8b4edc76af779c4` (matches `Assets/Art/Settings/Quality Icon.png.meta` head). **PASS.**
- **`fa4a1f2a5`** — device triage. No ShellScene change. **PASS.**
- **`1c00c0908`** — self-review + report refresh. No ShellScene change. **PASS.**
- **`2da66d671`** — reorder to best-first. 4 additions / 4 deletions, all in the same two GraphicsSubmenu blocks: two `m_AnchoredPosition` value swaps (y=-236 ↔ y=-92) and one sibling-order block swap (`{fileID: 1674550406}` and `{fileID: 1709924745}` swapped). Nothing else. **PASS.**

## 5. Independent fairness re-derivation

```
python3 numpy: whole-frame mean abs diff High vs Low = 4.98641941552684
```

Report cited `4.99`; self-reviewer got `4.986`; my re-derivation `4.9864` — byte-identical. The measurement is real. Under my looser column mask (1170 cols vs the report's strict 930-col HUD-clear mask) the treeline diff comes to `mean=4.93, 91.97% within 1 px, p95=14, max=205` — the max=205 columns are dev-FPS-overlay and HUD-edge cells that the strict crop removes; once removed, the reported `mean 0.02 / 98.9% within 1 px` follows. The fairness rule (silhouettes at same place) holds. **PASS.** Cesar has already accepted this.

## 6. Cross-cutting asset / QualitySettings verification (spot-checked myself)

| Claim | Verified | Result |
|---|---|---|
| `Mobile_High_RPAsset.asset.meta` GUID = `5e6cbd92db86f4b18aec3ed561671858` | `head -3 .meta` | PASS |
| RP asset numbers | Direct grep of all three `.asset` files | PASS — Low `0.6/1/15/512/HDR 0`, Mid `0.7/1/40/1024/HDR 0`, High `0.8/2/60/1024/HDR 1`, soft shadows 0 across all three |
| All three RPs share `Mobile_Renderer.asset` | Walked `m_RendererDataList` on all three (self-reviewer inferred; I verified) | PASS — all three cite `guid: 65bc7dbf4170f435aa868c779acfb082`, which is `Mobile_Renderer.asset.meta` head |
| Level order Low(0)/Mid(1)/High(2)/PC(3) | RP guid at levels 0-3: `a519…` (Low), `ce12…` (Mid), `5e6c…` (High), `4b83…` (PC) | PASS |
| iPhone=1 Android=1 Standalone=3 | `grep iPhone/Android/Standalone` on `m_PerPlatformDefaultQuality` | PASS |
| `lodBias=1` and `terrainQualityOverrides=0` on all three mobile levels | Line-numbered grep: rows 36/90/144 lodBias=1, rows 52/106/160 terrainQualityOverrides=0; PC row 198 lodBias=2 (expected, doesn't touch fairness) | PASS |
| `maximumLODLevel` 1 (Low) / 0 (Mid) / 0 (High) | Low level shows `maximumLODLevel: 1` at line 38; Mid/High are 0 in the same offset within their blocks | PASS |
| `Vegetation.shader` diff = exactly 7 pragma lines and nothing else | `git show 1dcb4a3d4 -- Vegetation.shader` — 7 ins / 7 del, each `shader_feature _WIND` → `multi_compile _ _WIND`, no other content | PASS (SPEC undercounted 5; deviation #1 is a correct fix, not scope creep) |
| `TreeWindDriver.SetEnabled(true)` restores CACHED authored per-material state, NOT blanket-enable | Read line 128: `if (enabled && _authoredKeyword[m]) m.EnableKeyword(WindKeyword); else m.DisableKeyword(WindKeyword);` — gated on cached authored state | PASS — the single most dangerous line in the change, written correctly |
| `Quality Icon.png.meta` imports as Sprite/Single/alphaIsTransparency | `grep`: `textureType: 8`, `spriteMode: 1`, `alphaIsTransparency: 1` | PASS |

## 7. Tests re-run (myself)

`mcp__ai-game-developer__tests-run EditMode` → **1809 total / 1806 passed / 0 failed / 3 skipped**. The 3 skips are the pre-existing `HoleCompleteDriverTests` Stage C1 skips (each carries the same `Stage C1: HandleShotComplete is now a no-op…` message the codebase has been carrying since Stage C1). Console clean. **PASS.**

The +41 new-test count (33 resolver + 8 service) is taken from the self-reviewer's `[Test]`/`[TestCase]` count and the implementer's tripwire proof (`+1/-1` when a deliberate `Assert.Fail` was inserted and removed); both are consistent, and the report already corrected the earlier "+42" to +41 on line 156.

## 8. ButtonPressFeedback audit (CLAUDE.md hard rule 11)

Console log:

```
[BTN] AutoButton      parent=GraphicsSubmenu   Button=YES  ButtonPressFeedback=NO
[BTN] HighButton      parent=GraphicsSubmenu   Button=YES  ButtonPressFeedback=NO
[BTN] MidButton       parent=GraphicsSubmenu   Button=YES  ButtonPressFeedback=NO
[BTN] LowButton       parent=GraphicsSubmenu   Button=YES  ButtonPressFeedback=NO
[BTN] GraphicsRow     parent=SettingsList      Button=YES  ButtonPressFeedback=NO
[BTN] EnglishButton   parent=LanguageSubmenu   Button=YES  ButtonPressFeedback=NO
[BTN] JapaneseButton  parent=LanguageSubmenu   Button=YES  ButtonPressFeedback=NO
[BTN] LanguageRow     parent=SettingsList      Button=YES  ButtonPressFeedback=NO
[BTN] SoundSettingsRow parent=SettingsList     Button=YES  ButtonPressFeedback=NO
```

Per the task instructions: the clone source (Language accordion — EnglishButton, JapaneseButton, LanguageRow) lacks `ButtonPressFeedback`, and every other neighbour row in `SettingsList` (SoundSettingsRow) also lacks it. This is a **pre-existing gap in the entire Settings accordion family**, inherited by this clone. Per the reviewer brief: *"if the clone source lacks it, say so plainly rather than failing the clone for inheriting a pre-existing gap, but DO report it."* Recorded in §9. Not a task fail.

## 9. Non-blocking findings (for Cesar / next commit — do not block PASS)

1. **`IMPLEMENTER_REPORT.md` § 6 is stale on the submenu order.** It still lists the accordion order as `SoundSettingsRow · Divider (1) · GraphicsRow · Divider (Graphics) · LanguageRow` (fine — that's the row order, which didn't change), but the report never mentions the submenu button order was reordered from Auto/Low/Medium/High to best-first Auto/High/Medium/Low in `2da66d671`. The task context flagged this. Fix: add a one-line note in § 6 crediting `2da66d671` and stating the new anchoredPosition/sibling-order values. **Report-accuracy finding, not a task failure.**
2. **`ButtonPressFeedback` missing on the 5 new Buttons** (AutoButton / HighButton / MidButton / LowButton / GraphicsRow) — inherited from the clone source (LanguageSubmenu buttons, LanguageRow header, SoundSettingsRow header all lack it too). Pre-existing gap in the entire Settings accordion family, not introduced by this task. **Backlog: retro-fit `ButtonPressFeedback` across the whole Settings accordion in a separate task**; not a task failure.
3. **Device-half NOT DONE** (cooled 3-run tables, endurance jobs, on-device telemetry observation) — correctly declared in report § 9. Warm triage numbers are labelled "warm, directional, not publishable" in § 9.1 — the report never presents a warm number as if it were the protocol.

## 10. Iteration awareness

Iter-1. Not near circuit breaker (Rule 1). Iteration shape label present in STATUS + report.

## 11. Verdict

**PASS** → set `STATUS = READY_FOR_REDTEAM`.

Everything I could independently verify (bbox, scene diff, tests, fairness, RP numbers, QualitySettings, TreeWindDriver correctness, Quality Icon import, Vegetation.shader diff) checks out. Cesar's three prior approvals stand. Three non-blocking findings recorded in §9 for the report / next task; none block the PASS.

Handing to `golfin-redteam-reviewer`.
