# Red-Team Review — `tap_feedback_fx`

> Red-team (adversarial) gate — 2026-06-06 13:25 JST
> Reviewing after golfin-reviewer PASS (STATUS was `READY_FOR_REDTEAM`).
> Posture: hostile skeptic. Default-to-FAIL on uncertainty. Goal: break it before Cesar sees it.

## Verdict

**ARCHITECT_REVIEW_PASS** — I genuinely tried to break this across visual, input-geometry, and
spec-intent/drift attack surfaces and came up empty on every hard blocker. The headline defect
that drove three iterations (sub-pixel / invisible sparkles) is concretely, independently
confirmed fixed in my own freshly-extracted full-res frames.

## Evidence I generated myself (did NOT reuse prior crops)

Canonical video re-shot frame-by-frame: `videos/tap_feedback_demo_v3.mp4` (1170×2532, 11.195s, 682KB — probed myself).

Frames I extracted and zoomed (nearest-neighbor) myself:
- Black-splash multi-touch phase: `/tmp/frame_{1.42,1.46,1.50,1.54,1.58}.png`
- Right-ring zoom 3×: `/tmp/zoom_R_{1.46,1.50,1.54}.png`
- Left-ring zoom 3×: `/tmp/zoom_L_{1.46,1.50,1.54}.png`
- Right-ring lifecycle 2×: `/tmp/seqR_{1.40,1.44,1.48,1.52,1.56,1.62,1.68}.png`
- In-game over 3D: `/tmp/ig_{7.0,8.0,9.0,10.0,10.2,10.5}.png` (t=10.2 is the strongest — two rings over LabScaffold)
- Modal still resized: `/tmp/canon_full.png` (from `screenshots/tap_fx_canonical.png`)

## Prior-rejection replay (iter-1/2 sub-pixel sparkle defect)

Defect: iter-1/2 had sparkles at 0.6px on-screen (startSize 0.06 PS-units × UIParticle.scale 10),
i.e. sub-pixel and invisible across 58 frames. iter-3 fix: `ConfigureSparkles()` overrides
`main.startSize = SparkleTargetPx/UiParticleScale = 8/10 = 0.8` PS-units → 8px on-screen, and
`main.startSpeed = 120/10 = 12` PS-units/s → 120px/s. Verified at `TapFeedbackFX.cs:67,70`.

**Verdict: GONE.** At every black-splash timestamp I sampled (1.40 → 1.58s), BOTH discs contain
2–3 distinct cream-white additive sparkle squares (~8px on-screen) that are visibly brighter and
more saturated than the grey disc surface — not baked specular smudges, distinct additive squares.
The fix is real both in code and in my own pixels.

## Adversarial checks (each could have independently FAILed)

| # | Check | Result | How I verified |
|---|---|---|---|
| 1 | Sparkles render at every surface, not one lucky frame | PASS | Distinct sparkle squares at t=1.40/1.44/1.46/1.48/1.50/1.54/1.56 on black splash; rings over modal (canonical still) and in-game (t=10.2). |
| 2 | Multi-touch = two truly independent effects | PASS | t=1.50 shows two discs ~640px apart, identical style, same instant, each with its own sparkle squares. Code iterates `Touchscreen.current.touches`, skips touchId 0 to avoid double-firing the primary touch (`TapFeedbackController.cs:142-149`). |
| 3 | Renders ABOVE UI incl. modal + in-game 3D | PASS | `tap_fx_canonical.png`: ring sits over the MAINTENANCE NOTICE modal + trophy. `/tmp/ig_10.2.png`: two rings over LabScaffold 3D scene above SPIN/STRAIGHT/GOLFIN/DRIVER buttons. sortingOrder=5000 (`:89`). |
| 4 | Input never intercepted | PASS | No GraphicRaycaster in prefab (`grep` GUID = 0 matches) or code (only comments at `:17,:100`). UIParticle's baked `m_RaycastTarget: 1` (prefab line 5038) is provably INERT — no GraphicRaycaster on the `[TapFeedback]` canvas to query it. Image raycastTarget=0 (prefab line 62) + set false in code (`:94`). Input is read-only `wasPressedThisFrame`. `EnhancedTouch` appears only in a comment, no `.Enable()`. `git diff HEAD -- InputSimulationBootstrap.cs` empty (name-status confirmed). |
| 5 | Scene-mutation audit | PASS | `git status --porcelain` + grep `.unity` → ZERO scene changes anywhere. Only new .prefab is `Assets/Resources/UI/TapFeedbackFX.prefab`. No `m_IsActive: 0` / sizeDelta / position mutations to existing scenes. |
| 6 | File-table / drift integrity (Rule 13) | PASS | Task-caused outside task folder: `Packages/manifest.json` (ui-particle add), `packages-lock.json`, `ProjectSettings/ProjectSettings.asset` (preloadedAssets), `Assets/Scripts/UI/TapFeedback*.cs(.meta)`, `Assets/Resources/UI/TapFeedbackFX.prefab(.meta)`, `Assets/Prefabs/UI/TapSparkle_Additive.mat(.meta)`, `Assets/ProjectSettings/UIParticleProjectSettings.asset(.meta)`, `Assets/Scripts/UI/Editor/TapFeedbackDemoRecorder.cs(.meta)`. Report table materially accurate. Minor omissions (`ProjectSettings/ProjectSettings.asset`, `Assets/ProjectSettings.meta` folder meta) are auto-generated installer artifacts — disclosure nit, not a violation. MCP bump (0.77→0.78 + 4 subpackages) is PRE-EXISTING drift: HEAD manifest has 0.77.0 and no subpackages, and `Packages/manifest.json` was already `M` in the iter-1 baseline porcelain (09:20:08Z), so it was dirty BEFORE the task. Not penalized. |
| 7 | Subtlety | PASS | `_peakAlpha=0.5`, ring 0.30s + sparkle 0.45s (both ≤0.5s). Black-splash corner mean gray = 0.0 → NO dimming overlay / screen darkening. No GraphicRaycaster → no layout shift possible. |
| 8 | Persistence-from-boot + DontDestroyOnLoad + sortingOrder | PASS | `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` bootstrap, `DontDestroyOnLoad(root)` (`:83`), sortingOrder=5000 above Toast(950)/Loading(1000). Video shows FX active splash → in-game across scene load. |
| 9 | Knob filled disc as "soft glow ring" | ACCEPTABLE | Disc reads as a soft glow with center→edge falloff at peak alpha 0.5; openly flagged deviation; both reviewers accepted. Material ripples are typically filled translucent discs, not hollow outlines. Polish follow-up if Cesar wants a hollow ring shader — not a blocker. |

## Three break-attempts and why each failed

1. **Visual.** Tried to find a frame with absent/sub-pixel sparkles or a white-box placeholder.
   Failed — sparkles present at every sampled timestamp on max-contrast black, plus in-game and
   modal. The only genuine softness vs the literal spec: sparkles cluster INSIDE the disc rather
   than "drifting outward" past its rim, and the ring is a filled Knob disc rather than a hollow
   ring. BOTH are transparently flagged spec deviations, reviewer-accepted — not hidden defects.

2. **Geometric / input.** Tried to find input interception or a double-fire. Failed — no
   GraphicRaycaster anywhere, UIParticle's raycastTarget=1 is inert without a raycaster,
   read-only polling, touchId-0 skip prevents primary-touch double-count, InputSim untouched,
   EnhancedTouch never enabled.

3. **Spec-intent / drift.** Tried to find scene corruption or mis-attributed task drift. Failed —
   zero `.unity` mutations, only the new prefab; MCP-bump drift pre-existed iter-1; file table
   accurate for task-caused files.

## Residual notes for Cesar (not blockers)

- Two openly-flagged deviations are judgment calls Cesar may want to revisit as polish: (a) the
  ring is a filled grey Knob disc, not a hollow expanding ring outline; (b) sparkles stay inside
  the disc instead of drifting outward past its rim. Neither hides a defect; both are visible and
  disclosed.
- Housekeeping for close-out: superseded `videos/tap_feedback_demo.mp4` (iter-2, 1.16MB) still
  sits beside the canonical `tap_feedback_demo_v3.mp4` — delete before moving to Completed.
- The "canonical" still `tapfx_sparkle_proof_dark_f6418.png` is over a LIGHT sky (weak); the
  genuinely strong sparkle evidence is the black-splash video frames (t≈1.50s) I extracted.

## Bottom line

The system meets the spec's intent (soft + sparkly + subtle + non-consuming + above-all-UI +
from-boot) and the three-iteration headline defect is concretely fixed. I could not break it.
**ARCHITECT_REVIEW_PASS.**
