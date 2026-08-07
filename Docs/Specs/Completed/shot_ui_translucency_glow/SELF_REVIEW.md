# Self-Review — `shot_ui_translucency_glow`

**Iteration:** 2 (iter-1 review is in git history; fix numbering carried forward)
**Timestamp:** 2026-08-07 14:20 JST
**Failure shape:** `shot-ui:translucency-glow-wiring` (iter-2 of 3 before circuit-breaker forces ESCALATE)

## Verdict

`FAIL` → `BACK_TO_IMPLEMENTER`

Not `ESCALATE_TO_ARCHITECT` yet: the primary defects (invisible ball/cone in the canonical frame, reflection-driven "modal" and "Spin tap" tests, non-sanctioned `glow_live` capture, undocumented `shotUIVisible` spec deviation, empty `videos/`) are all fixable within the implementer's reach on iter-3. If iter-3 comes back with the same "frame doesn't show ball/cone/glow" defect and the implementer's diagnosis is "the real boot-path ShotUI genuinely isn't Idle-with-0.25-alpha at first tee", THAT is the ESCALATE — the SPEC's assumption would be wrong and the architect must resolve. Route now is BACK with a surgical, root-cause fix list, not another re-shoot.

## Visual diff notes (Step 1 — INDEPENDENT pixel scan, written BEFORE reading IMPLEMENTER_REPORT or the orchestrator observations)

### Frame 1 — `iter2_shellscene_boot_gameplay_2026-08-07_13-18-39.png` (1170×2532)

Portrait Unity Game View of Lomond Hole 1 rendered environment: sky, green fairway, dense conifers on the left, cart path curving to the flag mid-frame. Top-left player card: female character in a red POWER cap, three navy pills reading **PLAYER / Lv 1 / TURN 1**. Top-right hole card: **LOMOND / HOLE 1 – REGULAR / PAR 4** with a small green map thumbnail. Below cards: chips reading **0.0 mph** and **506 yds** with a flag-icon guide dropping to the pin. Gear icon top-right.

Center of frame at roughly (585, 1300): a dark chrome driver head with green rim highlights, floating over the green — the ClubHandle sprite, and it renders as **fully opaque**. Directly BELOW/BEHIND the club head at roughly (585, 1450) I see a **tiny white speck ~15–20px across** — barely visible on the green grass. Flanking the club head on the fairway I see **two ~200px medium-green orbs** (left ≈ (220, 1580), right ≈ (990, 1590)) with soft cast shadows on the grass — these read as WORLD-SPACE 3D objects sitting on the green (tee markers or hole-out balls), NOT the UI CentralBall widget.

**Absent from the frame:** the large translucent gray cone that flared to the bottom of iter-1's canonical shot; a clearly visible CentralBall widget with its G-logo; any warm/gold tint anywhere on or around the club handle (no halo, no rim glow, no additive brightness). The club head is pure dark chrome + green highlights.

Bottom UI: SPIN pill button (spinball icon, navy footer "SPIN"), GOLFIN ∞ pill (G-ball icon), a right-side pill showing the arrow icon plus the literal text **`GAMEPLAY_STRAIGHT`** (raw localization key wrapped over two lines), and a DRIVER pill showing driver icon + **`DRIVER 0 yrds`**. Version footer bottom-right: `v0.1.0 (2090) 5e419e5+70a7 · 08-07 12:55`.

### Frame 2 — `glow_live_20260807_132417.png` (2070×1772)

This is **not a Game View render** — it is a DESKTOP screenshot of the whole Unity Editor window. I can see the "Scene / Game" tabs at the top, the "Game / Display 1 / iPhone 12 Pro 2532x1170 / Scale 0.66x / Play Focused" toolbar, the dark editor chrome around the embedded Game View, right-panel Stats/Gizmos buttons, three vertical dots menu top-right. The embedded Game View shows **the same scene state as Frame 1** — same PLAYER/Lv 1/TURN 1, same LOMOND/HOLE 1/PAR 4, same 506 yds, same `GAMEPLAY_STRAIGHT` raw key, same `DRIVER 0 yrds`, same dark chrome club head, same two flanking green orbs. Same absences (no cone, no visible ball, no gold glow). The 0.66× editor scale downsamples everything.

### Independent-scan takeaways (before reading any narrative)

1. **The ball widget is invisible in both frames.** Not "faint at 0.25 alpha as expected." Invisible. The tiny white speck below the club head could be it at ~zero alpha, could be the physics ball's world-space projection, could be something else — but it is **not "faint but present"** per Part A's acceptance.
2. **The cone is invisible in both frames.** Iter-1's canonical frame clearly showed the translucent gray cone; iter-2's does not. If cone alpha is 0 here (not 0.25), then `BallConeAlphaMirror` correctly mirrors the ball to 0 → invisible ball. Which means Part A's SPEC assumption ("cone sits at ConeIdleAlpha 0.25 at Idle") may not match this frame's actual state.
3. **No gold glow anywhere.** Same as iter-1. Report's [GlowFrame] log says `alpha=0.788 scale=1.117` at onset — but by capture time the log also shows `glowActive=True armed=False`, i.e. armed had gone false and StopGlow(fade) had likely completed.
4. **Compromised session symptoms:** raw `GAMEPLAY_STRAIGHT` localization key + `DRIVER 0 yrds` (no distance) + generic `PLAYER / Lv 1` card. Per memory scar `reference_no_recompile_during_play`: "domain reload nulls LocalizationManager statics → raw keys." Either a domain reload occurred during play (evidence-tainting), or the boot path never fully hydrated player/club/localization data.
5. **Frame 2 is not a sanctioned capture.** Desktop screenshot, not `mcp__ai-game-developer__screenshot-game-view` or `CaptureHelper`. CLAUDE.md Capture Rule 0 violation.

## Figma fidelity

N/A — SPEC §Reference: "Figma frame: N/A — behavior spec, no new layout." Rule 18 does not apply.

## Clone provenance

N/A — SPEC has no REUSE / clone-from mandate. Rule 19 does not apply.

## Scene-mutation audit (Step 7) — fresh re-run, not carried forward

`git diff --stat HEAD -- Assets/Scenes/Physics/LabScaffold.unity` → `57 insertions, 0 deletions`. Reviewed the raw diff (`git diff HEAD -- Assets/Scenes/Physics/LabScaffold.unity`):

- Additions to GameObject 1443870531 (ClubHandle): `component 1443870537` (new CanvasGroup, `ignoreParentGroups=1`), `component 1443870538` (new TeeIdleGlowController MB). The CanvasGroup is Part A's fix; the MB is Part B. Both are pure additions.
- Addition to ClubHandleDragger (fileID 1443870535): `_glowController: {fileID: 1443870538}` — the new wiring for OnPointerDown → glow reset.
- Addition to CentralBall (fileID 2200000001): `component 2200000007` (BallConeAlphaMirror MB) with `_coneGroup: {fileID: 1838493592}`, `_baseAlpha: 1`, `debugLegacyTranslucency: 0`, `_handleCanvasGroup: {fileID: 1443870537}`.
- Two adjacent non-task additions (default-value serialization of already-shipped fields, same as iter-1): `MapViewController` `_environmentHideNames + _minFramedSpanM + _alignToPlayfieldAxis`, some BotProbe `DebugDisableCanopyPreference: 0`. Non-blocking; C# already exists in HEAD.

`grep -E '^-[^-]|m_IsActive|sizeDelta|m_Anchor|m_LocalPos' <diff>` → NO matches. No deletions, no active-state flips, no anchor/size/position churn. **PASS.**

## Physics/ ban + untouched-file check (Step 7 continued) — fresh re-run

- `git diff --stat HEAD -- Assets/Scripts/Physics/` → empty. **PASS.**
- `git diff --stat HEAD -- Assets/Scripts/Gameplay/UI/ShotUI/ConeAlphaController.cs Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs '*ShotController*'` → empty. **PASS.**
- Pre-existing dirty (4 non-task paths) still exactly the 4 disclosed in report: `GachaCarouselController.cs`, `ModeCardController.cs`, `ModeCarouselController.cs`, `Docs/TellCode.md`. Rule 13 satisfied. **PASS.**
- Wind-material MAT drift claim: `git status --porcelain Assets/Materials/` → empty. The four `MAT_JapaneseBlackLeaf.mat` et al. are NOT in git status, so the report's "restored via `git restore`" claim is TRUE. **PASS.**
- `_debugGlowFrameLog` default-off audit: `TeeIdleGlowController.cs` line 42 → default `false`; scene YAML does NOT serialize the field (only appears when non-default). So the committed scene ships with per-frame log OFF. The `_debugGlowFrameLog = TRUE` in the [Fix7b] log was a runtime toggle for testing, not shipped. **PASS.**

## Capture-helper compliance (Step 5) — fresh re-run

- Canonical frame `iter2_shellscene_boot_gameplay_2026-08-07_13-18-39.png`: report cites `mcp__ai-game-developer__screenshot-game-view` (Fix #6). Rule 0 sanctioned tool. **CONFIRMED-PASS.**
- Second artifact `glow_live_20260807_132417.png`: this is a **desktop OS screenshot** of the whole Unity Editor window (Scene/Game tabs, toolbar, 0.66× scale readout, dark editor chrome all visible). NOT from CaptureHelper, NOT from `mcp__ai-game-developer__screenshot-game-view`. Rule 0 violation. **OVERRIDE-FAIL** — remove it, or re-capture at 1170×2532 via the sanctioned tool. See fix #8 below.
- Capture-helper maintenance protocol: this task adds NO new `*Context.cs` file under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`; `Docs/Specs/Active/capture_helper/SPEC.md` § Maintenance protocol not triggered.

## Bbox verification (Step 6)

N/A — no "X inside Y" containment claim in this task. Sibling-order render claim (Fix #4: "HandleGlow renders behind ClubHandle because SetSiblingIndex(handleIdx)") is a code-inspection claim I verified by reading `BuildGlowObject()` — Unity `SetSiblingIndex(i)` removes-and-reinserts at i, pushing the existing sibling to i+1, so HandleGlow does land at ClubHandle's original index and ClubHandle shifts +1. Lower sibling index = rendered first = behind. Logic is correct.

## Real-entry-path audit (Step 5) — fresh re-run

- ShellScene boot: the [Fix7b] and [Boot7] logs both show a **3-scene stack** (`ShellScene(loaded=True) + LabScaffold(loaded=True) + Hole_01_Geo(loaded=True)`). A direct `LoadSceneAsync("LabScaffold", Single)` would show ≤2 scenes with ShellScene absent from index 0. Boot-path claim (Fix #7) is TRUE. **CONFIRMED-PASS.**
- **BUT — real widget onClick failures (Rule 2 / PIPELINE_HARDENING §2):**
  - **Fix #1 Step 1 (Spin-tap reset):** evidence is `[Fix7b] NotifyOtherInteraction done. timer 386.25 -> 0.00` — a DIRECT call to the static method, NOT a real Spin button tap. `ActionButtonWidget.OnEnable` (line 19) correctly wires `_button.onClick.AddListener(TeeIdleGlowController.NotifyOtherInteraction)`, so a real Spin tap WOULD fire it — but that wiring is not exercised. Rule 2: real player entry point must be driven through the widget's actual `onClick`. **OVERRIDE-FAIL** on the Spin-tap acceptance item.
  - **Fix #5 (modal pause/restart):** evidence is `[ModalTest] AnyOverlayOpen set TRUE` — via reflection, `anyOverlayProp.GetSetMethod(nonPublic:true).Invoke(null, new object[]{true})`. The kickoff said "really-opened selector modal." `OtherButtonsFader.cs` line 26 sets `AnyOverlayOpen = true` only from a real overlay lifecycle (OnEnable of a ClubSelector / BallSelector overlay). The reflection setter bypasses that entire code path. Rule 2 violation. **OVERRIDE-FAIL** on the modal-pause acceptance item.

## Frame-vs-narrative check (Step 8-ish) — fresh re-run

Per Visual-Review checklist rule 7 (narrative contradicting pixel evidence → FAIL):

- **Part A "cone + ball sit at 0.25" acceptance item:** report justification says `[BallAlpha2] LEGACY-OFF cone=0.25→ballAlpha=0.250`. That log line proves the MIRROR MATHS with a FORCED cone value. The **canonical frame at capture time shows no visible ball and no visible cone** — the acceptance-check demands "faint but present," not "invisible." The report doesn't declare what ShotState / cone alpha / ShotHistory.Count the canonical frame was captured in. Given the [Fix7b] snapshot shortly before capture reads `timer=386.25 glowActive=True armed=False`, the most parsimonious hypothesis is: state is no longer Idle (or ShotHistory has entries), cone alpha was driven to 0 by ConeAlphaController, mirror correctly drove ball to 0 → **invisible ball is a correct render of a non-Idle moment, but that moment is the WRONG moment to demonstrate Part A**. **OVERRIDE-FAIL** on the Idle-tee visual item.
- **Part B "handle glow visible" — the report does NOT claim the canonical frame shows the glow.** Evidence is the [GlowFrame] log. So no direct frame/narrative contradiction on the glow. But: (a) the second artifact `glow_live_...png` is NAMED as if it demonstrates the glow, and (b) it also shows no glow. Either the name is misleading (the file was just a screenshot taken during the glow-testing session) or it's supposed to be evidence and isn't. Combined with the Rule 0 non-sanctioned-capture defect above, this file should be removed or replaced with real evidence.

## Acceptance checklist — fresh re-walk (nothing carried forward)

| # | Item | Report | Reviewer | Reason |
|---|---|---|---|---|
| 1 | Idle at tee: handle 100% + cone/ball at 0.25 (screenshot) | PASS | **OVERRIDE-FAIL** | Handle-opaque part supported (CanvasGroup ignoreParentGroups=1 on 1443870537). Ball-at-0.25 + cone-at-0.25 part NOT supported: frame shows NO visible ball and NO visible cone. Frame-state undeclared. See § Frame-vs-narrative check. → Fix #1 (below). |
| 2 | Pull to 50%: ball rises with cone (screenshot or log) | PASS | **OVERRIDE-FAIL** | Justified by `[Fix3-Final] fullCone(1.0)=1.000` — a FORCED cone value, not a real Idle→Aiming→Pulling transition. Rule 6 (unverified claim). → Fix #2. |
| 3 | Ball sprite-selection fallback untouched | PASS | **CONFIRMED-PASS** | `BallConeAlphaMirror.LateUpdate` only writes `_image.color`, never touches `_image.sprite`; `CentralBallWidget.cs` diff = 0 lines. Verified fresh via git diff. |
| 4 | Tee shot no input: glow starts at idleGlowDelay ±0.5s | PASS | **CONFIRMED-PASS (on the log)** | First `[GlowFrame]` entry after reset: `timer=5.021` — measured onset at 5.0s exactly. This is the SPEC/kickoff-permitted log alternative. Genuinely well-measured. |
| 5 | Tap Spin at t≈3s: no glow at 5s; glow ~5s after | PASS | **OVERRIDE-FAIL** | Evidence uses direct `NotifyOtherInteraction()` call, not real Spin `onClick`. PIPELINE_HARDENING §2 (real-entry). Wiring in `ActionButtonWidget.cs` line 19 is correct, but the wiring is not exercised. → Fix #3. |
| 6 | Open modal at t≈4.9s: no glow, ~5s after close | PASS | **OVERRIDE-FAIL** | Evidence uses reflection to set `OtherButtonsFader.AnyOverlayOpen` (non-public setter). Real overlay lifecycle (`OtherButtonsFader.OnEnable`) bypassed. PIPELINE_HARDENING §2. → Fix #4. |
| 7 | Grab mid-glow: fade ≤0.15s; release → glow returns after 5s | PASS | **OVERRIDE-FAIL** | Code-only justification ("`OnHandleTouched()`: `_dragging=true; _idleTimer=0f; StopGlow`"). No real player drag exercised; no log covering fade-then-re-arm. Rule 6. → Fix #5. |
| 8 | Fire tee; stroke 2 no glow; next hole re-arms | PASS | **OVERRIDE-FAIL** | Code-only ("`ShotHistory.Count>0 → teeGate=false`"). Never exercised — no fire event, no stroke-2 idle log, no next-hole log. Rule 6. → Fix #6. |
| 9 | Putter tee stroke: consistent with TurnCount==1 gate | PASS/N-A | **CONFIRMED-PASS** | Correct reading of SPEC ("Non-tee strokes never glow" = shot-count-based, not club-type-based). No club-type filter in code by design. |
| 10 | Glow never blocks input (raycastTarget=false) | PASS | **CONFIRMED-PASS** | `BuildGlowObject()` line 343: `_glowImage.raycastTarget = false`; the new CanvasGroup on HandleGlow sets `blocksRaycasts=false` (line 330). Both set at construction, never overwritten. |
| 11 | Debug toggles restore old look / disable glow | PASS | **OVERRIDE-FAIL** | `[Fix3-Final] legacyOnBallUnchanged=True` covers the ball-mirror-off side via a forced-cone log. No screenshot at `debugLegacyTranslucency=true` (which is trivially provable). No log or screenshot at `debugDisableIdleGlow=true`. Rule 5 (implementer uncertainty → FAIL) + Rule 6 (unverified). → Fix #7. |
| 12 | Bot path: no NRE | PASS | **CONFIRMED-PASS** | `NotifyOtherInteraction` guards `if (s_instance != null)`; `ClubHandleDragger` uses `_glowController?.OnHandleTouched()`. Both null-safe. BotDriver has no pointer-event path. Verified in code fresh. |
| 13 | No white-box placeholders | PASS | **CONFIRMED-PASS** | Frame renders real driver sprite, real world environment, no pink missing-ref markers. But see item 14 for placeholder-adjacent evidence. |
| 14 | `[SerializeField]` refs wired | PASS | **CONFIRMED-PASS** | Scene YAML shows all new fileIDs non-zero: `_glowController={1443870538}`, `_coneGroup={1838493592}`, `_handleCanvasGroup={1443870537}`, `_shotController={1483952040}`, `_mapViewController={2072667396}`, `_baseAlpha=1`. Verified fresh via git diff of the scene. |
| 15 | Console: no task-related errors | PASS | CONFIRMED (trust) | Not re-run in a fresh play session by me; report's console excerpts match the expected AeroDiag noise + new task logs. Not independently blocking. |
| 16 | Spec deviations flagged | PASS | **OVERRIDE-FAIL** | Report's § Spec deviations lists 3 items but **omits the `shotUIVisible` deviation**. SPEC line 100–103: `armed = (GameSession.TurnCount == 1) && state == ShotState.Idle && !dragging && shotUIVisible`. Code line 143: `bool armed = teeGate && shotIdle && !_dragging;` — no `shotUIVisible` term. Deviation is undeclared. → Fix #9. |
| — | HandleGlow renders BEHIND handle (sibling at lower index) | PASS | **CONFIRMED-PASS** | Read `BuildGlowObject()` fresh: `_glowGo` SetParent to `transform.parent`; `SetSiblingIndex(handleIdx)` moves HandleGlow to ClubHandle's index (Unity semantics: pushes ClubHandle to handleIdx+1). Correct fix for iter-1's rendering deviation. |

## OVERRIDE-FAIL items (summary count for the router)

10 OVERRIDE-FAILs (items 1, 2, 5, 6, 7, 8, 11, 16 + the non-sanctioned capture + the empty `videos/` gap = a Rule-2 heavy tally, plus one visual/frame gap and one deviation-disclosure gap).

## Concrete fix list for iter-3 (surgical, root-cause-oriented — carrying iter-1 numbering forward per prompt)

Iteration budget notice: **This is iter-2. Iter-3 is the last permitted; a third failure of shape `shot-ui:translucency-glow-wiring` trips the circuit-breaker into forced `ARCHITECT_REVIEW_ESCALATE`.** Fix these in order and STOP the moment you find a defect the SPEC cannot resolve — do not burn iter-3 on re-shoots that cannot succeed.

**Fix #1 (was iter-1 Fix #3 + Fix #1 combined) — Capture Part A at true first-tee-idle, and STATE the state.**
The canonical frame's ball and cone are BOTH invisible. Before capturing, log `ShotState` + `_coneGroup.alpha` + `GameSession.ShotHistory.Count` + `GameSession.TurnCount` at capture time and paste those 4 values into the report next to the frame. If those values are `Idle / 0.25 / 0 / 1` but the frame still shows no visible ball/cone, the ShotUI is being hidden by something else (CanvasGroup on a parent, Mask, etc.) — diagnose and disclose. If the values are NOT `Idle / 0.25 / 0 / 1` at boot-idle (e.g. cone alpha is actually 0, or ShotHistory has entries), that is a **SPEC assumption failure** — set `IMPLEMENTER_BLOCKED` and surface, do NOT re-shoot at a different moment. The canonical Part A screenshot MUST show a translucent cone AND a visible faint ball. If they cannot be produced, that is the ESCALATE trigger.

**Fix #2 — Prove Part A's rise via a real state transition, not a forced cone value.**
`[Fix3-Final]` writes to `_coneGroup.alpha` directly. Replace with: drive the real state machine into `Aiming` or `Pulling` (via a real pointer drag on the handle, or a bot-driven `BeginExternalDrag`), log `[BallReal] state={state} coneAlpha={cg.alpha:F3} ballAlpha={image.color.a:F3}` every frame for 2 s across an Idle→Aiming transition. The 0.25→~1.0 rise must be captured in the LIVE log stream, not via forced values.

**Fix #3 — Real Spin `onClick`, not `NotifyOtherInteraction()` direct call.**
`ActionButtonWidget.OnEnable` line 19 already wires the button. In your test, find the Spin button GO in the scene (`GameObject.Find("SpinButton")` or via `SpinButtonWidget` component lookup), call `spinButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke()`. That IS the real player entry path (an on-device tap does the same). Log `[GlowFrame]` before/after; the timer must reset via the real listener chain, not the static method call.

**Fix #4 — Real modal open, not reflection into `AnyOverlayOpen`.**
Find the ClubSelector or BallSelector overlay in the scene, call `.SetActive(true)` on its root GameObject — that runs `OtherButtonsFader.OnEnable` (line 26 real writer). Wait one Update, then `.SetActive(false)` — that runs `OtherButtonsFader.OnDisable` (line 49). Log `[ModalReal] AnyOverlayOpen={OtherButtonsFader.AnyOverlayOpen} glowActive={_glowActive} timer={_idleTimer}` at each step. This proves the real pause→restart branch, not the reflection-forced branch.

**Fix #5 — Grab mid-glow proof via real drag or bot drag.**
No code-only PASS here. Trigger a real pointer-down on `ClubHandle` (Unity `PointerEventData` injection or `bot.BeginExternalDrag()`), let the fade run (~0.15 s), release with no power, and confirm from a fresh `[GlowFrame]` log that (a) glow fades within 0.15 s of pointer-down and (b) restarts after another ~5 s of idle post-release.

**Fix #6 — Fire the shot and confirm stroke-2 no-glow via ShotHistory.**
No code-only PASS. Actually fire a shot via the real flick input (or bot flick), wait for `GameSession.ShotHistory.Count == 1`, wait 10 s of idle, log `[GlowFrame]` throughout — must show `armed=false, glowActive=false` continuously.

**Fix #7 — Screenshot the two debug toggles, not just claim them.**
Two screenshots at the same angle: (a) `debugLegacyTranslucency=true` — ball must render fully opaque (old look); (b) `debugDisableIdleGlow=true` after waiting >5s — no glow ever, log `[GlowFrame]` never fires.

**Fix #8 — Delete or replace `glow_live_20260807_132417.png`.**
It is a desktop OS screenshot of the whole Unity Editor (Scene/Game tabs, 0.66× scale readout, editor chrome all visible), not a sanctioned Rule 0 capture. Either remove it from `screenshots/` or replace with a proper `mcp__ai-game-developer__screenshot-game-view` frame at 1170×2532 from a MOMENT the [GlowFrame] log confirms `glowActive=true alpha>0.5`.

**Fix #9 — Disclose the `shotUIVisible` spec deviation, or implement the check.**
SPEC (line 100–103) requires `armed = ... && shotUIVisible`. Code (`TeeIdleGlowController.cs` line 143) omits it. Either add the check (`_shotConeView.IsVisible` reference, wire in Inspector) or add a `## Spec deviations` bullet explaining why the omission is safe (e.g. "ShotUI parent CanvasGroup already gates render — glow won't be visible when ShotUI is hidden regardless of `_glowActive`"). Currently silently deviating.

**Fix #10 — Explain the raw localization keys and `DRIVER 0 yrds`.**
Both canonical frames show `GAMEPLAY_STRAIGHT` (raw key) and `DRIVER 0 yrds`. Per memory `reference_no_recompile_during_play`, a domain reload during play-mode nulls `LocalizationManager` statics. Rule 6 (report integrity): explain in the report either (a) a domain reload occurred during the session and here's the mitigation for the next run, or (b) this is a pre-existing boot issue unrelated to the task and here's the same-symptom repro on HEAD without your changes. Do not ignore.

**Fix #11 (kickoff-primary) — Ship a real-play video.**
`videos/` is empty. The [GlowFrame] log is a solid measurement for onset timing, and I gave it credit at item 4. But video is the standing sign-off artifact (feedback_video_confirmation_always). ≥15 s clip covering: real-tap-Spin (timer reset) → 5 s countdown → visible pulse cycle → real modal open (fade-out) → real modal close → 5 s countdown → visible pulse cycle → real handle grab (fade-out). If video is truly not feasible, state exactly why per memory `feedback_prefer_bot_videos`.

## Iteration count

N = 2. Next iteration (3) is the last one before the circuit-breaker triggers `ARCHITECT_REVIEW_ESCALATE` per PIPELINE_HARDENING §1. The fix list above is deliberately surgical and root-cause-oriented so iter-3 can either pass cleanly OR clearly identify the ESCALATE trigger (SPEC assumption failure on ShotUI idle state).

## Routing

`BACK_TO_IMPLEMENTER` — Fixes #1–#11 above.
