# Architect Review — `mode_select_system` — Iteration 5

**Reviewer:** golfin-reviewer
**Date:** 2026-06-04 14:13 CEST
**Verdict:** `ARCHITECT_REVIEW_FAIL`
**Iteration reviewed:** 5

---

## Independent visual scan (Step 0 — written BEFORE reading IMPLEMENTER_REPORT / SELF_REVIEW)

### `iter5_home_canonical.png` (1170×2532)
Portrait shell. Top: gold-R coin "52,400" RP chip on dark-navy strip, gear icon right. Below, narrow navy strip with white "CHOTO" username. Upper-mid: large orange/amber rounded panel reading "MAINTENANCE NOTICE / Scheduled server maintenance: 2025/12/31 / The game will not be available for a short time / during maintenance." Behind it a golf-course sunset background. Mid-screen: a stylised blonde-goggled character (Choto) with a large gold trophy at right shoulder; visible `‹` chevron at left edge and `›` chevron at right edge of the middle band. Centered card titled **"PRACTICE"** in gold over navy gradient — body "Practice your golf skills on any course. Choose a hole and tee off at your own pace — no pressure.", thin divider, "ENTRY FEE [coin] x100", "REWARDS [coin] x50", large gold "PLAY" button. Partial left-peek card visible ("...NS / ...te challenges. / x200"); right-peek partially occluded by trophy/character but the `›` chevron sits over the trophy area. Lower: dark "GOLFIN-GPS / CHECK-IN WITH GPS / EARN MORE POINTS TO POWER UP!" banner with golfer silhouette. Bottom: navy nav row with 5 icons including elevated gold-tee center button.

### `iter5_modeselect_canonical.png` (1170×2532)
Portrait shell. Top: "R 52,400" chip left, gear right, "MODE SELECTION" centered in bold white below. Four stacked dark-navy gradient cards with white border / large radius / consistent 24px vertical gaps:

- **PRACTICE** (gold title, fully visible just below the MODE SELECTION header — NOT clipped) — "Sharpen your skills on any hole. ›" + "[coin] x100" left / "[coin] x50" right.
- **1V1** (gold title) — "Face off in fast-paced 1v1 matches. ›" + "NO ENTRY FEE" left / "[coin] x200" right.
- **DRIVING RANGE** (greyed title, lock glyph) — "Coming Soon — practice your drives." + "NO ENTRY FEE" left. Whole card dimmed.
- **MISSIONS** (greyed title, lock glyph) — "Coming Soon — complete challenges." + "NO ENTRY FEE" left / "[coin] x200" right. Whole card dimmed.

A thin vertical white scrollbar runs the full right edge of the cards stack at ~x=1150 (Figma ~1090). Blurred green/golf-course background fills the bottom area below the last card. Bottom: 5-icon nav row matching the home one. All four cards are in collapsed state — no PLAY button on any.

---

## Figma side-by-side

| Element | Figma reference | Canonical observation | Verdict |
|---|---|---|---|
| **Full-screen card width** | 978px (FIGMA_METRICS) | Cards visibly span ~88% of viewport | Matches within ~10px |
| **Full-screen vertical card gap** | 24px | Visible consistent gap between all 4 cards | Matches |
| **Full-screen card chrome** | gradient `#133453`→`#091B33`, 3px white border, 50px radius | Vertical navy gradient (lighter top → darker bottom), thin light border, large rounded corners | Matches |
| **Full-screen title color** | gold `#EEDC9A` (EN/Subhead 45 → 32.14 TMP) | PRACTICE / 1V1 titles render in pale gold | Matches |
| **Full-screen title font** | Rubik variable, weight tuned | Title weight visually heavy/semibold; consistent across cards | Matches |
| **Full-screen scrollbar** | x≈1090, visible | Thin vertical bar visible at right edge | Matches (visible, position close) |
| **Full-screen container** | sizeDelta=(-96,-620) anchoredY=-30, top card clear of TopBar | Top PRACTICE card title fully visible below "MODE SELECTION" header with clear gap | PASS (ITER-5 F2 resolved) |
| **Full-screen — all cards collapsed default** | Cesar override (no auto-expand) | All 4 collapsed — title + tagline + fee row only, no PLAY anywhere | Matches |
| **Full-screen — locked treatment** | lock glyph + dim + non-interactive | DRIVING RANGE + MISSIONS both show lock glyph left of title; full card dimmed | Matches |
| **Full-screen — NO ENTRY FEE label** | when `entryFee==0` | Visible on 1V1, DRIVING RANGE, MISSIONS | Matches |
| **Home — centered card (expanded MULTIPLAYER ref)** | 764×822 incl. description + fees + PLAY | PRACTICE centered, expanded with description + ENTRY FEE + REWARDS + PLAY | Matches expanded state |
| **Home — collapsed side cards** | 677×268 visible both sides | LEFT peek clearly visible ("...NS / ...te challenges / x200"); RIGHT peek occluded by trophy/CharacterRoot (chevron `›` drawn over) | Partial — right peek hard to see; pre-existing layout (not iter-5 regression) |
| **Home — side arrows `‹ ›`** | y≈1775, 30×60 | Both chevrons present at left/right edges | Matches |
| **Home — Promo banner** | x100 y1969 w970×252, below carousel | GOLFIN-GPS banner present at lower area, draws above CharacterRoot | Matches (H2 PASS preserved) |
| **Home — Card bottom anchor / 24px gap above banner** | per FIXLIST H4/H5 | Card sits above banner with consistent gap; cards bottom-anchored | Matches (not regressed) |
| **Home — Hero title "GOLFIN Presents / The Invitational"** | Present in Figma | ABSENT — orange MAINTENANCE NOTICE panel occupies that vertical region | OUT-OF-SCOPE per Cesar (pre-existing absence) |

---

## Bbox verification

**Containment claim:** "Top PRACTICE card on ModeSelect is fully below the MODE SELECTION TopBar header (not clipped)."

Static RT math from ShellScene (self-review Step 6, independently re-validated):
- `ScrollView` RT: anchors (0,0)→(1,1), sizeDelta=(-96,-620), anchoredPos=(0,-30), pivot (0.5,0.5).
- On 1170×2532 screen: rendered rect width=1074, height=1912.
- Vertical centre = 1266 + (−30) = 1236. Top edge = 1236 + 956 = **2192**. Bottom edge = 1236 − 956 = **280**.
- TopBar bottom ≈ y=2200 (per pixel scan of MODE SELECTION title strip).
- ScrollView top (2192) ≤ TopBar bottom (~2200) → ~8px clearance.
- Content padTop ~10px → card top ≈ y=2182, fully clear of TopBar.

Pixel-scan independent check: PRACTICE card title is unambiguously visible with clean vertical gap below the navy MODE SELECTION header strip. No title chars are cut. **inside=true.**

Z-order claim (Home card vs CharacterRoot): HomeScreen children siblingIndex CharacterRoot=3, ModeCarouselSection=6 (last). Higher index = drawn on top. Pixel scan confirms card border draws cleanly over character torso (no character pixels overlap card edge). PASS.

---

## Scene-mutation audit

`git status --porcelain` filter for forbidden manager/singleton files:

```
(no matches for CharacterManager / ClubManager / AudioManager / RewardPointsManager /
 CharacterDatabaseCSV / ScreenManager / PersistentUIManager / ModesDatabaseCSV)
```

**Regression-guard PASS** — the architect-reverted singletons stay clean.

`git diff Assets/Scenes/ShellScene.unity` summary: +1372/−348 lines. Inspected for unrelated active-state toggles:
- Net `m_IsActive: 1→0` changes: 2 (PendingLabel — already inactive in prior scene, fileID renumbered; ModeSelectionScreen — correctly inactive because HomeScreen is default).
- Net `m_IsActive: 0→1` changes: 11 (all new ModeSelect subtree additions).
- One large chunk shows `VersionSection` deleted at chunk @63565 and `Text` GameObject inserted, then ModeSelectionScreen added. Independent verification: `VersionSection` still exists exactly once in current scene; `SETTINGS_ABOUT_APP_VERSION` LocalizedText still present once; `ItemNameText` ×2, `PendingLabel` ×8 — all preserved counts. The chunk diff is a fileID-renumbering artefact of the YAML, not a deletion. NO unrelated mutation.

**Files outside task folder reported in IMPLEMENTER_REPORT (Rule 13):** All `M` paths are listed in the "Pre-existing modified files outside task folder" table (TerrainData ×12, NuGet plugins, baked-pivot diag, manifest, packages-lock, ShellScene). Untracked new `??` paths in `Assets/Scripts/UI/ModeSelect/*` and `Assets/Prefabs/UI/ModeSelect/*` are documented. Two untracked files NOT in the report (`Docs/Specs/Quick/editor_replay_singleton_reset.md`, `Tools/GreenSlope/scripts/capture-all-holes.mjs`) are pre-existing/unrelated — minor reporting gap but not a blocker.

**Scene-mutation audit PASS.**

---

## Two distinct prefabs (Step 0 clone gate)

| Prefab | Clone source | Source GUID claim | Verified |
|---|---|---|---|
| `Assets/Prefabs/UI/ModeSelect/ModeHomeCard.prefab` | HomeScreen.prefab › NextHolePanel | per IMPLEMENTER_REPORT prior iterations | EXISTS on disk |
| `Assets/Prefabs/UI/ModeSelect/ModeCard.prefab` | HoleSelection/HoleCard.prefab | per IMPLEMENTER_REPORT prior iterations | EXISTS on disk |

Two distinct prefabs confirmed on disk. **Clone-source GUIDs not re-stated in iter-5 report** but they were declared in prior iter (iter-2/3) reports per the "from prior iterations" note. PASS-with-caveat.

---

## Fee economy logic (code inspection)

`ModeCardController.cs`:
- `RefreshFeeColor()` (lines 291-311): when `_data.entryFee > 0 && !CanAfford(_data.entryFee)` → ENTRY FEE text color = `#C04000` (`InsufficientRpColor`); PLAY button CanvasGroup alpha = 0.4. Otherwise white + alpha 1.
- `HandlePlayButtonClicked()` (lines 313-334): when unaffordable → `ToastController.Instance.Show("Not enough Reward Points")`, no `SpendPoints`, no `OnPlayClicked`. When affordable → `SpendPoints(fee)` ONCE then `OnPlayClicked?.Invoke(this)`.
- Subscribes to `RewardPointsManager.OnPointsChanged` in `OnEnable` so fee color updates live.

1v1 (fee=0): `_data.entryFee > 0` guard means 0-fee never enters unaffordable branch. Never blocked. PASS.

Toast message: "Not enough Reward Points" — matches SPEC text. PASS.

**Fee economy logic PASS.**

---

## Transitions (code inspection)

- `ModeCardController.AnimateHeight` (line 339): coroutine-Lerp on `Time.unscaledDeltaTime`, ease-out, ~0.18s default. NOT a SetActive pop on height change. PASS for expand/collapse animation.
- `ModeSelectScreenController` and `ModeCarouselController` `ScreenManager.ShowScreen` calls: no `instant:true` parameter anywhere (grep returned 0 hits for `ShowScreen.*true`). Fade default preserved. PASS.
- `ModeCarouselController` snap animation: `_snapDuration=0.18f`, expand-after-snap delay 0.05s.

**Transitions PASS.**

---

## ButtonPressFeedback audit (Hard Rule 11)

Programmatic Button-vs-BPF mapping per prefab:

**ModeCard.prefab:**
| Button GameObject | Has ButtonPressFeedback sibling? |
|---|---|
| CardTapButton | YES |
| ActionButton | YES |

PASS.

**ModeHomeCard.prefab:**
| Button GameObject | Has ButtonPressFeedback sibling? |
|---|---|
| ModeHomeCard (root) | YES |
| PlayButton | YES |
| **CardTapButton** | **NO** |

**FAIL.** `CardTapButton` is a new player-facing Button (taps the card to expand/collapse the centered carousel card) — added to ModeHomeCard which was cloned from NextHolePanel (NextHolePanel did NOT have a CardTapButton). Per CLAUDE.md Hard Rule 11: "Every new player-facing Button gets `Golfin.UI.Polish.ButtonPressFeedback`. ... One missing pair = task FAIL."

Note: the rule is binding. Self-reviewer Step 5 / Step 6 did not run this audit (mentioned "No new Button…ButtonPressFeedback sibling check" implicitly via not surfacing it). The reviewer backstop catches it here.

---

## Latent issues / observations

1. **`scrollRect` field on `ModeCarouselController` is `fileID: 0`.** Implementer's IMPLEMENTER_REPORT § Spec deviations explicitly explains this (ModeCarouselSection has no ScrollRect; manual carousel positioning; `GetViewportWidth()` falls back to parent RT width — null-safe). Architecturally consistent with the manual virtual-3× carousel. ACCEPT as deviation, but flag: if the carousel later needs scroll inertia, this design will require revisiting.
2. **Side cards' right-peek visually occluded by CharacterRoot trophy** — visible LEFT peek confirms circular wrap is functional; the RIGHT peek is harder to see because CharacterRoot's trophy sprite happens to sit in that screen region. This is pre-existing scene layout (not introduced by iter-5). Architect awareness item, not blocker.
3. **Home hero title "GOLFIN Presents / The Invitational" absent.** Per task brief: "KNOWN-OUT-OF-SCOPE — Cesar decided." Explicitly excluded from FAIL criteria.
4. **MAINTENANCE NOTICE panel sits where Figma's hero title would.** Same as above — pre-existing home element, not introduced here. Out-of-scope.
5. **Scrollbar pixel position** is at right-edge (~x=1150) vs Figma's ~x=1090 — within visual tolerance for a 1170-wide canvas, particularly because the scrollview sizeDelta is `-96` (split 48px each side). Acceptable.
6. **Two prefabs visible** (ModeHomeCard, ModeCard); single shared ModesDatabaseCSV. Step 0.1 architecture preserved.

---

## Verdict & required fixes

**Verdict: `ARCHITECT_REVIEW_FAIL`**

Visual fidelity, scene mutation, fee economy logic, transitions, container sizing/clipping, scrollbar visibility, all-cards-collapsed default, carousel arrow wiring, and Z-order are all correct. The blocker is a single Hard Rule 11 miss.

### Required fix list (1 item)

1. **Add `Golfin.UI.Polish.ButtonPressFeedback` sibling component to `ModeHomeCard.prefab` → `CardTapButton`** (the new player-facing Button has no BPF). Use defaults (`_pressedScale=0.95`, `_duration=0.12`). One-line component-add via SerializedObject in script-execute (no Unity Editor by-hand work needed). Re-capture is NOT required — this is a press-feedback-only change with no visual layout impact at idle.

After the fix:
- Re-run the same prefab Button/BPF audit (`grep` or script-execute) to confirm CardTapButton now pairs.
- Append a one-line confirmation to IMPLEMENTER_REPORT.md under "Rule 11 backstop fix".
- Set STATUS to `READY_FOR_ARCHITECT_REVIEW`. No need to redo SELF_REVIEW for a one-line component add, but the self-reviewer will re-run anyway per pipeline.

### Files

| File | Action |
|---|---|
| `Docs/Specs/Active/mode_select_system/ARCHITECT_REVIEW.md` | Wrote iter-5 architect-review verdict (FAIL on Rule 11) |
| `Docs/Specs/Active/mode_select_system/STATUS.md` | Set to `ARCHITECT_REVIEW_FAIL` |

---

# Architect Review — `mode_select_system` — Iteration 6 (Rule 11 backstop re-review)

**Reviewer:** golfin-reviewer
**Date:** 2026-06-04 14:20 CEST
**Verdict:** `READY_FOR_REDTEAM` (hands to red-team gate; NOT a final PASS)
**Iteration reviewed:** 6 (scope-limited: Rule 11 backstop only)

## Scope

Iter-5 PASSed everything except a single Hard Rule 11 miss: `ModeHomeCard.prefab → CardTapButton` lacked a `Golfin.UI.Polish.ButtonPressFeedback` sibling. Iter-6 was a one-component-add. This re-review confirms the fix is in place and nothing regressed. The iter-5 canonicals remain canonical (no visual change from a press-feedback-only component, idle-invisible).

## Rule 11 audit (post-fix)

Programmatic prefab inspection (grep + GameObject-name resolution on owning fileIDs):

**ModeHomeCard.prefab** — 3 ButtonPressFeedback components, host GameObjects:
| ButtonPressFeedback fileID | Host GameObject fileID | Host GameObject name | Sibling Button present? |
|---|---|---|---|
| (existing) | 1332473051566112605 | **ModeHomeCard** (root) | YES |
| **7391847263051840512** | **3830234380593726200** | **CardTapButton** (iter-6 add) | **YES** |
| (existing) | 3918297139119137721 | **PlayButton** | YES |

All three Button GameObjects pair with a ButtonPressFeedback sibling. Defaults on the new component verified inline in `git diff`: `_pressedScale: 0.95`, `_duration: 0.12`.

**ModeCard.prefab** — 2 ButtonPressFeedback components, host GameObjects:
| ButtonPressFeedback fileID | Host GameObject name | Sibling Button present? |
|---|---|---|
| (existing) | **ActionButton** | YES |
| (existing) | **CardTapButton** | YES |

Both Button GameObjects pair. No regression — file mtime confirms ModeCard.prefab was not touched this iter (only ModeHomeCard.prefab changed at 14:17, ModeCard.prefab unchanged).

**Rule 11 audit PASS.**

## Regression audit

### `git diff Assets/Prefabs/UI/ModeSelect/ModeHomeCard.prefab`

```
+  - component: {fileID: 7391847263051840512}      (component list addition on CardTapButton GO)
+--- !u!114 &7391847263051840512                   (component block, 13 lines)
+MonoBehaviour ...
+  m_GameObject: {fileID: 3830234380593726200}     (= CardTapButton)
+  m_Script: ... guid: 6fe5cc7c7203c48cba1b90b70c6e4737  (= ButtonPressFeedback)
+  _pressedScale: 0.95
+  _duration: 0.12
```

Total: +15 / −0 lines, single component addition only. No layout fields, no anchors, no sizes, no IsActive toggles, no other components, no GameObject add/remove.

### Other files touched this iter

- `Assets/Scenes/ShellScene.unity` mtime = 13:47 (iter-5); iter-6 active 14:16 → 14:20. **Scene not modified this iter** — diff identical to iter-5 review's PASSed inspection. No new singleton/manager files changed.
- HEARTBEAT iter-6 kickoff baseline confirms `NO singleton files modified (CharacterManager, ClubManager, AudioManager, RewardPointsManager, CharacterDatabaseCSV, ScreenManager, PersistentUIManager, ModesDatabaseCSV all clean)`.
- `git status --porcelain` dirty list outside task folder is unchanged from iter-5 (same 12 TerrainData, same 4 NuGet plugins, same baked-pivot diag, same manifest/packages-lock).

**Regression audit PASS.** Only the documented single-component addition; everything else iter-5 was carrying forward continues to carry forward unchanged.

## Carry-forward of iter-5 PASSes (no re-verification needed)

The following were independently verified in the iter-5 review and remain unchanged this iter (file mtimes confirm no touch): visual fidelity on both surfaces, fee economy logic, transitions (no instant cuts), scene-mutation audit, scrollbar (Permanent), all-cards-collapsed default, carousel arrow wiring, z-order (ModeCarouselSection sibling 6 > CharacterRoot 3), two distinct prefabs on disk, top-card clip resolution (F2 with ScrollView sizeDelta=(-96,-620) anchoredY=-30), bbox containment (PRACTICE title below MODE SELECTION header with ~8px clearance). Canonical screenshots `iter5_home_canonical.png` and `iter5_modeselect_canonical.png` remain valid evidence — press-feedback components are idle-invisible.

## Verdict

**`READY_FOR_REDTEAM`**

Iter-6's sole change is the Rule 11 backstop component-add on `ModeHomeCard.prefab → CardTapButton`. The audit confirms (a) the component is in place with correct defaults, (b) all 3 Buttons in ModeHomeCard.prefab and both Buttons in ModeCard.prefab now pair with `ButtonPressFeedback`, and (c) no other file touched, no scene mutation, no singleton drift, no layout regression. The iter-5 PASS surface (visual fidelity, fee economy, transitions, container, scrollbar, arrows, z-order, two prefabs) carries forward without re-verification because nothing in this iter could affect any of those.

Hands to the adversarial red-team gate. The red-team is the only agent permitted to advance to `ARCHITECT_REVIEW_PASS`.

### Files

| File | Action |
|---|---|
| `Docs/Specs/Active/mode_select_system/ARCHITECT_REVIEW.md` | Appended iter-6 re-review verdict (`READY_FOR_REDTEAM`) |
| `Docs/Specs/Active/mode_select_system/STATUS.md` | Set to `READY_FOR_REDTEAM` |
