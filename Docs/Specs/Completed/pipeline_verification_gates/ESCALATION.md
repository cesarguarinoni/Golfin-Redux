# ESCALATION — clone-provenance is fundamentally unverifiable for CopyAsset (2026-07-06)

**Circuit-breaker: 4 failures of the same shape** (iter-1 guid-paste bypass, iter-3 dead live-editor calls, iter-4 leaf guid-paste, iter-5 shallow-composite guid-paste). 1 implementer + 3 independent red-teams converge on ONE conclusion.

## The unavoidable finding
A CopyAsset clone's final artifact is **byte-identical** to a perfect from-scratch hand-rebuild. Therefore NO fact readable from the artifact — YAML, live-editor structure, sprite guid, render — can prove "this was cloned" vs "this was rebuilt." Skeleton comparison is defeatable at any bounded depth (leaf → 1-child → N-child; the forger just replicates the skeleton). The ONLY uncopyable lineage signal is **PrefabInstance `!u!1001 m_SourcePrefab`**, present only when the clone IS a prefab-instance/variant (CopyAsset produces none; `GetCorrespondingObjectFromSource` is null for it).

## What DOES work (keep regardless)
- Null-sprite / flat-fill → CRITICAL FAIL (the ACTUAL 610 fabrication signature).
- Gross composite structural MISMATCH → CRITICAL FAIL (catches lazy white-box from-scratch).
- P8 render-health (oval-pill / 9-slice collapse / distorted radius) — the actual visual scars.
- A3 re-skin → WARN.
These catch every REAL scar to date. What they can't catch is a *faithful* hand-rebuild indistinguishable from a clone — which is not a fidelity problem.

## The decision (supersedes the earlier "batchmode engine check" choice)
1. **Mandate PrefabInstance/variant clones.** Reuse workflow must produce `!u!1001` lineage → verifier trivially sound (check m_SourcePrefab guid, uncopyable). Cost: clones link to source (a variant inherits source edits) or use nested-prefab composition — less independent than CopyAsset. (Rejected earlier, BEFORE we knew CopyAsset provenance is unprovable.)
2. **Reframe the gate to FIDELITY, not provenance.** Drop the impossible "was it cloned" proof. Verify the RESULT is faithful: real sprites + correct render-health + gross-structure sanity. A from-scratch element with real sprites + correct render IS acceptable — which matches every actual scar (all were wrong RESULTS, not "technically not a clone"). Honest, achievable, sound for the real goal.
3. **Hybrid / accept residual.** Keep the current best-effort checks; explicitly ACCEPT that a perfect hand-rebuild passes (it's faithful, so fine). Document the residual as a known, acceptable limit rather than chasing it forever.

Cesar decides the direction; implementer executes.
