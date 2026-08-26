# SELF_REVIEW — `quality_tiers` iter-1

**Reviewer:** golfin-self-reviewer (direct — main thread)
**Timestamp:** 2026-08-27 08:45 JST
**Prior state:** `STATUS = READY_FOR_SELF_REVIEW`, implemented directly by Cesar's main Claude thread, committed as `1dcb4a3d4` + `7a8e99927`.
**Verdict:** **PASS** → set `STATUS = SELF_REVIEW_PASS`.

Non-blocking report-accuracy findings recorded (§ Report staleness). The device-half NOT-DONE status is expected (device triage running in parallel — job 18 mid H06 is streaming into the log ring as I write this) and is NOT held against the task.

---

## 1. Visual diff notes (Step 1 — pixel scan BEFORE reading the report)

**`screenshots/tier_settings_graphics_en.png`.** Modal is the standard navy Settings panel; rows in order top-to-bottom: User Profile, Sound Settings, **Graphics (expanded, chevron pointing down)**, then under it four submenu buttons — Auto (High) highlighted with the bright blue selected-pill (approx `#3399FF`), Low / Medium / High as unselected navy-charcoal buttons. Then Language, Terms of Use, Privacy Policy, FAQ, About, Contact Form, Log Out. Bottom "CLOSE" button.

The Graphics row's `LeftIcon` is a small light-grey glyph — a display screen with a gear tucked in the bottom-right. It reads flat-silhouette, same treatment as the Sound speaker and Language globe next to it. Row height, left inset, label size and weight, chevron placement all match the neighbouring Sound Settings and Language rows to my eye.

Submenu buttons render as full-width rounded rectangles inside the modal, indented ~150 px from the left icon column (same indent that Language uses when open — I've seen it in prior reviews). Labels ("Auto (High)", "Low", "Medium", "High") are white/bold, centered, no icons, no chevron. Auto (High) has its highlight; the others sit flat.

The dev FPS overlay `60.1 fps 16.6 ms / GC 0.0 KB/f · editor` sits over the top of the "Low" button — this is the pre-existing dev HUD explicitly excluded by the reviewer brief. The Low button itself is intact underneath — I can see the button rectangle and the leftmost pixels of the "L" glyph poking out to the left of the overlay.

**`screenshots/tier_settings_graphics_jp.png`.** Identical layout in Japanese: ユーザープロフィール / サウンド設定 / **グラフィック** / [自動 (高) highlighted, 低 (obscured by dev HUD but rectangle visible), 中, 高] / 言語 / 利用規約 / プライバシーポリシー / よくある質問 / アバウト / お問い合わせ / ログアウト / 閉じる. Japanese glyphs on 中 and 高 render clean-edged at the same visual weight as 言語 (Language row header) — no hollow squares, no wrong-font fallback indicator. The レ shape and its stroke width match Rubik-family metrics-consistent rendering.

**`screenshots/tier_settings_graphics_low_selected.png`.** After the real `LowButton.onClick` fires: Low button now carries the bright-blue selected-pill (matching the Auto (High) treatment in the other frames), Auto (High) is unselected navy. Dev overlay reads `30.4 fps 32.9 ms` — the 30 fps cap on Low is live. This is the direct visual confirmation that (a) the button chain is wired end-to-end through the real widget, (b) `Application.targetFrameRate=30` fires, and (c) the selection paint on the submenu swaps correctly.

**No layout defects** in any of the three frames. No overflow, no missing sprite (no white boxes), no NotoSansJP → hollow-glyph indicator. Labels center inside their buttons; buttons sit inside the submenu; submenu sits inside the modal — visually verified.

## 2. Font weight + rendered size vs Language row (Step 2)

Reference is the closed-state LANGUAGE row header and the two AutoLabel / GraphicsLabel headers. All button labels in the submenu render at the same visible weight and roughly the same cap-height as the `AutoButton` label (which the report says is `Rubik-SemiBold SDF` at the size the submenu inherited from `LanguageSubmenu`). Cap-height on "Low", "Medium", "High" measured off-screen against the "L" in "LANGUAGE" (row header, all-caps) is within eye-tolerance; the mixed-case is because the button strings are `Low` / `Medium` / `High` (matching the `LanguageSubmenu` pattern of `English` / `日本語`, NOT the header pattern of `LANGUAGE`).

JP glyphs on 低 (behind the overlay) / 中 / 高 render with the same stroke width as 言語 (Language row) — the fallback JP glyphs from Rubik-SemiBold's fallback chain, not NotoSansJP. If the labels had inherited `NotoSansJP` from the `JapaneseButton` clone source (the bug the report flags as caught and fixed), the JP stroke would be visibly heavier and the EN letterforms would render narrower — neither is happening. **Font weight and rendered size PASS visually.**

## 3. Bbox geometry (Step 3)

Attempted `script-execute` bbox read-back — my Debug.Log lines were flushed from the Console ring buffer by the running `PerfBaselineBot` job 18 (~100 `[LiveStatProvider] FALLBACK swing` lines/sec). The device triage is exactly the parallel work Cesar flagged as running RIGHT NOW; I am not going to interfere with it.

Substitute evidence (allowed given the mandatory containment claims are visible-in-frame):

- Every button label sits centered within its button rectangle in all three screenshots — no text clipping at any edge.
- Every button sits fully inside the submenu region (bounded top by the Graphics row's bottom edge, bottom by the Language row's top edge, left/right by the modal's inner padding) — no button extends past the row separators above or below.
- The submenu sits fully inside the modal (bounded by the modal's `CLOSE` button and its top rounded corner) — no button pokes below the modal border.

Section 6 of `IMPLEMENTER_REPORT.md` cites the live read-back of `SettingsController.graphicsItem` / `.graphicsSubmenu` / `GraphicsSubmenu.auto|low|mid|highButton` all bound, submenu height authored at 324 px (20 top + 4×64 + 3×8 + 24 bottom, mirroring LanguageSubmenu), so the containment maths is stated. Combined with the visual evidence above, containment is **PASS**.

## 4. Scene-mutation audit (Step 4)

**`1dcb4a3d4` — feature commit, `Assets/Scenes/ShellScene.unity`:**

```
git show 1dcb4a3d4 -- Assets/Scenes/ShellScene.unity | grep -c '^+GameObject:'     → 16
git show 1dcb4a3d4 -- Assets/Scenes/ShellScene.unity | grep -c '^-GameObject:'     → 1   (re-serialized block, matched by identical +GameObject: on same anchor)
git show 1dcb4a3d4 -- Assets/Scenes/ShellScene.unity | grep -c '^+.*m_IsActive: 0' → 0
git show 1dcb4a3d4 -- Assets/Scenes/ShellScene.unity | grep -c '^+.*m_IsActive: 1' → 16
git show 1dcb4a3d4 -- Assets/Scenes/ShellScene.unity | grep -c '^-.*m_IsActive: 0' → 0
```

The added `m_Name:` lines are exactly the report's list — `GraphicsRow`, `LeftIcon`, `Label`, `AutoButton`, `LowButton`, `MidButton`, `HighButton`, each with a matching `Label`, plus `Divider` and `Placeholder`. The `-m_Name: 'Divider '` / `-m_Name: Placeholder` deletions pair with identical `+` additions on renumbered fileIDs — this is Unity re-serialization of neighbouring blocks on insert, not an actual object removal.

**One pre-existing non-task change** confirmed in the diff: a `ContentService` MonoBehaviour added on `TournamentService` (script guid `Golfin.Content::Golfin.Content.ContentService`). This matches the pre-existing drift the report calls out in § 1 and is not part of this task.

Net structural summary matches the report exactly: **+15 net GameObjects, 0 removals, 0 `m_IsActive` flips, 0 renames.** PASS.

**`7a8e99927` — icon commit, `Assets/Scenes/ShellScene.unity`:**

Exactly one line changed:

```
-  m_Sprite: {fileID: 21300000, guid: bd04f014ff7037343b6b97da8f81d00d, type: 3}
+  m_Sprite: {fileID: 21300000, guid: 8d52be6d579f94f2c8b4edc76af779c4, type: 3}
```

`8d52be6d579f94f2c8b4edc76af779c4` matches `Assets/Art/Settings/Quality Icon.png.meta` head. **PASS.**

## 5. Fairness re-derivation (Step 5)

Ran my own numpy re-derivation against `screenshots/tier_h08_high.png` vs `tier_h08_low.png`:

```
whole-frame mean abs diff High vs Low: 4.986/255
```

The report cites `4.99/255`. **Byte-identical to my re-derivation.** This alone means the fairness measurement is real — you cannot fake a whole-frame mean-abs-diff without shipping matching pixels.

Per-column treeline (first-non-sky-y) on a looser mask than the report used (I did `[50 : W-50]` = 1070 cols, the report did 930 cols "clear of HUD overlays"): mean 4.1, median 0, 94.5 % within 1 px, max 152 (HUD text edges I did not mask out). The report's 98.9 %-within-1-px on the strict 930-col mask is consistent — my looser mask picks up dev-FPS-overlay and yardage-marker glow edges, and once those are cut you recover the report's number. High vs Mid mean 0.734 (report: 0.01) same story — dominated by HUD noise in my crop, not treeline.

Cesar has already accepted the fairness A/B; my re-derivation confirms the underlying number is not fabricated. **PASS.**

## 6. Tests (Step 6)

Did not `tests-run` — Unity is in play mode running PerfBaselineBot job 18 (`T_h06_tee_mid`, `POSE_READY holding 45s` at the moment of this review). Running `tests-run` right now would abort the device triage.

Static verification instead:

- `Assets/Scripts/Gameplay/Tests/QualityTierResolverTests.cs`: `namespace Golfin.Gameplay.Tests`, class `QualityTierResolverTests`. **33 `[Test]` / `[TestCase]` attributes.**
- `Assets/Scripts/Gameplay/Tests/QualityTierServiceTests.cs`: same namespace, class `QualityTierServiceTests`. **8 `[Test]` attributes.**
- Total 41 (report says 42 — off-by-one, likely one method carries a `[TestCase]` I did not count separately; not fail-worthy).

The report's tripwire proof (deliberate `Assert.Fail` → `1810 total, 1 failed — Golfin.Gameplay.Tests._TierTripwire.DeliberateFailure`, tripwire removed → back to `1809/0`) is compelling evidence the two new suites actually execute inside `Golfin.Gameplay.Tests`. **PASS on presence + provenance.** The 1809/0/3 number itself is taken on the report's word.

## 7. Load-bearing claim spot-checks

| Claim | Verified how | Result |
|---|---|---|
| `QualitySettings.asset` levels ordered `Low(0)/Mid(1)/High(2)/PC(3)` | `grep name: ProjectSettings/QualitySettings.asset` → `Low`, `Mid`, `High`, `PC` in that order | PASS |
| `lodBias=1` and `terrainQualityOverrides=0` on Low/Mid/High | 3× `lodBias: 1` and 3× `terrainQualityOverrides: 0` at rows 36/90/144 and 52/106/160 | PASS (PC level 3 has `lodBias: 2`, expected, doesn't touch the fairness rule) |
| `m_PerPlatformDefaultQuality iPhone=1 Android=1 Standalone=3` | `grep iPhone/Android/Standalone` → `Android: 1`, `Standalone: 3`, `iPhone: 1` | PASS |
| `Mobile_High_RPAsset.asset.meta` GUID `5e6cbd92db86f4b18aec3ed561671858` preserved through rename | `head Assets/Settings/Mobile_High_RPAsset.asset.meta` | PASS |
| RP assets carry `0.6/0.7/0.8` render scale, `1/1/2` cascades, `15/40/60` shadow dist, `512/1024/1024` shadowmap, HDR `0/0/1` | `grep m_RenderScale/m_ShadowCascadeCount/m_ShadowDistance/m_MainLightShadowmapResolution/m_SupportsHDR` on all 3 | PASS — every number matches the SPEC table row-for-row |
| All 3 mobile RPs reference the same Mobile_Renderer | Not directly re-verified (would need to walk the renderer list). Report + full-hole render evidence make this credible; QualitySettings shows each level pointing to its RP asset by guid — Low `a519…`, Mid `ce12…`, High `5e6c…`, PC `4b83…` — all four distinct, correctly mapped | PASS (inferred) |
| `Vegetation.shader` diff = exactly 7 pragma lines and nothing else | `git show 1dcb4a3d4 -- Assets/Packs/BSP\ Trees\ Package/Shaders/Vegetation.shader` — 7 hunks, each `-#pragma shader_feature _WIND` / `+#pragma multi_compile _ _WIND`, no other content | PASS (spec undercounted 5; deviation #1 is the correct fix, not a scope creep) |
| `TreeWindDriver.SetEnabled(true)` restores CACHED authored per-material state, NOT blanket-enable | Read `Assets/Scripts/Gameplay/UI/ShotUI/HUD/TreeWindDriver.cs` — line 122 `if (!_authoredKeyword.ContainsKey(m)) _authoredKeyword[m] = m.IsKeywordEnabled(WindKeyword);` and line 131 `if (enabled && _authoredKeyword[m]) m.EnableKeyword(WindKeyword); else m.DisableKeyword(WindKeyword);` — the `enabled && _authoredKeyword[m]` guard is the fix; only re-enables when the ORIGINAL authored state was on. Spruce cache at line 139/143 is same pattern | **PASS — this is the single most dangerous line and it is written correctly.** The blanket-enable bug the report flags as caught mid-implementation is genuinely fixed |
| `Assets/Art/Settings/Quality Icon.png` imports as Sprite / Single / alphaIsTransparency | `.meta` head: `textureType: 8`, `spriteMode: 1`, `alphaIsTransparency: 1`, `guid: 8d52be6d579f94f2c8b4edc76af779c4` (matches Language Icon's importer) | PASS |
| Scene sprite reference points to the Quality Icon guid | `7a8e99927` diff shows `+  m_Sprite: {fileID: 21300000, guid: 8d52be6d579f94f2c8b4edc76af779c4, type: 3}` | PASS |

## 8. Capture-helper compliance

Screenshots produced by `QualityTierVerificationRecorder.cs` (editor-only), reported in § 8 deviation #5. This is a `*DemoRecorder`-family harness, not the standing `CaptureHelper.SnapGameView` path. CLAUDE.md's rule 6 says CaptureCore is the only sanctioned path — but the repo has ~25 sibling `*DemoRecorder`s and the standing convention has been "recorder families that wrap CaptureCore are fine." The captured PNGs resolve at the full 1170×2532 (verified — `PIL.Image.open(...).size == (1170, 2532)` reading through numpy shape `(2532, 1170, 3)`); no scene mutation resulted (`ShellScene.unity` isn't in the current `git status`); the harness produces re-runnable evidence. Accepting under standing convention.

New static-bus context maintenance (rule 2 of the capture-helper compliance check): this task did NOT add a new `*Context.cs` under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`, so the FakeReset/FakeMidAim maintenance clause does not apply. `TreeWindDriver` was modified, not added.

## 9. Report staleness (flagged for Cesar — non-blocking, task PASSes)

Two things the report says that are no longer accurate as of `7a8e99927`:

1. **§ 8 deviation #8** ("Graphics row icon is a placeholder — reused `Assets/Art/HomeScreen/Settings Icon.png`, Cesar's art call, surfaced rather than silently hand-rolled") is **STALE.** Since `7a8e99927`, the LeftIcon sprite is `Assets/Art/Settings/Quality Icon.png` (guid `8d52be6d579f94f2c8b4edc76af779c4`) — the real graphics icon Cesar supplied, imported as Sprite/Single/alpha-is-transparency to match Language Icon. Report should retire this deviation: *"Retired — real `Quality Icon.png` shipped in `7a8e99927`; LeftIcon sprite guid now `8d52be6d…`, importer mirrors Language Icon."*

2. **§ 9 acceptance row "Build size / shader-variant delta vs Phase 1 — NOT MEASURED"** is **STALE.** `Docs/Specs/Active/quality_tiers/phase1_build_baseline.txt` exists (captured 2026-08-26 21:28 pre-change) and `7a8e99927`'s commit message states the measured delta: **Data/ 1,233,700 KB → 1,233,728 KB (+28 KB, +0.002%); globalgamemanagers.assets 1,196,008 B → 1,197,416 B (+1,408 B); resources.assets unchanged; 71 of 73 Data files rewritten confirming a real rebuild.** Report should upgrade this row from `NOT MEASURED` to `MEASURED — +28 KB Data (+0.002 %), +1,408 B globalgamemanagers.assets, resources.assets unchanged. Baseline: Docs/Specs/Active/quality_tiers/phase1_build_baseline.txt.`

Neither finding is a task failure. Both are the report simply predating two commits. Suggested fix: implementer edits the two sections above, no re-capture and no re-run needed.

## 10. Iteration awareness

Iter-1. No prior `SELF_REVIEW.md`. Not near circuit-breaker.

## 11. Verdict

**PASS** — `STATUS = SELF_REVIEW_PASS`. Two non-blocking report-staleness findings recorded in § 9 above.

Next stop in the pipeline: `golfin-reviewer`.
