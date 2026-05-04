# Pipeline Lessons

> Cross-task accumulator for what each task taught us about the multi-agent pipeline. Subagent prompts get refined based on patterns visible across multiple entries here, not single one-offs.

---

## 2026-04-28 — `8_3_topbar` iteration 1 self-review FAIL

Self-reviewer correctly caught the visible "PL"/"TI" truncation but missed three other defects, and hallucinated a wrong root cause for the one it did catch.

### Lesson A — Trust pixels over YAML when they disagree

The self-reviewer wrote "no overlap" / "chips correct" based on RectTransform values from the scene YAML, without checking what the screenshot actually showed. YAML answers "what is configured." Screenshot answers "what renders." When they disagree, **screenshot wins**. YAML can be perfectly correct and still produce a broken visual due to render-time effects (Image.PreserveAspect, CanvasScaler match mode, layout group runtime sizing, etc.).

**Fix in subagent prompt:** make this explicit and structural — describe screenshot first, then compare to spec, then check YAML only when needed to explain a divergence.

### Lesson B — Describe before diagnosing

The reviewer's analysis of "VLG ChildControlHeight + overflowMode + truncation" was internally coherent but completely wrong about what the screenshot shows. It pattern-matched on "only 2 chars visible" and invented a TMP truncation root cause, when the actual cause was right-aligned text rendering against the right edge of a chip whose right edge was further from the portrait than the design intended.

**Fix:** force a "describe what's visible" step BEFORE any diagnosis. List visible elements, their approximate positions, their colors, their text. Then compare to reference. Then judge the spec checklist. Only at the end, if needed, propose root causes — and only after acknowledging the visible evidence.

### Lesson C — Spec gaps the reviewer can't catch

Things the spec didn't ask for (visible portrait frame, visible hole map frame) won't appear in the checklist, so the reviewer has no item to fail. The reference image shows clear visible frames around portrait and hole map; the spec didn't list them; the reviewer trusted the spec checklist as exhaustive.

**Fix:** the architect-review subagent (not the self-reviewer) gets explicit instruction to compare screenshot vs reference image globally, not just spec-checklist-by-spec-checklist. The architect catches gaps the spec missed; the self-reviewer catches false PASSes within the spec.

### Lesson D — Image render size ≠ RectTransform size

`Image.PreserveAspect = true` with non-square sprites renders the visible image smaller than the RectTransform. Self-reviewer trusted the 180×180 RectTransform and concluded "fills container," but the actual visible portrait pixels were ~130×180 (preserving aspect of a portrait-orientation sprite), leaving green-grass gaps on the sides.

**Fix:** when a checklist item asks "does X fill its container," the reviewer must visually measure the rendered pixels in the screenshot, not the RectTransform values. For sprite-bearing Images, ALWAYS check the source sprite's aspect ratio and PreserveAspect setting.

### Lesson E — Architect re-extracted Figma values incorrectly

Original spec said Settings 106px from right edge based on the architect (me) reading the wrong Figma node — the 243-wide wrapper Frame instead of the 86-wide button itself. Re-extraction confirmed actual right margin is 58px.

**Fix:** when extracting RectTransform-mappable values from Figma, the architect must verify the node it's reading is the EXACT visible element (the button), not its containing frame. Walk the hierarchy, find the leaf or near-leaf node that maps 1:1 to a Unity GameObject.

### Lesson F — Architect overthought past Cesar's stated answers

When Cesar gave a concrete diagnosis ("text is misaligned, not truncated"; "chips don't touch frames"; "portraits need backgrounds"), the architect ran a full speculative analysis on top of those answers — measuring screenshot pixels, theorizing about CanvasScaler scale factors, second-guessing whether "overlap" meant overlap or just "too close." The result was a spec patch that buried the right fix under unnecessary qualifying text and a wrong claim ("chip alignment is correct, keep it") that contradicted what Cesar said.

**Fix:** when Cesar gives a direct diagnosis of a visible issue, treat it as ground truth and patch the spec to match. Save the deep analysis for cases where Cesar is asking for it, or where the diagnosis is ambiguous and needs disambiguation. Don't relitigate confirmed observations.

### Lesson G — Architect committed thinking-aloud text into the spec

When writing the iteration 3 spec patch (Fix 6, chip width adjustment), the architect's first draft included literal phrases like "Wait — actually the cleanest layout..." and "Wait, I'm double-counting the gain. Let me redo this more carefully..." — mid-reasoning self-corrections that should have been discarded before finalizing the spec. These got committed into the document and only caught on review.

**Fix:** specs are final reference docs that the Implementer reads as authoritative. Before finalizing any spec patch, scan for "wait," "actually," "let me reconsider," "hmm," or any phrasing that signals mid-reasoning self-correction. Strip those passages and present only the resolved conclusion. The reasoning is for the architect; the spec is for the Implementer.

### Lesson H — Architect makes confident numerical claims about Figma without grounding them visually (PATTERN)

This is the third instance of the same failure mode (see also Lessons B and F). When asked to compare the Unity render to the Figma reference, the architect repeatedly:
1. Pulls raw XY/width/height numbers via Plugin API.
2. Does math on those numbers.
3. Asserts a conclusion ("Figma has chips touching the card edge with no gap", "chip alignment is correct", "text is truncated") that is **visibly wrong** when the actual Figma image is examined.
4. Doubles down on the wrong conclusion when Cesar pushes back, instead of looking at the visual.

In this case (iteration 3, Fix 6): architect claimed "Figma also has the chips reaching all the way to the card edges — no inset" based on the Plugin API returning chip width 298 inside a 478-wide card with chip x-offset of 180 (so 180+298=478, edge-to-edge). What architect missed: Figma's frames have **auto-layout / padding** that visually creates gaps not represented in raw child XY coordinates. The 118px center gap is plainly visible in the rendered Figma frame. Architect denied its existence based on math.

**Why this keeps happening:** the Figma Plugin API exposes geometric numbers, but Figma's renderer applies layout rules (auto-layout padding, constraints, item spacing) that affect the visible result. Reading raw `child.x` / `child.width` and concluding "this is what it looks like" is wrong by construction.

**Fix — binding rule for the Architect agent:** before making ANY claim about "Figma shows X" or "Figma has Y," the architect MUST:
1. Open the actual rendered Figma image (the PNG export at `Docs/Reference/...` or via `mcp__figma__get_screenshot` for the live frame).
2. Visually verify the claim against the rendered pixels.
3. Only THEN cite Plugin API numbers as supporting evidence — not as primary evidence.

If the rendered image and the API numbers disagree, the rendered image wins. The API tells you what's in the data model; the image tells you what the user sees.

This lesson supersedes the Lesson A guidance ("pixels over YAML") for Figma specifically: it's not just Unity render ≠ YAML; **Figma render ≠ Plugin API numbers** too. The architect must check the actual visual on both sides of any comparison.

### Lesson I — Pipeline has no state for "Cesar rejected after architect-pass" (WORKFLOW GAP)

Iteration 2 of `8_3_topbar` had architect-subagent PASS (verdict in `ARCHITECT_REVIEW.md` at 17:05 JST), but Cesar manually rejected on visual grounds the architect had explicitly deferred ("cornerRadius 8 — ACCEPTED as polish follow-up"). At that point STATUS was at `ARCHITECT_REVIEW_PASS`. The architect (this Claude) updated STATUS to `ARCHITECT_REVIEW_FAIL` to route back to the implementer.

Problem: when the implementer subagent loaded next, it saw the contradiction (`STATUS = ARCHITECT_REVIEW_FAIL` but `ARCHITECT_REVIEW.md = PASS` with no fail items) and concluded "STATUS is stale, architect already passed this." It reverted STATUS to `ARCHITECT_REVIEW_PASS` and recommended Cesar move the task to Completed — contradicting Cesar's actual rejection.

**Why this happened:** The pipeline state machine has no formal "Cesar rejected after architect-pass" state. Cesar's rejection lives in chat, which Code can't read. From any file-only view, the architect's PASS appears to be the authoritative final word.

**Stopgap (applied to this task):** create `CESAR_REJECTION.md` in the task folder, dated after `ARCHITECT_REVIEW.md`, that explicitly records the rejection. Subagents read this file before trusting the architect review. STATUS goes back to `ARCHITECT_REVIEW_FAIL` and the rejection file's existence is the signal that supersedes.

**Real fix (defer to workflow improvements pass):**
1. Add a new STATUS state: `CESAR_REJECTED`. Route hook treats it identically to `ARCHITECT_REVIEW_FAIL` (back to implementer). 
2. Update implementer subagent prompt: when STATUS is anything other than the expected forward states, do NOT "correct" STATUS to match files — ask Cesar what's intended. STATUS is the authoritative source of pipeline state, not the architect-review verdict.
3. Document in CLAUDE.md: "only Cesar can move STATUS to DONE; only Cesar's chat input can override an architect PASS."
4. Consider: a one-line `CESAR_NOTES.md` file with rolling notes from Cesar's manual reviews, so no future Cesar reaction is lost between chat sessions.

Filed in TODO list; tackle alongside hook ergonomics + cross-platform port + heartbeat.

### Lesson J — Rounded-corner Mask with PNG sprite failed; UISprite fallback unacceptable

In `8_3_topbar` iteration 3, the Implementer attempted Fix 5 (rounded corners radius 8 on PortraitContainer / HoleMapContainer) using a Mask + custom PNG sprite. The mask did not render correctly. Implementer fell back to Unity's built-in `UISprite` (the 9-sliced rounded square that ships with Unity) which DID render, but Cesar finds this unacceptable for production — the corner radius doesn't match the Figma 8px design, and the styling is too generic.

**Status:** UNRESOLVED. Cesar will fix this manually in Unity tomorrow alongside the size investigation.

**For future tasks involving rounded UI corners:**
- Custom PNG with 9-slice borders is the correct approach but requires the sprite to be:
  - Sized appropriately (e.g., 32×32 with 8px corner radius)
  - Sprite Editor: 9-slice borders set to 8px on all sides
  - Mesh Type: Tight
  - Filter Mode: Bilinear (for smooth corners at scale)
- Alternative: shader-based rounded corners (more flexible, requires URP shader work)
- Alternative: TextMeshPro background or `Procedural Image` package
- **Don't fall back to UISprite without flagging it explicitly as unacceptable** — it's a stopgap, not a solution.

---

## How to use this file

When updating the self-reviewer or reviewer subagent prompts (`.claude/agents/golfin-self-reviewer.md`, `golfin-reviewer.md`), look for **patterns across multiple entries** here. A single one-off doesn't justify a prompt edit. Two or more entries flagging the same kind of failure justify one.

Each entry should follow this format:
1. **Date — task — what failed**
2. **One named lesson per failure mode** (Lesson A, B, C...)
3. **Suggested fix in subagent prompt**
