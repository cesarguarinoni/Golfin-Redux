# Architect Review — `ball_flight_trail`

> Written by `golfin-reviewer` subagent. Iteration **2** of architect review (iter-1 was bounced for scene corruption + fake evidence; resolved in iter-2). Two-gate review: a PASS here sets STATUS to `READY_FOR_REDTEAM` and hands to `golfin-redteam-reviewer`, which is the only agent that may advance to `ARCHITECT_REVIEW_PASS`.

**Reviewed:** 2026-06-03 (JST)
**Task tier:** TELLCODE visual/code task. Rule 16 mesh-metrics gate and Rule 17 mesh-bake-video gate do NOT apply (no `green.json`/`TerrainData`/mesh-cut/skirt-vertex content).

---

## Independent pixel scan (Step 0 — performed BEFORE reading SPEC/report/self-review)

I opened the canonical screenshot `screenshots/trail_ob_recolor.png` first.

The image is a downward-tilted PhysicsLab view: a vertical paved grey path runs through the middle of the frame; to the right of it is a thick, uniformly-colored **bright red ribbon** descending roughly parallel to the path from upper-mid frame down past the green pad and flag-stick on the right. The red is saturated and uniform along the full visible length of the ribbon — there is no surviving blue segment, no gradient transition between colors, and no isolated red tip; the WHOLE ribbon reads red. Color appears consistent with `#FF2E2E` (saturated red, slight pink-leaning rather than blood-red, matching the spec value). HUD chips are unchanged from the other state captures (SPIN/Lv1/TURN 5, GOLFIN avatar, HOLE 18-REGULAR / PAR 5 / 481 yds, DRIVER 250 yrds). The ball itself is not clearly visible at any tip of the ribbon — consistent with the SPEC's "ribbon stays for visual reference until next shot" behavior.

Second-pass scan of `trail_blue_inflight.png`: same camera framing, "TURN 1", a saturated **cyan-blue ribbon** hugging the grey path along its centre. Consistent with `#2E9BFF`. Third-pass scan of `trail_gold_inflight.png`: same framing, "TURN 3", a warm **gold/yellow ribbon** of the same length, distinctly different hue from the blue version. Consistent with `#FFD24A`. Three states are visually distinct, in the claimed colors, and each shows an actual deposited ribbon — not a pre-shot aiming frame.

---

## Video motion verification (frame-extracted from `videos/ball_trail_motion.mp4`)

`ffprobe`: 1080×1920 **portrait** (the report says "1920×1080 landscape" — minor inaccuracy flagged below), 19.125 s, 8 fps, 153 frames, H.264, 1.5 MB. Caption strip burned in.

I extracted frames at 1, 2, and 4 fps and inspected:

- **Blue segment (0–8 s):** Real, frame-distinct gameplay motion. Across `g_002`, `g_004`, `g_006`, `g_010` the ribbon visibly **grows** behind a ball that moves across the frame; distance label ticks down (242 → 163 → 112 yds → at-rest). This is genuine flight motion, not a slideshow. PASS for "blue ribbon forms and follows the ball."
- **Gold segment (~9–17 s):** Frames `g_010..g_026` and `h_055` show a yellow vertical ribbon along the path. Less obviously kinetic than the blue segment because the camera angle is more top-down/aerial, but the ribbon is visible and gold. Frame-extracts are MD5-distinct.
- **OB recolor moment (~17–18 s):** This is the critical claim. Frames `h_055` (gold visible) → `h_057` (faint blue ribbon visible — the new shot's deposit) → `h_059` (the same vertical segment is now **bright red**) → `h_060` (red, slightly thinner due to the alpha fade) capture the recolor in motion. The red segment in `h_059` is the same geometry that was blue in `h_057`, recolored entirely — not just a red tip. Combined with the still `trail_ob_recolor.png` (which shows the recolor from a more flattering angle), the whole-ribbon flip is demonstrated.

The OB shot in motion is triggered by the `#if UNITY_EDITOR` `ForceOBRecolorForCapture()` seam because flat PhysicsLab has no OB zone. I verified the seam (`BallTrailController.cs` L202-208) executes the IDENTICAL two-line code path (`SetRibbonColor(_obColor); _tr.emitting = false;`) as the production `c.Next == BallState.OB` branch (L91-99). The only difference is the trigger source (seam call vs `BallStateMachine` event). This is a legitimate substitute and consistent with the SPEC's bot-seam allowance.

---

## Diff verification

| Check | Result | Evidence |
|---|---|---|
| `git diff Assets/Scripts/Physics/Viewer/BallAnimator.cs` | EMPTY (PASS) | Ran; zero output. SPEC hard requirement (no BallAnimator diff) is met. |
| `git diff --stat` LabScaffold.unity | 21 lines added, 0 removed (PASS) | Identical to self-reviewer's number. Confirms the surgical-diff claim. |
| Scene diff body | Only the documented mutations (PASS) | (a) `1075126837` component-add on BallAnimator's `m_Component` list, (b) the BallTrailController MonoBehaviour block with `_flightColor`, `_obColor`, `_perfectColor`, `_trailMaterial` (guid 554ba121...), `_time=8`, `_minVertexDistance=0.3`, `_startWidth=0.09` — all matching SPEC values, (c) `_ballTrail: {fileID: 1075126837}` ref on PhysicsLabController. **No** `m_IsActive: 0`, **no** removed GOs, **no** transform/`sizeDelta` drift, **no** unrelated mutations. The exact mutation predicted by the SPEC. |
| `git diff ShotController.cs` | 6 added lines (PASS) | `LastShotWasClean { get; private set; }` property + `LastShotWasClean = !IsPutt && Mathf.Approximately(degradYaw, 0f);` latched immediately after `degradYaw` is computed. Exactly per SPEC Change A. **`!IsPutt` guard present (Gate 3 latch logic confirmed).** |
| `git diff PhysicsLabController.cs` | 1 field + 1 line + cosmetic alignment (PASS) | `[SerializeField] BallTrailController _ballTrail;` added in the existing serialized block; `_ballTrail?.Configure(ballAnimator, _ballSM, _shotController);` called in `Awake()` after `_ballSM` creation and the shotController null-check. Exactly per SPEC Change C. |
| GO count in scene | 285 (PASS) | Confirms the self-reviewer's grep result and HEAD parity. |

---

## Architectural / cross-cutting checks

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries | PASS | `Golfin.Physics.Viewer` asmdef already references `Golfin.Gameplay.Loop` (for `BallStateMachine`/`BallStateChange`) and `Golfin.Gameplay.Input` (for `ShotController`). No new asmdef edits required. No autoref violation, no backdoor. |
| Pattern adherence | PASS | One MonoBehaviour, serialized config, idempotent Configure with explicit unsubscribe-then-subscribe, OnDestroy unsubscribe, lazy `EnsureTrail` covering both prefab AND fallback-sphere paths via `GetComponentInChildren` → `AddComponent`. Matches the established "external system wired by a controller" pattern in the Viewer assembly. |
| Logic duplication | PASS | Uses `MaterialPropertyBlock` directly (standard Unity), no homegrown color-renderer wrapper. Reuses the existing `BallAnimator.CurrentBall` access surface. No code duplicated. |
| Spec intent vs letter | PASS | Implementation is precisely what the SPEC's "Why this design" section reasons through: ride a TrailRenderer on the respawned ball so the per-shot self-clear is free; recolor via `_BaseColor` MPB so already-laid segments flip; never touch `BallAnimator`. Intent and letter match. |
| Cross-feature regressions | PASS | Touch surface is minimal: `_ballTrail?.Configure(...)` is null-conditional, so a scene without the component is unaffected; the new property on `ShotController` is read-only and additive (no signature change); no `OnShotResolved` change. 16-fairway and other systems unaffected. |
| Latent bugs / null safety | PASS | `_anim`, `_tr`, `_sm`, `_mpb` all null-checked at use; `Configure` is idempotent under domain reload (explicit unsubscribe before subscribe); `OnDestroy` unsubscribes; `MaterialPropertyBlock` is lazy-initialised; `EnsureTrail` no-ops if a `TrailRenderer` already exists. No obvious order-of-init issue. |
| Putt exclusion (NOTE P) | PASS | `LastShotWasClean = !IsPutt && Mathf.Approximately(degradYaw, 0f);` — putts can never set the latch, so the gold branch in `HandleStateChanged` cannot fire on a putt. Gate 3 (`putt → _flightColor never perfect`) is satisfied by construction. |
| Material asset | PASS | `Assets/Art/3D/Balls/BallTrail.mat`: shader `0406db5a14f94604a8c57ccfbc9f3b46` (URP Particles/Unlit), `_Surface: 1` (Transparent), `_Blend: 0` (Alpha), `RenderType: Transparent`, `_SURFACE_TYPE_TRANSPARENT` keyword enabled, `SHADOWCASTER`+`DepthOnly` passes disabled. Respects `_BaseColor` per URP convention. Matches SPEC Change D. |
| Trail tuning per SPEC | PASS | `BallTrailController.EnsureTrail` sets `time=_time` (8), `minVertexDistance=0.3`, `numCapVertices=0`, `numCornerVertices=0`, taper widthCurve from `_startWidth` (0.09) → 0, alignment=View, textureMode=Stretch, gradient white-RGB / alpha 1→0, all shadow/probe usage Off, `emitting=false` on creation. Every bullet in SPEC Change D matched. |

---

## Visual fidelity verdict

| State | SPEC color | Screenshot color reading | Match? |
|---|---|---|---|
| In-flight default | `#2E9BFF` blue | `trail_blue_inflight.png` reads as saturated cyan-blue along the path | YES |
| Perfect shot | `#FFD24A` gold | `trail_gold_inflight.png` reads as warm yellow-gold, clearly distinct from blue | YES |
| OB (whole-ribbon flip) | `#FF2E2E` red | `trail_ob_recolor.png` reads as uniform saturated red along entire ribbon | YES |
| OB flip happens in motion | "entire ribbon flips at OB transition" | Video `h_057`→`h_059` shows blue→red on same geometry, not just a red tip | YES |
| Putt | blue (never perfect) | No visual (flat lab has no putt-only context), but `!IsPutt` latch confirmed in code | YES (logic) |

---

## Acceptance gate walk-through

| Gate | Verdict | Reasoning |
|---|---|---|
| 1. `Aiming→Flying`, `emitting==true`, blue for degraded | PASS | `trail_blue_inflight.png` + video segment A frames `g_002..g_010` show ribbon forming behind moving ball. EditMode readback cited in report: `rgb=(0.180,0.608,1.000)`=#2E9BFF. |
| 1b. Perfect (clean Green) → gold | PASS | `trail_gold_inflight.png` + video frames show gold ribbon. Readback: `lastClean=True rgb=(1.000,0.824,0.290)`=#FFD24A. |
| 2. OB → whole ribbon `_obColor` | PASS | Still `trail_ob_recolor.png` shows uniform red ribbon. Motion video frames `h_057`→`h_059` show same-geometry blue→red flip. `ForceOBRecolorForCapture` is line-identical to production OB branch — verified by reading both code sites. |
| 3. Putt → blue never perfect | PASS (logic) | `!IsPutt` guard present in `ShotController.CommitFlick` latch. No visual possible without a putt-capable surface, but the latch logic guarantees gold can never fire on a putt. |
| 4. No BallAnimator diff; unrelated systems untouched | PASS | `git diff BallAnimator.cs` empty. Scene at HEAD GO parity (285). No production wiring of `BallTrailCaptureRunner` (GUID grep on Assets/Scenes + Assets/Prefabs returned zero refs). |

---

## Housekeeping verdict (carry-forward from SELF_REVIEW)

The self-reviewer flagged 5 housekeeping items; 4 have already been resolved (stale iter-1 PNGs deleted, slideshow video deleted, raw uncaptioned video deleted, vidframes PNG dump deleted). The folder contains only the 4 cited screenshots + the canonical captioned MP4 — clean.

**One item remains:** `Assets/Scripts/Physics/Viewer/BallTrailCaptureRunner.cs(.meta)` — dev scaffolding. Verified inert: (a) `#if UNITY_EDITOR` guarded, so excluded from player builds, (b) GUID `addaceca5a83f4b3fbc5b0d54123193e` does NOT appear in any scene or prefab YAML (`grep -r` returned zero matches in `Assets/Scenes/` and `Assets/Prefabs/`), so it cannot auto-run, (c) report explicitly states "They will be removed at task close-out after this review pass."

**Decision:** I am **NOT requiring deletion before redteam** but I am requiring it before Cesar's final approval / DONE. Rationale:
- The runner is the only path that exercises the `ForceOBRecolorForCapture` seam at capture time; keeping it through the red-team gate gives Cesar the option to re-record if redteam asks for tighter or different evidence without forcing the implementer to re-author it.
- It's verifiably inert (no scene wiring, editor-only). Risk of regression in player builds is zero.
- The `ForceOBRecolorForCapture` seam on `BallTrailController` itself is fine to ship as a permanent QA/bot hook (SPEC sanctions `#if UNITY_EDITOR` bot seams in Change B).

**Action item for close-out (NOT blocking redteam):**
1. Delete `Assets/Scripts/Physics/Viewer/BallTrailCaptureRunner.cs` and its `.meta` at the close-out commit, OR move it to a dedicated `Tools/` or `Assets/Editor/Diagnostics/` location with a comment marking it as a permanent debug runner. Implementer's stated plan ("removed at task close-out") is acceptable.

**Minor inaccuracy in IMPLEMENTER_REPORT.md (non-blocking, should be corrected on next touch):**
- Report says video is "1920×1080" landscape; `ffprobe` shows 1080×1920 portrait. Doesn't affect verdict; should be corrected.

---

## Verdict

`PASS` → STATUS → `READY_FOR_REDTEAM`

The four acceptance gates are met. Diffs are surgical (21-line scene diff, 6-line ShotController diff, 4-line PhysicsLabController diff, zero BallAnimator diff). The OB whole-ribbon recolor is demonstrated both as a still AND as a same-geometry blue→red flip in the motion video, with the seam shown to be line-identical to the production code path. Material, asmdef, namespace, and pattern adherence are all correct. The folder housekeeping is clean except for an inert editor-only capture runner that I am permitting through the red-team gate but flagging for deletion at close-out.

Handing to `golfin-redteam-reviewer` as the adversarial second gate.

## Open questions for Cesar

None.

## Lessons captured

- For runtime VFX features without a Figma reference, the canonical screenshot + a frame-extracted motion video together can substitute for the bbox/pixel-level Figma comparison normally required of UI tasks — provided the video shows the dynamic claim (here, the whole-ribbon recolor) on actual gameplay geometry, not a slideshow.
- `MaterialPropertyBlock._BaseColor` propagates to already-laid `TrailRenderer` geometry at draw time on URP Particles/Unlit shaders, because the shader samples the per-renderer block per draw call rather than per-vertex. This is the right primitive for "recolor a streaming line at a state-change moment."
- When the production trigger for a state-change cannot be exercised in the test lab (flat PhysicsLab has no OB zone), a `#if UNITY_EDITOR` capture seam that calls the EXACT same code path as the production branch is a legitimate substitute, provided the reviewer reads both sites and confirms line-identity.

---

# RED-TEAM REVIEW (adversarial gate) — `ball_flight_trail`

> Written by `golfin-redteam-reviewer`. **Reviewed: 2026-06-03 08:50 CEST.** I did not trust the reviewer's PASS; I re-generated every piece of evidence and tried three ways to break it.

**Verdict: `ARCHITECT_REVIEW_PASS`** — I attacked all 7 prior defects + 3 independent break vectors and could not find a real blocker.

## Evidence I generated/verified myself (not re-used from the report)

- **My own GO count:** `grep -c '^--- !u!1 &'` working = **285**, `git show HEAD` = **285**. `m_IsActive: 0` count working = **22**, HEAD = **22** (no new disables). Scene `--numstat` = 21/0 (zero deletions). Full `git diff` body = ONLY (a) component `1075126837` added to BallAnimator's `m_Component`, (b) the BallTrailController MonoBehaviour block (serialized `_flightColor`=#2E9BFF, `_obColor`=#FF2E2E, `_perfectColor`=#FFD24A, `_trailMaterial` guid 554ba121…, `_time`=8, `_minVertexDistance`=0.3, `_startWidth`=0.09 — all SPEC-exact), (c) `_ballTrail: {fileID: 1075126837}` ref on PhysicsLabController. Nothing else.
- **My own MD5 of all 4 screenshots:** mutually distinct (`e3b6d277…`, `88358c98…`, `a942bb36…`, `e189b36e…`). I OPENED each: `trail_blue_inflight` = real blue ribbon down cart path (TURN 1); `trail_gold_inflight` = real gold ribbon (TURN 3); `trail_ob_recolor` = uniform red ribbon full length (TURN 5, no surviving blue); `trail_vid_extract_blue` = blue ribbon video frame (md5 matches video frame @4s — a real extract).
- **My own video frame extraction:** `ffprobe` = 1080×1920 portrait, 19.1s, 8fps, 153 frames, H.264. I extracted a 13-frame spread (0/2/4/9/12/16/17.0/17.5/17.8/18.0/18.3/18.6/19.0s) — **all 13 MD5-distinct** (genuine motion, not a slideshow). I then extracted all 153 native frames and ran a PIL pixel-classifier on the **central play column** (excluding HUD chips + the burned-in caption band): blue ribbon frames 1–41, gold 43–108, **blue 110–115 → RED 116–118** (1117 red px, blue→0). I visually confirmed `a_0110` (thin blue ribbon centre) → `a_0116` (same geometry, fully red): a genuine whole-ribbon blue→red flip in motion on real gameplay geometry.

## Prior-defect replay (each with my own verdict)

| # | Prior defect | Verdict | Proof I generated |
|---|---|---|---|
| 1 | Scene corruption (deleted 35 GOs) | **GONE** | 285=285 GOs, 22=22 m_IsActive:0, numstat 21/0, diff body = only the 3 documented mutations. |
| 2 | Fake/duplicate evidence | **GONE** | 4 PNGs MD5-distinct & visually correct; 153-frame video, my 13-frame spread MD5-distinct; classifier found a real central-column blue(f110)→red(f116) flip, visually confirmed. |
| 3 | OB seam ≠ production path | **GONE** | Seam L202-208 (`if(_tr==null)return; SetRibbonColor(_obColor); _tr.emitting=false; Debug.Log`) is recolor-line-identical to production L91-99 (`SetRibbonColor(_obColor); _tr.emitting=false`); only adds a log; same `_obColor` source; no re-clear/re-emit. Real OB branch IS reachable: `BallStateMachine` sets `terminalState=BallState.OB` from Water/HitOOB/ExitedWorldBounds and fires it via `OnStateChanged` with `Next==OB`. |
| 4 | BallAnimator diff | **GONE** | `git diff …/BallAnimator.cs` = empty (zero bytes). |
| 5 | Putt reads perfect | **GONE** | `LastShotWasClean = !IsPutt && Mathf.Approximately(degradYaw, 0f)` — `!IsPutt` present; `IsPutt` is a real `{get;set;}` member (L21). |
| 6 | Compile / leftover auto-run | **GONE** | No `error CS`/exceptions in Editor.log; capture run logged `=== ALL CAPTURES COMPLETE ===` + `Temp runner removed`. `BallTrailCaptureRunner` GUID `addaceca5…` absent from all scenes/prefabs/resources; plain MonoBehaviour, no `[InitializeOnLoad]`/`[RuntimeInitializeOnLoadMethod]`/`[ExecuteAlways]`, fully `#if UNITY_EDITOR`. Genuinely inert — close-out deletion item, NOT a blocker. |
| 7 | Rule 13 drift | **GONE** | All 6 task-code paths (LabScaffold.unity, ShotController.cs, PhysicsLabController.cs, BallTrailController.cs+meta, BallTrailCaptureRunner.cs+meta, BallTrail.mat+meta) are in the report's Files table. All other drift (NuGet, Packages, Taiheyo metas incl. new Hole05/06, h07 captures, Diag regression .md, capture-all-holes.mjs) is pre-existing course/tooling drift, not task-code. |

## My three break-attempts (all failed)

1. **Visual** — hunted the OB window for a red-tip-on-blue-body or a fresh-short ribbon at the recolor moment (the named iter-1 failure). Found a *complete* single-frame whole-ribbon flip instead (a_0110 all-blue → a_0116 all-red, identical geometry); no surviving blue segment. Attack failed.
2. **Geometric/metric** — classified ribbon pixels across all 153 frames; state transitions blue→gold→blue→red are clean and complete with no partial/fragile intermediate. Serialized scene colors match SPEC hex exactly (#2E9BFF/#FFD24A/#FF2E2E). Material has `_BaseColor` (the property `SetRibbonColor` writes), `_Surface:1` Transparent, `_Blend:0` Alpha. Nothing near a threshold. Attack failed.
3. **Spec-intent** — the SPEC's point is recoloring ALREADY-LAID segments via `_BaseColor` MPB (not startColor/endColor). The video empirically proves the whole laid ribbon recolors at draw-time, and the seam runs the identical production path that is itself reachable from a real OB `BallStateChange`. Intent satisfied. Attack failed.

## Non-blocking note for Cesar (close-out, not a FAIL)

The video's OB-recolor moment is genuinely present (frame 116) but the segment-C camera is near-end-on/top-down, so the flipping ribbon is thin, AND the burned-in caption lags the visual by a few frames (caption still reads the gold line at the frame where the ribbon is already red). This is cosmetic — the still `trail_ob_recolor.png` shows the red flip from a clear angle. If Cesar wants a punchier deliverable, a re-recorded clip with a side-on OB camera + caption-timing fix is worth doing at close-out. Close-out also: delete `BallTrailCaptureRunner.cs(.meta)` (inert dev scaffolding) and fix the report's "1920×1080 landscape" → "1080×1920 portrait" inaccuracy.

**Routing → `ARCHITECT_REVIEW_PASS`.** Hands to Cesar for final approval.

---

## Cesar's final approval

Cesar fills this section after eyeballing the screenshot one last time, after `golfin-redteam-reviewer` runs.

- [ ] Approved by Cesar — task moves to `Docs/Specs/Completed/`
- [ ] Rejected by Cesar — reason: <...>
