# RESOLUTION — P1 reframed from provenance to FIDELITY (Cesar, 2026-07-06)

The ESCALATION established that clone *provenance* is unprovable for a CopyAsset
workflow (a clone's artifact is byte-identical to a faithful hand-rebuild; skeleton
comparison is defeatable at any bounded depth — leaf, then N-child). Cesar's decision:
**verify FIDELITY, not provenance.**

## What the P1 gate is now
- **Hard CRITICAL FAIL (unfakeable, pure-Python):** a reuse-mandated element whose
  `Image.sprite` is NULL/blank while the source carries one — the actual 610 /
  tournament / stamina fabrication signature (white box / flat fill).
- **PrefabInstance `!u!1001` lineage present → proven clone (PASS).**
- **Real sprite present (same or re-skin) → sprite-fidelity MET → ACCEPT.** A
  best-effort live structural comparison runs and emits a **WARN** on a gross
  mismatch (reviewer + reference-diff Rule 18 look harder), but MATCH / bare-leaf /
  unreachable-editor all ACCEPT. A faithful from-scratch rebuild passing is CORRECT
  by design — it carries the real atoms, which is the goal.
- **A3 re-skin (different real sprite) → WARN.**
- **Sprite-less element → WARN** (no fabrication signature; reviewer confirms).
- **Visual-correctness scars** (oval pill, 9-slice collapse, wrong radius) are the
  job of P8 render-health via P2, NOT P1.

## Why this is right (not a weakening)
Every real scar was a fidelity failure caught by fidelity checks; none was "a
faithful build that wasn't technically a clone" (that mode never happened and would
be fine). The gate keeps catching every fabrication that has ever hurt us, stops
chasing an unprovable process fact, and keeps the CopyAsset workflow.

## Optional hard-provenance escape hatch
For the rare element where uncopyable provenance IS required, make it a PrefabInstance
clone; the `!u!1001` m_SourcePrefab check verifies it. Not mandated.

Verified E2E (live editor): composite clone → PASS, leaf clone → PASS, null-sprite
fabrication → CRITICAL FAIL, structural mismatch on a real-sprite element → WARN,
editor unreachable → ACCEPT. Suite 117 green.
