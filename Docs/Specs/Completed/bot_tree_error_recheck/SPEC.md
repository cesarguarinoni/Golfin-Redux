# SPEC — bot_tree_error_recheck

**Tier:** 2 — bot behaviour, small diff, log + unit-test gated (no video gate required).
**Priority:** P2. **Status:** SPEC_READY.
**Figma:** N/A — no UI surface.
**Handoff file:** `Docs/Specs/Active/bot_tree_error_recheck/SPEC.md`
**Kickoff:** `Use the implementer subagent on "bot_tree_error_recheck"`

---

## 1. Why

Cesar report (2026-08-06): bots still fire into trees often. Diagnosis (Architect, static audit of
HEAD): **tree_aware_bot (Order 351) is wired and working** — the trunk probe runs in
`VersusBot.TakeShot` after H2 and validates a trunk-clear line. The problem is the block
immediately after it: **2b error injection perturbs the aim AFTER the tree re-check, with no
re-validation** (the 2b block's own comment: *"No safety re-check runs on the perturbed values —
they fire straight to commit"*).

Magnitude: `bot_difficulty.csv` gives level 1–9 opponents `aimErrorDegMax=6.0`, level 10–24 `4.5`.
At driver carry (~230 m), ±6° = **±24 m lateral scatter**, while the tree_aware_bot sweep measured
trunk gaps to the aim line of **1.4 m (Hole_08)** and **0.62 m (Hole_12)**. Low-level bots are
routinely perturbed straight into the tree lines the probe just cleared. The probe cannot help —
it validated a line the bot then doesn't fire.

Fix: make the 2b **aim** sample tree-aware — resample until the perturbed line is trunk-clear
(bounded tries), else fall back to the already-validated pre-2b aim. The miss model is preserved
(bots still spray within their bracket); only samples that would drive into a trunk corridor are
rejected.

**Known causes intentionally NOT addressed here** (documented for triage of any remaining reports):
canopy contact (tree_aware_bot v1 is trunk-only by design → `canopy avoidance v2` if wanted) and
the flat-Y elevation blind spot (accepted v1 limitation, Hole_05 case in the sweep).

---

## 2. Scope

### In
- `BotTreeProbe`: new pure static helper `TrySampleTrunkClearAimError(...)` (deterministic-testable
  via injected sampler).
- `VersusBot.TakeShot` 2b block: route the D2 aim-error sample through the helper. Keep power
  error and club noise (D3) exactly as they are.
- One debug toggle (keep-old-systems rule): `[SerializeField] public bool DebugDisableTreeRecheck = false;`
  on VersusBot — true restores today's unchecked sampling byte-for-byte.
- Keep the carry current: the tree re-aim block currently discards the re-selected carry
  (`SelectShotCalibrated(treeDist, ..., out _)`) — change to `out probeCarry` so the re-check uses
  the carry of the line actually being fired.

### Out (do NOT do)
- Canopy avoidance (v2 task), any probe-window/flat-Y change, any `BotTreeProbe` window tuning.
- Re-checking **power** error or club noise against trees (power changes carry; accepted
  approximation — the probe re-check uses pre-error carry; NOTE in code).
- Re-checking the perturbed line against **water** — 2b can already perturb into water today;
  changing that is a separate decision, not this task.
- `bot_difficulty.csv` retune. BotDriver (it has no 2b error injection). Any sim/CSV edit.
- Putts (existing `isPutt` exclusion mirrors the tree block).

---

## 3. Grounding (verified this session — re-confirm at step 0)

- `Assets/Scripts/Physics/Viewer/VersusBot.cs` — tree block at ~line 647 (`if (!isPutt)` →
  `GetTreeProvider` → `BotTreeProbe.TryFindTrunkClearAim(..., probeCarry, ...)`), 2b block directly
  after it (D3 club noise, then D2 `deltaAimDeg = Random.Range(-bkt.aimErrorDegMax, ...)`).
- `BotTreeProbe.LineHasTrunkInWindows` was made **public** in tree_aware_bot iter-3 (for
  script-execute validation) — the helper can call it directly.
- `trees` is currently a local inside the tree block — hoist it (or re-call
  `_controller.GetTreeProvider()`; it's a trivial getter) so the 2b block can see it.
- `bot_difficulty.csv` (Resources/Data): 1→6.0°, 10→4.5°, 25→3.0°, 50→2.0°, 100→1.0°, 180→0.4°.

---

## 4. Design

### 4.1 Helper (BotTreeProbe.cs — additive)

```csharp
/// bot_tree_error_recheck: sample a 2b aim error whose resulting line is trunk-clear.
/// Pure w.r.t. randomness: caller injects the sampler (UnityEngine.Random.Range in prod,
/// seeded System.Random in tests). trees == null → first sample accepted (treeless no-op,
/// preserves current behaviour exactly).
/// Returns false when maxTries samples were all trunk-blocked → caller uses deltaAimDeg = 0
/// (fires the already-validated pre-2b line).
public static bool TrySampleTrunkClearAimError(
    ITreeObstacleProvider trees, Vector3 ball, float safeYaw, float carry,
    float aimErrorDegMax, int maxTries,
    System.Func<float, float, float> sampleRange,
    out float deltaAimDeg)
{
    for (int i = 0; i < maxTries; i++)
    {
        deltaAimDeg = sampleRange(-aimErrorDegMax, aimErrorDegMax);
        if (trees == null) return true;
        if (!LineHasTrunkInWindows(trees, ball, safeYaw + deltaAimDeg * Mathf.Deg2Rad, carry))
            return true;
    }
    deltaAimDeg = 0f;
    return false;
}
```

Constant: `MaxAimErrorResamples = 5` (const in VersusBot, next to LayupPutterFloor usage).
NOTE: rejection-sampling truncates the error distribution near tree corridors — intended; that IS
the feature.

### 4.2 VersusBot wiring (D2 inside the 2b block — minimal diff)

Replace the raw aim sample only; power error unchanged:

```csharp
if (bkt.aimErrorDegMax > 0f || bkt.powerErrorMax > 0f)
{
    // bot_tree_error_recheck: aim error must not point the shot back into a trunk
    // corridor the tree_aware_bot probe just cleared. Power error unchanged (NOTE:
    // re-check uses pre-error carry — accepted approximation, see spec §2 Out).
    bool clamped = false;
    if (!isPutt && trees != null && !DebugDisableTreeRecheck)
    {
        if (!BotTreeProbe.TrySampleTrunkClearAimError(
                trees, ball, aimYaw, probeCarry, bkt.aimErrorDegMax,
                MaxAimErrorResamples, Random.Range, out deltaAimDeg))
            clamped = true;   // deltaAimDeg == 0 → fire the validated pre-2b line
    }
    else
    {
        deltaAimDeg = Random.Range(-bkt.aimErrorDegMax, bkt.aimErrorDegMax);
    }
    deltaPow = Random.Range(-bkt.powerErrorMax, bkt.powerErrorMax);
    aimYaw  += deltaAimDeg * Mathf.Deg2Rad;
    power01  = Mathf.Clamp01(power01 + deltaPow);
    if (clamped)
        Debug.Log("[VersusBot] 2b tree re-check: all aim samples trunk-blocked — clamped to pre-2b line");
}
```

Also update the existing 2b log line to include a `treeChecked={0|1}` marker so match logs can
count how often the re-check engaged.

Prerequisites in the same file (see §3): hoist `trees`, change tree re-aim's discarded carry to
`out probeCarry`, add `DebugDisableTreeRecheck` field + `MaxAimErrorResamples` const.

---

## 5. Traps

- **Do not touch D3 club noise or the power sample.** Only the aim sample is routed through the
  helper. The 2b block's structure, logging order, and putt behaviour stay otherwise identical.
- **Treeless holes must be byte-identical to today:** `trees == null` path returns the FIRST
  sample — same single `Random.Range` call count as today on that branch. (Random call-count
  parity does NOT hold on tree holes — resampling consumes more draws. That's fine; nothing
  depends on UnityEngine.Random sequence, but confirm no test asserts it.)
- **`probeCarry` staleness:** the tree re-aim currently discards the new carry (`out _`). If left
  discarded, the re-check probes the wrong landing window after a tree layup. The `out probeCarry`
  change is REQUIRED, not cosmetic.
- **Production-safe:** VersusBot ships in player builds — no `#if UNITY_EDITOR` anywhere in this
  diff. Helper stays in `BotTreeProbe` (already production-safe).
- H2/H3/tree-block code above the 2b block: untouched.

---

## 6. Acceptance / Gates

1. **EditMode unit tests** (extend `BotTreeProbeTests.cs`, seeded `System.Random` injected as
   `sampleRange`):
   - trees == null → returns true on first sample, delta within ±max.
   - straight line clear, all sampled deltas clear → returns true, helper called ≤ maxTries.
   - trunk corridor blocking one side: seed chosen so early samples are blocked, a later one is
     clear → returns true with the clear delta.
   - all samples blocked (tight corridor, maxTries small) → returns false, deltaAimDeg == 0.
   - Full suite stays green (baseline 888; expect + new tests).
2. **Log smoke, tree-dense hole:** 1v1 on Hole_12 or Hole_08 with `DebugLevelOverride=1`
   (aim ±6°): 2b log shows `treeChecked=1` on non-putt strokes; occasional clamp line; ZERO
   regressions in H2/H3/tree-re-aim log lines. With `DebugDisableTreeRecheck=true`, logs match
   today's shape.
3. **No-op proofs:** Hole_17 (null provider) → behaviour identical to HEAD; putts → aim error
   sampled exactly as today.

---

## 7. Handoff

- Touch list (expected diff): `Assets/Scripts/Physics/Viewer/BotTreeProbe.cs` (one additive
  method), `Assets/Scripts/Physics/Viewer/VersusBot.cs` (hoist `trees`, `out probeCarry`, D2
  rewire, field + const), `Assets/Scripts/Physics/Tests/BotTreeProbeTests.cs` (new tests).
  **No asmdef, no sim, no CSV, no prefab/scene edits.**
- Kickoff: `Use the implementer subagent on "bot_tree_error_recheck"`
