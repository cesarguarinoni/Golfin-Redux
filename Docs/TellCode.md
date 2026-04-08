# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Remove Splatmap Blur

**Problem:** The Gaussian blur bleeds fairway into surrounding zones
even after re-stamping. The fringe rings already handle zone transitions,
so the blur is redundant and causing harm.

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

---

### What to do

In `ApplySplatmap()`, **delete** step 5 entirely (the Gaussian blur +
re-normalize block). Also delete step 5b (the fairway re-stamp — no
longer needed without blur).

Keep the helper methods `GaussianBlur2D`, `ExtractChannel`, `SetChannel`
in the file for now — they might be useful later. Just remove the calls.

The splatmap will now have hard pixel edges between zones, but the fringe
rings (green fringe + fairway fringe) provide the visual transitions.

---

### Verification

- [ ] Re-import Hole 1
- [ ] Fairway has clean, sharp edges
- [ ] Fairway fringe (semi-rough) visible as a border ring
- [ ] Green fringe still visible
- [ ] Zone transitions look acceptable without blur
- [ ] No console errors

### Do NOT

- Delete the blur helper methods (keep for future use)
- Modify zone meshes or export pipeline

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Water Shore Slope
✅ DONE: 2026-04-08 — Tee Markers: FBX props
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain plastic sheen fixed via Mask Map
✅ DONE: 2026-04-08 — Cleaned up failed terrain sheen fixes
✅ DONE: 2026-04-08 — Swapped fairway/fringe textures + rotated fairway grain
✅ DONE: 2026-04-08 — Fairway fringe ring + sharp fairway re-stamp (blur still bled through)
✅ DONE: 2026-04-08 — Removed splatmap blur (fringe rings handle transitions)
