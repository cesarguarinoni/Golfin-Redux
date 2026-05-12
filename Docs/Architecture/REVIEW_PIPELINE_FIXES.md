# Review Pipeline Fixes — For Architect Chat

Drafted 2026-05-13 after Cesar caught text-outside-container on iter-6, iter-8, iter-11, AND iter-12 of the same task. The full pipeline (implementer + self-reviewer + architect-reviewer) green-lit every one of those iterations. Cesar caught all of them in live play within seconds.

This is NOT about visual difficulty. The screenshots had ~black backdrop vs navy-blue card BG. Text floating above the card was plainly visible. The failure was procedural.

## Real failure modes (revised, honest)

### 1. Reviewers validate the implementer's claim instead of independently examining pixels

When the IMPLEMENTER_REPORT says "LOCKED header inside BG", reviewers find what they expect. Confirmation bias. The screenshot is right there in their context window, but the eye scans for checklist items rather than examining the actual pixels independently.

**Fix:** First action a self-reviewer MUST take is to open the canonical screenshot and write a pixel-level description (3-5 sentences, "what I see, no narrative, no checklist") BEFORE reading the IMPLEMENTER_REPORT. Then read the report. If the pixel description and the report's claims disagree → automatic FAIL. The pixel description goes in `SELF_REVIEW.md` § "Independent visual scan" near the top.

### 2. Self-reviewer and architect-reviewer rubber-stamp each other

Architect-reviewer reads `SELF_REVIEW.md` first, tends to agree with the prior verdict. Two reviewers in series catch fewer issues than one reviewer doing the job properly.

**Fix:** Architect-reviewer's prompt must REQUIRE independent pixel re-examination BEFORE reading the self-review verdict. Self-review goes to the BOTTOM of the architect's input, not the top. Architect writes their own "what I see" paragraph first.

### 3. Move-forward bias produces qualified PASSes

After N iterations, both reviewers know Cesar wants to ship. They lean toward PASS to avoid loops. Real qualitative misses get reported as "subtle but present" or "within tolerance". The iter-9 DarkenOverlay (0.65 alpha) was the canonical case — self-reviewer flagged "subtle ~15% darker", architect-reviewer accepted as PASS, Cesar saw no dim at all in production.

**Fix:** When the implementer self-grades any item as PARTIAL or notes a concern (e.g. "slightly darker but not dramatically"), the reviewers MUST treat it as a FAIL unless they can articulate why a full PASS is correct WITH a specific pixel reference. "I overrode to PASS because it looks fine to me" is not sufficient. The reviewer's burden of justification scales with the implementer's expressed uncertainty.

### 4. No Figma side-by-side discipline

Reviewers reference the Figma in narrative ("matches Figma proportions") but don't perform alignment-level checks against the actual reference image. The Figma is right there; not opening it is laziness.

**Fix:** Every visual review must include a mandatory section: "Figma side-by-side comparison" — open the reference image at the path named in SPEC §E, write a per-element comparison (size, position, color, contrast). "Matches" is not acceptable as a row value; specific dimensions or "matches within X pixels" is. Reviewer agents must have read access to `Docs/Reference/` and use it.

### 5. No bounding-box geometry verification

Every "is X inside Y" question is currently answered by eyeballing. There are layouts where the eye gets fooled (and there are layouts where it doesn't — iter-12 was the latter and reviewers still missed). Geometry is deterministic; eyeballing isn't.

**Fix:** For any containment claim (text-inside-BG, child-inside-parent, modal-inside-canvas), the reviewer MUST run a programmatic MCP check via `script-execute`:

```csharp
var card = GameObject.Find("Card2");
var cardCorners = new Vector3[4]; card.GetComponent<RectTransform>().GetWorldCorners(cardCorners);
foreach (var childName in new[] { "LockedHeader", "Subhead", "RewardsRow" }) {
    var child = card.transform.Find($"ContentRoot/{childName}");
    if (!child) continue;
    var childCorners = new Vector3[4]; child.GetComponent<RectTransform>().GetWorldCorners(childCorners);
    bool inside = true;
    foreach (var c in childCorners) {
        if (c.x < cardCorners[0].x || c.x > cardCorners[2].x ||
            c.y < cardCorners[0].y || c.y > cardCorners[2].y) inside = false;
    }
    Debug.Log($"[bbox] {childName}: inside={inside} child={childCorners[0]}-{childCorners[2]} card={cardCorners[0]}-{cardCorners[2]}");
}
```

ANY `inside=false` → automatic FAIL. No qualitative override.

### 6. Smoke-runner captures hide production-flow bugs

The smoke runner has different layout-pass timing than actual gameplay. iter-11's `LayoutRebuilder.ForceRebuildLayoutImmediate + SetSizeWithCurrentAnchors` trick produced clean smoke screenshots but the production flow (Cesar triggering HoleOut from gameplay) ran into different timing.

**Fix:** For any modal/panel layout change, the implementer must capture in BOTH smoke-runner AND production-flow paths. Reviewer must verify both screenshots are present. Production-flow capture = trigger via `DebugShotPanel.HoleOutBtn` from a normal play session, not via SmokeRunner2dHost's pre-scripted state injection.

### 7. Capture paths mutate scene state

Iter-12 specifically: the implementer's custom ortho-camera capture path deactivated 10 ShotUI GameObjects in `LabScaffold.unity` to clean up the frame, and saved the scene with the broken state. Reviewers approved because the captured screenshot looked fine — the scene corruption was invisible UNTIL Cesar launched normal play.

**Fix:** Reviewer must `git diff` the scene file (`LabScaffold.unity` or whichever) at the `m_IsActive` / `sizeDelta` / `position` level for any iter that captured screenshots. If GameObjects were deactivated in the scene as a capture side-effect, that's a hard FAIL — must be reverted before forward. This is now mechanical and easy to enforce.

### 8. CaptureCore re-invented per-task

**The followup TODO is to fix CaptureCore so this isn't a per-task reinvention.** iter-12 hit MCP-frozen-time (Unity's `Time.frameCount` didn't advance, so `WaitForEndOfFrame` never returned) and the implementer wrote a custom ortho-camera-render workaround. The workaround was the source of the scene-corruption regression. If `CaptureCore.SnapPlayModeSafe` had handled the MCP-frozen-time case natively, the implementer wouldn't have invented a new capture path with no try/finally restore.

**Fix:** Extend `CaptureCore.SnapPlayModeSafe` (and `SnapAtEndOfFrameAndPause`) to detect MCP-frozen-time symptoms and fall back to `ScreenCapture.CaptureScreenshotAsTexture()` cleanly without mutating any scene state. The helper should be the ONLY capture path in the project; per-task workarounds are banned. Update CLAUDE.md to make this explicit.

### 9. Reviewer "approval cascade"

After self-reviewer FORWARD_TO_ARCHITECT, the architect-reviewer's job description currently allows for soft "lean toward APPROVE if X, Y, Z" framing. That phrasing in the orchestrator's prompt (mine, repeatedly) biases approval. iter-12 architect approval text literally said "Lean toward APPROVE if Bug A/B/C all visibly correct" — the architect dutifully approved.

**Fix:** Orchestrator prompts to the architect-reviewer MUST be neutral. No "lean toward" framing. The prompt asks for an independent verdict, period. If I (orchestrator) want to bias the decision, that's a sign I should make the call myself.

## Mandatory checklist for any future visual review

```
[ ] 1. Open canonical screenshot. Write 3-5 sentence pixel-level description with no
       reference to the report or checklist.
[ ] 2. Open Figma reference. Write per-element differences (specific px/color, not
       "matches").
[ ] 3. For any containment claim, run bbox geometry MCP check; paste log into review.
[ ] 4. `git diff` the scene file for any GameObject state changes outside the intended
       fix. Hard FAIL on unexpected scene mutations.
[ ] 5. If implementer self-graded any item PARTIAL or expressed uncertainty, treat as
       FAIL unless reviewer can articulate specific reasoning for PASS.
[ ] 6. For layout changes, verify a production-flow screenshot exists (not just smoke
       runner).
[ ] 7. Read implementer's narrative ONLY AFTER steps 1-6. If narrative contradicts
       pixel evidence, FAIL.
```

## Specific text to add to subagent definitions

For `.claude/agents/golfin-self-reviewer.md` and `.claude/agents/golfin-reviewer.md`:

> **Independent pixel scan FIRST.** Before reading any IMPLEMENTER_REPORT, SELF_REVIEW, or prior verdict, open the canonical screenshots for the iter being reviewed. Write a 3-5 sentence § "Independent visual scan" at the top of your review describing what you actually see in the pixels — no narrative, no checklist, no comparison to claims. THEN read the report. If your visual scan and the report's claims disagree, that's an automatic FAIL.

> **Bbox geometry verification.** For any containment claim ("text inside BG", "modal inside canvas", "child inside parent"), run a programmatic MCP `script-execute` bbox check. ANY `inside=false` is a hard FAIL — no qualitative override.

> **Scene-mutation audit.** If the iter captured screenshots, run `git diff <scene>` and verify no `m_IsActive`, `sizeDelta`, or position changes were made to GameObjects outside the intended fix. Capture-driven scene corruption is a recurring failure mode.

> **Implementer-self-graded PARTIAL → FAIL default.** If the implementer flagged any item as PARTIAL or expressed uncertainty in the report, treat it as a FAIL by default. Override to PASS only with specific pixel-level reasoning (cite coordinates, colors, sizes).

## How to roll this out

1. Architect Claude amends `.claude/agents/golfin-self-reviewer.md` and `golfin-reviewer.md` with the four new rules above.
2. Architect Claude updates `CLAUDE.md` § "Screenshots" to declare `CaptureCore.SnapPlayModeSafe` as the only sanctioned capture path and forbids per-task workarounds.
3. Architect Claude updates `CLAUDE.md` § "Reviewing" to enforce the mandatory checklist.
4. A small backlog task: extend `CaptureCore.SnapPlayModeSafe` to detect MCP-frozen-time and fall back to `ScreenCapture.CaptureScreenshotAsTexture()` without scene mutation. This makes rule #3 above (capture paths must not mutate scene state) trivially enforceable because there's only one capture path.

---

The reviewer agents have full pixel access. They have Figma access. They have MCP access for bbox checks. The pipeline has all the tools — what's been missing is the discipline to use them in a specific independent order. The fixes above are all process-level, not capability-level.
