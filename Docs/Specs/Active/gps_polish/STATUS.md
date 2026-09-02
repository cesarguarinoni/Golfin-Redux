READY_FOR_SELF_REVIEW

# STATUS — `gps_polish`

**Current:** `READY_FOR_SELF_REVIEW` — iteration 2 (the `KICKOFF_ADDENDUM.md` R1–R9 continuation)
is complete and **every A-item is filled**. The push Cesar approved on 2026-09-02 is unchanged.

**Opened:** 2026-09-02. First commit closed `gps_pill_entry` (`96d60fab4`).

## Where every gate landed

| gate | result |
|---|---|
| A1 invariants | `fail=0` over 10 pushes; 0.2527–0.2667 s vs 0.25; t0 ±1170; seam 1.000 |
| A2 rest parity | **0 differing px on all 7 screens** — within-one-run animated-vs-instant pairs |
| A3 boundary | `FadeController` byte-identical; no scene change at all |
| A4 videos | 6 of 6, captioned, 1170×2532 — (b) re-recorded cold, (c)(d′)(e)(f) new |
| A5 nav-bar seam | worst mid-push mean ǀΔRGBǀ = **0.920** (budget 2), 70 consecutive frames |
| A6 lint | identical prefab-for-prefab vs HEAD — **zero new findings** |
| A7 pending | wired on all 6 CTAs + the `…` frame captured |
| A8 shimmer | 5 sites placed, 4 cold frames captured with the host proven active, cache-hit path logged |
| A9 modals | `animateShow` default pinned; **no non-GPS prefab and no scene changed** |
| A10 sweep | safe area / scroll / 208 Rubik sites from iter-1, **plus the keyboard row** |
| A11 importer | `--check` clean, texts v31, no new strings |
| A12 EditMode | 2319 / 2316 passed / 0 failed / 3 pre-existing skips |
| A13 perf | measured twice: in situ (whole app, upper bound) and isolated (the tweens: ≤32 B/frame) |

## What the reviewer should look at hardest

1. **§3 D-8 — iteration 1 was wrong about the scene copies.** They ARE prefab instances; the
   earlier check ran in play mode where the flag is false for everything. `ApplyToScene`'s header
   comment is now false and was deliberately left unedited so it can be seen.
2. **§2 A8 — a real product defect, found by this task's own placeholder.** The badges grid could
   show a loading state it could never leave. Fixed, tested, and the shape audited across all five
   fetch sites with a per-site verdict table.
3. **§3 D-9 — the gift panels fade on a cold OPEN, not "with their data."** Deliberate: the
   placeholder lives inside the panel.
4. **R6 keyboard needs the device pass to be SEEN.** The maths is pinned in EditMode; the one link
   the phone adds is whether `TouchScreenKeyboard.area` reports what iOS says.

## Live votes

Two of the four seeded `GOLFIN AI` votes are now spent —
`e47a04bc-bed3-43c6-bc53-0d92b18eef5a` (iteration 1) and
`541bcde9-9979-400b-ad35-93bb205c092f` (video (f), +10 RP → 6968). **Two remain** for the device pass.

## History

| Date | State | Note |
|---|---|---|
| 2026-09-02 | `SPEC_READY` | Motion/polish spec, no Figma nodes. |
| 2026-09-02 | `IMPLEMENTER_WORKING` | Push built and measured (`a7902da27`); D1/D2/D3/D5 done, D4 and D7–D9 partial. |
| 2026-09-02 | `IMPLEMENTER_WORKING` | Cesar approved the push; it went into the daily report. Folder stayed in `Active/` for the §D remainder. |
| 2026-09-02 | `READY_FOR_SELF_REVIEW` | Iteration 2: R1–R9 complete, every A-item filled, one iter-1 correction and one product defect closed. |
