# RED-TEAM REVIEW — pipeline_verification_gates (Order 611, iter-3)

**Verdict: ARCHITECT_REVIEW_FAIL** (hard, fatal — the entire iter-3 fix is non-functional in the live editor).
**Reviewer:** golfin-redteam-reviewer · **Date:** 2026-07-06 · **STATUS at review:** READY_FOR_SELF_REVIEW (parallel trial; I did NOT set STATUS).

The suite is 113 green, but **113 green is meaningless here**: every test monkeypatches the two
live-editor seams (`_do_live_editor_structure_check`, `_rerun_ui_lint_via_editor`) with lambdas that
return `"MATCH"`/`"MISMATCH"`/`0`/`None`. Not one test drives the real MCP protocol. When I drove the
REAL live editor (reachable, HTTP 200) through the actual hook functions, **both seams return `None`
100% of the time** because of two independent, provable bugs in the C# the hook sends. The check the
whole iter-3 fix is built on **never executes.**

---

## 0. Environment sanity (so there is no "editor was down" excuse)

Live editor IS reachable: `POST localhost:21573/mcp initialize` → **HTTP 200**, session id issued,
`script-execute` runs. A trivial `public class Script { Main(){ Debug.Log("ok"); } }` returns
`value:"Success"`. So every failure below is a real-editor failure, not an unreachable-editor artifact.

---

## Break-attempt #1 (FATAL) — the live structure check never runs: wrong class name

The MCP `script-execute` tool **requires the C# class to be named `Script`.** Any other class name is
rejected before execution:

```
$ _call_live_editor('... public class StructureCompare { public static void Main(){...} }')
-> "Tool execution failed for 'Script / Execute': 'csharpCode' does not contain class 'Script'.
    Please ensure the class is defined in the provided code."
```

The hook's `_do_live_editor_structure_check` emits `public class StructureCompare` (enforce_implementer_done.py:316)
and `_rerun_ui_lint_via_editor` emits `public class LintRerun` (:1949). **Both are rejected by the tool
every time.** `_call_live_editor` receives the error string, finds no `STRUCTURE_MATCH`/`STRUCTURE_MISMATCH`
line, and returns `None`. Verified live for BOTH class names.

## Break-attempt #2 (FATAL, independent) — `Debug.Log` output is never returned

Even after fixing the class name to `Script`, the hook's result-passing mechanism is broken. The hook
emits its verdict via `Debug.Log("STRUCTURE_MATCH")` / `Debug.Log("LINT_FAIL_COUNT:n")` and parses it
out of the returned value. But `script-execute` returns **only the method's return value**, never console
output. Full SSE payload for `public class Script { Main(){ Debug.Log("STRUCTURE_MATCH_LEAKTEST"); } }`:

```
data: {"result":{"content":[{"type":"text","text":"{...\"value\":\"Success\"}"}],
       "structuredContent":{"result":{"name":"result","typeName":"System.Void","value":"Success"}},
       "isError":false},"id":2,"jsonrpc":"2.0"}
```

`STRUCTURE_MATCH_LEAKTEST` appears **nowhere** in the response. `Main()` is `System.Void` → value is
literally the string `"Success"`. The hook scans that string for `STRUCTURE_MATCH` → not found → returns
`None`. So even with the class-name bug fixed, the seam still cannot return MATCH/MISMATCH.

**Net:** `_do_live_editor_structure_check` and `_rerun_ui_lint_via_editor` can only ever return `None`
in the real editor. Two independent bugs, either one alone is fatal.

## Break-attempt #3 (the consequence) — A2 real-clone regression is BACK

I ran the ACTUAL hook function against a genuinely real clone with the editor up:

```
eid._do_live_editor_structure_check(
    built="Assets/Prefabs/UI/Shop/GeneralShopCard.prefab",
    source_guid="baac145d1783f41758376281a61c83e0",  # TournamentSelectionCard
    element_path="", repo_root=<repo>)
-> None
```

`None` feeds the P1 branch at enforce_implementer_done.py:2474–2487 → **`CRITICAL FAIL (P1 — editor
unreachable)`**. So a legitimate CopyAsset clone whose sprite GUID matches its source is **BLOCKED**,
with the editor fully reachable. This is *exactly* the A2 case the SPEC (§5 A2) and the postmortem say
must PASS, and the exact regression iter-2 was rejected for. The "MATCH → PASS" arm at :2460 is
**unreachable code in production** — nothing can produce "MATCH". iter-3 reintroduces the A2 failure it
claims to fix; it is only "green" because A2b/A2c tests stub the seam to hand back `"MATCH"`.

The forgery (A1) is also blocked — but for the WRONG reason (fail-closed on a broken RPC), not because
any structure was compared. The "un-fakeable structural comparison" that is the deliverable of this
order **does not exist at runtime.**

---

## P2 fail-open (attack item 4 + §0 design-law violation) — CONFIRMED, and it's the ONLY reachable path

`validate_ui_lint` (:2089) accepts the implementer-authored cached `_lint.json` when `_rerun_ui_lint_via_editor`
returns `None`. Because that function ALWAYS returns `None` in the live editor (bugs #1/#2), **P2 accepts
the self-authored JSON on every single run, editor up or down.** The "fresh re-run" P2 exists to add never
happens. This is a textbook §0 DEFECT: "any gate that parses an implementer-authored artifact as its
evidence." The report frames this as an intentional "quality gate, not security gate" softness — but even
as a quality gate, it provides **zero** independent verification because the re-run cannot execute. An
implementer never even needs to kill the connection to bypass P2; it self-bypasses.

---

## Prior-rejection replay

| Prior defect | Verdict now |
|---|---|
| iter-1: guid-paste forgery PASSES (no-lineage + pasted sprite guid = silent PASS) | Addressed on paper, but the "fix" (live structure check) never runs → the forgery is now blocked only by an accidental fail-closed on a broken RPC. Not a real fix. |
| iter-2: "require PrefabInstance lineage for all clones" breaks real CopyAsset clones (A2) | **REGRESSED / PRESENT.** Real CopyAsset clone returns `None` → CRITICAL FAIL. A2 is broken again, just via a different code path. |

---

## Why the suite passing is not evidence (report-integrity note)

The IMPLEMENTER_REPORT lists "113 passed" as acceptance rows 7–13, all backed by tests that replace the
seam with `lambda *a, **kw: "MATCH"` etc. (test file lines 1363, 1475, 1535, 1861, 1881, 1906). The claim
"live-editor batchmode structural comparison" (report §3) is **not demonstrated by any test and is
non-functional when actually invoked.** No fabrication of tool output, so not a Rule-6 CRITICAL, but the
central capability claim is unverified-and-false in practice.

---

## Required fixes before re-review

1. **Class name must be `Script`** in both C# scripts (`StructureCompare` → `Script`, `LintRerun` → `Script`).
2. **Return the verdict as the method's return value**, not via `Debug.Log`. `Main()` must be non-void
   and `return "STRUCTURE_MATCH"` (or the script must `return` the string so it lands in
   `structuredContent.result.value`). Then parse `value`, not stdout.
3. **Add at least one NON-monkeypatched integration test** that drives the real `_call_live_editor` against
   a known real clone and asserts `"MATCH"`, and against the fabricated_610 fixture and asserts `"MISMATCH"`.
   Until a test exercises the real RPC, the seam can silently rot exactly like this.
4. **Re-verify A2 end-to-end live**: `Assets/Prefabs/UI/Shop/GeneralShopCard.prefab` vs source
   `baac145d1783f41758376281a61c83e0` must return MATCH (PASS), and `fixtures/fabricated_610/*` must
   return MISMATCH (CRITICAL FAIL) — both through the real editor, no stubs.
5. **Structure check is also too shallow** (secondary, surfaces once #1–#2 are fixed): it compares only the
   element root's `childCount` + its own sorted component-type list — no recursion, no serialized-property
   compare. A forger who matches top-level child count and component list would pass. Deepen it (recursive
   component/child signature) before relying on it as "un-fakeable."
6. **P2 §0 violation**: as long as the live re-run can silently no-op into "accept cached JSON," P2 reads an
   implementer-authored fact. Either make the re-run mandatory-and-verified (fail-closed like P1) or remove
   the claim that P2 "stops trusting cited JSON" — currently it always trusts it.

---

## Break-attempts that did NOT find an additional hole (for completeness)

- **YAML-parse path (case C: null sprite where source has art)** — this one works and correctly CRITICAL
  FAILs; it's the pure-Python part and is sound. The regression is confined to the two live-editor seams.
- **P4 shipped-asset guard / P5 test-run gate** — not exercised in this red-team pass; out of the failing
  path. Their tests are stub-free enough to trust for now, but they were not the iter-3 deliverable.

**Bottom line:** the deliverable of iter-3 — a live-editor structural comparison that distinguishes a real
clone from a guid-paste forgery — is dead code in the live environment. It blocks real clones (A2 regression)
and never compares any structure. FAIL, route back to implementer.
