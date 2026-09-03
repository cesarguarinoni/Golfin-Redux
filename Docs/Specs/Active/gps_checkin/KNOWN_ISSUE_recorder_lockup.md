# Known issue — recording the Rounds screen locks the machine

**Status:** open, deferred by Cesar 2026-09-03. Not a blocker for `gps_checkin`.
**Impact:** tooling only. The shipped screen is unaffected; the app does not
encode video.

## What happens

Running `GOLFIN/Gps/Record — (g) Rounds check-in loop` hard-locks the Mac. It
happened twice, and both times Cesar had to power-cycle by hand. The output
`raw_g.mp4` is left at 0 bytes.

The freeze needs a manual reset and writes NO crash report, which places the
hang below the application — a GPU / media-driver level stall rather than a
managed exception. Unity does not crash on its own; the whole machine stops.

## What it is NOT

Three explanations were tested and eliminated:

| Hypothesis | Test | Result |
|---|---|---|
| The recorder is inherently too heavy | Scenarios `a`–`f` encode fine, same `GameViewInputSettings`, same 1170x2532, same VideoToolbox path | **Eliminated** — six existing clips in `Docs/Reports/Media/gps_flow/` |
| The scenario `(g)` is bad (bad taps, runaway coroutine) | `GOLFIN/Gps/Record — DRY RUN (g), no encoder` — identical flow, `StartRecording()` never called | **Eliminated** — reached `GpsRounds`, `POST /activity/checkin -> 200`, Unity stable at ~2 GB RSS, machine healthy |
| Memory exhaustion | `memory_pressure` / `vm.swapusage` after the crash | **Eliminated** — 67-74% free, ZERO swap used |

## What is left

Neither ingredient is dangerous alone; only the COMBINATION locks:

- scenario `(g)` with the encoder off — safe
- the encoder with scenarios `a`–`f` — safe
- the encoder plus the Rounds screen — locks

So the provocation is something the Rounds screen puts in front of the encoder
that the other six screens do not. The leading candidate, untested, is the live
**Google Static Maps** texture: a large dynamically-uploaded surface being
composited every frame while the Recorder reads the same framebuffer back. The
Rounds screen is the only GPS screen that continuously displays one.

## How to investigate later (do NOT run unattended)

Each of these ends in "encode the Rounds screen", which is the thing that took
the machine down twice. Run only with the machine idle and work saved.

1. **Truncated record** — hub -> ROUNDS only, no check-in, no modals. Shortest
   exposure that either reproduces the lock or clears the map theory.
2. **Half-resolution record** (585x1266), DIAGNOSTIC ONLY — never a deliverable,
   the standing rule is full size. If it survives, encoder bandwidth is
   implicated rather than the map specifically.
3. **Map disabled** — stub the map tile to a static sprite and record `(g)` at
   full size. If that survives, the map texture is confirmed.

`GOLFIN/Gps/Record — DRY RUN (g), no encoder` stays in the recorder as the
control for all three: it runs the scenario with `StartRecording()` skipped and
nothing touching VideoToolbox.

## Consequence for acceptance item 12

Motion parity ships on its objective gates, which are complete:

- `gps_rounds_motion_invariants.json` — 12 transitions, `fail=0`, Rounds measured
  both ways at 0.257 s / 0.264 s against a 0.250 s target (tolerance 0.0533 s)
- `gps_rounds_motion_perf.json` — A13 over 12 pushes; Rounds 5.28 / 5.21 MB and
  27.3 / 21.2 ms, INSIDE the existing family envelope (worst: 6.84 MB ->GpsGift,
  45.6 ms ->GpsVote)

The video was the review artifact for Cesar, not the gate. He waived it
explicitly rather than spend a third reboot on it.
