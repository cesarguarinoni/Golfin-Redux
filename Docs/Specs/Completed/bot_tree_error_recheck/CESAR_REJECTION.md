# CESAR_REJECTION — bot_tree_error_recheck (after iter-1 ARCHITECT PASS)

**Rejected:** 2026-08-06, after `golfin-reviewer` wrote PASS → `READY_FOR_REDTEAM`.
**Rejected by:** Cesar (via architect main thread, which independently re-derived the evidence).
**Scope of rejection:** evidence only. **The code diff is accepted as correct** — do not rewrite it.

---

## What is NOT wrong (do not touch)

The production diff was verified line-by-line against SPEC §4.1/§4.2 by three independent
passes (self-reviewer, reviewer, architect main thread) and is byte-equivalent to the spec:

- `BotTreeProbe.TrySampleTrunkClearAimError` — helper body, loop order, and the
  `if (trees == null) return true;` placement INSIDE the loop (single-draw treeless parity).
- `VersusBot` — `trees` hoist, `SelectShotCalibrated(..., out probeCarry)` (was `out _`),
  the D2 rewire, `DebugDisableTreeRecheck`, `MaxAimErrorResamples = 5`, `treeChecked` log marker.
- The 4 new unit tests (7–10) are non-vacuous; their trunk geometry was re-derived
  independently by two reviewers and holds.
- EditMode suite re-run on the main thread: **995 total / 992 passed / 0 failed / 3 skipped**.
  (The 3 skips are pre-existing documented Stage C1 skips in `HoleCompleteDriverTests`,
  unrelated to this task. Note for the report: iter-1 stated "0 skipped" — state the real
  number next time; conflating skipped into passed is an evidence-quality defect.)

**Keep all of the above exactly as it is.** This rejection adds evidence; it changes no code.

---

## DEFECT 1 — the §6.2 "log smoke" was not produced by `VersusBot.TakeShot` (BLOCKING)

`IMPLEMENTER_REPORT.md` cites 23 lines of the form:

```
[VersusBot] 2b error: Δaim=-4.3° Δpow=+0.000 clubNoise= treeChecked=1
```

These lines were emitted by a `script-execute` harness that calls the helper directly and
prints look-alike output, **not** by the production `VersusBot.TakeShot` path. Proof, from
`Assets/Resources/Data/bot_difficulty.csv` row `minLevel=1`:

| Field | CSV value at level 1 | What a real run must show | What the cited logs show |
|---|---|---|---|
| `powerErrorMax` | `0.12` | `Δpow` uniform ±0.12 | `+0.000` on **all 23** — probability ≈ 0 |
| `clubNoiseChance` | `0.25` | noise on ~25% of shots (≈6 of 23) | `clubNoise=` **empty on all 23** |

The report does disclose the harness ("Smoke via `script-execute`"), so this is not concealment
— but printing harness output under a `[VersusBot]` prefix makes it indistinguishable from
production logs, and it was cited as clearing a gate that asks for something else entirely.
SPEC §6.2 requires: *"1v1 on Hole_12 or Hole_08 with `DebugLevelOverride=1`"*.

**Why this is blocking:** the helper is now well proven in isolation (unit tests + 20 samples
against Hole_08's real 3926-tree provider). What has **zero** live evidence is the
**integration** — that `TakeShot` actually routes its aim sample through the helper during a
real match. The integration IS the fix. A helper nobody calls fixes nothing. This is
PIPELINE_HARDENING rule 2 (real-entry rule) and the standing "test via the real flow" rule.

## DEFECT 2 — §6.2 clamp line and `DebugDisableTreeRecheck` control were never observed (BLOCKING)

Both were substituted with unit-test / code-reading argument. Neither reviewer saw either one
happen. They are cheap to observe once a real match is running.

## DEFECT 3 — §6.3 Hole_17 null-provider no-op was asserted, not run (BLOCKING)

The report substitutes a generic "null provider" harness section. SPEC §6.3 names Hole_17.

---

## What iter-2 must deliver

Run a **real 1v1 match**, driven through the player's own entry point — boot ShellScene →
the real widget `onClick` → `BeginGameplayLoad`. Do **not** direct-load a lab scaffold, and do
**not** hand-roll a `script-execute` harness that prints `[VersusBot]`-prefixed lines. If a
production log line is in your report, it must have come out of production code.

1. **Hole_08 (or Hole_12), `DebugLevelOverride=1`, real 1v1.** Capture genuine
   `[VersusBot] 2b error:` lines from the Unity Editor log. They must show **varying `Δpow`**
   (non-zero, spread across ±0.12) and **`clubNoise` firing on roughly a quarter of shots** —
   that variation is what proves the lines are real. Show `treeChecked=1` on non-putt strokes
   and `treeChecked=0` on putts, from the same run.
2. **At least one live clamp line** — `"all aim samples trunk-blocked — clamped to pre-2b line"`.
   If it genuinely will not fire in normal play, say so explicitly and force it honestly
   (e.g. a tree-dense lie), labelled as a forced case rather than presented as organic.
3. **`DebugDisableTreeRecheck = true` control run** on the same hole: logs must show
   `treeChecked=0` throughout and match HEAD's shape.
4. **Hole_17 null-provider no-op**, on Hole_17 specifically as SPEC §6.3 names it.
5. **`## Rejection follow-up` section** (Rule 15) with an explicit GONE / RESOLVED /
   STILL-PRESENT verdict for each of Defects 1–3, citing a real full-res Game View screenshot
   (long edge ≥ 900px — capture at 1170×2532) of the live 1v1 on the tree-dense hole. Capture
   Rule 0 applies: use `mcp__ai-game-developer__screenshot-game-view`, never a hand-rolled
   `script-execute` capture, and **look at the PNG** before citing it.

**Do not modify `Assets/Resources/FX/M_Splash*.mat`.** Three of them are already dirty in the
working tree from an unrelated source (`m_CustomRenderQueue: 3100 → 3000`) and are standing-ban
files (PIPELINE_HARDENING rule 7). Leave them alone; do not stage, revert, or report them as
yours. They are being handled separately.

**Iteration shape:** keep it distinct from iter-1 — this is `bot:realflow-evidence`, not a
repeat of `bot-2b-error:tree-corridor-bypass`. The code is done; only the proof is missing.
