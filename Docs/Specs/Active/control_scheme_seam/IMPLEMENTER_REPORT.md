# IMPLEMENTER_REPORT — `control_scheme_seam`

**Built:** 2026-09-04 → 2026-09-05, Claude Code main thread (not the subagent chain — Cesar
asked for the spec directly).
**Canonical screenshot:** `screenshots/settings_controls_flick.png` (1170×2532, real navigation).

---

## 1. What shipped

The shot pipeline is scheme-agnostic, the 4-way Control Scheme setting persists and is stamped
on every shot, and **Flick is unchanged** — which is the only requirement of this spec that
could actually fail.

### The seam (SPEC §3.1)

`CommitFlick()`'s tail is extracted to `ResolveAndPublish(...)`; `CommitFlick` keeps its own
maths and calls it. New `ShotIntent` + `CommitExternal(in ShotIntent)` for non-Flick drivers.

Three deliberate deviations from the spec's literal text, each for a reason:

| Spec said | Built | Why |
|---|---|---|
| `ResolveAndPublish(flickMag, aimYaw, timingMul, timing01, spin, fadeDraw)` | + a 7th `fadeDrawMaxTiltRad` param | Today the max-tilt term is gated on the MODE, not the input value: an armed FadeDraw with the handle dead-centre still passes `FadeDrawMaxTiltRad`. Deriving it from `input != 0` inside the shared tail would have quietly changed that. |
| extract "from `PublishShotSfx()` through `OnShotResolved`" | `PublishShotSfx()` stays in each CALLER | The spec's own `CommitExternal` pseudocode puts it in the caller. Keeping it there is what preserves CommitFlick's exact side-effect ORDER, which the same section demands. |
| `BeginExternalDrag(bool ownsTiming = false)` | an **overload pair**: `BeginExternalDrag()` and `BeginExternalDrag(bool)` | Four Editor bots resolve this method reflectively with `Type.EmptyTypes`; one method with a default argument returns **null** at every one of those sites. Two sites that looked it up untyped would then have thrown `AmbiguousMatchException`, so they were changed to `Type.EmptyTypes` (`TreeOccludeFadeCaptureBot`, `PerfBaselineBot`). |

### Assembly placement (SPEC §3.2 explicitly asked for this to be reported)

`ControlScheme` + `ControlSchemeService` are in **`Golfin.Gameplay.UI`**
(`Golfin.Gameplay.UI.Controls`), not `Golfin.Gameplay.Config`.

`Golfin.Gameplay.Config` is `autoReferenced: false`, so Assembly-CSharp — where
`SettingsController`, `ControlsSubmenu`, `InGameSettingsModalController` and `TelemetryHooks`
all live — cannot see it. `GameSession.AppendShotTimingKeys` already documents that exact wall
in its own comment. `Golfin.Gameplay.Input` is `autoReferenced: false` too, so the spec's
fallback does not work either. `Golfin.Gameplay.UI` is `autoReferenced: true` and is literally
where `QualityTierService` — the service §3.2 says to clone — already lives, and TelemetryHooks
already imports it for the `tier` key.

### Telemetry (SPEC §3.5) — one deviation

`shot_taken` carries `scheme`. **But not via `ShotRecord.SchemeId`.** The only production site
that builds a `ShotRecord` is `HoleSessionDriver`, under `Assets/Scripts/Physics/` — a
**standing ZERO-EDIT ban** (CLAUDE.md PIPELINE_HARDENING rule 7). A `SchemeId` field nothing
could populate would have been dead code. Instead `AppendShotTimingKeys` gained an optional
`schemeId` parameter (default `0` = Flick, which is also what an older row with no `scheme` key
means to the dashboard), and `TelemetryHooks` — the only assembly that can see both
`ControlSchemeService` and the session record — passes it.

`controls_scheme_changed` is raised from `ControlSchemeService` through a second, detailed
event that `TelemetryHooks` subscribes to, because `Golfin.Gameplay.UI` does not reference
`Golfin.Telemetry` — the same relay shape `ShotTelemetryRelay` uses for the flick signals.

---

## 2. Tests

**Full EditMode sweep, all assemblies: 2467 passed, 0 failed, 3 skipped.** The three skips are
pre-existing `HoleCompleteDriverTests` entries carrying their own "Stage C1" reasons. Re-run
after every stage, including after the scene, prefab and Assembly-CSharp changes.

`ShotControllerSeamParityTests` (11 tests) compares Flick against the seam on **raw `fp` values
with `Assert.AreEqual` and no delta** — a tolerance would hide exactly the class of bug this
refactor could introduce. Two of them caught real mistakes in my own first draft:

- `SetExternalPower` is `Clamp01`, so 1.2 is **unreachable** through the drag API. The first
  overpower parity test was comparing a clamped 1.0 flick against a 1.2 intent and failing at a
  ratio of exactly 1.2. Overpower parity now goes via `FireDebugShot`, which is the only
  production path that sets `PowerNormalized` above 1.
- `TransitionToIdle` wipes `PendingSpinInput`, so spin set *before* the reset silently became
  zero — the spin test was comparing two spinless shots while claiming to test spin. It now
  pushes spin after the reset and carries a tripwire (spin vs no-spin must differ) so it cannot
  pass vacuously again.

A third assertion was wrong and was removed rather than worked around: `Spin.Rate == 0` on a
putt. `ShotInputBuilder` gives every shot a base spin, so the putt rule is "the HUD's spin input
is ignored", not "there is no spin" — asserted now by comparing against the same putt fired with
no spin.

## 3. The Flick-unchanged proof (SPEC §7)

Rather than eyeball two screenshots, every `Graphic` under `ShotUI_Canvas` was dumped
before and after the re-parent — world rect, active state, colour, raycast flag:

- `evidence/canvas_geometry_before.txt` / `evidence/canvas_geometry_after.txt`
- **Every flick-UI row is identical**: PutterTrack, PutterTimingSlab, ConeMesh, TimingSlab,
  ClubHandle, TargetingLine.
- The rows that do differ are all inside layout groups (ChipStack, DebugPanel, ButtonsRow) and
  differ because the *before* dump caught them unresolved — degenerate zero-width rects with
  `bl == tr` — in a freshly-opened scene. That is a layout rebuild, not the re-parent.

The scene save also baked two unrelated deltas, both reverted so the diff is only this task:
a TMP auto-size `30 → 29.65`, and two dead `BallTrailController` colour fields Unity dropped
because they no longer exist on the script. Final `LabScaffold.unity` diff: **361 insertions,
6 deletions**, the 6 being the re-parent itself.

`ShellScene.unity` is additions only. Verified by diffing the full sorted `m_Name` inventory
before/after: the only entries that move are mine. A `Scrollbar` and a `m_margin: {x: 16}` line
appear as deletions in `git diff` — both are YAML block reordering; the counts are unchanged
(7 Scrollbars before and after, 2 sixteen-px margins before and after).

## 4. Fidelity — what I got wrong first, and how it was caught

Every one of these was found by MEASURING, not by looking:

| Defect | How it was caught | Fix |
|---|---|---|
| Settings icon rendered with a grey plate behind it | alpha histogram: the Figma export was **100 % opaque**; its siblings are 19–35 % | Exporting the sibling Language icon the same way came back opaque too — so it is an export-pipeline artifact, not the node. Rasterised from the node's own SVG instead (`Docs/Scripts/make_controls_settings_icon.py`), placement solved against the Figma export: **mean 1.8/255 per channel**. |
| Submenu labels title-case | reference render is `FLICK` / `PENDULUM` | `fontStyle |= UpperCase` — later REVERTED, see §4b. |
| Submenu labels indented right of `CONTROLS`; font 44 | node `14089:101955` gives text left **120**, radio **1002–1050**, Rubik SemiBold **48**, 75 px pitch | Rebuilt to those numbers — later REVERTED, see §4b. |
| Unselected modal segments rendered mid-grey | sampled the play-mode frame: **(91,95,105)** vs the node's (36,55,77) | See below. |

### 4b. Cesar's correction: the Figma frame lost to the menu it lives in

Cesar, 2026-09-05, on seeing the built row: *"The settings menu options match figma, but they do
not match the other settings in the game (they are selected with a blue rectangle, not a circle on
the side). Change the design to match the other options. Use Language as an example."*

So the radio button is gone and selection is the blue row fill, like Language and Graphics. Every
number is now READ OFF the live Language row rather than re-derived — row rect 862×64 at x=100,
72 px pitch, `S_Common_BGCorner8` sliced, font 44, left-aligned, and **title case** (English and
日本語 are title case, so `UpperCase` would have made Controls the odd one out a second time). The
node-derived geometry from §4 is reverted with it.

Worth recording as a rule, not just a fix: the Figma frame was right about this component in
isolation and wrong about it in context. An internal-consistency check against the neighbouring
submenus would have caught it before Cesar did, and the fidelity gates as written only compare
against the node.

Verified live, not asserted: tapping PENDULUM moves the fill (Flick `(0.20,0.60,1.00)` → navy,
Pendulum navy → blue) and writes `pref=1` in the same call; the canonical frame was re-shot after
the first one caught a stale pre-repaint frame (sampled `(49,153,255)` on Flick and `(38,66,95)` on
the other three to confirm).

### The linear-space defect (worth reading)

The unselected segment was baked as Figma specifies it — 10 % white fill, 55 % white border.
On screen it rendered (91,95,105) instead of (36,55,77).

The project renders in **Linear** colour space, so Unity blends in linear while Figma composites
in sRGB. The linear-blend maths reproduces the measured numbers to the unit — 10 % white over
the card's (11,32,58) gives 91.2, measured 91; 59.5 % gives 203, measured 201 — which is what
identifies the cause rather than guessing at it.

It is not fixable by choosing a better alpha. Solving `a·1 + (1−a)·card_linear` for the target
per channel gives **a = 0.0144 (R), 0.0241 (G), 0.0333 (B)** — three different alphas. One
white-with-alpha sprite cannot reproduce an sRGB-composited colour under linear blending. So the
composite is baked opaque, the same pre-composite the GPS screens' `A(o, a, backdrop)` helper
does. Trade-off stated in the baker: the segments no longer follow the card if the card is
re-skinned; the card is a fixed sprite measured at a near-uniform (11,32,58)…(9,27,52).

Both segment sprites were validated against the node render before use: outside the label band,
**worst ΔRGB 29 (selected) and 18 (unselected)**, both at an antialiased corner pixel.

## 5. Localization — published, not just written

`SETTINGS_CONTROLS` + the four scheme labels, EN + JA.

Writing the CSV was **not** enough and this nearly shipped broken: the runtime reads a generated
`LocalizationTextTable.asset`, and the labels rendered as raw keys on screen until
`Tools/Localization/Import Text CSV` was run. Caught by reading the frame, not by assuming.

Full pipeline, per SPEC §4:
1. `import_content.py --catalogs texts` → PLAN: **5 add, 0 change, 0 conflict**
2. `--apply` → 5 drafts at `min_build 2701`
3. `content_publish` RPC → **texts v37 → v38**
4. `export_content.py --check` → **clean**; `content_version.txt` stamped `texts=38`

Zero new hardcoded `.text` literals: every label is a `LocalizedText` key.

## 6. Verified in a real hole (Lomond 2, via Home → PLAY → hole card)

Everything below was driven through the player's own entry points and read back from live state,
not asserted:

| Acceptance | Evidence |
|---|---|
| Both surfaces share one value | Pendulum picked in **Settings**; the in-game gear modal opened later showing PENDULUM gold-selected. `screenshots/ingame_modal_controls.png` |
| Segment colours after the linear-space fix | Scanned the segment's top edge row by row: border **(156,165,175)** at y=1109–1111, fill **(36,55,77)** from y=1113 — both the node's targets exactly, 3 px thick, over a card measured at (11,32,58) |
| A non-Flick scheme still plays | `ActiveScheme=Pendulum` **and** `SchemeRoot_Flick active=True`, `ClubHandleDragger activeInHierarchy=True` |
| …and logs once | `PlaceholderSchemeDriver._warned == true` on the Pendulum root, `false` on Needle and FreeSwing — read off the component, not scraped from a log buffer that had already rolled |
| Flick UI unchanged at Idle and Timing | `screenshots/lab_idle_pendulum_selected.png` (State=Idle, ConeRoot alpha 0.25) and `lab_timing_pendulum_selected.png` (State=Timing, driven through `BeginExternalDrag → SetExternalPower → Tick`) — the handle, cone and targeting line are the flick's, while the selected scheme is Pendulum |
| `shot_taken` carries the scheme | Fired a real shot with Pendulum selected; payload = `timing01=null, timing_mul=1, timing_band=null, **scheme=1**` |
| A change mid-swing lands at the next Idle | Switched to Needle during `Timing`: `ActiveScheme` stayed **Pendulum**, `pending=True`, `SchemeRoot_Needle active=False`. After the shot returned to Idle: `ActiveScheme=Needle`, `pending=False`, Needle root on, Pendulum root off, Flick still live |
| No new hardcoded `.text` literals | `grep '\.text\s*=\s*"'` over every file this task touched → no matches. Both surfaces carry 5 `SETTINGS_CONTROLS*` keys each |

The pref was reset to absent (reads as Flick) and the Editor left out of play mode with no dirty
scene.

### Still outstanding

- **On-device pass at 1170×2532 and 16:9.** The modal stack was re-centred after the card grew
  (it was centred before; growing it by 342 broke that), giving 304.75 px clearance top and
  bottom — which is what makes the 16:9 case safe. That is arithmetic, not a device.
- **Dashboard `telemetryData` unit test** (§6.7, "if the suite exists"). It does not: the module
  opens with `import "server-only"`, so vitest cannot load it. Testing it properly means
  extracting the scheme bucketing into a pure module the way `telemetryGacha.ts` is — a refactor
  this spec did not ask for. `tsc` covers the shape and mock mode renders it.
- **The pipeline's own review gates never ran** — Cesar asked for the spec directly, so the
  subagent chain was skipped.

## 7. Working-tree drift NOT mine (CLAUDE.md rule 13)

Present before I finished, not authored by me — **do not include in this task's commit**:

```
 M Assets/Scripts/Gameplay/UI/ShotUI/MapPinIndicator.cs
 M Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs
 M Docs/Specs/Active/map_view_v2/*.json   (6 files)
 M Docs/GPS/GPS_BACKLOG.md
 M Docs/Reports/content_art.txt
 M Docs/TellCode.md
```

The first three are a parallel `map_view_v2` session; the last three predate this session (they
were in the working tree at kickoff).
